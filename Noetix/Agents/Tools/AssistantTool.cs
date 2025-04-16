
using System.Text.Encodings.Web;
using System.Text.Json;
using Noetix.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Noetix.Agents.Tools;

public delegate void ToolStatusCallback(ToolStatusUpdate update);

    
public abstract class AssistantTool
{
    public abstract string Id { get; }
    public abstract string Purpose { get; }
    public abstract string Description { get; }
    public abstract ToolParamsSchema Parameters { get; }
    public abstract ToolResultsSchema Results { get; }
    public abstract Option<List<ToolExample>> Examples { get; }
    public List<ToolStatusCallback> StatusCallbacks { get; set; } = new List<ToolStatusCallback>();
        
    public abstract Task<JToken> Exec(JToken input);

    public ToolResult<O> Unwrap<O>(JToken input)
    {
        return JsonConvert.DeserializeObject<ToolResult<O>>(input.ToString());
    }
        
    protected async Task<JToken> WrapExec<I, O>(JToken input, Func<I, Task<ToolResult<O>>> wrapper)
    {   
        var jsonInput = input.ToString();
        var validationResult = Parameters.Validate(jsonInput);
        if (!validationResult.IsValid)
        {
            throw new ValidationError(validationResult.Error ?? "Failed to validate JSON input - did not match schema");
        }
            
        var typedInput = JsonHandling.DeserializeJson<I>(jsonInput);
        if (typedInput == null)
        {
            return JsonConvert.SerializeObject(new ToolResult<O>
            {
                Success = false, Error = $"Failed to deserialize input, resulted in a null object",
                Result = default(O)
            });
        }

        try
        {
            var toolResult = await wrapper(typedInput);
            return JsonConvert.SerializeObject(toolResult);
        } 
        catch (Exception e)
        {
            return JsonConvert.SerializeObject(new ToolResult<O>
            {
                Success = false, Error = e.Message, Result = default(O)
            });
        }
    }

    protected void UpdateStatus(ToolState state, string message, Dictionary<string, object> data)
    {
        foreach (var callback in StatusCallbacks)
        {
            callback(new ToolStatusUpdate
            {
                ToolId = Id, State = state, Message = message, Data = data
            });
        }
    }

    public string Instructions()
    {
        var serializeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        var inputSchemaDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(Parameters.Schema.ToJson());
        var outputSchemaDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(Results.Schema.ToJson());
        return JsonConvert.SerializeObject(new 
        {
            Id,
            Purpose,
            Description,
            Input = inputSchemaDict,
            Output = outputSchemaDict,
        });
    }

    public async Task<ToolResults> Invoke(string invocationId, JToken jsonInput)
    {
        try
        {
            UpdateStatus(ToolState.Running, $"Invoking tool {Id}", new Dictionary<string, object> { { "input", jsonInput } });
            var result = await Exec(jsonInput);
            UpdateStatus(ToolState.Completed, $"Tool {Id} completed", new Dictionary<string, object> { { "result", result } });
                
            return new ToolResults(toolId: Id, id: invocationId, success: true, null, JsonConvert.SerializeObject(result));
        }
        catch (ValidationError error)
        {
            UpdateStatus(ToolState.Failed, $"Tool {Id} failed", new Dictionary<string, object> { { "result", error.CollectErrors() } });
            return new ToolResults(toolId: Id, id: invocationId, success: false, $"Validation error: {error.CollectErrors()}", default);
        }
        catch (Exception error)
        {
            return new ToolResults(toolId: Id, invocationId, success: false, error.Message, default);
        }
    }
}

public class ValidationError(string message) : Exception(message)
{
    public string CollectErrors() => Message;
}

public class Option<T>
{
    public static Option<T> None => new Option<T>();
    public static Option<T> Some(T value) => new Option<T>(value);

    private Option() { }
    private Option(T value) { Value = value; }

    public T? Value { get; }
    public bool IsSome => Value != null;
}