using System.Text.RegularExpressions;
using Noetix.LLM.Common;

namespace Noetix.Agents;

public class MemoryProcessor(MemoryStore storage)
{
    public bool ShouldProcess(AssistantMessage message)
    {
        return message.Content.Contains("<memory>");
    }

    public string ExtractMemories(string message)
    {
        var memoryRegex = new Regex("<memory>(.*?)</memory>", RegexOptions.Singleline);
        var matches = memoryRegex.Matches(message);
        int storeCount = 0;

        foreach (Match match in matches)
        {
            var memory = match.Groups[1].Value;
            if (!string.IsNullOrEmpty(memory))
            {
                storage.Store(new Memory(memory));
                storeCount++;
            }
        }

        return memoryRegex.Replace(message, string.Empty);
    }

    public string FetchMemories()
    {
        var memories = storage.All();
        if (!memories.Any())
        {
            return string.Empty;
        }

        return memories
            .Select(memory => $"<memory>{memory.MemoryContent}</memory>")
            .Aggregate("<memories>\n", (current, memory) => current + memory + "\n") + "</memories>";
    }
}