namespace Noetix.Agents.Tools;

public class ToolParseError(string message, string block) : Exception(message)
{
    public string Block { get; } = block;
}