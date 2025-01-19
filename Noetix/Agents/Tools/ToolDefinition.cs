using NJsonSchema;

namespace Noetix.Agents.Tools;

public record ToolDefinition
{
    public string Name { get; init; }
    public string Purpose { get; init; }
    public string Description { get; init; }
    public JsonSchema ParametersSchema { get; init; }
}