using AlovaChat.Models;

namespace AlovaChat.Services;

public interface IChatSessionService
{
    Task<ChatSession> CreateSessionAsync(string userId, string? title = null);
    Task<ChatSession?> GetSessionAsync(string sessionId);
    Task<List<ChatSession>> GetUserSessionsAsync(string userId);
    Task<ChatMessage> AddMessageAsync(string sessionId, string content, bool isFromUser, string? userId = null);
    Task<List<ChatMessage>> GetSessionMessagesAsync(string sessionId);
    Task<bool> DeleteSessionAsync(string sessionId);
    Task<ChatSession?> UpdateSessionTitleAsync(string sessionId, string title);
}