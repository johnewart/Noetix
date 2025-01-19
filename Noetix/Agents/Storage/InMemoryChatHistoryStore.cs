using Noetix.LLM.Common;
using ServiceStack.Text;

namespace Noetix.Agents.Storage;

public class InMemoryChatHistoryStore : ChatHistoryStore
{
    private Dictionary<string, List<Message>> _messages = new();
    
    public void Store(string chatSessionId, Message message)
    {
        if (!_messages.ContainsKey(chatSessionId))
        {
            _messages[chatSessionId] = new List<Message>();
        }
        _messages[chatSessionId].Add(message);
    }

    public List<Message> History(string chatSessionId, int? maxLength = null)
    {
        if (!_messages.ContainsKey(chatSessionId))
        {
            return new List<Message>();
        }
        return maxLength.HasValue ? _messages[chatSessionId].Take(maxLength.Value).ToList() : _messages[chatSessionId];
    }

    public void Clear(string chatSessionId)
    {
        _messages.Remove(chatSessionId);
    }

    public void Remove(string chatSessionId, int id)
    {
        if (_messages.ContainsKey(chatSessionId))
        {
            _messages[chatSessionId].RemoveAll(m => m.Timestamp.ToUnixTimeMs() == id);
        }
    }

    public List<string> SessionIds()
    {
        return _messages.Keys.ToList();
    }

    public int TokenCount(string chatSessionId)
    {
        return _messages.ContainsKey(chatSessionId) ? _messages[chatSessionId].Sum(m => m.Content.Split(' ').Length) : 0;
    }
}