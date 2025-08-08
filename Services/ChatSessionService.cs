using AlovaChat.Models;

namespace AlovaChat.Services;

public class ChatSessionService : IChatSessionService
{
    private readonly ILogger<ChatSessionService> _logger;
    private readonly Dictionary<string, ChatSession> _sessions = new();
    private readonly Dictionary<string, List<ChatMessage>> _messages = new();

    public ChatSessionService(ILogger<ChatSessionService> logger)
    {
        _logger = logger;
    }

    public async Task<ChatSession> CreateSessionAsync(string userId, string? title = null)
    {
        var session = new ChatSession
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Title = title ?? "New Chat",
            CreatedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow
        };

        _sessions[session.Id] = session;
        _messages[session.Id] = new List<ChatMessage>();

        _logger.LogInformation("Created new chat session {SessionId} for user {UserId}", session.Id, userId);
        return await Task.FromResult(session);
    }

    public async Task<ChatSession?> GetSessionAsync(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return await Task.FromResult(session);
    }

    public async Task<List<ChatSession>> GetUserSessionsAsync(string userId)
    {
        var userSessions = _sessions.Values
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.LastActivity)
            .ToList();

        return await Task.FromResult(userSessions);
    }

    public async Task<ChatMessage> AddMessageAsync(string sessionId, string content, bool isFromUser, string? userId = null)
    {
        if (!_messages.ContainsKey(sessionId))
        {
            _messages[sessionId] = new List<ChatMessage>();
        }

        var message = new ChatMessage
        {
            SessionId = sessionId,
            Content = content,
            IsFromUser = isFromUser,
            Timestamp = DateTime.UtcNow
        };

        _messages[sessionId].Add(message);

        // Update session timestamp
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.LastActivity = DateTime.UtcNow;
            if (isFromUser && string.IsNullOrEmpty(session.Title))
            {
                session.Title = content.Length > 50 ? content[..50] + "..." : content;
            }
        }

        _logger.LogInformation("Added message to session {SessionId}: {Content}", sessionId, content[..Math.Min(50, content.Length)]);
        return await Task.FromResult(message);
    }

    public async Task<List<ChatMessage>> GetSessionMessagesAsync(string sessionId)
    {
        if (_messages.TryGetValue(sessionId, out var messages))
        {
            return await Task.FromResult(messages.OrderBy(m => m.Timestamp).ToList());
        }

        return await Task.FromResult(new List<ChatMessage>());
    }

    public async Task<bool> DeleteSessionAsync(string sessionId)
    {
        var removed = _sessions.Remove(sessionId);
        _messages.Remove(sessionId);

        if (removed)
        {
            _logger.LogInformation("Deleted session {SessionId}", sessionId);
        }

        return await Task.FromResult(removed);
    }

    public async Task<ChatSession?> UpdateSessionTitleAsync(string sessionId, string title)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.Title = title;
            session.LastActivity = DateTime.UtcNow;
            _logger.LogInformation("Updated session {SessionId} title to: {Title}", sessionId, title);
            return await Task.FromResult(session);
        }

        return await Task.FromResult<ChatSession?>(null);
    }
}