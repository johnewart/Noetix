namespace Noetix.Agents.Tools;

public class ToolResults(string toolId, string id, bool success, string? error = null, string? result = "")
{
    public string ToolId { get; } = toolId; 
    public string Id { get; } = id;
    public bool Success { get; } = success;
    public string? Error { get; } = error;
    public string? Result { get; } = result;

    public ToolResults() : this("", "", false)
    {
    }
}