using Noetix.Agents.Tools;
using Noetix.LLM.Common;

namespace Noetix.LLM.Requests;

public record CompletionRequest
{
    public required string Model { get; set; }
    public required string SystemPrompt { get; set; }
    public required List<Message> Messages { get; set; }
        
    public GenerationOptions? Options { get; set; }
    public List<ToolDefinition>? ToolDefinitions { get; set; }
    public IStreamingResponseHandler? StreamingHandler { get; set; }
}