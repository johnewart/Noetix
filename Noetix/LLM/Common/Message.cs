using Noetix.Agents.Tools;
using Noetix.LLM.Tools;

namespace Noetix.LLM.Common;

public abstract class Message(
    string role,
    string content,
    DateTime? timestamp = null,
    List<ToolResults>? toolResults = null,
    List<ToolInvocationRequest> toolRequests = null)
{
    public string Role { get; } = role;
    public string Content { get; } = content;

    public DateTime Timestamp { get; init; } = timestamp ?? DateTime.Now;

    public List<ToolResults>? ToolResults { get; init; } = toolResults;

    public List<ToolInvocationRequest>? ToolRequests { get; init; } = toolRequests;
}

public class AssistantMessage(
    string content,
    DateTime? timestamp = null,
    List<ToolResults>? toolResults = null,
    List<ToolInvocationRequest>? toolRequests = null)
    : Message("assistant", content, timestamp, toolResults: toolResults, toolRequests: toolRequests);

public class UserMessage(
    string content,
    DateTime? timestamp = null,
    List<ToolResults>? toolResults = null,
    List<ToolInvocationRequest>? toolRequests = null) : Message("user", content, timestamp, toolResults: toolResults,
    toolRequests: toolRequests);

public class SystemMessage(
    string content,
    DateTime? timestamp = null,
    List<ToolResults>? toolResults = null,
    List<ToolInvocationRequest>? toolRequests = null) : Message("system", content, timestamp, toolResults: toolResults,
    toolRequests: toolRequests);

public class DebugMessage(
    string content,
    DateTime? timestamp = null,
    List<ToolResults>? toolResults = null,
    List<ToolInvocationRequest>? toolRequests = null) : Message("debug", content, timestamp, toolResults: toolResults,
    toolRequests: toolRequests);