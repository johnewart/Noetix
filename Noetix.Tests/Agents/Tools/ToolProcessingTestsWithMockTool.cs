using Newtonsoft.Json.Linq;
using Noetix.Agents.Tools;
using Noetix.LLM.Tools;
using Xunit;

namespace Noetix.Tests.Agents.Tools;

public class ToolProcessingTestsWithMockTool
{
    private readonly MockTool _mockTool;
    private readonly ToolProcessor _processor;

    public ToolProcessingTestsWithMockTool()
    {
        _mockTool = new MockTool();
        _processor = new ToolProcessor([_mockTool]);
    }

    [Fact]
    public void ExtractToolsRequests_ValidJson_ReturnsRequests()
    {
        // Arrange
        var input = @"
                <tools>
                [
                    {
                        ""tool"": ""mock_tool"",
                        ""id"": ""test1"",
                        ""parameters"": {
                            ""message"": ""Hello"",
                            ""shouldFail"": false
                        }
                    }
                ]
                </tools>";

        // Act
        var requests = _processor.ExtractToolsRequests(input).ToList();

        // Assert
        Assert.Single(requests);
        Assert.Equal("mock_tool", requests[0].Tool);
        Assert.Equal("test1", requests[0].Id);
    }

    [Fact]
    public void ExtractToolsRequests_InvalidJson_ThrowsException()
    {
        // Arrange
        var input = @"<tools>invalid json</tools>";

        // Act & Assert
        Assert.Throws<ToolParseError>(() => _processor.ExtractToolsRequests(input).ToList());
    }

    [Fact]
    public async Task Process_ValidToolRequest_ReturnsSuccess()
    {
        // Arrange
        var toolRequests = new[]
        {
            new ToolInvocationRequest
            {
                Tool = "mock_tool",
                Id = "test1",
                Parameters = JObject.Parse(@"{
                        ""message"": ""Hello"",
                        ""shouldFail"": false
                    }")
            }
        };

        // Act
        var results = _processor.Process("test content", toolRequests);

        // Assert
        Assert.Single(results);
        Assert.True(results[0].Result.Success);
        Assert.Contains("Processed: Hello", results[0].Result.Result);
    }

    [Fact]
    public async Task Process_FailingToolRequest_ReturnsError()
    {
        // Arrange
        var toolRequests = new[]
        {
            new ToolInvocationRequest
            {
                Tool = "mock_tool",
                Id = "test1",
                Parameters = JObject.Parse(@"{
                        ""message"": ""Hello"",
                        ""shouldFail"": true
                    }")
            }
        };

        // Act
        var results = _processor.Process("test content", toolRequests);

        // Assert
        Assert.Single(results);
        Assert.False(results[0].Result.Success);
        Assert.Contains("failed to execute", results[0].Result.Error);
    }

    [Fact]
    public async Task Process_InvalidToolId_ReturnsError()
    {
        // Arrange
        var toolRequests = new[]
        {
            new ToolInvocationRequest
            {
                Tool = "nonexistent_tool",
                Id = "test1",
                Parameters = JObject.Parse(@"{
                        ""message"": ""Hello"",
                        ""shouldFail"": false
                    }")
            }
        };

        // Act
        var results = _processor.Process("test content", toolRequests);

        // Assert
        Assert.Single(results);
        Assert.False(results[0].Result.Success);
        Assert.Contains("couldn't find the tool", results[0].Result.Error);
    }

    [Fact]
    public async Task Process_InvalidSchema_ReturnsError()
    {
        // Arrange
        var toolRequests = new[]
        {
            new ToolInvocationRequest
            {
                Tool = "mock_tool",
                Id = "test1",
                Parameters = JObject.Parse(@"{
                        ""invalid_field"": ""value""
                    }")
            }
        };

        // Act
        var results = _processor.Process("test content", toolRequests);

        // Assert
        Assert.Single(results);
        Assert.False(results[0].Result.Success);
        Assert.Contains("Validation error", results[0].Result.Error);
    }
}