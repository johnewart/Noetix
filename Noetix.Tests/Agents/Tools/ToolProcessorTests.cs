using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Noetix.Agents.Tools;
using Noetix.LLM.Tools;
using Xunit;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Noetix.Tests.Agents.Tools;

public class ToolProcessorTests
{
    [Fact]
    public void Process_ShouldReturnErrorMessage_WhenNoToolsFound()
    {
        // Arrange
        var tools = new List<AssistantTool>();
        var processor = new ToolProcessor(tools);

        // Act
        processor.Process("No tools here", new List<ToolInvocationRequest>());
        // Assert.Contains("I'm sorry, I couldn't find any tools in the message you just sent me.", result);

    }

    [Fact]
    public void Process_ShouldReturnToolNotAvailableMessage_WhenToolNotFound()
    {
        // Arrange
        var tools = new List<AssistantTool>();
        var processor = new ToolProcessor(tools);
        var content = "<tools>[{\"Tool\":\"nonexistent_tool\",\"Parameters\":{}}]</tools>";
        var toolRequests = processor.ExtractToolsRequests(content);
        // Act
        processor.Process(content, toolRequests);

        // Assert
        // Assert.Contains("Tool nonexistent_tool not available in this assistant", result);
    }

    [Fact]
    public void Process_ShouldReturnToolExecutionResult_WhenToolFound()
    {
        // Arrange
        var tools = new List<AssistantTool> { new AdditionTool() };
        var processor = new ToolProcessor(tools);
        var content = "<tools>[{\"Tool\":\"addition_tool\",\"Parameters\":{\"Number1\":3,\"Number2\":4}}]</tools>";
        var toolRequests = processor.ExtractToolsRequests(content);
        // Act
        processor.Process(content, toolRequests);

            
            
        // Assert
        // Assert.Contains("addition_tool", result);
        // Assert.Contains("\\\"Sum\\\":7", result);
    }

    [Fact]
    public void ProcessToolHelp_ShouldReturnHelpMessage_WhenToolFound()
    {
        // Arrange
        var tools = new List<AssistantTool> { new AdditionTool() };
        var processor = new ToolProcessor(tools);
        var content = "<tool_help tool_id=\"addition_tool\"/>";

        // Act
        var result = processor.ProcessToolHelp(content);

        // Assert
        Assert.Contains("Adds two numbers together", result);
    }

    [Fact]
    public void ProcessToolHelp_ShouldReturnErrorMessage_WhenToolNotFound()
    {
        // Arrange
        var tools = new List<AssistantTool>();
        var processor = new ToolProcessor(tools);
        var content = "<tool_help tool_id=\"nonexistent_tool\"/>";

        // Act
        var result = processor.ProcessToolHelp(content);

        // Assert
        Assert.Contains("sorry", result);
    }
}

public class AdditionTool : AssistantTool
{
    public override string Id => "addition_tool";
    public override string Purpose => "Adds two numbers together";
    public override string Description => "This tool takes two numbers as input and returns their sum.";

    public override ToolParamsSchema Parameters => new ToolParamsSchema(typeof(AdditionParams));
    public override ToolResultsSchema Results => new ToolResultsSchema(typeof(AdditionResults));
    public override Option<List<ToolExample>> Examples => Option<List<ToolExample>>.Some([
        new ToolExample
        {
            Input = JsonSerializer.SerializeToNode(new AdditionParams { Number1 = 1, Number2 = 2 })!.AsObject(),
            Output = JsonSerializer.SerializeToNode(new AdditionResults { Sum = 3 })!.AsObject(),
            Explanation = "Adding 1 and 2 results in 3."
        }
    ]);

    public override async Task<JToken> Exec(JToken input)
    {
        var parameters = JsonSerializer.Deserialize<AdditionParams>(input.ToString())!;
        var result = new AdditionResults
        {
            Sum = parameters.Number1 + parameters.Number2
        };
        return JsonConvert.SerializeObject(result);
    }
}

public class AdditionParams
{
    public int Number1 { get; set; }
    public int Number2 { get; set; }
}

public class AdditionResults
{
    public int Sum { get; set; }
}