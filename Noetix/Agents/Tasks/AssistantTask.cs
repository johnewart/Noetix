using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Noetix.LLM.Common;

namespace Noetix.Agents.Tasks;

public interface IAssistantTask
{
    string Name { get; }
    string Description { get; }
    List<string> Instructions { get; }
    GenerationOptions TaskGenerationOptions { get; }
    List<string> AdditionalInstructions { get; }
    string ContentType { get; }
}

public abstract class AssistantTask<I, O> : IAssistantTask where I : TaskParams
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract List<string> Instructions { get; }
    public abstract List<TaskExample<I, O>> Examples { get; }

    public AssistantTask()
    {
    }

    public GenerationOptions TaskGenerationOptions { get; } = new GenerationOptions
    (
        maxTokens: -1,
        temperature: 0.9,
        topP: 0.9,
        topK: 50,
        frequencyPenalty: 0,
        presencePenalty: 0,
        stopSequences: null
    );

    public abstract List<string> AdditionalInstructions { get; }
    public abstract string ContentType { get; }
    public abstract O ParseResponse(string response);

    private string ContextToXMLLikeDoc(Dictionary<string, object> context)
    {
        if (context == null || !context.Any())
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var kvp in context)
        {
            var value = kvp.Value;
          
            if (value is string strValue && !string.IsNullOrEmpty(strValue))
            {
                sb.AppendLine($"<{kvp.Key}>{strValue}</{kvp.Key}>");
            }
            else if (value is Array arrayValue)
            {
                sb.AppendLine($"<{kvp.Key}>");
                foreach (var item in arrayValue)
                {
                    if (item is string itemStr)
                    {
                        sb.AppendLine($"<item>{itemStr}</item>");
                    }
                    else if (item is Dictionary<string, object> itemContext)
                    {
                        sb.AppendLine($"<{kvp.Key}>{ContextToXMLLikeDoc(itemContext)}</{kvp.Key}>");
                    }
                }
                sb.AppendLine($"</{kvp.Key}>");
            }
            else if (value is Dictionary<string, object> contextValue)
            {
                sb.AppendLine($"<{kvp.Key}>{ContextToXMLLikeDoc(contextValue)}</{kvp.Key}>");
            }
        }

        return sb.ToString();
    }

    public GenerationOptions GenerationOptions => TaskGenerationOptions;

    public string TaskSpecifics(I input)
    {
        if (input is TaskParams taskParams)
        {
            return taskParams.TaskSpecifics;
        }

        return string.Empty;
    }

    public TaskDefinition<I, O> Definition => new TaskDefinition<I, O>(
        Name,
        Description,
        string.Join("\n", Instructions),
        Examples
    );

    public string Prompt(I p)
    {
        return $@"
<context>
{JsonConvert.SerializeObject(p.Context.GetContextDictionary())}
</context>

<task_instructions>
{string.Join("\n", Instructions)}
{string.Join("\n", AdditionalInstructions ?? new List<string>())}
</task_instructions>

<prompt>
{TaskSpecifics(p)}
</prompt>
";
    }
}

public record TaskData
{
    public Dictionary<string, object> GetContextDictionary()
    {
        return this.ToDictionary(this);
    }

    private Dictionary<string, object> ToDictionary(object obj)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        var dictionary = new Dictionary<string, object>();
        var properties = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            var value = property.GetValue(obj);
            dictionary.Add(property.Name, value);
        }

        return dictionary;
    }
}



public interface TaskParams
{
    TaskData Context { get; init; }
    TaskData Instructions { get; init; }

    string TaskSpecifics { get; init; }
}