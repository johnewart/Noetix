using Codecs;
using Noetix.Agents;
using Noetix.LLM.Common;
using Noetix.LLM.Requests;
using Xunit;

namespace Noetix.LLM.Providers.Anthropic.Tests;

public class StreamingTest
{
    // Get anthropic API key from environment variable
    private readonly string _apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "NO_API_KEY";

    [Fact]
    public async Task StreamsAResponse()
    {
        var config = new AnthropicLLM.AnthropicConfig
        {
            ApiKey = _apiKey
        };
        Console.WriteLine($"API Key: {config.ApiKey}");
        var client = new AnthropicLLM(config);
        var request = new CompletionRequest
        {
            Model = "claude-3-5-haiku-20241022",
            SystemPrompt = "You are an expert AI assistant - your name is Bob",
            Messages = new() {
                new UserMessage("What is your name?"),
            }
        };

        string messageContent = "";
        AssistantMessage? response = null;
        var handler = new Assistant.StreamHandler((text) =>
        {
            messageContent += text;
        }, (msg) =>
        {
            response = msg;
        });
        
        await client.StreamComplete(request: request, handler: handler, CancellationToken.None);
        
        Assert.NotNull(response);
        Assert.Contains("Bob", response!.Content);
        Assert.Contains("Bob", messageContent);
        
    }
    
}