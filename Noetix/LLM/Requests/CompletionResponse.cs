

using Noetix.LLM.Common;
using Noetix.LLM.Tools;

namespace Noetix.LLM.Requests;

public class CompletionResponse(
    string model,
    string[] textBlocks,
    TokenUsage? usage = null,
    ToolInvocationRequest[]? toolRequests = null)
{
    public string Content { get; init; } = String.Join(" ", textBlocks);
    public string Model { get; init; } = model;
    public TokenUsage? Usage { get; init; } = usage;
    public List<ToolInvocationRequest>? ToolInvocations { get; init; } = toolRequests != null ?  new List<ToolInvocationRequest>(toolRequests) : null;
    public string[] TextBlocks { get; init; } = textBlocks;
}