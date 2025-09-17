namespace AlovaChat.Services;

public class SessionEventService : ISessionEventService
{
    public event Action<string>? SessionCreated;
    public event Action<string>? SessionUpdated;
    public event Action<string>? SessionDeleted;

    public void NotifySessionCreated(string sessionId)
    {
        SessionCreated?.Invoke(sessionId);
    }

    public void NotifySessionUpdated(string sessionId)
    {
        SessionUpdated?.Invoke(sessionId);
    }

    public void NotifySessionDeleted(string sessionId)
    {
        SessionDeleted?.Invoke(sessionId);
    }
}