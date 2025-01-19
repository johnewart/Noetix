using NJsonSchema;
using Noetix.Agents;
using Xunit;

namespace Noetix.Tests.Agents;

public class SystemPromptBuilderTests
{
    [Fact]
    public void Generate_ShouldReturnCorrectPrompt_WhenNoAdditionalInstructionsAreProvided()
    {
        // Arrange
        var context = new SystemPromptContext
        {
            AssistantName = "TestAssistant",
            NeedsToolInstructions = false,
            NeedsMemoryInstructions = false,
            UsefulInfo =
            [
                new(Label: "TestLabel1", Value: "TestValue1"),
                new(Label: "TestLabel2", Value: "TestValue2")
            ],
            Memories = ["Memory1", "Memory2"],
            ToolDefinitions = [],
            Instructions = "Follow these instructions.",
            Persona = "Helpful Assistant"
        };

        // Act
        var result = SystemPromptBuilder.Generate(context);

        // Assert
        Assert.Contains("<core_instructions>", result);
        Assert.Contains("<useful_info>", result);
        Assert.DoesNotContain("<tool_instructions>", result);
        Assert.DoesNotContain("<memory_instructions>", result);
        Assert.Contains("<assistant_instructions>", result);
        Assert.Contains("<persona>", result);
        Assert.Contains("TestAssistant", result);
        Assert.Contains("TestLabel1", result);
        Assert.Contains("TestValue1", result);
        Assert.Contains("TestLabel2", result);
        Assert.Contains("TestValue2", result);
    }

    [Fact]
    public async Task JsonSchemaSerializationWorks()
    {
        var schema = JsonSchema.FromSampleJson("""
        {
        "name": "John Doe",
        "age": 29,
        "children": [
        {
        "name": "Jane Doe",
        "age": 5
        },
        {
        "name": "John Doe Jr.",
        "age": 2
        }
        ]
        }
        """);
        
        var json = schema.ToJson();
        var schema2 = await JsonSchema.FromJsonAsync(json);
        var json2 = schema2.ToJson();
        Assert.Equal(json, json2);
    }
}