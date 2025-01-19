using Newtonsoft.Json.Linq;
using Noetix.Agents.Tools;
using Xunit;

namespace Noetix.Tests.Agents.Tools;

public class AssistantToolTests
{
    private readonly MockTool _tool = new();

    [Fact]
    public async Task Invoke_ValidInput_ReturnsSuccess()
    {
        // Arrange
        var input = JToken.Parse(@"{
                ""message"": ""Hello"",
                ""shouldFail"": false
            }");

        // Act
        var result = await _tool.Invoke("test1", input);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("Processed: Hello", result.Result);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Invoke_InvalidInput_ReturnsValidationError()
    {
        // Arrange
        var input = JToken.Parse(@"{
                ""invalid_field"": ""value""
            }");

        // Act
        var result = await _tool.Invoke("test1", input);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Validation error", result.Error);
    }

    [Fact]
    public async Task Invoke_FailureRequested_ReturnsError()
    {
        // Arrange
        var input = JToken.Parse(@"{
                ""message"": ""Hello"",
                ""shouldFail"": true
            }");

        // Act
        var result = await _tool.Invoke("test1", input);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("fail", result.Error);
    }

    [Fact]
    public void Instructions_ReturnsValidJson()
    {
        // Act
        var instructions = _tool.Instructions();

        // Assert
        Assert.NotNull(instructions);
        Assert.Contains("mock_tool", instructions);
        Assert.Contains("Message", instructions);
        Assert.Contains("ShouldFail", instructions);
    }

    [Fact]
    public async Task StatusCallbacks_AreInvoked()
    {
        // Arrange
        var statusUpdates = new List<ToolStatusUpdate>();
        _tool.StatusCallbacks.Add(update => statusUpdates.Add(update));
        var input = JToken.Parse(@"{
                ""message"": ""Hello"",
                ""shouldFail"": false
            }");

        // Act
        _ = await _tool.Invoke("test1", input);

        // Assert
        Assert.NotEmpty(statusUpdates);
        Assert.Contains(statusUpdates, u => u.State == ToolState.Running);
        Assert.Contains(statusUpdates, u => u.State == ToolState.Completed);
    }
}