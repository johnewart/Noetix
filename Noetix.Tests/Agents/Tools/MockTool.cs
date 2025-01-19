using Newtonsoft.Json.Linq;
using Noetix.Agents.Tools;

namespace Noetix.Tests.Agents.Tools;

public class MockTool : AssistantTool
{
    public override string Id => "mock_tool";
    public override string Purpose => "A mock tool for testing";
    public override string Description => "A test tool that echoes input";

    public override ToolParamsSchema Parameters => new(typeof(MockToolInput));


    public override ToolResultsSchema Results => new(typeof(MockToolOutput));
    

    public override Option<List<ToolExample>> Examples => Option<List<ToolExample>>.None;

    public override async Task<JToken> Exec(JToken input)
    {
        return await WrapExec<MockToolInput, MockToolOutput>(input, (typedInput) =>
        {
            if (typedInput.ShouldFail)
            {
                throw new Exception("Tool failed as requested");
            }

            return Task.FromResult(new ToolResult<MockToolOutput>
            {
                Success = true,
                Result = new MockToolOutput(message: $"Processed: {typedInput.Message}")
            });
        });
    }
}

public class MockToolInput(string message)
{
    public string Message { get; set; } = message;
    public bool ShouldFail { get; set; }
}

public class MockToolOutput(string message)
{
    public string Message { get; set; } = message;
}