using Codecs;
using Microsoft.FSharp.Collections;
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

    private RetryPolicy _retryPolicy = new RetryPolicy(
        maxAttempts: 4,
        initialDelay: TimeSpan.FromSeconds(3),
        maxDelay: TimeSpan.FromSeconds(30),
        backoffStrategy: RetryPolicy.BackoffStrategy.Exponential,
        shouldRetry: ex => ex is Exception
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

    private async Task<HttpResponseMessage> SendRequestAsync(AnthropicRequest request)
    {
        var json = AnthropicRequestModule.encode(request).ToString();
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        _logger.Info("Content: " + json);

        _logger.Info($"Sending request to Anthropic API with model {request.Model}");
        var response = await _httpClient.PostAsync("https://api.anthropic.com/v1/messages", content);
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

        if (message.Content != "")
        {
            contentBlocks.Add(ContentBlock.NewTCB(new TextContentBlock(message.Content)));
        }

        return new AnthropicMessage(role: message.Role, content: ListModule.OfSeq(contentBlocks));
    }


    public async Task<CompletionResponse> Complete(CompletionRequest request)
    {
        var DefaultMaxTokens = 8192;

        var anthropicMessages =
            request.Messages.FindAll(m => m.Role != "system").Select(m => convertMessage(m)).ToList();
        var tools = request.ToolDefinitions?.Select(t => new AnthropicToolDefinition(
            name: t.Name,
            description: t.Description,
            inputSchema: JsonConvert.DeserializeObject<JToken>(t.ParametersSchema.ToJson())
        ));
        var anthropicRequest = new AnthropicRequest(
            model: request.Model,
            messages: ListModule.OfSeq(anthropicMessages),
            maxTokens: request.Options?.MaxTokens ?? DefaultMaxTokens,
            systemPrompt: request.SystemPrompt,
            tools: ListModule.OfSeq(tools ?? new List<AnthropicToolDefinition>()));

        _logger.Info($"Completing messages with model {request.Model}");
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var response = await SendRequestAsync(anthropicRequest);

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

            throw new ApiError($"API request failed with status {response.StatusCode}");
        });
    }

    public async Task<bool> StreamComplete(CompletionRequest request, IStreamingResponseHandler handler)
    {
        var anthropicMessages = request.Messages.Select(m => convertMessage(m)).ToList();
        // var request = new AnthropicRequest { Model = options.Model, Messages = anthropicMessages, MaxTokens = 1024, Tools = null };
        var anthropicRequest = new AnthropicRequest(
            model: request.Model,
            messages: ListModule.OfSeq(anthropicMessages),
            maxTokens: 1024,
            systemPrompt: request.SystemPrompt ?? DefaultSystemPrompt,
            tools: null);

        var response = await SendRequestAsync(anthropicRequest);

        if (response.IsSuccessStatusCode)
        {
            var stream = await response.Content.ReadAsStreamAsync();
            using (var reader = new StreamReader(stream))
            {
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    handler.OnToken(line);
                }
            }

            handler.OnComplete();
            return true;
        }

        throw new ApiError($"API request failed with status {response.StatusCode}");
    }

    public async Task<CompletionResponse> Generate(CompletionRequest request)
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
        var models = new List<ModelDefinition>()
        {
            new ModelDefinition
            {
                Name = "Claude Sonnet 3.5 (latest)",
                Model = "claude-3-5-sonnet-latest",
                Description =
                    "Great for reasoning, coding, multilingual tasks, long-context handling, honesty, and image processing. Generates rich and coherent text.",
                ContextWindow = 200000,
                MaxTokens = 8192,
                SupportsJsonOutput = true,
                SupportsTextInput = true,
                SupportsTextOutput = true,
                SupportsImageInput = true,
                SupportsImageOutput = false,
                SupportsAudioInput = false,
                SupportsAudioOutput = false,
            },
            new ModelDefinition
            {
                Name = "Claude Haiku 3.5 (latest) - 2",
                Model = "claude-3-5-haiku-latest-2",
                Description =
                    "Faster and less expensive than Sonnet, but less accurate. Great for generating shorter text.",
                ContextWindow = 200000,
                MaxTokens = 8192,
                SupportsJsonOutput = true,
                SupportsTextInput = true,
                SupportsTextOutput = true,
                SupportsImageInput = false,
                SupportsImageOutput = false,
                SupportsAudioInput = false,
                SupportsAudioOutput = false,
            }
        };
        return models;
    }
}