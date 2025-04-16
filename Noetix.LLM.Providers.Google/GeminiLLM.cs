using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using Noetix.LLM.Common;
using Noetix.LLM.Providers.Google.Gemini;
using Noetix.LLM.Providers.Google.Gemini.Request;
using Noetix.LLM.Providers.Google.Gemini.Response;
using Noetix.LLM.Requests;
using Noetix.LLM.Tools;
using ServiceStack;
using Content = Noetix.LLM.Providers.Google.Gemini.Request.Content;
using Part = Noetix.LLM.Providers.Google.Gemini.Request.Part;

namespace Noetix.LLM.Providers.Google;

public class GeminiLLM(GeminiLLMConfig llmConfig, HttpClient? httpClient = null) : LLMProvider
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient();
    private readonly string _baseTextUrl = "https://generativelanguage.googleapis.com/v1beta/models";
    private readonly string _modelsUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    private string TextUrl(string model)
    {
        return $"{_baseTextUrl}/{model}:generateContent?key={llmConfig.ApiKey}";
    }

    public bool SupportsToolsNatively => true;

    private readonly GenerationConfig _defaultGenerationOptions = new GenerationConfig
    {
        Temperature = 1.0f,
        TopP = 1.0f,
        TopK = 32,
        MaxOutputTokens = 2048
    };

    private GenerationConfig GetGenerationConfig(GenerationOptions? options)
    {
        if (options == null) return _defaultGenerationOptions;

        return new GenerationConfig
        {
            Temperature = (float)(options.Temperature ?? _defaultGenerationOptions.Temperature),
            TopP = (float)(options.TopP ?? _defaultGenerationOptions.TopP),
            TopK = options.TopK ?? _defaultGenerationOptions.TopK,
            MaxOutputTokens = options.MaxTokens ?? _defaultGenerationOptions.MaxOutputTokens,
        };
    }

    private List<Content> GetContentsFromMessages(List<Message> messages)
    {
        
        
        return messages.Select(m =>
        {
            var parts = new List<Part>();

            if (!string.IsNullOrEmpty(m.Content))
            {
                parts.Add(new Part() { Text = m.Content });
            }
            
            if (m.ToolResults != null)
            {
                foreach (var toolResult in m.ToolResults)
                {
                    if (toolResult.Result == null) continue;
                    
                    var resultsDictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(toolResult.Result);
                    if (resultsDictionary == null) continue;
                    
                    var toolResponse = new JObject();
                    foreach (var arg in resultsDictionary)
                    {
                        toolResponse[arg.Key] = arg.Value switch
                        {
                            JObject jObject => jObject,
                            string str => str,
                            int i => i,
                            double d => d,
                            bool b => b,
                            JArray jArray => jArray,
                            _ => toolResponse[arg.Key]
                        };
                    }


                    parts.Add(new Part()
                    {
                        FunctionResponse = new FunctionResponse()
                        {
                            Name = toolResult.Id,
                            Response = new Response()
                            {
                                Name = toolResult.Id,
                                Content = toolResponse
                            }
                        }
                    });
                }
            }
            
            return new Content()
            {
                Role = m is UserMessage ? "user" : "model",
                Parts = new()
                {
                    new Part() { Text = m.Content }
                }
            };
        }).ToList();
    }

    private async Task<HttpResponseMessage> SendRequestAsync(string model, GeminiMessageRequest request, CancellationToken cancellationToken = default)
    {
        var json = JsonConvert.SerializeObject(request);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        _logger.Info("Content: " + json);

        var url = TextUrl(model);
        _logger.Info($"Sending request to Gemini API with model {model} at {url}");
        var response = await _httpClient.PostAsync(url, content, cancellationToken: cancellationToken);
        _logger.Info(
            $"Received response from Gemini API with status {response.StatusCode} and content {await response.Content.ReadAsStringAsync()}");

        return response;
    }

    public async Task<CompletionResponse> Complete(CompletionRequest request, CancellationToken cancellationToken = default)
    {
        var contents = GetContentsFromMessages(request.Messages);
        
        
        
        try
        {
            var geminiMessageRequest = new GeminiMessageRequest()
            {
                Contents = contents,
                GenerationConfig = GetGenerationConfig(request.Options),
                SystemInstruction = !string.IsNullOrEmpty(request.SystemPrompt)
                    ? new SystemInstruction()
                    {
                        Parts = new List<Part>()
                        {
                            new Part() { Text = request.SystemPrompt }
                        }
                    }
                    : null,
            };

            if (request.ToolDefinitions != null)
            {
                var functionDeclarations = request.ToolDefinitions.Select((t) =>
                {
                    return new FunctionDeclaration()
                    {
                        Description = t.Description,
                        Name = t.Name,
                        Parameters = new Parameters()
                        {
                            Type = "object",
                            Properties = t.ParametersSchema.Properties.ToDictionary(p => p.Key, p =>
                                new Parameter()
                                {
                                    Type = "string",
                                    Description = p.Value.Description ?? "",
                                }),
                        }
                    };
                }).ToList();

                var toolDeclarations = new List<ToolDeclaration>()
                {
                    new ToolDeclaration()
                    {
                        FunctionDeclarations = functionDeclarations
                    }
                };

                geminiMessageRequest.Tools = toolDeclarations;
            }

            var response = await SendRequestAsync(request.Model, geminiMessageRequest, cancellationToken);

            var responseContent = await response.Content.ReadAsStringAsync();

            var geminiResponse = responseContent.FromJson<GeminiMessageResponse>();

            if (geminiResponse.Candidates == null)
            {
                throw new Exception("Gemini response did not contain any candidates.");
            }
            
            var functionBlocks =
                geminiResponse?.Candidates.Select(p => p.Content.Parts[0].FunctionCall).Where(f => f != null).ToArray() ?? [];
            
            var toolInvocationRequests = functionBlocks.Select(t =>
            {
                var toolArgs = new JObject();
                foreach (var arg in t.Args)
                {
                    toolArgs[arg.Key] = arg.Value switch
                    {
                        JObject jObject => jObject,
                        string str => str,
                        int i => i,
                        double d => d,
                        bool b => b,
                        JArray jArray => jArray,
                        _ => toolArgs[arg.Key]
                    };
                }

                return new ToolInvocationRequest
                {
                    Id = Guid.NewGuid().ToString(),
                    Tool = t.Name,
                    Parameters = toolArgs
                };
                }).ToArray();

            var textBlocks = (geminiResponse?.Candidates.Select(p => p.Content.Parts[0].Text).ToArray()) ?? [];
            return new CompletionResponse(model: request.Model, textBlocks: textBlocks,
                toolRequests: toolInvocationRequests);
        }
        catch (Exception e)
        {
            _logger.Error("Unable to complete request", e);
            throw new Exception("Unable to complete request", e);
        }
    }


    public async Task<bool> StreamComplete(CompletionRequest request, IStreamingResponseHandler handler,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Streaming is not supported by Vertex API.");
    }

    public async Task<CompletionResponse> Generate(CompletionRequest request, CancellationToken cancellationToken = default)
    {
        return await Complete(request);
    }

    public async Task<List<ModelDefinition>> GetModels()
    {
        var response = await _httpClient.GetAsync(_modelsUrl);
        var responseContent = await response.Content.ReadAsStringAsync();
        var modelsResponse = responseContent.FromJson<RootGeminiModelResponse>();

        if (modelsResponse != null)
        {
            return modelsResponse.GeminiModelResponses.Where(m => m != null).Select(m => new ModelDefinition
            {
                Model = m.Name.Replace("models/", ""),
                Name = m.DisplayName,
                Description = m.Description + "\n\nVersion: " + m.Version +
                              $"\n\nSupported uses: {string.Join(", ", m.SupportedGenerationMethods)}.",
                MaxTokens = m.OutputTokenLimit,
            }).ToList();
        }

        return new List<ModelDefinition>();
    }
}