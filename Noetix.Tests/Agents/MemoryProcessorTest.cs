using Noetix.Agents;
using Noetix.LLM.Common;
using Xunit;

namespace Noetix.Tests.Agents;

public class MemoryProcessorTest
{


    [Fact]
    public void SingleLineMessageExtractsMemories()
    {
        var processor = new MemoryProcessor(new InMemoryMemoryStore());
        var message = """
                      This is a message from the assistant. It has some stuff, and a memory. <memory>Remember this</memory> And some more stuff.
                      """;
        var result = processor.ExtractMemories(message);
        Assert.DoesNotContain("<memory>", result);
        Assert.DoesNotContain("</memory>", result);
        Assert.DoesNotContain("<memory>Remember this</memory>", result);
        Assert.Contains("Remember this", processor.FetchMemories());
    }


    [Fact]
    public void MultiLineMessageExtractsMemories()
    {
        var processor = new MemoryProcessor(new InMemoryMemoryStore());
        var message = """
                      This is a message from the assistant. It has some stuff, and a memory. 
                      <memory>Remember this</memory> 
                      And some more stuff.
                      """;
        var result = processor.ExtractMemories(message);
        Assert.DoesNotContain("<memory>", result);
        Assert.DoesNotContain("</memory>", result);
        Assert.DoesNotContain("<memory>Remember this</memory>", result);
        Assert.Contains("Remember this", processor.FetchMemories());
    }

    [Fact]
    public void MultiLineMessageWithMultiLineMemoryExtractsMemory()
    {
        var processor = new MemoryProcessor(new InMemoryMemoryStore());
        var message = @"
                This is a message from the assistant. It has some stuff, and a memory. 
                <memory>
                Remember this
                </memory> 
                And some more stuff.
                ";
        var result = processor.ExtractMemories(message);
        Assert.DoesNotContain("<memory>", result);
        Assert.DoesNotContain("</memory>", result);
        Assert.DoesNotContain("<memory>Remember this</memory>", result);
        Assert.Contains("Remember this", processor.FetchMemories());
    }
}

internal class InMemoryMemoryStore : MemoryStore
{
    private readonly List<Memory> _memories = [];

    public List<Memory> All()
    {
        return _memories;
    }

    public void Clear()
    {
        _memories.Clear();
    }

    public List<Memory> Recall(UserMessage? prompt = null)
    {
        return _memories;
    }

    public void Store(Memory memory)
    {
        _memories.Add(memory);
    }
}