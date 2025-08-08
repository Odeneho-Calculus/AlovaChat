namespace AlovaChat.Models;

public class AIResponse
{
    public string Content { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class AIRequest
{
    public string Prompt { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string? Context { get; set; }
    public int MaxTokens { get; set; } = 150;
    public float Temperature { get; set; } = 0.7f;
    public float TopP { get; set; } = 0.9f;
}