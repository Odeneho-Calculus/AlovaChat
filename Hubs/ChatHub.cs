using AlovaChat.Models;
using AlovaChat.Services;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

namespace AlovaChat.Hubs;

public class ChatHub : Hub
{
    private readonly IChatSessionService _sessionService;
    private readonly IAIModelService _aiModelService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IChatSessionService sessionService,
        IAIModelService aiModelService,
        ILogger<ChatHub> logger)
    {
        _sessionService = sessionService;
        _aiModelService = aiModelService;
        _logger = logger;
    }

    public async Task JoinSession(string sessionId)
    {
        try
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
            _logger.LogInformation("Connection {ConnectionId} joined session {SessionId}",
                Context.ConnectionId, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining session {SessionId}", sessionId);
            await Clients.Caller.SendAsync("Error", "Failed to join session");
        }
    }

    public async Task LeaveSession(string sessionId)
    {
        try
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
            _logger.LogInformation("Connection {ConnectionId} left session {SessionId}",
                Context.ConnectionId, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving session {SessionId}", sessionId);
        }
    }

    public async Task SendMessage(string sessionId, string message)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                await Clients.Caller.SendAsync("Error", "Message cannot be empty");
                return;
            }

            // Save user message
            var userMessage = await _sessionService.AddMessageAsync(sessionId, message, true);

            // Broadcast user message to session group
            await Clients.Group(sessionId).SendAsync("ReceiveMessage", new
            {
                Id = userMessage.Id,
                Content = userMessage.Content,
                IsFromUser = true,
                Timestamp = userMessage.Timestamp,
                Status = "sent"
            });

            // Indicate AI is typing
            await Clients.Group(sessionId).SendAsync("TypingIndicator", true);

            // Generate AI response
            var aiRequest = new AIRequest
            {
                Prompt = message,
                SessionId = sessionId,
                MaxTokens = 150,
                Temperature = 0.7f,
                TopP = 0.9f
            };

            var aiResponse = await _aiModelService.GenerateResponseAsync(aiRequest);

            // Stop typing indicator
            await Clients.Group(sessionId).SendAsync("TypingIndicator", false);

            // Log the AI response details for debugging
            _logger.LogInformation("AI Response - Success: {IsSuccess}, Content: {Content}, Error: {ErrorMessage}",
                aiResponse.IsSuccess, aiResponse.Content, aiResponse.ErrorMessage);

            if (aiResponse.IsSuccess)
            {
                // Save AI response
                var aiMessage = await _sessionService.AddMessageAsync(sessionId, aiResponse.Content, false);

                // Broadcast AI response
                await Clients.Group(sessionId).SendAsync("ReceiveMessage", new
                {
                    Id = aiMessage.Id,
                    Content = aiMessage.Content,
                    IsFromUser = false,
                    Timestamp = aiMessage.Timestamp,
                    Status = "sent"
                });
            }
            else
            {
                // Log the specific error for debugging
                _logger.LogError("AI Response failed: {ErrorMessage}", aiResponse.ErrorMessage);

                // Send error response
                var errorContent = "I apologize, but I'm experiencing technical difficulties. Please try again.";
                var errorMessage = await _sessionService.AddMessageAsync(sessionId, errorContent, false);

                await Clients.Group(sessionId).SendAsync("ReceiveMessage", new
                {
                    Id = errorMessage.Id,
                    Content = errorMessage.Content,
                    IsFromUser = false,
                    Timestamp = errorMessage.Timestamp,
                    Status = "error"
                });
            }

            _logger.LogInformation("Processed message for session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message for session {SessionId}", sessionId);

            await Clients.Group(sessionId).SendAsync("TypingIndicator", false);
            await Clients.Caller.SendAsync("Error", "Failed to process message. Please try again.");
        }
    }

    public async Task GetModelStatus()
    {
        try
        {
            await Clients.Caller.SendAsync("ModelStatus", new
            {
                IsLoaded = _aiModelService.IsModelLoaded,
                Status = _aiModelService.ModelStatus
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting model status");
            await Clients.Caller.SendAsync("Error", "Failed to get model status");
        }
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogError(exception, "Client disconnected with error: {ConnectionId}", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}