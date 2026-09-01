namespace Cinematron.Models;

public sealed class Joke
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Author { get; set; }
    public DateTime PublishedUtc { get; set; }
    public bool IsPublished { get; set; } = true;
}
