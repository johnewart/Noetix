using Noetix.LLM.Common;

namespace Noetix.Agents;

public interface ChatHistoryStore
{
    void Store(string chatSessionId, Message message);
    List<Message> History(string chatSessionId, int? maxLength = null);
    void Clear(string chatSessionId);
    void Remove(string chatSessionId, int id);
    List<string> SessionIds();
    int TokenCount(string chatSessionId);
}