using Microsoft.AspNetCore.Identity;

namespace Cinematron.Models;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }

    public int? Age { get; set; }

    public string? Gender { get; set; }

    public string? ProfileMemo { get; set; }

    public DateTime? LastActiveUtc { get; set; }

    public DateTime? BannedUntilUtc { get; set; }
}
