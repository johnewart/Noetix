namespace Noetix.Agents.Status;

public record AssistantStatusMessage(
    string Title,
    AssistantStatusKind Kind,
    AssistantStatusState State,
    string Message,
    string AssistantName,
    string? Id = null,
    long? Timestamp = null,
    Dictionary<string, string>? Attributes = null
);