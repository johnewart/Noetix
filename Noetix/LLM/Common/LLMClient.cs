namespace Noetix.LLM.Common;

public record LLMClient
{
    public required string DefaultModel { get; init; }
    public required LLMProvider Provider { get; init; }
    public GenerationOptions? DefaultOptions { get; init; }
}