using NLog;
using Noetix.LLM.Common;
using Noetix.LLM.Requests;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;
using ServiceStack;

namespace Noetix.LLM.Providers.OpenAI;

public class OpenAILLM : LLMProvider
{

    private Logger _logger = LogManager.GetCurrentClassLogger();
    
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


    private string GenerateSystemPrompt(CompletionRequest request)
    {
        var prompt = request.SystemPrompt;
        var contextData = request.ContextData?.Select(p => p.ToXmlLikeBlock()).ToList() ?? [];
        prompt += $"\n\n<CONTEXT>\n{contextData.Join("\n\n")}\n</CONTEXT>";

        prompt += request.ResponseSchema != null
            ? $"\n\n<RESPONSE_FORMAT>\n{request.GenericResponseSchemaPreamble}\n</RESPONSE_FORMAT>\n"
            : "";
        return prompt;
    }
    
    public async Task<CompletionResponse> Complete(CompletionRequest request, CancellationToken cancellationToken = default)
    {
          
        try
        {
            var messages = new List<ChatMessage>(){ new SystemChatMessage(GenerateSystemPrompt(request)) };
            messages.AddRange(request.Messages.Select(ConvertMessage).ToList());
            
            var chat = client.GetChatClient(request.Model);
            // var options = new ChatCompletionOptions()
            // {
            //     ResponseFormat = request.ResponseSchema != null
            //         ? ChatResponseFormat.CreateJsonSchemaFormat("json_schema",
            //             BinaryData.FromString(request.ResponseSchema.ToJson()))
            //         : ChatResponseFormat.CreateTextFormat()
            // };
            
            // options.ResponseFormat = ChatResponseFormat.CreateTextFormat();
            // var options = new ChatCompletionOptions()
            // {
            //     ResponseFormat = ChatResponseFormat.CreateTextFormat()
            // };
            
            var response = (await chat.CompleteChatAsync(messages: messages, options: null, cancellationToken: cancellationToken))
                .Value;
            if (response.Content == null)
            {
                throw new ApiError("No response content");
            }

            var textBlocks = response.Content.Select(c => c.Text).ToArray();
            var result = new CompletionResponse(model: request.Model, textBlocks: textBlocks);
        
            return result;
        }
        catch (Exception e)
        {
            _logger.Error(e);
            throw new ApiError(e.Message);
        }

       
    }

    public async Task<bool> StreamComplete(CompletionRequest request, IStreamingResponseHandler handler, CancellationToken cancellationToken)
    {
        var systemPrompt = GenerateSystemPrompt(request);
        var messages = new List<ChatMessage>(){ new SystemChatMessage(systemPrompt) };
        messages.AddRange(request.Messages.Select(ConvertMessage).ToList());
        
        _logger.Info("Streaming completion for {model}", request.Model);
        var chat = client.GetChatClient(request.Model);
        var response = chat.CompleteChatStreamingAsync(messages: messages, cancellationToken: cancellationToken);
        
        await foreach (var message in response)
        {
            if (message?.ContentUpdate == null) continue;
            
            var textBlocks = message.ContentUpdate.Select(c => c.Text).ToArray();
                
            _logger.Info("Calling handler.OnToken with text blocks: {TextBlocks}", textBlocks.Join(" "));
            handler.OnToken(textBlocks.Join(" "));
        }

        return true;
    }

    public async Task<CompletionResponse> Generate(CompletionRequest request, CancellationToken cancellationToken = default)
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
