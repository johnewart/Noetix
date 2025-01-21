using System.Net;
using Codecs;
using Newtonsoft.Json.Linq;
using Noetix.LLM.Providers.Anthropic;
using Noetix.LLM.Common;
using Noetix.LLM.Requests;
using Xunit;
using static Newtonsoft.Json.JsonConvert;

namespace Noetix.Tests.LLM;

public class AnthropicLlmTests
{
  
    class FakeHttpMessageHandler(string[] responseContent) : HttpMessageHandler
    {
        private int _responseIndex;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var result = Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseContent[_responseIndex])
            });
            _responseIndex++;
            return result;
        }
    }
    
    [Fact]
    public Task SerializeToolUseBlockSchemaShouldBeCorrect()
    {
        var message = new ToolUseBlock(
            name: "CreateSchema",
            input: DeserializeObject<JToken>("{}"),
            id: "toolu_01DV4VEBAe5HmXDnqQU2MmsW"
        );

        var encoded = ToolUseBlockModule.encoder(message);
        SerializeObject(encoded);
        Assert.Equal("tool_use", encoded["type"]);
        Assert.Equal("CreateSchema", encoded["name"]);
        Assert.Equal("toolu_01DV4VEBAe5HmXDnqQU2MmsW", encoded["id"]);
        Assert.Equal(JValue.CreateNull(), encoded["input"]);
        return Task.CompletedTask;
    }
    
    [Fact]
    public async Task Complete_ShouldReturnCompletionResponse_WhenApiCallIsSuccessful()
    {
        
        var httpMessageHandler = new FakeHttpMessageHandler(
            [
                """
                {
                  "content": [
                    {
                      "text": "Hi! My name is Claude.",
                      "type": "text"
                    },
                    {
                    "type": "tool_use",
                        "id": "toolu_01D7FLrfh4GYq7yT1ULFeyMV",
                        "name": "get_stock_price",
                        "input": { "ticker": "^GSPC" }
                   },
                  ],
                  "id": "msg_013Zva2CMHLNnXjNJJKqJ2EF",
                  "model": "claude-3-5-sonnet-20241022",
                  "role": "assistant",
                  "stop_reason": "use_tool",
                  "stop_sequence": null,
                  "type": "message",
                  "usage": {
                    "input_tokens": 2095,
                    "output_tokens": 503
                  }
                }
                """
            ]
        );
        var httpClient = new HttpClient(httpMessageHandler);
        var config = new AnthropicLLM.AnthropicConfig { ApiKey = "test-api-key" };
        var anthropicLlm = new AnthropicLLM(config, httpClient);
        
   
        var messages = new List<Message>
        {
            new UserMessage("Test prompt")
        };

        var request = new CompletionRequest
        {
            Model = "claude-3-5-sonnet-20241022",
            Messages = messages,
            SystemPrompt = "Test system prompt"
        };
        
        // Act
        var result = await anthropicLlm.Complete(request);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.TextBlocks);
        Assert.Equal("Hi! My name is Claude.", result.TextBlocks[0]);
    }
}