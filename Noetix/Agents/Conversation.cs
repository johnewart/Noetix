using Noetix.Agents.Status;
using NLog;
using Noetix.Agents.Tools;
using Noetix.LLM.Requests;
using Newtonsoft.Json;
using Noetix.LLM.Common;
using Noetix.LLM.Tools;

namespace Noetix.Agents;

public enum ResponseReason
{
    ToolHelpRequest,
    ToolsRequest,
    MemoryRequest
}

public class Conversation
{
    private readonly ToolProcessor? _toolProcessor;
    private readonly MemoryProcessor? _memoryProcessor;
    private readonly LLMProvider _llm;
    private readonly string _model;
    private readonly int? _maxDepth;
    private readonly List<Message> _thread;
    private readonly int _startingThreadSize;
    private readonly Action<Message>? _onMessage;
    private readonly GenerationOptions _options; 

    private Logger logger = LogManager.GetCurrentClassLogger();
    private readonly string _systemPrompt;
    private readonly List<ToolDefinition>? _toolDefinitions;
    private readonly Assistant _assistant;
    private readonly CancellationToken _cancellationToken;
    private readonly Action<string>? _streamHandler;

    public Conversation(
        Assistant assistant,
        string systemPrompt,
        string model,
        LLMProvider llm,
        ToolProcessor? toolProcessor,
        MemoryProcessor? memoryProcessor,
        List<ToolDefinition>? toolDefinitions,
        int? maxDepth,
        List<Message>? history = null,
        GenerationOptions? options = null,
        Action<Message>? onMessage = null,
        Action<string>? streamHandler = null,
        CancellationToken cancellationToken = default
    )
    {
        _assistant = assistant;
        _toolProcessor = toolProcessor;
        _memoryProcessor = memoryProcessor;
        _llm = llm;
        _maxDepth = maxDepth;
        _thread = new List<Message>();
        _model = model;
        _systemPrompt = systemPrompt;
        if (history != null)
        {
            _thread.AddRange(history);
        }
        _startingThreadSize = _thread.Count;
        _onMessage = onMessage;
        _options = options;
        _toolDefinitions = toolDefinitions;
        _streamHandler = streamHandler;
        _cancellationToken = cancellationToken;
    }

    public async Task<AssistantMessage> Send(UserMessage message)
    {
        _assistant.UpdateStatus(AssistantStatusKind.Chat, AssistantStatusState.Working, "Sending message...", "Sending message...");
        var maxDepth = 200; //_maxDepth ?? 20;        
        
        if (_thread.Count - _startingThreadSize > maxDepth)
        {
            throw new Exception($"Max depth ({maxDepth}) reached");
        }

        logger.Trace($"Sending message to {_llm.ToString()}: {message.Content}");
        _thread.Add(message);
        _onMessage?.Invoke(message);

        var request = new CompletionRequest
        {
            Messages = _thread,
            Model = _model,
            SystemPrompt = _systemPrompt,
            Options = _options,
            ToolDefinitions = _toolDefinitions,
            
        };

        
        
        var response = (_streamHandler != null) ? await StreamMessage(request): await _llm.Complete(request);
        
        logger.Info($"Received response from LLM: {response.Content}");
        try
        {
            var messageContent = response.Content;
            logger.Info($"Received response: {messageContent}");
            messageContent = _memoryProcessor?.ExtractMemories(messageContent) ?? messageContent;
            var assistantMessage = new AssistantMessage(messageContent, toolRequests: response.ToolInvocations);
            _onMessage?.Invoke(assistantMessage);
            _thread.Add(assistantMessage);

            if (response.ToolInvocations is { Count: > 0 } && _toolProcessor != null)
            {
                _assistant.UpdateStatus(AssistantStatusKind.Tool, AssistantStatusState.Started, "Processing tool requests", $"Processing {response.ToolInvocations.Count} tool requests...");
                var results = await _toolProcessor.Process(response.ToolInvocations, ToolStatusHandler);
                _assistant.UpdateStatus(AssistantStatusKind.Tool, AssistantStatusState.Completed, "Tool requests processed", $"Processed {response.ToolInvocations.Count} tool requests.");
                var messages = results.Select(r =>
                {
                    var successStatus = r.Success
                        ? "successfully."
                        : $"with the following error: {r.Error}";
                    return $"{r.ToolId} executed {successStatus}";
                }).ToList();
                var responseMessage = new UserMessage(content: $"{string.Join("\n\n", messages)}",
                    toolResults: results.ToList());
                return await Send(responseMessage);
            }

            var triggers = ResponseTriggers(messageContent).ToList();
            // If no triggers are found, return the message as is
            if (!triggers.Any()) return new AssistantMessage(messageContent);
            _assistant.UpdateStatus(AssistantStatusKind.Status, AssistantStatusState.Working, "Processing triggers", $"Responding to {triggers.Count()} triggers...");
                
            // Otherwise, process the triggers
            var userResponse = await GenerateResponse(messageContent, triggers);
            logger.Info($"Generated response: {userResponse.Content}");
            return await Send(userResponse);

        } catch (Exception e)
        {
            logger.Error(e, "Error processing response");
            return new AssistantMessage("I'm sorry, I'm having trouble processing that request.");
        }
    }
    
    private void ToolStatusHandler(ToolStatusUpdate update)
    {
        _assistant.UpdateStatus(AssistantStatusKind.Tool, update.State switch
        {
            ToolState.Running => AssistantStatusState.Working,
            ToolState.Completed => AssistantStatusState.Completed,
            ToolState.Failed => AssistantStatusState.Failed,
            _ => AssistantStatusState.Working
        }, $"Tool {update.ToolId} status update", $"Tool {update.ToolId}: {update.Message}");
    }

    private async Task<CompletionResponse> StreamMessage(CompletionRequest request)
    {
        if (_streamHandler == null)
        {
            throw new Exception("Stream handler is not set");
        }
        var messageContent = "";
        var toolRequests = new List<ToolInvocationRequest>();
        
        var messageAccumulator = new Assistant.StreamHandler(token =>
        {
            
            _streamHandler.Invoke(token);
            messageContent += token;
        }, onToolRequest: invocation => toolRequests.Add(invocation));
        
        await _llm.StreamComplete(request, messageAccumulator, _cancellationToken);
        CompletionResponse response =
            new CompletionResponse(request.Model, [messageContent], toolRequests: toolRequests.ToArray()); 
        
        return response;
    }

    private async Task<UserMessage> GenerateResponse(string content, List<ResponseReason> triggers)
    {
        var result = "";

        for (int i = 0; i < triggers.Count(); i++)
        {
            var trigger = triggers.ElementAt(i);
            switch (trigger)
            {
                case ResponseReason.ToolsRequest:
                    if (_toolProcessor == null)
                    {
                        logger.Info("Tools request received, but no tool processor is available");
                        break;
                    }
                    var toolResults = await _toolProcessor.ExtractAndProcess(content, update => {
                        _assistant.UpdateStatus(AssistantStatusKind.Tool, update.State switch
                            {
                                ToolState.Running => AssistantStatusState.Working,
                                ToolState.Completed => AssistantStatusState.Completed,
                                ToolState.Failed => AssistantStatusState.Failed,
                                _ => AssistantStatusState.Working
                            }, "Update on tool ${update.ToolId}", $"Tool {update.ToolId}: {update.Message}");
                    });
                     var toolResultMessages = toolResults.Select(r =>
                        "<tool_result>\n" + JsonConvert.SerializeObject(r.Result) + "\n</tool_result>");
                    var toolResultContent = string.Join("\n\n", toolResultMessages);
                    result += toolResultContent;
                    break;
                case ResponseReason.ToolHelpRequest:
                    result += _toolProcessor?.ProcessToolHelp(content);
                    break;
            }
        }

        return new UserMessage(content: result);
    }




    private IEnumerable<ResponseReason> ResponseTriggers(string content)
    {
        var triggers = new List<ResponseReason>();

        if (content.Contains("<tools>"))
        {
            triggers.Add(ResponseReason.ToolsRequest);
        }
        if (content.Contains("<tool_help tool_id"))
        {
            triggers.Add(ResponseReason.ToolHelpRequest);
        }

        return triggers;
    }
}