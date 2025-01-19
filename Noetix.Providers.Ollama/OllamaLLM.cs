using Newtonsoft.Json.Linq;
using Noetix.LLM.Common;
using Noetix.LLM.Requests;
using Noetix.LLM.Tools;
using Noetix.Ollama.Ollama;
using ServiceStack;

namespace Noetix.Ollama;

public class OllamaConfig(string? baseUrl = null)
{
    public string? BaseUrl { get; set; } = baseUrl;
}


public class OllamaLLM(OllamaConfig config) : LLMProvider
{
    private readonly OllamaClient _client = new(config.BaseUrl ?? "http://localhost:11434");

    private ChatMessage ConvertMessage(Message message)
    {
        return new ChatMessage { Role = message.Role, Content = message.Content };
    }

    public bool SupportsToolsNatively => false;


    public async Task<CompletionResponse> Complete(CompletionRequest request)
    {
        var requestTools = request.ToolDefinitions?.Select(t => new OllamaTool()
        {
            Type = "function",
            Function = new()
            {
                Name = t.Name,
                Parameters = OllamaClient.ConvertJsonSchemaToInputSchema(t.ParametersSchema),
                Description = t.Description
            }
        }).ToList();
            
        var chatMessages = request.Messages.Select(ConvertMessage).ToList();
        if (!request.SystemPrompt.IsNullOrEmpty())
        {
            chatMessages.Insert(0, new ChatMessage { Role = "system", Content = request.SystemPrompt });
        }
            
        var response = await _client.Chat(request.Model, chatMessages, tools: requestTools);
        if (response == null)
        {
            throw new ApiError("No response from Ollama");
        }

        var toolRequests = response.ToolCalls?.Select(tc =>
        {
            var toolInput = new JObject();
            foreach (var (paramName, paramValue) in tc.Function.Arguments)
            {
                toolInput[paramName] = paramValue;
            }

            return new ToolInvocationRequest
            {
                Tool = tc.Function.Name,
                Parameters = toolInput,
                Id = Guid.NewGuid().ToString()
            };
        });
            
        return new CompletionResponse(model: request.Model, textBlocks: new string[]
        {
            response.Message.Content
        }, toolRequests: toolRequests?.ToArray() ?? []);
    }

    public async Task<bool> StreamComplete(CompletionRequest request,
        IStreamingResponseHandler handler)
    {
        await _client.StreamChat(
            request.Model,
            request.Messages.Select(ConvertMessage).ToList(),
            handler.OnToken,
            handler.OnComplete,
            error => throw new Exception(error)
        );
        return true;
    }

    public async Task<CompletionResponse> Generate(CompletionRequest request)
    {
        var response = await _client.Generate(request);
        if (response == null)
        {
            throw new ApiError("No response from Ollama");
        }

        var textBlocks = new string[] { response };
        return new CompletionResponse(model: request.Model, textBlocks: textBlocks);
    }

    public async Task<List<ModelDefinition>> GetModels()
    {
        var response = await _client.ListModels();
        return response.Select(m => new ModelDefinition
        {
            Name = m.Name,
            Description = m.Name,
            Model = m.Name
        }).ToList();
    }
}