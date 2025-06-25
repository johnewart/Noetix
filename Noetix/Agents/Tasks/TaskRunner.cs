using NLog;
using Noetix.Common;
using ServiceStack;
using JsonSchema = NJsonSchema.JsonSchema;

namespace Noetix.Agents.Tasks;

public record TaskResult<TO>
{
    public TO? Content { get; init; }
    public string? ErrorMessage { get; init; }
    public bool Successful => ErrorMessage.IsNullOrEmpty();
}

public record TaskResponse<TO>
{
    public required TO Result { get; init; }
}

public class TaskRunner
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static TO ParseResponse<TO>(string response)
    {
        var schema = JsonSchema.FromType<TO>();

        if (string.IsNullOrEmpty(response))
        {
            throw new Exception("Empty response");
        }

        string jsonText = response.Contains("<response>") && response.Contains("</response>")
            ? response.Substring(response.IndexOf("<response>", StringComparison.Ordinal) + "<response>".Length,
                response.IndexOf("</response>", StringComparison.Ordinal) - response.IndexOf("<response>", StringComparison.Ordinal) -
                "<response>".Length)
            : response;

        if (string.IsNullOrEmpty(jsonText))
        {
            throw new Exception("Empty JSON response between <response> tags");
        }

        var extractedJson = JsonHandling.ExtractJSON(jsonText);
        if (!extractedJson.Valid)
        {
            throw new Exception($"Invalid JSON response: {extractedJson.Error}");
        }

        var result = JsonHandling.DeserializeJson<TO>(extractedJson.Json);
        if (result == null)
        {
            throw new Exception("Invalid JSON response");
        }

        // Validate the JSON response against the ObjectSchema
        var errors = schema.Validate(extractedJson.Json).ToList();
        if (errors.Any())
        {
            throw new Exception($"Invalid JSON response: {string.Join(", ", errors)}");
        }

        return result;
    }

    public static async Task<TaskResult<TO>> ExecuteTask<TO>(Assistant assistant, TaskContextData<TO> context)
    {
        Logger.Info($"Executing task {context.TaskName} using assistant {assistant}");

        var prompt = $@"
I need you to complete a task for me. That task is:

<task>
 {context.TaskSpecifics}
</task>

Follow these additional instructions when completing this task:

<task_instructions>
{string.Join("\n", context.Instructions.Select(i => " * " + i))}
</task_instructions>
";

        var message = await assistant.Generate(
            prompt,
            responseSchema: JsonSchema.FromType<TaskResponse<TO>>(),
            contextData: [context]);
        
        try
        {
            var result = ParseResponse<TaskResponse<TO>>(message.Content);
            return new TaskResult<TO>
            {
                Content = result.Result,
                ErrorMessage = "",
            };
        }
        catch (Exception e)
        {
            Logger.Error(e);
            return new TaskResult<TO>
            {
                ErrorMessage = e.Message,
            };
        }
    }
}