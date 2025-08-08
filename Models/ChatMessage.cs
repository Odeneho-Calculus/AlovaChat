using System.ComponentModel.DataAnnotations;

namespace AlovaChat.Models;

public class ChatMessage
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string SessionId { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    [Required]
    public bool IsFromUser { get; set; }

    [Required]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string? Metadata { get; set; }

    // Navigation property
    public virtual ChatSession? Session { get; set; }
}

public class ChatMessageDto
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsFromUser { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Metadata { get; set; }
}