namespace Cinematron.Models;

public enum VideoReactionType
{
    Like,
    Dislike,
    Love,
    Hate
}

public sealed class VideoReaction
{
    public Guid Id { get; set; }
    public Guid MovieId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public VideoReactionType Type { get; set; }
    public DateTime CreatedUtc { get; set; }

    public Movie? Movie { get; set; }
}
