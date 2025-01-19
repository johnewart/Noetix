using Noetix.LLM.Common;

namespace Noetix.Agents;

public class FollowupTrigger(string trigger, Func<AssistantMessage, Task<UserMessage>> handler, string name)
{
    public string Trigger { get; } = trigger;
    public Func<AssistantMessage, Task<UserMessage>> Handler { get; } = handler;
    public string Name { get; } = name;
}