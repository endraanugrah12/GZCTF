using GZCTF.Models.Data;
using GZCTF.Middlewares;
using GZCTF.Models.Request.Admin;
using GZCTF.Models.Response.Account;
using GZCTF.Services;
using GZCTF.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GZCTF.Controllers;

public partial class AccountController
{
    [HttpPost]
    [EnableRateLimiting(nameof(RateLimiter.LimitPolicy.Register))]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> PreviewInvitation(TeamInvitationTokenModel model,
        [FromServices] AppDbContext db, CancellationToken ct)
    {
        var hash = TeamInvitationTokens.Hash(model.Token);
        var invitation = await db.TeamInvitations.AsNoTracking().SingleOrDefaultAsync(i => i.TokenHash == hash, ct);
        if (invitation is null || !invitation.CanRedeem(DateTimeOffset.UtcNow))
            return BadRequest(new RequestResponse("Invitation is invalid, expired, revoked, or already used."));
        return Ok(new { invitation.Email, invitation.TeamName, invitation.ExpiresAt });
    }

    [HttpPost]
    [EnableRateLimiting(nameof(RateLimiter.LimitPolicy.Register))]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> RedeemInvitation(TeamInvitationRegisterModel model,
        [FromServices] AppDbContext db, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated == true)
            return BadRequest(new RequestResponse("Sign out before creating an invited account."));
        // Explicit admin invitations intentionally work while public registration is closed.
        if (accountPolicy.Value.UseCaptcha && !await captcha.VerifyAsync(model, HttpContext, ct))
            return BadRequest(new RequestResponse(localizer[nameof(Resources.Program.Account_TokenValidationFailed)]));
        var hash = TeamInvitationTokens.Hash(model.Token);
        var invitation = await db.TeamInvitations.SingleOrDefaultAsync(i => i.TokenHash == hash, ct);
        if (invitation is null || !invitation.CanRedeem(DateTimeOffset.UtcNow))
            return BadRequest(new RequestResponse("Invitation is invalid, expired, revoked, or already used."));
        if (userManager.NormalizeEmail(model.Email) != invitation.NormalizedEmail
            || await userManager.FindByEmailAsync(invitation.Email) is not null)
            return BadRequest(new RequestResponse("This invitation cannot create an account for that email."));
        if (await db.Teams.AnyAsync(t => t.Name == invitation.TeamName, ct))
            return Conflict(new RequestResponse("The reserved team name is now in use. Contact an administrator."));
        var password = configService.DecryptApiData(model.Password);
        if (string.IsNullOrWhiteSpace(password))
            return BadRequest(new RequestResponse(localizer[nameof(Resources.Program.Model_PasswordRequired)]));
        var fingerprint = await ValidateBrowserFingerprint(model.Fingerprint, model.FingerprintProof, ct);
        if (accountPolicy.Value.EnableBrowserFingerprint && fingerprint is null)
            return BadRequest(new RequestResponse(localizer[nameof(Resources.Program.Parameter_FingerprintInvalid)]));

        try
        {
            var user = new UserInfo
            {
                UserName = model.UserName, Email = invitation.Email,
                EmailConfirmed = true, Role = Role.User, RegisterTimeUtc = DateTimeOffset.UtcNow
            };
            user.UpdateByHttpContext(HttpContext);
            var result = await TeamInvitationRedemption.RedeemAsync(db, userManager, invitation, user, password, ct);
            if (!result.Succeeded)
                return HandleIdentityError(result.Errors);
            // Do not set an auth cookie before the account+team+token transaction commits.
            await signInManager.SignInWithClaimsAsync(user, true, BuildFingerprintClaims(fingerprint));
            return Ok(new RequestResponse<RegisterStatus>("Account created and assigned to your team.",
                RegisterStatus.LoggedIn, StatusCodes.Status200OK));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new RequestResponse("Invitation changed or was used. Request a new link from an administrator."));
        }
    }
}
