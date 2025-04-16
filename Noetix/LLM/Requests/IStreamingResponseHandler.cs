using Noetix.LLM.Tools;

namespace Noetix.LLM.Requests;

public interface IStreamingResponseHandler
{
    void OnToken(string token);
    void OnComplete();
    void OnToolRequest(ToolInvocationRequest toolRequest);
    void OnError(Exception error);
}