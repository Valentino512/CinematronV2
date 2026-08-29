using Microsoft.AspNetCore.Identity;

namespace Cinematron.Models;

public sealed class Movie
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Genre { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string OwnerId { get; set; } = string.Empty;

    public IdentityUser Owner { get; set; } = null!;

    public DateTime CreatedUtc { get; set; }

    public ICollection<MovieFile> Files { get; set; } = new List<MovieFile>();
}
