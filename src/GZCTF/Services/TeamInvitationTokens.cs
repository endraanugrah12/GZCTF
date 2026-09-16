using System.Security.Cryptography;
using System.Text;
using GZCTF.Models.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace GZCTF.Services;

public static class TeamInvitationTokens
{
    public const string SettingsKey = "TeamInvitations.LifetimeDays";
    public const string ProtectionPurpose = "GZCTF.TeamLeaderInvitation.v1";
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static void Issue(TeamInvitation invitation, IDataProtector protector, int days, DateTimeOffset now)
    {
        if (days is < 1 or > 3650) throw new ArgumentOutOfRangeException(nameof(days));
        if (invitation.RedeemedAt is not null) throw new InvalidOperationException("Invitation already redeemed.");
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        invitation.TokenHash = Hash(token);
        invitation.ProtectedToken = protector.Protect(token);
        invitation.ExpiresAt = now.AddDays(days);
        invitation.RevokedAt = null;
        invitation.Version = Guid.NewGuid();
    }
}
