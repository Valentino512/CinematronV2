namespace Cinematron.Models;

public sealed record MovieCard(
    string Title,
    string Genre,
    string Year,
    string Runtime,
    string Description,
    string PosterUrl);
