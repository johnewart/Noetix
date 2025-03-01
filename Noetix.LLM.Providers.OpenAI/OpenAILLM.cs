using Noetix.LLM.Common;
using Noetix.LLM.Requests;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;
using ServiceStack;

namespace Noetix.LLM.Providers.OpenAI;

public class OpenAILLM : LLMProvider
{

    public class OpenAIConfig
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
        var apiKey = config.ApiKey.IsNullOrEmpty() ? "dummykey" : config.ApiKey;
        var credentials = new System.ClientModel.ApiKeyCredential(apiKey);
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
        var messages = new List<ChatMessage>(){ new SystemChatMessage(request.SystemPrompt) };
        messages.AddRange(request.Messages.Select(ConvertMessage).ToList());
        
        
        var chat = client.GetChatClient(request.Model);
        var response = (await chat.CompleteChatAsync(messages: messages)).Value;

        if (response.Content == null)
        {
            throw new ApiError("No response content");
        }

        var textBlocks = response.Content.Select(c => c.Text).ToArray();
        var result = new CompletionResponse(model: request.Model, textBlocks: textBlocks);
        
        return result;
    }

    public async Task<bool> StreamComplete(CompletionRequest request, IStreamingResponseHandler handler, CancellationToken cancellationToken)
    {
        var messages = new List<ChatMessage>(){ new SystemChatMessage(request.SystemPrompt) };
        messages.AddRange(request.Messages.Select(ConvertMessage).ToList());
        
        var chat = client.GetChatClient(request.Model);
        var response = chat.CompleteChatStreamingAsync(messages: messages, cancellationToken: cancellationToken);
        
        await foreach (var message in response)
        {
            if (message.ContentUpdate != null)
            {
                var textBlocks = message.ContentUpdate.Select(c => c.Text).ToArray();
                handler.OnToken(textBlocks.Join(" "));
            }
        }

        return true;
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
