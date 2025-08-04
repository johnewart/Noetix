using Codecs;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using Noetix.LLM.Common;
using Noetix.LLM.Requests;
using Noetix.LLM.Tools;

namespace Noetix.LLM.Providers.Anthropic;

public class AnthropicLLM : LLMProvider
{
    private readonly AnthropicConfig config;
    private readonly HttpClient _httpClient;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private const string DefaultSystemPrompt = "You are a helpful assistant. You are here to help me with my tasks.";
    private const int DefaultMaxTokens = 8192;

    private RetryPolicy _retryPolicy = new RetryPolicy(
        maxAttempts: 4,
        initialDelay: TimeSpan.FromSeconds(3),
        maxDelay: TimeSpan.FromSeconds(30),
        backoffStrategy: RetryPolicy.BackoffStrategy.Exponential,
        shouldRetry: ex => (ex is Exception && ex.Message.Contains("429") || ex.Message.Contains("500"))
    );

    public AnthropicLLM(AnthropicConfig config, HttpClient? httpClient = null)
    {
        this.config = config;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("x-api-key", config.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public record AnthropicConfig
    {
        public string ApiKey { get; set; }
    }

    private async Task<HttpResponseMessage> SendRequestAsync(AnthropicRequest request,
        CancellationToken cancellationToken = default)
    {
        var json = AnthropicRequestModule.encode(request).ToString();
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        _logger.Info("Content: " + json);


        _logger.Info($"Sending request to Anthropic API with model {request.Model}");
        var response = await _httpClient.PostAsync("https://api.anthropic.com/v1/messages", content,
            cancellationToken: cancellationToken);
        _logger.Info(
            $"Received response from Anthropic API with status {response.StatusCode} and content {await response.Content.ReadAsStringAsync()}");

        return response;
    }

    public bool SupportsToolsNatively => true;

    private AnthropicMessage convertMessage(Message message)
    {
        var contentBlocks = new List<ContentBlock>();

        if (message.ToolResults != null)
        {
            foreach (var toolResult in message.ToolResults)
            {
                contentBlocks.Add(ContentBlock.NewTRB(new ToolResultBlock(toolUseId: toolResult.Id,
                    content: toolResult.Result)));
            }
        }

        // Only add text if there's no tool results in this message
        if (message.Content != "" && message.ToolResults == null || message.ToolResults?.Count == 0)
        {
            contentBlocks.Add(ContentBlock.NewTCB(new TextContentBlock(message.Content)));
        }

        if (message.ToolRequests != null)
        {
            foreach (var toolRequest in message.ToolRequests)
            {
                var toolParams = toolRequest.Parameters;
                if (toolParams.HasValues == false)
                {
                    toolParams = new JObject();
                    toolParams["input"] = new JValue("empty");
                }

                contentBlocks.Add(ContentBlock.NewTUB(new ToolUseBlock(name: toolRequest.Tool, input: toolParams,
                    id: toolRequest.Id)));
            }
        }


        return new AnthropicMessage(role: message.Role, content: ListModule.OfSeq(contentBlocks));
    }

    private List<SystemPromptBlock> BuildSystemPrompt(CompletionRequest completionRequest)
    {
        var systemPrompt = completionRequest.SystemPrompt;
        var promptBlocks = new List<SystemPromptBlock>();
        if (systemPrompt.Length == 0)
        {
            systemPrompt = DefaultSystemPrompt;
        }

        promptBlocks.Add(new SystemPromptBlock(type: "text", text: systemPrompt,
            cacheControl: FSharpOption<CacheControl>.None));

        if (completionRequest.ContextData != null)
        {
            var contextBlocks = completionRequest.ContextData.Select(p => p.ToString()).ToList();

            if (contextBlocks.Count == 0)
            {
                _logger.Warn("No context data provided in the request");
            }
            else
            {
                _logger.Info($"Adding {contextBlocks.Count} context blocks to the system prompt");
            }


            var additionalSystemPromptBlocks = completionRequest.ContextData
                .Select(p => new SystemPromptBlock(type: "text", text: p.ToString(),
                    cacheControl: FSharpOption<CacheControl>.None))
                .ToList();

            promptBlocks.AddRange(additionalSystemPromptBlocks);
        }

        if (promptBlocks.Count > 0)
        {
            var lastBlock = promptBlocks.Last();
            promptBlocks.Remove(lastBlock);
            // Because... Anthropic
            promptBlocks.Add(new SystemPromptBlock(
                type: lastBlock.Type,
                text: lastBlock.Text,
                cacheControl: new CacheControl(type: "ephemeral")
            ));
        }

        return promptBlocks;
    }


    private List<AnthropicToolDefinition> BuildToolDefinitions(CompletionRequest request)
    {
        var tools = request.ToolDefinitions?.Select(t => new AnthropicToolDefinition(
            name: t.Name,
            description: t.Description,
            inputSchema: JsonConvert.DeserializeObject<JToken>(t.ParametersSchema.ToJson()),
            cacheControl: FSharpOption<CacheControl>.None
        )).ToList() ?? [];

        var lastTool = tools?.LastOrDefault();
        if (lastTool != null)
        {
            tools.Remove(lastTool);
        }

        // Because... Anthropic 
        tools.Add(new AnthropicToolDefinition(
            name: lastTool.Name,
            description: lastTool.Description,
            inputSchema: lastTool.InputSchema,
            cacheControl: new CacheControl(type: "ephemeral")
        ));

        return tools;
    }

    public async Task<CompletionResponse> Complete(CompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var anthropicMessages =
            request.Messages.FindAll(m => m.Role != "system").Select(m => convertMessage(m)).ToList();
        var tools = BuildToolDefinitions(request);

        var anthropicRequest = new AnthropicRequest(
            model: request.Model,
            messages: ListModule.OfSeq(anthropicMessages),
            maxTokens: request.Options?.MaxTokens ?? DefaultMaxTokens,
            systemPrompt: ListModule.OfSeq(BuildSystemPrompt(request)),
            tools: ListModule.OfSeq(tools),
            stream: false);

        _logger.Info($"Completing messages with model {request.Model}");
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var response = await SendRequestAsync(anthropicRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.Info("API request succeeded");
                var jsonString = await response.Content.ReadAsStringAsync();
                var anthropicResponse = AnthropicResponseModule.decode(jsonString);
                if (anthropicResponse.IsOk)
                {
                    var toolInvocationRequests = anthropicResponse.ResultValue.ToolBlocks.Select(t =>
                        new ToolInvocationRequest
                        {
                            Id = t.Id,
                            Tool = t.Name,
                            Parameters = t.Input.Value<JObject>()
                        });


                    var textBlocks = anthropicResponse.ResultValue.TextBlocks.Select(c => c.Text).ToArray();
                    return new CompletionResponse(model: request.Model, textBlocks: textBlocks,
                        toolRequests: toolInvocationRequests.ToArray());
                }
                else
                {
                    throw new ApiError("API response was not ok: " + anthropicResponse.ErrorValue);
                }
            }

            throw new ApiError($"API request failed with status {response.StatusCode} ({(int)response.StatusCode}");
        }, cancellationToken);
    }

    public async Task<bool> StreamComplete(CompletionRequest request, IStreamingResponseHandler handler,
        CancellationToken cancellationToken)
    {
        var anthropicMessages = request.Messages.Select(m => convertMessage(m)).ToList();
        var tools = BuildToolDefinitions(request);


        var anthropicRequest = new AnthropicRequest(
            model: request.Model,
            messages: ListModule.OfSeq(anthropicMessages),
            maxTokens: request.Options?.MaxTokens ?? DefaultMaxTokens,
            systemPrompt: ListModule.OfSeq(BuildSystemPrompt(request)),
            tools: ListModule.OfSeq(tools ?? new List<AnthropicToolDefinition>()),
            stream: true);

        _logger.Debug("Streaming completion from Anthropic API");
        var json = AnthropicRequestModule.encode(anthropicRequest).ToString();
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        _logger.Debug("Content: " + json);

        var httpRequest = new HttpRequestMessage()
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri("https://api.anthropic.com/v1/messages"),
            Content = content,
        };

        ContentBlockStart? currentBlock = null;
        var currentToolJsonString = "";

        var blockHandler = (StreamBlock block) =>
        {
            switch (block)
            {
                case StreamBlock.ContentBlockDelta cbd:
                    switch (cbd.Item.Delta)
                    {
                        case DeltaBlock.TextDelta td:
                            handler.OnToken(td.Item.Text);
                            break;
                        case DeltaBlock.JsonDelta jd:
                            currentToolJsonString += jd.Item.PartialJson;
                            break;
                        default:
                            _logger.Warn($"Unknown delta type {cbd.Item.Delta.GetType()}");
                            break;
                    }

                    break;
                case StreamBlock.ContentBlockStart cb:
                    currentBlock = cb.Item;
                    break;
                case StreamBlock.ContentBlockStop cb:
                    switch (currentBlock?.ContentBlock)
                    {
                        case ContentBlock.TUB tub:
                            var toolInputObject =
                                JsonConvert.DeserializeObject<JObject>(currentToolJsonString)
                                ?? new JObject();

                            currentToolJsonString = String.Empty;

                            handler.OnToolRequest(new ToolInvocationRequest
                            {
                                Id = tub.Item.Id,
                                Tool = tub.Item.Name,
                                Parameters = toolInputObject
                            });
                            break;

                        default:
                            break;
                    }

                    currentBlock = null;
                    break;
                default:
                    _logger.Debug($"Unhandled block type {block.GetType()}");
                    break;
            }
        };

        using (var response = await _httpClient.SendAsync(httpRequest))
        {
            using (Stream responseStream = await response.Content.ReadAsStreamAsync())
            {
                using (var reader = new StreamReader(responseStream))
                {
                    var bufferSize = 8192;
                    var buffer = new char[bufferSize];
                    var offset = 0;
                    var readSize = 128;
                    var stringoffset = 0;

                    while (true)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            handler.OnError(new OperationCanceledException("Operation was canceled"));
                            return false;
                        }

                        var readBuffer = new char[readSize];
                        var bytesRead = await reader.ReadAsync(readBuffer, 0, readSize);
                        if (bytesRead == 0)
                        {
                            _logger.Info($"End of stream reached, {offset} bytes read in total");
                            _logger.Info($"Read: {new string(buffer, 0, offset)}");
                            break;
                        }

                        offset += bytesRead;
                        if (offset >= bufferSize)
                        {
                            bufferSize *= 2;
                            Array.Resize(ref buffer, bufferSize);
                        }

                        Array.Copy(readBuffer, 0, buffer, offset - bytesRead, bytesRead);

                        var line = new string(buffer, 0, offset);
                        var lineoffset = 0;
                        while (lineoffset != -1)
                        {
                            lineoffset = line.IndexOf("\n", stringoffset, StringComparison.Ordinal);
                            if (lineoffset == -1)
                            {
                                break;
                            }

                            var data = line.Substring(stringoffset, lineoffset - stringoffset);
                            stringoffset = lineoffset + 1;
                            if (data.StartsWith("data:"))
                            {
                                data = data.Substring(5);

                                var result = StreamBlockModule.decode(data).ResultValue;
                                if (result == null)
                                {
                                    continue;
                                }

                                blockHandler(result);
                            }
                        }
                    }

                    handler.OnComplete();
                    return true;
                }
            }
        }
    }

    public async Task<CompletionResponse> Generate(CompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Messages.Count == 0)
        {
            throw new InvalidOperationException("No messages to use for the prompt");
        }

        var messages = new List<Message> { request.Messages.Last() };
        var completionRequest = request with
        {
            Model = request.Model,
            Messages = messages,
        };


        return await Complete(completionRequest);
    }

    public async Task<List<ModelDefinition>> GetModels()
    {
        _logger.Info($"Listing models from Anthropic API");
        var response = await _httpClient.GetAsync("https://api.anthropic.com/v1/models");
        _logger.Debug(
            $"Received response from Anthropic API with status {response.StatusCode} and content {await response.Content.ReadAsStringAsync()}");

        if (response.IsSuccessStatusCode)
        {
            var jsonString = await response.Content.ReadAsStringAsync();
            var models = JsonConvert.DeserializeObject<AnthropicModelResponse>(jsonString);
            return models.Data.Select(m => new ModelDefinition
            {
                Name = m.DisplayName,
                Model = m.Id,
                Description = $"Anthropic AI model {m.DisplayName}",
                ContextWindow = 200000,
                MaxTokens = 8192,
                SupportsJsonOutput = true,
                SupportsTextInput = true,
                SupportsTextOutput = true,
                SupportsImageInput = false,
                SupportsImageOutput = false,
                SupportsAudioInput = false,
                SupportsAudioOutput = false,
            }).ToList();
        }

        throw new ApiError($"API request failed with status {response.StatusCode}");
    }


    protected struct AnthropicModel
    {
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("display_name")] public string DisplayName { get; set; }
        [JsonProperty("created_at")] public DateTime CreatedAt { get; set; }
    }

    protected struct AnthropicModelResponse
    {
        [JsonProperty("data")] public List<AnthropicModel> Data { get; set; }
    }
}