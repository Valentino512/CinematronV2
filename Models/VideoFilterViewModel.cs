namespace Cinematron.Models;

public sealed class VideoFilterViewModel
{
    public string? Name { get; init; }

    public string? Genre { get; init; }

    public DateTime? FromDate { get; init; }

    public DateTime? ToDate { get; init; }

    public string Sort { get; init; } = "newest";

    public IReadOnlyList<string> Genres { get; init; } = Array.Empty<string>();

    public IReadOnlyList<MovieCard> Movies { get; init; } = Array.Empty<MovieCard>();
}
