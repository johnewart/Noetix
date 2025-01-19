namespace Noetix.LLM.Requests;

public interface IStreamingResponseHandler
{
    void OnToken(string token);
    void OnComplete();
    void OnError(Exception error);
}