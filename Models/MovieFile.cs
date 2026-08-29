namespace Cinematron.Models;

public sealed class MovieFile
{
    public Guid Id { get; set; }

    public Guid MovieId { get; set; }

    public string AssetType { get; set; } = string.Empty;

    public string StoragePath { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public DateTime UploadedUtc { get; set; }

    public Movie Movie { get; set; } = null!;
}
