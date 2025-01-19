namespace Noetix.Agents.Tasks;

public abstract class TextTask<I> : AssistantTask<I, string> where I : TaskParams
{
    public override string ContentType { get; } = "text";

    public override List<TaskExample<I, string>> Examples => [];

    public override List<string> AdditionalInstructions => [];

    public override string ParseResponse(string response)
    {
        return response;
    }
}