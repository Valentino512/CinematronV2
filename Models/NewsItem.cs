namespace Cinematron.Models;

public sealed class NewsItem
{
    public Guid Id { get; set; }
    public string Headline { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? Source { get; set; }
    public DateTime PublishedUtc { get; set; }
    public bool IsPublished { get; set; } = true;
}
