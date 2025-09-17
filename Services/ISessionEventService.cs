namespace AlovaChat.Services;

public interface ISessionEventService
{
    event Action<string>? SessionCreated;
    event Action<string>? SessionUpdated;
    event Action<string>? SessionDeleted;

    void NotifySessionCreated(string sessionId);
    void NotifySessionUpdated(string sessionId);
    void NotifySessionDeleted(string sessionId);
}