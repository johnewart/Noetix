using NLog;

namespace Noetix.Agents.Tasks;

public class TaskRunner
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public static async Task<O> ExecuteTask<I, O>(Assistant assistant, AssistantTask<I, O> task, I input) where I : TaskParams
    {
        _logger.Info($"Executing task {task.Name} with input {input}");
        var result = await assistant.Generate(task.Prompt(input));
        return task.ParseResponse(result.Content);
    }
}