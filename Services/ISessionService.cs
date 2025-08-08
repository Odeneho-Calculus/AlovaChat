using AlovaChat.Models;

namespace AlovaChat.Services;

public interface ISessionService
{
    Task<ChatSession> CreateSessionAsync(string userId, string? title = null);
    Task<ChatSession?> GetSessionAsync(string sessionId);
    Task<List<ChatSessionDto>> GetUserSessionsAsync(string userId, int skip = 0, int take = 20);
    Task<ChatSession> UpdateSessionAsync(ChatSession session);
    Task DeleteSessionAsync(string sessionId);
    Task<string> GetOrCreateUserIdAsync(HttpContext httpContext);
    Task UpdateSessionTitleAsync(string sessionId, string title);
    Task DeactivateSessionAsync(string sessionId);
}