using Newtonsoft.Json.Linq;
using NJsonSchema;
using Noetix.Agents.Context;
using Noetix.LLM.Common;

namespace Noetix.Agents.Tasks;

public interface IAssistantTask
{
    string Name { get; }
    string Description { get; }
    List<string> Instructions { get; }
    GenerationOptions TaskGenerationOptions { get; }
    string ContentType { get; }
}

public abstract class TaskContextData<O> : ContextData
{
    public override string Name => "Task Context";
    public override string Description => "Context relevant to the task you are being asked to do - examples, and detailed instructions on how to complete the task.";
    public abstract string TaskName { get; }
    public abstract string TaskSpecifics { get; }
    public abstract List<string> Instructions { get; }
    public abstract Dictionary<JObject, O>? Examples { get; }
}

public abstract class AssistantTask<O>(TaskContextData<O> data) : IAssistantTask
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public List<string> Instructions => data.Instructions;

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

    public abstract string ContentType { get; }
    public JsonSchema? ResponseSchema { get; set; }
    public abstract O ParseResponse(string response);
    
    public GenerationOptions GenerationOptions => TaskGenerationOptions;
}
