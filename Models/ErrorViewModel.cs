namespace Cinematron.Models
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }

    public sealed record MovieCard(
        string Title,
        string Genre,
        string Year,
        string Runtime,
        string Description,
        string PosterUrl,
        Guid? Id = null,
        string? VideoUrl = null,
        string Author = "Cinematron",
        string? OwnerId = null,
        bool IsPublic = true);

    public sealed record HomeContentViewModel(
        IReadOnlyList<MovieCard> Movies,
        IReadOnlyList<Joke> Jokes,
        IReadOnlyList<NewsItem> News);

    public sealed record AdminContentViewModel(
        IReadOnlyList<AdminJokeViewModel> Jokes,
        IReadOnlyList<AdminNewsViewModel> News);

    public sealed record AdminJokeViewModel(Guid Id, string Text, string? Author, bool IsPublished);
    public sealed record AdminNewsViewModel(Guid Id, string Headline, string Summary, string? Source, bool IsPublished);

    public sealed record WatchVideoViewModel(
        Guid Id,
        string Title,
        string Genre,
        string Description,
        string PosterUrl,
        string VideoUrl,
        string OriginalFileName,
        IReadOnlyList<CommentViewModel> Comments,
        string OwnerId,
        IReadOnlyDictionary<VideoReactionType, int> ReactionCounts,
        VideoReactionType? CurrentReaction);

    public sealed record CommentViewModel(
        Guid Id,
        string UserId,
        string UserName,
        string Text,
        DateTime CreatedUtc,
        DateTime? EditedUtc,
        bool IsHighlighted = false);

    public sealed class UploadVideoViewModel
    {
        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(100)]
        [System.ComponentModel.DataAnnotations.Display(Name = "Movie title")]
        public string Title { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(60)]
        public string Genre { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Please select a video file.")]
        [System.ComponentModel.DataAnnotations.Display(Name = "Video file")]
        public Microsoft.AspNetCore.Http.IFormFile? VideoFile { get; set; }

        [System.ComponentModel.DataAnnotations.Display(Name = "Poster image")]
        public Microsoft.AspNetCore.Http.IFormFile? PosterFile { get; set; }
    }
}
