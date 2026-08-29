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
        string PosterUrl);

    public sealed class UploadVideoViewModel
    {
        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(100)]
        [System.ComponentModel.DataAnnotations.Display(Name = "Movie title")]
        public string Title { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(60)]
        public string Genre { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Display(Name = "Video file")]
        public Microsoft.AspNetCore.Http.IFormFile? VideoFile { get; set; }

        [System.ComponentModel.DataAnnotations.Display(Name = "Poster image")]
        public Microsoft.AspNetCore.Http.IFormFile? PosterFile { get; set; }
    }
}
