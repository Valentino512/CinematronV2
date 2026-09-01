namespace Cinematron.Models;

public sealed class Message
{
    public Guid Id { get; set; }
    public string FromUserId { get; set; } = string.Empty;
    public string ToUserId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public bool IsRead { get; set; }
}
