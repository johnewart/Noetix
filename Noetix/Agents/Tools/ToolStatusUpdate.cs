namespace Noetix.Agents.Tools;

public enum ToolState
{
    Running,
    Completed,
    Failed
}
public class ToolStatusUpdate
{
    public string InvocationId { get; init; }
    public string ToolId { get; init;}
    public ToolState State { get; init;}
    public string Message { get; init;}
    public Dictionary<string, object> Data { get; init;}
}