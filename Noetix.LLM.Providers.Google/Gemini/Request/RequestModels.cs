using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Noetix.LLM.Providers.Google.Gemini.Request;

public class Content
{
    [JsonProperty("role", NullValueHandling = NullValueHandling.Ignore)]
    public string? Role { get; set; }

    [JsonProperty("parts")] public List<Part> Parts { get; set; }
}

public class Part
{
    [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)] public string? Text { get; set; }

    [JsonProperty("functionResponse", NullValueHandling = NullValueHandling.Ignore)] public FunctionResponse? FunctionResponse { get; set; }
    
    [JsonProperty("inlineData", NullValueHandling = NullValueHandling.Ignore)]
    public InlineData? InlineData { get; set; }
}

public class SystemInstruction
{
    [JsonProperty("parts")]
    public List<Part> Parts { get; set; }
}
    
public class GeminiMessageRequest
{
    [JsonProperty("system_instruction", NullValueHandling = NullValueHandling.Ignore)] public SystemInstruction? SystemInstruction { get; set; }
    [JsonProperty("contents")] public List<Content> Contents { get; set; }

    [JsonProperty("tools", NullValueHandling = NullValueHandling.Ignore)] public List<ToolDeclaration>? Tools { get; set; }
    
    [JsonProperty("generationConfig", NullValueHandling = NullValueHandling.Ignore)]
    public GenerationConfig? GenerationConfig { get; set; }

    [JsonProperty("safetySetting", NullValueHandling = NullValueHandling.Ignore)]
    public SafetySetting? SafetySetting { get; set; }
    
}

public class GenerationConfig
{
    [JsonProperty("stopSequences")] public List<string> StopSequences { get; set; }

    [JsonProperty("temperature")] public double Temperature { get; set; }

    [JsonProperty("maxOutputTokens")] public int MaxOutputTokens { get; set; }

    [JsonProperty("topP")] public double TopP { get; set; }

    [JsonProperty("topK")] public int TopK { get; set; }
        
    [JsonIgnore]
    public string? SystemPrompt { get; set; }
}

public class SafetySetting
{
    [JsonProperty("category")] public string Category { get; set; }

    [JsonProperty("threshold")] public string Threshold { get; set; }
}

public class InlineData
{
    [JsonProperty("mime_type")] public string MimeType { get; set; }

    [JsonProperty("data")] public string Data { get; set; }
}


public class ToolDeclaration {
    [JsonProperty("function_declarations", NullValueHandling = NullValueHandling.Ignore)]
    public List<FunctionDeclaration>? FunctionDeclarations { get; set; }
}

public class FunctionDeclaration {
    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public string Name { get; set; }

    [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
    public string Description { get; set; }

    [JsonProperty("parameters", NullValueHandling = NullValueHandling.Ignore)]
    public Parameters Parameters { get; set; }
}

public class Parameters {
    [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
    public string Type { get; set; } = "object";

    [JsonProperty("properties", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, Parameter>? Properties { get; set; }

    [JsonProperty("required", NullValueHandling = NullValueHandling.Ignore)]
    public List<string> Required { get; set; }
}

public class Parameter {
    [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
    public string Type { get; set; }

    [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
    public string Description { get; set; }
}

/*
functionResponse": {
   "name": "find_theaters",
   "response": {
     "name": "find_theaters",
     "content": {
       "movie": "Barbie",
       "theaters": [{
         "name": "AMC Mountain View 16",
         "address": "2000 W El Camino Real, Mountain View, CA 94040"
       }, {
         "name": "Regal Edwards 14",
         "address": "245 Castro St, Mountain View, CA 94040"
       }]
     }
   }
   */
public class FunctionResponse {
    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public string Name { get; set; }

    [JsonProperty("response", NullValueHandling = NullValueHandling.Ignore)]
    public Response Response { get; set; }
}

public class Response {
    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public string Name { get; set; }

    [JsonProperty("content", NullValueHandling = NullValueHandling.Ignore)]
    public JObject Content { get; set; }
}

