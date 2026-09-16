using System.Globalization;
using System.Security.Cryptography;
using GZCTF.Middlewares;
using GZCTF.Models.Data;
using GZCTF.Models.Request.Admin;
using GZCTF.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GZCTF.Controllers;

[ApiController]
[RequireAdmin]
[Route("api/admin/team-invitations")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class TeamInvitationsController(
    AppDbContext db, UserManager<UserInfo> users, IDataProtectionProvider protection) : ControllerBase
{
    private IDataProtector Protector => protection.CreateProtector(TeamInvitationTokens.ProtectionPurpose);

    private async Task<int> Lifetime(CancellationToken ct)
    {
        var config = await db.Configs.FindAsync([TeamInvitationTokens.SettingsKey], ct);
        return int.TryParse(config?.Value, out var days) && days is >= 1 and <= 3650 ? days : 30;
    }

    [HttpGet("settings")]
    public async Task<IActionResult> Settings(CancellationToken ct) =>
        Ok(new TeamInvitationSettings { LifetimeDays = await Lifetime(ct) });

    [HttpPut("settings")]
    public async Task<IActionResult> Settings(TeamInvitationSettings settings, CancellationToken ct)
    {
        var value = settings.LifetimeDays.ToString(CultureInfo.InvariantCulture);
        var config = await db.Configs.FindAsync([TeamInvitationTokens.SettingsKey], ct);
        if (config is null) db.Configs.Add(new Config(TeamInvitationTokens.SettingsKey, value));
        else config.Value = value;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Conflict(new RequestResponse("Settings changed concurrently. Reload and try again.")); }
        return Ok(settings);
    }

    private object View(TeamInvitation i)
    {
        var status = i.RedeemedAt is not null ? "redeemed" :
            i.RevokedAt is not null ? "revoked" : i.ExpiresAt <= DateTimeOffset.UtcNow ? "expired" : "pending";
        string? token = null;
        if (status == "pending")
        {
            try { token = Protector.Unprotect(i.ProtectedToken); }
            catch (CryptographicException) { /* lost key: administrator can regenerate */ }
        }
        return new { i.Id, i.Email, i.TeamName, i.CreatedAt, i.ExpiresAt, i.TeamId, status, token };
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, [FromQuery] int page = 1)
    {
        page = Math.Max(1, Math.Min(page, 100000));
        var rows = await db.TeamInvitations.AsNoTracking().OrderByDescending(i => i.CreatedAt).ThenBy(i => i.Id)
            .Skip((page - 1) * 50).Take(50).ToListAsync(ct);
        return Ok(new { items = rows.Select(View), total = await db.TeamInvitations.CountAsync(ct) });
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import(TeamInvitationImport request, CancellationToken ct)
    {
        var emails = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);
        var invitations = new List<TeamInvitation>();
        var days = await Lifetime(ct);
        foreach (var row in request.Rows)
        {
            var email = row.Email.Trim();
            var name = row.TeamName.Trim();
            var normalizedEmail = users.NormalizeEmail(email);
            var normalizedName = name.ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrEmpty(normalizedEmail)
                || !emails.Add(normalizedEmail) || !names.Add(normalizedName))
                return BadRequest(new RequestResponse("Each row must have a distinct email and non-empty team_name."));
            if (await users.FindByEmailAsync(email) is not null
                || await db.Teams.AnyAsync(t => t.Name.ToUpper() == normalizedName, ct)
                || await db.TeamInvitations.AnyAsync(i => i.NormalizedEmail == normalizedEmail
                    || i.NormalizedTeamName == normalizedName, ct))
                return Conflict(new RequestResponse($"Email or team already exists/reserved: {email}, {name}. Use the existing invitation's Regenerate button instead."));
            var invitation = new TeamInvitation
            {
                Email = email, NormalizedEmail = normalizedEmail,
                TeamName = name, NormalizedTeamName = normalizedName
            };
            TeamInvitationTokens.Issue(invitation, Protector, days, DateTimeOffset.UtcNow);
            invitations.Add(invitation);
        }
        db.TeamInvitations.AddRange(invitations);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Conflict(new RequestResponse("Email or team was reserved concurrently. Reload and try again.")); }
        return Ok(new { created = invitations.Count });
    }

    [HttpPost("{id:guid}/regenerate")]
    public Task<IActionResult> Regenerate(Guid id, CancellationToken ct) => Change(id, false, ct);

    [HttpPost("{id:guid}/revoke")]
    public Task<IActionResult> Revoke(Guid id, CancellationToken ct) => Change(id, true, ct);

    private async Task<IActionResult> Change(Guid id, bool revoke, CancellationToken ct)
    {
        var invitation = await db.TeamInvitations.FindAsync([id], ct);
        if (invitation is null) return NotFound();
        if (invitation.RedeemedAt is not null)
            return Conflict(new RequestResponse("This invitation has already been redeemed."));
        if (revoke)
        {
            invitation.RevokedAt = DateTimeOffset.UtcNow;
            invitation.ProtectedToken = "";
            invitation.Version = Guid.NewGuid();
        }
        else TeamInvitationTokens.Issue(invitation, Protector, await Lifetime(ct), DateTimeOffset.UtcNow);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Conflict(new RequestResponse("Invitation changed concurrently. Reload and try again.")); }
        return Ok(View(invitation));
    }
}
