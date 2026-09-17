using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using GZCTF.Controllers;
using GZCTF.Models;
using GZCTF.Models.Data;
using GZCTF.Models.Internal;
using GZCTF.Models.Request.Admin;
using GZCTF.Services;
using GZCTF.Services.Config;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace GZCTF.Test.UnitTests.Services;

public sealed class InvitationDatabaseFactAttribute : FactAttribute
{
    public InvitationDatabaseFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GZCTF_INVITATION_TEST_CONNECTION")))
            Skip = "Requires an isolated PostgreSQL server via GZCTF_INVITATION_TEST_CONNECTION.";
    }
}

public class TeamInvitationDatabaseTests
{
    private sealed class Fixture : IAsyncDisposable
    {
        public required ServiceProvider Services { get; init; }
        public required string Connection { get; init; }
        public static async Task<Fixture> Create()
        {
            var root = Environment.GetEnvironmentVariable("GZCTF_INVITATION_TEST_CONNECTION")!;
            var name = "invitation_test_" + Guid.NewGuid().ToString("N");
            await using (var admin = new NpgsqlConnection(root))
            {
                await admin.OpenAsync();
                await using var command = new NpgsqlCommand($"CREATE DATABASE {name}", admin);
                await command.ExecuteNonQueryAsync();
            }
            var connection = new NpgsqlConnectionStringBuilder(root) { Database = name }.ConnectionString;
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddLocalization();
            services.AddHttpContextAccessor();
            services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connection));
            services.AddIdentityCore<UserInfo>(o => {
                o.User.RequireUniqueEmail = true;
                o.User.AllowedUserNameCharacters = string.Empty;
            }).AddEntityFrameworkStores<AppDbContext>().AddSignInManager();
            var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS pg_trgm");
            await db.Database.EnsureCreatedAsync();
            return new Fixture { Services = provider, Connection = connection };
        }
        public async ValueTask DisposeAsync()
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
            await Services.DisposeAsync();
        }
        public async Task<Guid> Invite()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invitation = new TeamInvitation {
                Email = "leader@example.com", NormalizedEmail = "LEADER@EXAMPLE.COM",
                TeamName = "Team Alpha", NormalizedTeamName = "TEAM ALPHA"
            };
            TeamInvitationTokens.Issue(invitation,
                Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(TeamInvitationTokens.ProtectionPurpose),
                30, DateTimeOffset.UtcNow);
            db.TeamInvitations.Add(invitation);
            await db.SaveChangesAsync();
            return invitation.Id;
        }
    }

    private static UserInfo Leader(string name = "chosenName") => new()
    {
        UserName = name, Email = "leader@example.com", EmailConfirmed = true,
        RegisterTimeUtc = DateTimeOffset.UtcNow
    };

    private sealed class Snapshot<T>(T value) : IOptionsSnapshot<T> where T : class
    {
        public T Value => value;
        public T Get(string? name) => value;
    }

    // Only the existing API decryptor is replaced; Identity validation, user store,
    // transactions, membership and sign-in use their real implementations.
    public class TestApiDecryptor : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == nameof(IConfigService.DecryptApiData)
                ? args![0] : throw new NotSupportedException();
    }

    private static AccountController Account(IServiceProvider services)
    {
        var http = new DefaultHttpContext { RequestServices = services };
        http.Connection.RemoteIpAddress = IPAddress.Loopback;
        services.GetRequiredService<IHttpContextAccessor>().HttpContext = http;
        var controller = new AccountController(
            null!, null!, null!, null!, null!,
            DispatchProxy.Create<IConfigService, TestApiDecryptor>(),
            new Snapshot<AccountPolicy>(new AccountPolicy { AllowRegister = false, UseCaptcha = false, EnableBrowserFingerprint = false }),
            new Snapshot<GlobalConfig>(new GlobalConfig()),
            services.GetRequiredService<UserManager<UserInfo>>(),
            services.GetRequiredService<SignInManager<UserInfo>>(),
            NullLogger<AccountController>.Instance,
            services.GetRequiredService<IStringLocalizer<Program>>());
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    [InvitationDatabaseFact]
    public async Task RegistrationController_BindsEmailAndCreatesOrdinaryLeader_WhenPublicRegistrationClosed()
    {
        await using var fixture = await Fixture.Create();
        await fixture.Invite();
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var invitation = await db.TeamInvitations.SingleAsync();
        var token = fixture.Services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(TeamInvitationTokens.ProtectionPurpose).Unprotect(invitation.ProtectedToken);
        var account = Account(scope.ServiceProvider);
        Assert.IsType<OkObjectResult>(await account.PreviewInvitation(new TeamInvitationTokenModel { Token = token }, db, default));
        Assert.IsType<BadRequestObjectResult>(await account.RedeemInvitation(new TeamInvitationRegisterModel {
            Token = token, Email = "attacker@example.com", UserName = "chosenLeader", Password = "Chosen.Pass123!"
        }, db, default));
        Assert.Equal(0, await db.Users.CountAsync());
        Assert.IsType<OkObjectResult>(await account.RedeemInvitation(new TeamInvitationRegisterModel {
            Token = token, Email = invitation.Email, UserName = "chosenLeader", Password = "Chosen.Pass123!"
        }, db, default));
        var user = await db.Users.SingleAsync();
        Assert.Equal("chosenLeader", user.UserName);
        Assert.Equal(GZCTF.Utils.Role.User, user.Role);
        Assert.Equal(user.Id, (await db.Teams.SingleAsync()).CaptainId);
        Assert.NotEmpty(account.Response.Headers.SetCookie);
        Assert.IsType<BadRequestObjectResult>(await account.PreviewInvitation(new TeamInvitationTokenModel { Token = token }, db, default));
    }

    [InvitationDatabaseFact]
    public async Task TeamCreationFailure_RollsBackCreatedUserAndTokenClaim()
    {
        await using var fixture = await Fixture.Create();
        await fixture.Invite();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Teams\" ADD CONSTRAINT reject_test_team CHECK (\"Name\" <> 'Team Alpha')");
            var invitation = await db.TeamInvitations.SingleAsync();
            await Assert.ThrowsAsync<DbUpdateException>(() => TeamInvitationRedemption.RedeemAsync(db,
                scope.ServiceProvider.GetRequiredService<UserManager<UserInfo>>(), invitation, Leader(), "Chosen.Pass123!", default));
        }
        using var verify = fixture.Services.CreateScope();
        var context = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True((await context.TeamInvitations.SingleAsync()).CanRedeem(DateTimeOffset.UtcNow));
        Assert.Equal(0, await context.Users.CountAsync());
        Assert.Equal(0, await context.Teams.CountAsync());
    }

    [InvitationDatabaseFact]
    public async Task Migration_CreatesInvitationTableAndUniqueIndexes()
    {
        await using var fixture = await Fixture.Create();
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("DROP TABLE \"TeamInvitations\"");
        var commands = db.GetService<IMigrationsSqlGenerator>().Generate(
            new GZCTF.Migrations.TeamLeaderInvitations().UpOperations, db.Model);
        foreach (var command in commands)
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        var id = await fixture.Invite();
        Assert.True(await db.TeamInvitations.AnyAsync(i => i.Id == id));
    }

    [InvitationDatabaseFact]
    public async Task Redeem_CreatesChosenCredentialsAndTeamCaptain_ExactlyOnce()
    {
        await using var fixture = await Fixture.Create();
        var id = await fixture.Invite();
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<UserInfo>>();
        var invitation = await db.TeamInvitations.SingleAsync(i => i.Id == id);
        var user = Leader();
        var result = await TeamInvitationRedemption.RedeemAsync(db, users, invitation, user, "Chosen.Pass123!", default);
        Assert.True(result.Succeeded);
        Assert.True(await users.CheckPasswordAsync(user, "Chosen.Pass123!"));
        var team = await db.Teams.Include(t => t.Members).SingleAsync();
        Assert.Equal("Team Alpha", team.Name);
        Assert.Equal(user.Id, team.CaptainId);
        Assert.Contains(team.Members, m => m.Id == user.Id);
        Assert.Equal(team.Id, invitation.TeamId);
        Assert.Equal(user.Id, invitation.UserId);
        Assert.Empty(invitation.ProtectedToken);
        var repeat = await TeamInvitationRedemption.RedeemAsync(db, users, invitation, Leader("secondName"), "Chosen.Pass123!", default);
        Assert.False(repeat.Succeeded);
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [InvitationDatabaseFact]
    public async Task InvalidPassword_RollsBackTokenClaimAndLeavesNoAccountOrTeam()
    {
        await using var fixture = await Fixture.Create();
        var id = await fixture.Invite();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invitation = await db.TeamInvitations.SingleAsync(i => i.Id == id);
            var result = await TeamInvitationRedemption.RedeemAsync(db,
                scope.ServiceProvider.GetRequiredService<UserManager<UserInfo>>(), invitation, Leader(), "x", default);
            Assert.False(result.Succeeded);
        }
        using var verify = fixture.Services.CreateScope();
        var context = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True((await context.TeamInvitations.SingleAsync()).CanRedeem(DateTimeOffset.UtcNow));
        Assert.Equal(0, await context.Users.CountAsync());
        Assert.Equal(0, await context.Teams.CountAsync());
    }

    [InvitationDatabaseFact]
    public async Task ConcurrentRedemptions_OnlyOneCreatesAnAccount()
    {
        await using var fixture = await Fixture.Create();
        var id = await fixture.Invite();
        using var first = fixture.Services.CreateScope();
        using var second = fixture.Services.CreateScope();
        var db1 = first.ServiceProvider.GetRequiredService<AppDbContext>();
        var db2 = second.ServiceProvider.GetRequiredService<AppDbContext>();
        var one = await db1.TeamInvitations.SingleAsync(i => i.Id == id);
        var two = await db2.TeamInvitations.SingleAsync(i => i.Id == id);
        var result = await TeamInvitationRedemption.RedeemAsync(db1,
            first.ServiceProvider.GetRequiredService<UserManager<UserInfo>>(), one, Leader(), "Chosen.Pass123!", default);
        Assert.True(result.Succeeded);
        // Second request loaded the old version before the first committed.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => TeamInvitationRedemption.RedeemAsync(db2,
            second.ServiceProvider.GetRequiredService<UserManager<UserInfo>>(), two, Leader("secondName"), "Chosen.Pass123!", default));
        Assert.Equal(1, await db1.Users.CountAsync());
        Assert.Equal(1, await db1.Teams.CountAsync());
    }

    [InvitationDatabaseFact]
    public async Task AdminImport_CreatesOnlyInvitations_AndRejectsDuplicateRowsAtomically()
    {
        await using var fixture = await Fixture.Create();
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var controller = new TeamInvitationsController(db,
            scope.ServiceProvider.GetRequiredService<UserManager<UserInfo>>(),
            fixture.Services.GetRequiredService<IDataProtectionProvider>());
        Assert.IsType<OkObjectResult>(await controller.Settings(new TeamInvitationSettings { LifetimeDays = 180 }, default));
        Assert.IsType<OkObjectResult>(await controller.Import(new TeamInvitationImport {
            Rows = [new() { Email = "leader@example.com", TeamName = "Team Alpha" }]
        }, default));
        var invitation = await db.TeamInvitations.SingleAsync();
        Assert.InRange((invitation.ExpiresAt - DateTimeOffset.UtcNow).TotalDays, 179.99, 180);
        Assert.Equal(0, await db.Users.CountAsync());
        Assert.Equal(0, await db.Teams.CountAsync());
        Assert.IsType<ConflictObjectResult>(await controller.Import(new TeamInvitationImport {
            Rows = [new() { Email = "new@example.com", TeamName = "New Team" },
                    new() { Email = "LEADER@example.com", TeamName = "Another Team" }]
        }, default));
        Assert.Equal(1, await db.TeamInvitations.CountAsync());
    }

    [InvitationDatabaseFact]
    public async Task CaseSensitiveNames_MigrateExistingReservation_ImportAndRedeemVariants()
    {
        await using var fixture = await Fixture.Create();
        await fixture.Invite(); // Existing uppercase reservation key from the old release.
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var commands = db.GetService<IMigrationsSqlGenerator>().Generate(
            new GZCTF.Migrations.CaseSensitiveInvitationTeamNames().UpOperations, db.Model);
        foreach (var command in commands)
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        Assert.Equal("Team Alpha", (await db.TeamInvitations.SingleAsync()).NormalizedTeamName);
        var controller = new TeamInvitationsController(db,
            scope.ServiceProvider.GetRequiredService<UserManager<UserInfo>>(),
            fixture.Services.GetRequiredService<IDataProtectionProvider>());
        Assert.IsType<OkObjectResult>(await controller.Import(new TeamInvitationImport {
            Rows = [new() { Email = "second@example.com", TeamName = "team alpha" },
                    new() { Email = "third@example.com", TeamName = "TEAM ALPHA" }]
        }, default));
        Assert.IsType<ConflictObjectResult>(await controller.Import(new TeamInvitationImport {
            Rows = [new() { Email = "fourth@example.com", TeamName = "Team Alpha" }]
        }, default));
        Assert.IsType<ConflictObjectResult>(await controller.Import(new TeamInvitationImport {
            Rows = [new() { Email = "SECOND@example.com", TeamName = "Different" }]
        }, default));
        var invitations = await db.TeamInvitations.OrderBy(i => i.Email).ToListAsync();
        var protector = fixture.Services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(TeamInvitationTokens.ProtectionPurpose);
        foreach (var invitation in invitations)
        {
            var account = Account(scope.ServiceProvider);
            Assert.IsType<OkObjectResult>(await account.RedeemInvitation(new TeamInvitationRegisterModel {
                Token = protector.Unprotect(invitation.ProtectedToken), Email = invitation.Email,
                UserName = "leader" + invitation.Id.ToString("N"), Password = "Chosen.Pass123!"
            }, db, default));
        }
        Assert.Equal(3, await db.Teams.CountAsync());
        Assert.Equal(3, await db.Users.CountAsync());
    }

    [InvitationDatabaseFact]
    public async Task RevokeAndRegenerate_InvalidatePreviouslyLoadedClaim()
    {
        await using var fixture = await Fixture.Create();
        var id = await fixture.Invite();
        using var redeemScope = fixture.Services.CreateScope();
        var db = redeemScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stale = await db.TeamInvitations.SingleAsync(i => i.Id == id);
        using (var adminScope = fixture.Services.CreateScope())
        {
            var controller = new TeamInvitationsController(
                adminScope.ServiceProvider.GetRequiredService<AppDbContext>(),
                adminScope.ServiceProvider.GetRequiredService<UserManager<UserInfo>>(),
                fixture.Services.GetRequiredService<IDataProtectionProvider>());
            Assert.IsType<OkObjectResult>(await controller.Revoke(id, default));
            Assert.IsType<OkObjectResult>(await controller.Regenerate(id, default));
        }
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => TeamInvitationRedemption.RedeemAsync(db,
            redeemScope.ServiceProvider.GetRequiredService<UserManager<UserInfo>>(), stale, Leader(), "Chosen.Pass123!", default));
        db.ChangeTracker.Clear();
        var fresh = await db.TeamInvitations.SingleAsync();
        Assert.NotEqual(stale.TokenHash, fresh.TokenHash);
        Assert.True(fresh.CanRedeem(DateTimeOffset.UtcNow));
        Assert.Equal(0, await db.Users.CountAsync());
    }
}
