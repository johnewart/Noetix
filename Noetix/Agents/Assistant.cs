using System.Security.Cryptography;
using System.Text;
using Noetix.Agents.Status;
using Noetix.Agents.Tools;
using Noetix.LLM.Common;
using Noetix.LLM.Requests;
using Noetix.LLM.Tools;

namespace Noetix.Agents;

public class Assistant(
    string name,
    LLMProvider llm,
    string model,
    string greeting = null,
    string instructions = null,
    string description = null,
    List<AssistantTool>? tools = null,
    ChatHistoryStore? chatHistoryStore = null,
    MemoryStore? memoryStore = null,
    Action<AssistantStatusMessage>? onStatusUpdate = null,
    GenerationOptions? defaultGenerationOptions = null)
{
    public readonly string Name = name;
    public readonly LLMProvider LLM = llm;
    private readonly string? _greeting = greeting;
    private readonly List<AssistantTool> _tools = tools ?? [];

    public SystemMessage SystemPrompt()
    {
        var toolDefinitions = _tools.Select(t => new ToolDefinition
        {
            Name = t.Id,
            Description = t.Description,
            ParametersSchema = t.Parameters.Schema
        }).ToList();

        var memories = memoryStore?.All().Select(m => m.MemoryContent).ToList();

        var usefulInfo = new List<InfoBit>
        {
            new("Current date and time", DateTime.Now.ToString())
        };

        var context = new SystemPromptContext
        {
            AssistantName = Name,
            NeedsToolInstructions = !LLM.SupportsToolsNatively,
            NeedsMemoryInstructions = memoryStore != null,
            UsefulInfo = usefulInfo,
            Instructions = instructions,
            Persona = description,
            Memories = memories,
            ToolDefinitions = toolDefinitions
        };
        return new(SystemPromptBuilder.Generate(context));
    }


    public AssistantMessage Greeting => new(_greeting ?? "Hello, how can I help you today?");

    private List<Message> History(string chatSessionId, int? maxLength = null)
    {
        return chatHistoryStore?.History(chatSessionId, maxLength) ?? [];
    }

    public async Task<AssistantMessage> Stream(
        string chatSessionId,
        UserMessage prompt,
        Action<string> streamHandler,
        CancellationToken cancellationToken = default,
        GenerationOptions? options = null)
    {
        var responseText = "";
        var wrapper = new Action<string>(token =>
        {
            responseText += token;
            streamHandler(token);
        });
        
        var handler = new StreamHandler(wrapper);
        var llmOptions = defaultGenerationOptions?.OverrideWith(options) ?? options;
        var messages = chatHistoryStore?.History(chatSessionId: chatSessionId) ?? new List<Message>();
        messages.Add(prompt);
        
        chatHistoryStore?.Store(chatSessionId, prompt);
        
        var completionRequest = new CompletionRequest
        {
            SystemPrompt = this.SystemPrompt().Content,
            Model = model,
            Options = llmOptions,
            Messages = messages,
        };
            
        await LLM.StreamComplete(completionRequest, handler, cancellationToken);
        
        var response = new AssistantMessage(responseText);
        if (!cancellationToken.IsCancellationRequested)
        {
            chatHistoryStore?.Store(chatSessionId, response);
        }

        return response;
    }

    public async Task<AssistantMessage> Chat(
        UserMessage prompt,
        GenerationOptions? options = null,
        string chatSessionId = "default",
        int maxDepth = 10, 
        Action<Message>? onMessageCallback = null,
        Action<string>? streamHandler = null,
        CancellationToken cancellationToken = default
    )
    {
        UpdateStatus(AssistantStatusKind.Chat, AssistantStatusState.Started, "Generating response....", "Generating response....");
            
        var toolProcessor = new ToolProcessor(_tools);
        var memoryProcessor = memoryStore != null ? new MemoryProcessor(memoryStore) : null;
        var onMessage = new Action<Message>((message) =>
        {
            chatHistoryStore?.Store(chatSessionId, message);
            onMessageCallback?.Invoke(message);
        });

        var llmOptions = defaultGenerationOptions?.OverrideWith(options) ?? options;

        var conversation = new Conversation(
            assistant: this,
            systemPrompt: SystemPrompt().Content,
            model: model,
            llm: LLM,
            toolProcessor: toolProcessor,
            memoryProcessor: memoryProcessor,
            toolDefinitions: ToolDefinitions,
            maxDepth: maxDepth,
            History(chatSessionId),
            llmOptions,
            onMessage,
            streamHandler: streamHandler, 
            cancellationToken: cancellationToken); 

            
        var result = await conversation.Send(prompt);
        UpdateStatus(AssistantStatusKind.Chat, AssistantStatusState.Completed, "Completed", "Response generated");
        return result;
    }

    public async Task<AssistantMessage> Generate(
        string prompt, 
        GenerationOptions? options = null,             
        Action<Message>? onMessageCallback = null
    )
    {
        UpdateStatus(AssistantStatusKind.Chat,  AssistantStatusState.Started, "Generating response....", "Generating response....");

        var toolProcessor = new ToolProcessor(_tools ?? new List<AssistantTool>());

        var llmOptions = defaultGenerationOptions?.OverrideWith(options);
            
        var conversation = new Conversation(
            assistant: this,
            systemPrompt: SystemPrompt().Content,
            model: model,
            llm: LLM,
            toolProcessor: toolProcessor,
            memoryProcessor: null,
            toolDefinitions: ToolDefinitions,
            maxDepth: 2, // One message for the prompt, one for the response
            history: null,
            options: llmOptions,
            onMessage: onMessageCallback);

        var result = await conversation.Send(new(prompt));

        UpdateStatus(AssistantStatusKind.Chat, AssistantStatusState.Completed, "Completed", "Response generated");

        return result;
    }

    private List<ToolDefinition> ToolDefinitions =>  LLM.SupportsToolsNatively ?
        _tools.Select(t => new ToolDefinition
        {
            Name = t.Id,
            Description = t.Description,
            ParametersSchema = t.Parameters.Schema
        }).ToList() : [];


    public void UpdateStatus(AssistantStatusKind kind, AssistantStatusState state, string title, string message)
    {
        var status = new AssistantStatusMessage(
            Title: "Processing",
            Kind: kind,
            Message: message,
            AssistantName: Name,
            State: state);
        var id = status.Id ?? Md5(status.Message + status.AssistantName + status.Kind + status.State);
        onStatusUpdate?.Invoke(status);
    }

    public List<Memory> Memories => memoryStore?.All().ToList() ?? new List<Memory>();

    public void ClearHistory(string chatSessionId)
    {
        chatHistoryStore?.Clear(chatSessionId);
    }

    public void DeleteMessages(string chatSessionId, List<int> messageIds)
    {
        foreach (var id in messageIds)
        {
            chatHistoryStore?.Remove(chatSessionId, id);
        }
    }

    private static string Md5(string s)
    {
        using var md5 = MD5.Create();
        var inputBytes = Encoding.ASCII.GetBytes(s);
        var hashBytes = md5.ComputeHash(inputBytes);
        return string.Concat(hashBytes.Select(b => b.ToString("x2")));
    }
    
    public class StreamHandler : IStreamingResponseHandler
    {
        private string _responseText = "";
        private readonly Action<string> _streamHandler;
        private readonly Action<AssistantMessage>? _onComplete;
        private readonly Action<ToolInvocationRequest>? _onToolRequest;
        
        public StreamHandler(Action<string> streamHandler, Action<AssistantMessage>? onComplete = null, Action<ToolInvocationRequest >? onToolRequest = null)
        {
            _streamHandler = streamHandler;
            _onComplete = onComplete;
            _onToolRequest = onToolRequest;
        }
        
        public void OnToken(string token)
        {
            _responseText += token;
            _streamHandler(token);
        }

        public void OnToolRequest(ToolInvocationRequest toolRequest)
        {
            _onToolRequest?.Invoke(toolRequest);             
        }
        
        public void OnComplete()
        {
            var message = new AssistantMessage(_responseText);
            _onComplete?.Invoke(message);
        }

        public void OnError(Exception error) => _streamHandler(error.Message);
    }
}