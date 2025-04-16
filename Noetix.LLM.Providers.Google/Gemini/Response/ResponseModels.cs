using Newtonsoft.Json;

namespace Noetix.LLM.Providers.Google.Gemini.Response;


public class GeminiMessageResponse
{
    [JsonProperty("candidates")] public List<Candidate> Candidates { get; set; }

    [JsonProperty("promptFeedback")] public PromptFeedback PromptFeedback { get; set; }
}

public class Content
{
    [JsonProperty("parts")] public List<Part> Parts { get; set; }

    [JsonProperty("role")] public string Role { get; set; }
}

public class Candidate
{
    [JsonProperty("content")] public Content Content { get; set; }

    [JsonProperty("finishReason")] public string FinishReason { get; set; }

    [JsonProperty("index")] public int Index { get; set; }

    [JsonProperty("safetyRatings")] public List<SafetyRating> SafetyRatings { get; set; }
}

public class Part
{
    [JsonProperty("text")] public string? Text { get; set; }
    [JsonProperty("functionCall")] public FunctionCall? FunctionCall { get; set; }
}

public class FunctionCall
{
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("args")] public Dictionary<string, object> Args { get; set; }
}

public class PromptFeedback
{
    [JsonProperty("safetyRatings")] public List<SafetyRating> SafetyRatings { get; set; }
}

public class SafetyRating
{
    [JsonProperty("category")] public string Category { get; set; }

    [JsonProperty("probability")] public string Probability { get; set; }
}

public class RootGeminiModelResponse
{
    [JsonProperty("models", NullValueHandling = NullValueHandling.Ignore)]
    public IEnumerable<GeminiModelResponse?> GeminiModelResponses { get; set; } = new List<GeminiModelResponse>();
}

public class GeminiModelResponse
{
    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public string Name { get; set; }

    [JsonProperty("version", NullValueHandling = NullValueHandling.Ignore)]
    public string Version { get; set; }

    [JsonProperty("displayName", NullValueHandling = NullValueHandling.Ignore)]
    public string DisplayName { get; set; }

    [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
    public string Description { get; set; }

    [JsonProperty("inputTokenLimit", NullValueHandling = NullValueHandling.Ignore)]
    public int InputTokenLimit { get; set; }

    [JsonProperty("outputTokenLimit", NullValueHandling = NullValueHandling.Ignore)]
    public int OutputTokenLimit { get; set; }

    [JsonProperty("supportedGenerationMethods", NullValueHandling = NullValueHandling.Ignore)]
    public List<string> SupportedGenerationMethods { get; set; }

    [JsonProperty("temperature", NullValueHandling = NullValueHandling.Ignore)]
    public double Temperature { get; set; }

    [JsonProperty("topP", NullValueHandling = NullValueHandling.Ignore)]
    public double TopP { get; set; }

    [JsonProperty("topK", NullValueHandling = NullValueHandling.Ignore)]
    public double TopK { get; set; }
}

/*
 * "parts": [
   {
     "functionCall": {
       "name": "find_theaters",
       "args": {
         "movie": "Barbie",
         "location": "Mountain View, CA"
       }
     }
   }
 */



