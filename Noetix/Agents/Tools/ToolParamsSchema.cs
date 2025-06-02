using NJsonSchema;
using NJsonSchema.Validation;

namespace Noetix.Agents.Tools;

public record ValidationOutcome
{
    public bool IsValid { get; set; }
    public string? Error { get; set; }
}

public class ToolParamsSchema
{
    public readonly JsonSchema Schema;

    public ToolParamsSchema(Type inner)
    {
        Schema = JsonSchema.FromType(inner);
    }

    public ToolParamsSchema(JsonSchema schema)
    {
        Schema = schema;
    }
    
    public ValidationOutcome Validate(string input)
    {
        var settings = new JsonSchemaValidatorSettings
        {
            PropertyStringComparer = StringComparer.OrdinalIgnoreCase
        };
        var result = Schema.Validate(input, settings).ToList();
        return new ValidationOutcome { IsValid = (result.Count == 0), Error = String.Join(", ", result) };
    }
}