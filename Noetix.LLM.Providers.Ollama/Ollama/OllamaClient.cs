using System.Text;
using Newtonsoft.Json;
using NJsonSchema;
using Noetix.LLM.Common;
using Noetix.LLM.Requests;

namespace Noetix.Ollama.Ollama;

public class OllamaToolInputParameterSchema
{
    [JsonProperty(PropertyName = "type")]
    public string Type { get; set; }
    [JsonProperty(PropertyName = "description")]
    public string Description { get; set; }
    [JsonProperty(PropertyName = "properties")]
    public Dictionary<string, OllamaToolInputParameterSchema>? Properties { get; set; }
    [JsonProperty(PropertyName = "items")]
    public OllamaToolInputParameterSchema? Items { get; set; }
}
        
public class OllamaToolInputSchema
{
    [JsonProperty(PropertyName = "type")]
    public string Type { get; set; } = "object";
    [JsonProperty(PropertyName = "properties")]
    public Dictionary<string, OllamaToolInputParameterSchema> Properties { get; set; }
}
public class OllamaToolFunction
{
    [JsonProperty(PropertyName = "name")]
    public string Name { get; set; }
    [JsonProperty(PropertyName = "description")]
    public string Description { get; set; }
    [JsonProperty(PropertyName = "parameters")]
    public OllamaToolInputSchema Parameters { get; set; }
}
public class OllamaTool
{
    [JsonProperty(PropertyName = "type")]
    public string Type { get; set; }
    [JsonProperty(PropertyName = "function")]
    public OllamaToolFunction Function { get; set; }
}

public class OllamaModel
{
    [JsonProperty(PropertyName = "name")]
    public string Name { get; set; }
    [JsonProperty(PropertyName = "modified_at")]
    public string ModifiedAt { get; set; }
    [JsonProperty(PropertyName = "size")]
    public long Size { get; set; }
    [JsonProperty(PropertyName = "digest")]
    public string Digest { get; set; }
    [JsonProperty(PropertyName = "details")]
    public ModelDetails Details { get; set; }
}
    
public class ModelDetails
{
    [JsonProperty(PropertyName = "format")]
    public string Format { get; set; }
    [JsonProperty(PropertyName = "family")]
    public string Family { get; set; }
    [JsonProperty(PropertyName = "families")]
    public string[] Families { get; set; }
    [JsonProperty(PropertyName = "parameter_size")]
    public string ParameterSize { get; set; }
    [JsonProperty(PropertyName = "quantization_level")]
    public string QuantizationLevel { get; set; }
}
    
public class ModelResponse
{
    [JsonProperty(PropertyName = "models")]
    public List<OllamaModel> Models { get; set; }
}
public class EmbeddingsRequest
{
    public required string Model { get; set; }
    public required string Input { get; set; }
    public Dictionary<string, string>? Options { get; set; }
}

public class EmbeddingsResponse
{
    public required string Model { get; set; }
    public required double[][] Embeddings { get; set; }
    public long TotalDuration { get; set; }
    public long LoadDuration { get; set; }
    public int PromptEvalCount { get; set; }
}

public class ChatRequest
{
    [JsonProperty(PropertyName = "model")]
    public required string Model { get; set; }
    [JsonProperty(PropertyName = "messages")]
    public required List<ChatMessage> Messages { get; set; }
    [JsonProperty(PropertyName = "stream")]
    public bool Stream { get; set; } = false;
    [JsonProperty(PropertyName = "tools")]
    public List<OllamaTool>? Tools { get; set; }
}

public class GenerateResponse
{
    [JsonProperty(PropertyName = "model")]
    public required string Model { get; set; }
    [JsonProperty(PropertyName = "response")]
    public required string Response { get; set; }
}

public class GenerateRequest
{
    [JsonProperty(PropertyName = "model")]
    public required string Model { get; set; }
    [JsonProperty(PropertyName = "prompt")]
    public required string Prompt { get; set; }
}

public class ChatMessage
{
    [JsonProperty(PropertyName = "role")]
    public required string Role { get; set; }
    [JsonProperty(PropertyName = "content")]
    public required string Content { get; set; }
}

public class ToolFunctionCall
{
    [JsonProperty(PropertyName = "name")]
    public required string Name { get; set; }
    [JsonProperty(PropertyName = "arguments")]
    public Dictionary<string, dynamic> Arguments { get; set; }
}
    
public class ToolCall
{
    [JsonProperty(PropertyName = "function")]
    public required ToolFunctionCall Function { get; set; }
}

public class ChatResponse
{
    [JsonProperty(PropertyName = "model")]
    public required string Model { get; set; }
    [JsonProperty(PropertyName = "message")]
    public required ChatMessage Message { get; set; }
    [JsonProperty(PropertyName = "done")]
    public bool Done { get; set; }
    [JsonProperty(PropertyName = "tool_calls")]
    public List<ToolCall>? ToolCalls { get; set; }
}

public class OllamaClient
{
    private readonly string _baseUrl;
    private readonly HttpClient _httpClient;

    public OllamaClient(string baseUrl = "http://localhost:11434")
    {
        if (baseUrl.EndsWith($"/"))
        {
            _baseUrl = baseUrl.Substring(0, baseUrl.Length - 1);
        }
        else
        {
            _baseUrl = baseUrl;
        }

        _httpClient = new HttpClient();
    }

    public async Task StreamChat(
        string model,
        List<ChatMessage> messages,
        Action<string> onMessage,
        Action? onComplete = null,
        Action<string>? onError = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/chat");
            var chatRequest = new ChatRequest { Model = model, Messages = messages, Stream = true };
            var requestBody = JsonConvert.SerializeObject(chatRequest);

            message.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            var response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead);
            var stream = await response.Content.ReadAsStreamAsync();

            string read = "";
            byte[] buffer = new byte[1024];

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    stream.Close();
                    response.Dispose();
                    break;
                }

                var count = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (count == 0)
                {
                    break;
                }

                read += Encoding.UTF8.GetString(buffer, 0, count);
                var lines = read.Split("\n");
                read = lines.Last();
                foreach (var line in lines.Take(lines.Length - 1))
                {
                    try
                    {
                        var chatResponse = JsonConvert.DeserializeObject<ChatResponse>(line);

                        if (chatResponse == null)
                        {
                            onError?.Invoke("Error parsing response");
                            return;
                        }

                        onMessage(chatResponse.Message.Content);

                        if (chatResponse.Done)
                        {
                            onComplete?.Invoke();
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke($"Error parsing response: {e.Message}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            onError?.Invoke($"Stream error: {e.Message}");
        }
        //     }    
        //     using var reader = new StreamReader(responseStream);
        //     string? line;
        //
        //     while ((line = await reader.ReadLineAsync()) != null)
        //     {
        //         try
        //         {
        //             var chatResponse = JsonConvert.DeserializeObject<ChatResponse>(line);
        //
        //             if (chatResponse == null)
        //             {
        //                 onError?.Invoke("Error parsing response");
        //                 return;
        //             }
        //
        //             onMessage(chatResponse.Message.Content);
        //
        //             if (chatResponse.Done)
        //             {
        //                 onComplete?.Invoke();
        //                 return;
        //             }
        //         }
        //         catch (Exception e)
        //         {
        //             onError?.Invoke($"Error parsing response: {e.Message}");
        //         }
        //     }
        // }
        // catch (Exception e)
        // {
        //     onError?.Invoke($"Stream error: {e.Message}");
        // }
    }

        
    public static OllamaToolInputSchema ConvertJsonSchemaToInputSchema(JsonSchema schema)
    {
        var inputSchema = new OllamaToolInputSchema();
        inputSchema.Properties = new Dictionary<string, OllamaToolInputParameterSchema>();
        foreach (var property in schema.Properties)
        {
            inputSchema.Properties[property.Key] = new OllamaToolInputParameterSchema
            {
                Type = property.Value.Type.ToString(),
                Description = property.Value.Description,
                Properties = property.Value.Properties != null ? property.Value.Properties.ToDictionary(p => p.Key, p => new OllamaToolInputParameterSchema
                {
                    Type = p.Value.Type.ToString(),
                    Description = p.Value.Description
                }) : null,
            };
        }
        return inputSchema;
    }
        
    public async Task<ChatResponse> Chat(string model, List<ChatMessage> messages, List<OllamaTool>? tools = null)
    {
        try
        {
            var request = new ChatRequest { Model = model, Messages = messages, Stream = false};
            var requestBody = JsonConvert.SerializeObject(request);
            var response = await _httpClient.PostAsync(
                $"{_baseUrl}/api/chat",
                new StringContent(requestBody, Encoding.UTF8, "application/json"));

            var responseText = await response.Content.ReadAsStringAsync();
            var chatResponse = JsonConvert.DeserializeObject<ChatResponse>(responseText);
            if (chatResponse == null)
            {
                throw new ChatError("Error parsing response");
            }

            return chatResponse;
        } 
        catch (Exception e)
        {
            throw new ChatError($"Error chatting: {e.Message}");
        }
    }

    public async Task<string> Generate(CompletionRequest request)
    {
        var prompt = request.SystemPrompt + "\n\n" + request.Messages.Last().Content;
            
        var requestBody = JsonConvert.SerializeObject(new GenerateRequest { Model = request.Model, Prompt = prompt });
        try
        {
            var response = await _httpClient.PostAsync(
                $"{_baseUrl}/api/generate",
                new StringContent(requestBody, Encoding.UTF8, "application/json"));

            var responseText = await response.Content.ReadAsStringAsync();
            var generateResponse = JsonConvert.DeserializeObject<GenerateResponse>(responseText);
            if (generateResponse == null)
            {
                throw new ChatError("Error parsing response");
            }

            return generateResponse.Response;
        }
        catch (Exception e)
        { 
            throw new ChatError($"Error generating response: {e.Message}");
        }
    }

    public async Task<List<OllamaModel>> ListModels()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
            var responseText = await response.Content.ReadAsStringAsync();
            var modelResponse = JsonConvert.DeserializeObject<ModelResponse>(responseText);

            if (modelResponse == null)
            { 
                throw new ApiError("Error parsing response");
            }

            return modelResponse.Models;
        }
        catch (Exception e)
        {
            throw new ApiError($"Error listing models: {e.Message}");
        }
    }

    public async Task<EmbeddingsResponse> GetEmbeddings(EmbeddingsRequest request)
    {
        var requestBody = JsonConvert.SerializeObject(request);
        try
        {
            var response = await _httpClient.PostAsync(
                $"{_baseUrl}/api/embed",
                new StringContent(requestBody, Encoding.UTF8, "application/json"));

            var responseText = await response.Content.ReadAsStringAsync();
            var embeddingsResponse = JsonConvert.DeserializeObject<EmbeddingsResponse>(responseText);
            if (embeddingsResponse == null)
            {
                throw new EmbeddingError("Error parsing response");
            }
            return embeddingsResponse;
        }
        catch (Exception e)
        {
            throw new EmbeddingError($"Error getting embeddings: {e.Message}");
        }
    }
}