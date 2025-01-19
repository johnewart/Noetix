using System.Text.Json.Serialization;
using Newtonsoft.Json.Linq;

namespace Noetix.LLM.Tools;


public class ToolInvocationRequest
{
    [JsonPropertyName("id")]
    public required string Id { get; init; } = Guid.NewGuid().ToString();
    [JsonPropertyName("tool")]
    public required string Tool { get; set; }
    [JsonPropertyName("parameters")]
    public required JObject Parameters { get; set; }

    public ToolInvocationRequest()
    {
    }
} 
