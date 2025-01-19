using Noetix.LLM.Common;

namespace Noetix.Agents;

public class Memory(string memoryContent, DateTime? timestamp = null)
{
    public string MemoryContent { get; } = memoryContent;
    public DateTime Timestamp { get; } = timestamp ?? DateTime.Now;
}

public interface MemoryStore
{
    void Store(Memory memory);
    List<Memory> Recall(UserMessage? prompt = null);
    List<Memory> All();
    void Clear();
}