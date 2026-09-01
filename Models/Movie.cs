namespace Cinematron.Models;

public sealed class Movie
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Genre { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string OwnerId { get; set; } = string.Empty;

    public ApplicationUser Owner { get; set; } = null!;

    public DateTime CreatedUtc { get; set; }

    public bool IsPublic { get; set; } = true; 

    public ICollection<MovieFile> Files { get; set; } = new List<MovieFile>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<VideoReaction> Reactions { get; set; } = new List<VideoReaction>();
}
