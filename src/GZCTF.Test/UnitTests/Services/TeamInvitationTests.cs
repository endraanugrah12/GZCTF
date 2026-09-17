using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using GZCTF.Models;
using GZCTF.Models.Data;
using GZCTF.Models.Request.Admin;
using GZCTF.Models.Internal;
using GZCTF.Controllers;
using GZCTF.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GZCTF.Test.UnitTests.Services;

public class TeamInvitationTests
{
    [Fact]
    public void TeamCreationPolicy_BlocksPlayersButNotAdmins_WhenDisabled()
    {
        var policy = new AccountPolicy { AllowPlayerTeamCreation = false };
        Assert.False(TeamController.MayCreateTeam(policy, new UserInfo { Role = GZCTF.Utils.Role.User }));
        Assert.True(TeamController.MayCreateTeam(policy, new UserInfo { Role = GZCTF.Utils.Role.Admin }));
        policy.AllowPlayerTeamCreation = true;
        Assert.True(TeamController.MayCreateTeam(policy, new UserInfo { Role = GZCTF.Utils.Role.User }));
    }

    [Fact]
    public void PlayerTeamCreation_DefaultsEnabled()
    {
        Assert.True(new AccountPolicy().AllowPlayerTeamCreation);
    }

    private readonly IDataProtector _protector = new EphemeralDataProtectionProvider()
        .CreateProtector(TeamInvitationTokens.ProtectionPurpose);
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(3650)]
    public void Issue_UsesSelectedLifetimeAndProtectedRandomToken(int days)
    {
        var invitation = new TeamInvitation();
        TeamInvitationTokens.Issue(invitation, _protector, days, Now);
        var token = _protector.Unprotect(invitation.ProtectedToken);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", token);
        Assert.Equal(TeamInvitationTokens.Hash(token), invitation.TokenHash);
        Assert.NotEqual(token, invitation.ProtectedToken);
        Assert.Equal(Now.AddDays(days), invitation.ExpiresAt);
        Assert.True(invitation.CanRedeem(Now));
        Assert.False(invitation.CanRedeem(invitation.ExpiresAt));
    }

    [Fact]
    public void Regeneration_RotatesHashAndVersionAndStartsNewLifetime()
    {
        var invitation = new TeamInvitation();
        TeamInvitationTokens.Issue(invitation, _protector, 30, Now);
        var hash = invitation.TokenHash;
        var version = invitation.Version;
        invitation.RevokedAt = Now;
        TeamInvitationTokens.Issue(invitation, _protector, 90, Now.AddDays(2));
        Assert.NotEqual(hash, invitation.TokenHash);
        Assert.NotEqual(version, invitation.Version);
        Assert.Null(invitation.RevokedAt);
        Assert.Equal(Now.AddDays(92), invitation.ExpiresAt);
    }

    [Fact]
    public void RedeemedOrRevokedTokensCannotBeUsed()
    {
        var invitation = new TeamInvitation();
        TeamInvitationTokens.Issue(invitation, _protector, 30, Now);
        invitation.RevokedAt = Now;
        Assert.False(invitation.CanRedeem(Now));
        invitation.RevokedAt = null;
        invitation.RedeemedAt = Now;
        Assert.False(invitation.CanRedeem(Now));
        Assert.Throws<InvalidOperationException>(() => TeamInvitationTokens.Issue(invitation, _protector, 30, Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3651)]
    public void InvalidLifetimesRejected(int days)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TeamInvitationTokens.Issue(new(), _protector, days, Now));
        var settings = new TeamInvitationSettings { LifetimeDays = days };
        Assert.False(Validator.TryValidateObject(settings,
            new ValidationContext(settings), new List<ValidationResult>(), true));
    }

    [Fact]
    public void DefaultLifetimeIsThirtyDays() => Assert.Equal(30, new TeamInvitationSettings().LifetimeDays);

    [Fact]
    public void MigrationSnapshotMatchesModel()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql().Options);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
