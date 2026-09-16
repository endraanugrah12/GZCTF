using GZCTF.Models.Data;
using Microsoft.AspNetCore.Identity;

namespace GZCTF.Services;

public static class TeamInvitationRedemption
{
    // The UserManager store and invitation must share this scoped DbContext.
    public static async Task<IdentityResult> RedeemAsync(AppDbContext db, UserManager<UserInfo> users,
        TeamInvitation invitation, UserInfo user, string password, CancellationToken ct)
    {
        if (!invitation.CanRedeem(DateTimeOffset.UtcNow))
            return IdentityResult.Failed(new IdentityError { Description = "Invitation is no longer valid." });
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        invitation.RedeemedAt = DateTimeOffset.UtcNow;
        invitation.Version = Guid.NewGuid();
        invitation.ProtectedToken = "";
        // Concurrency-checked update holds the row lock until the entire transaction
        // commits. Another redemption/revocation/regeneration cannot also succeed.
        await db.SaveChangesAsync(ct);
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync(ct);
            return result;
        }
        var team = new Team { Name = invitation.TeamName, CaptainId = user.Id, Members = [user] };
        db.Teams.Add(team);
        await db.SaveChangesAsync(ct);
        invitation.TeamId = team.Id;
        invitation.UserId = user.Id;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }
}
