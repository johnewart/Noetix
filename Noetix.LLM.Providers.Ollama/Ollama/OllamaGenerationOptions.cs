namespace Noetix.Ollama.Ollama;
/*
  model: (required) the model name
  prompt: the prompt to generate a response for
  suffix: the text after the model response
  images: (optional) a list of base64-encoded images (for multimodal models such as llava)

  Advanced parameters (optional):
  format: the format to return a response in. Currently, the only accepted value is json
  options: additional model parameters listed in the documentation for the Modelfile such as temperature
  system: system message to (overrides what is defined in the Modelfile)
  template: the prompt template to use (overrides what is defined in the Modelfile)
  context: the context parameter returned from a previous request to /generate, this can be used to keep a short conversational memory
  stream: if false the response will be returned as a single response object, rather than a stream of objects
  raw: if true no formatting will be applied to the prompt. You may choose to use the raw parameter if you are specifying a full templated prompt in your request to the API
  keep_alive: controls how long the model will stay loaded into memory following the request (default: 5m)
 */

public enum ResponseFormat
{
    Text,
    Json
}

public class OllamaGenerationOptions
{
    public required string Model { get; set; }
    public required string Prompt { get; set; }
    public string? Suffix { get; set; }
    public List<string>? Images { get; set; }
    public ResponseFormat? Format { get; set; }
    public Dictionary<string, string>? Options { get; set; }
    public string? System { get; set; }
    public string? Template { get; set; }
    public string? Context { get; set; }
    public bool? Stream { get; set; }
    public bool? Raw { get; set; }
    public TimeSpan? KeepAlive { get; set; }

    public string? KeepAliveToString()
    {
        if (!KeepAlive.HasValue)
            return null;

        var duration = KeepAlive.Value;
        if (duration.TotalSeconds % 3600 == 0)
            return $"{duration.TotalHours}h";
        else if (duration.TotalSeconds % 60 == 0)
            return $"{duration.TotalMinutes}m";
        else
            return $"{duration.TotalSeconds}s";
    }
}