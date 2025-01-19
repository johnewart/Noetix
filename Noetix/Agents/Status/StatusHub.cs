namespace Noetix.Agents.Status;

public delegate void AssistantStatusCallback(AssistantStatusMessage status);

public class StatusHub
{
    private readonly Dictionary<string, AssistantStatusCallback> callbacks = new Dictionary<string, AssistantStatusCallback>();

    public void Register(string callbackId, AssistantStatusCallback callback)
    {
        // Log.Info($"Registering callback {callbackId}");
        callbacks[callbackId] = callback;
    }

    public void Unregister(string callbackId)
    {
        callbacks.Remove(callbackId);
    }

    public void Notify(AssistantStatusMessage status)
    {
        // Log.Info($"Notifying callbacks of status: {status}");
        var updatedStatus = status with { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        foreach (var key in callbacks.Keys)
        {
            // Log.Info($"Notifying callback {key}");
            if (callbacks.TryGetValue(key, out var callback))
            {
                callback(updatedStatus);
            }
        }
    }
}