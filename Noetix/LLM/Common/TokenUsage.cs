namespace Noetix.LLM.Common;

public class TokenUsage(int promptTokens, int completionTokens, int totalTokens)
{
    public int PromptTokens { get; set; } = promptTokens;
    public int CompletionTokens { get; set; } = completionTokens;
    public int TotalTokens { get; set; } = totalTokens;
}