using AlovaChat.Models;

namespace AlovaChat.Services;

public interface IChatService
{
    Task<ChatMessage> SendMessageAsync(string sessionId, string content, bool isFromUser);
    Task<List<ChatMessageDto>> GetMessagesAsync(string sessionId, int skip = 0, int take = 50);
    Task<ChatMessage?> GetMessageAsync(int messageId);
    Task DeleteMessageAsync(int messageId);
    Task ClearSessionMessagesAsync(string sessionId);
}