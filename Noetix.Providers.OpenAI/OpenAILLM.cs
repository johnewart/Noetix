using Noetix.LLM.Common;
using Noetix.LLM.Requests;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;

namespace Noetix.OpenAI;

public class OpenAILLM : LLMProvider
{

    public record OpenAIConfig
    {
        public string ApiKey { get; set; }
        public string BaseUrl { get; set; }
    }

    private readonly OpenAIConfig config;
    private readonly OpenAIClient client;
    private readonly OpenAIModelClient modelClient;

    public OpenAILLM(OpenAIConfig config)
    {
        this.config = config;
        var credentials = new System.ClientModel.ApiKeyCredential(config.ApiKey);
        var options = new OpenAIClientOptions { Endpoint = new Uri(config.BaseUrl) };
        this.client = new OpenAIClient(credentials, options);
        this.modelClient = new OpenAIModelClient(credentials, options);
    }

    private global::OpenAI.Chat.ChatMessage ConvertMessage(Message message)
    {
        return message.Role switch
        {
            "system" => new SystemChatMessage(message.Content),
            "user" => new global::OpenAI.Chat.UserChatMessage(message.Content),
            "assistant" => new AssistantChatMessage(message.Content),
            _ => throw new NotImplementedException()
        };       
    }
    
    public bool SupportsToolsNatively => true;


    public async Task<CompletionResponse> Complete(CompletionRequest request)
    {

        var chat = client.GetChatClient(request.Model);
        var response = chat.CompleteChat(messages: request.Messages.Select(ConvertMessage)).Value;

        if (response.Content == null)
        {
            throw new ApiError("No response content");
        }

        var textBlocks = response.Content.Select(c => c.Text).ToArray();
        
        return new CompletionResponse(model: request.Model, textBlocks: textBlocks);
    }

    public async Task<bool> StreamComplete(CompletionRequest request, IStreamingResponseHandler handler)
    {
        throw new NotImplementedException();
    }

    public async Task<CompletionResponse> Generate(CompletionRequest request)
    {
        throw new NotImplementedException();
    }
    

    public async Task<List<ModelDefinition>> GetModels()
    {
        var models = modelClient.GetModels();
        return models.Value.Select(m => new ModelDefinition {
            Name = m.Id,
            Model = m.Id,
            
        }).ToList();
    }
}
