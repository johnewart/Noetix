
using System.Text.Json.Nodes;

namespace Noetix.Agents.Tools;

public record ToolExample
{
    public JsonObject Input { get; set; }
    public JsonObject Output { get; set; }
    public string? Explanation { get; set; }
}