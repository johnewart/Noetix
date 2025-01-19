namespace Noetix.LLM.Common;

public class GenerationOptions
{
    public double? Temperature  { get; set; }
    public int? MaxTokens  { get; set; }
    public double? TopP  { get; set; }
    public int? TopK  { get; set; }
    public double? PresencePenalty  { get; set; }
    public double? FrequencyPenalty  { get; set; }
    public List<string>? StopSequences  { get; set; }
    public int? Seed  { get; set; }
        

    public GenerationOptions(
        double temperature = 0.7,
        double topP = 1.0,
        int topK = 50,
        double presencePenalty = 0.0,
        double frequencyPenalty = 0.0,
        int? maxTokens = null,
        List<string>? stopSequences = null,
        int? seed = null)
    {
        if (temperature < 0.0 || temperature > 2.0)
            throw new ArgumentException("Temperature must be between 0.0 and 2.0");
        if (topP < 0.0 || topP > 1.0)
            throw new ArgumentException("Top P must be between 0.0 and 1.0");
        if (presencePenalty < -2.0 || presencePenalty > 2.0)
            throw new ArgumentException("Presence penalty must be between -2.0 and 2.0");
        if (frequencyPenalty < -2.0 || frequencyPenalty > 2.0)
            throw new ArgumentException("Frequency penalty must be between -2.0 and 2.0");
        if (maxTokens.HasValue && maxTokens == 0)
            throw new ArgumentException("Max tokens must not be 0");

         
        Temperature = temperature;
        MaxTokens = maxTokens;
        TopP = topP;
        TopK = topK;
        PresencePenalty = presencePenalty;
        FrequencyPenalty = frequencyPenalty;
        StopSequences = stopSequences ?? new List<string>();
        Seed = seed;
            
    }

    private GenerationOptions()
    { }

    public GenerationOptions OverrideWith(GenerationOptions? other)
    {
            
        return other == null ? this : new GenerationOptions
        {
            Temperature = other.Temperature ?? Temperature,
            MaxTokens = other.MaxTokens ?? MaxTokens,
            TopP = other.TopP ?? TopP,
            TopK = other.TopK ?? TopK,
            PresencePenalty = other.PresencePenalty ?? PresencePenalty,
            FrequencyPenalty = other.FrequencyPenalty ?? FrequencyPenalty,
            StopSequences = other.StopSequences ?? StopSequences,
            Seed = other.Seed ?? Seed,
        };
    }
}

public abstract class ResponseFormat { }

public class TextResponseFormat : ResponseFormat { }

public class JsonResponseFormat : ResponseFormat { }

public class ParsedJsonResponseFormat : ResponseFormat { }