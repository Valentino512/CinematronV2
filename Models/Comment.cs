namespace Cinematron.Models;

public sealed class Comment
{
    public Guid Id { get; set; }

    public Guid MovieId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }

    public DateTime? EditedUtc { get; set; }

    public bool IsHighlighted { get; set; }

    public Movie? Movie { get; set; }
}
