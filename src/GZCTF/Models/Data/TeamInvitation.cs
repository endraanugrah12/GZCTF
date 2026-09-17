using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace GZCTF.Models.Data;

// A pending team reservation, not a placeholder user. Names/emails remain reserved
// after revocation: admins explicitly regenerate the same invitation to reissue it.
[Index(nameof(NormalizedEmail), IsUnique = true)]
[Index(nameof(NormalizedTeamName), IsUnique = true)]
[Index(nameof(TokenHash), IsUnique = true)]
public class TeamInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(254)] public string Email { get; set; } = "";
    [MaxLength(254)] public string NormalizedEmail { get; set; } = "";
    [MaxLength(Limits.MaxTeamNameLength)] public string TeamName { get; set; } = "";
    // Legacy column name: the reservation key now preserves case (trim only).
    [MaxLength(Limits.MaxTeamNameLength)] public string NormalizedTeamName { get; set; } = "";
    [MaxLength(64)] public string TokenHash { get; set; } = "";
    public string ProtectedToken { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RedeemedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public int? TeamId { get; set; }
    public Guid? UserId { get; set; }
    [ConcurrencyCheck] public Guid Version { get; set; } = Guid.NewGuid();

    public bool CanRedeem(DateTimeOffset now) =>
        RedeemedAt is null && RevokedAt is null && ExpiresAt > now;
}
