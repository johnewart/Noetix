using NJsonSchema;

namespace Noetix.Agents.Tools;

public class ToolResult<O>
{
    public bool Success { get; set; } = true;
    public string? Error { get; set; }
    public string Message { get; set; }
    public O? Result { get; set; }
}

public class ToolResultsSchema(Type inner)
{
    public readonly JsonSchema Schema = JsonSchema.FromType(inner);

    public ValidationOutcome Validate(string input)
    {
        var result = Schema.Validate(input).ToList();
        return new ValidationOutcome { IsValid = (result.Count == 0), Error = String.Join(", ", result) };
    }
}