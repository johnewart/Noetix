using Noetix.LLM.Requests;

namespace Noetix.LLM.Common;

public interface LLMProvider
{
    bool SupportsToolsNatively { get; }
        
    Task<CompletionResponse> Complete(CompletionRequest request);

    Task<bool> StreamComplete(
        CompletionRequest request,
        IStreamingResponseHandler handler,
        CancellationToken cancellationToken);

    Task<CompletionResponse> Generate(
        CompletionRequest request);
        
    Task<List<ModelDefinition>> GetModels();
        
}
    

public class ApiError(string message) : Exception(message);

public class RateLimitError(string message) : Exception(message);

public class AuthenticationError(string message) : Exception(message);

public class InvalidRequestError(string message) : Exception(message);

public class ModelNotFoundError(string message) : Exception(message);

public class UnknownError(string message) : Exception(message);