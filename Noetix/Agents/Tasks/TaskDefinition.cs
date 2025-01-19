namespace Noetix.Agents.Tasks;

public class TaskDefinition<I, O>(
    string name,
    string description,
    string instructions,
    List<TaskExample<I, O>>? examples = null)
{
    public string Name { get; set; } = name;
    public string Description { get; set; } = description;
    public string Instructions { get; set; } = instructions;
    public List<TaskExample<I, O>>? Examples { get; set; } = examples;
}

public class TaskExample<I, O>(string name, I input, O output)
{
    public string Name { get; set; } = name;
    public I Input { get; set; } = input;
    public O Output { get; set; } = output;
}

public class StringMap(Dictionary<string, object> map)
{
    public Dictionary<string, object> Map { get; set; } = map;
}