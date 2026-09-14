using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Reflection;
using GZCTF.Controllers;
using GZCTF.Models;
using GZCTF.Models.Data;
using GZCTF.Models.Request.Admin;
using GZCTF.Repositories;
using GZCTF.Services.Webhook;
using GZCTF.Services.Config;
using GZCTF.Utils;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GZCTF.Test.UnitTests.Services;

public class DiscordSettingsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProfileReturnsPersistedScoreboardVisibility(bool hidden)
    {
        var profile = GZCTF.Models.Request.Account.ProfileUserInfoModel.FromUserInfo(
            new UserInfo { HideFromScoreboard = hidden });
        Assert.Equal(hidden, profile.HideFromScoreboard);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(profile, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(hidden, json.RootElement.GetProperty("hideFromScoreboard").GetBoolean());
    }
    private static readonly DateTimeOffset Freeze = DateTimeOffset.Parse("2026-09-14T12:00:00Z");
    private static Game Game() => new() { Id = 1, Title = "Test game", StartTimeUtc = Freeze.AddHours(-2),
        EndTimeUtc = Freeze.AddHours(1), FreezeTimeUtc = Freeze };
    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    [Fact]
    public async Task AdminListsHiddenGamesWithoutAddingThemToPublicLists()
    {
        await using var db = Database();
        var hidden = Game();
        hidden.Hidden = true;
        hidden.StartTimeUtc = DateTimeOffset.UtcNow.AddDays(3);
        var admin = new UserInfo { UserName = "preview-admin", Role = Role.Admin };
        db.AddRange(hidden, admin, new Game { Id = 2, Title = "Public game" });
        await db.SaveChangesAsync();
        using var services = new ServiceCollection().AddSingleton(db).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services, User = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString())], "test")) };
        var ctor = typeof(GameController).GetConstructors().Single();
        var args = ctor.GetParameters().Select(p => p.ParameterType == typeof(AppDbContext) ? (object)db :
            p.ParameterType == typeof(IDataProtectionProvider) ? new EphemeralDataProtectionProvider() : null).ToArray();
        var controller = (GameController)ctor.Invoke(args);
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        var result = Assert.IsType<OkObjectResult>(await controller.RecentGames(50, default));
        Assert.Contains("Test game", JsonSerializer.Serialize(result.Value));
        Assert.Contains("no-store", http.Response.Headers.CacheControl.ToString());
        var repository = new GameRepository(NullLogger<GameRepository>.Instance, null!, null!, null!,
            null!, DispatchProxy.Create<IConfigService, ScoreboardVisibilityTests.ConfigStub>(), null!, null!, db);
        Assert.Single(await repository.FetchGameList(50, 0, default));
        admin.Role = Role.User;
        await db.SaveChangesAsync();
        Assert.False(await ContextHelper.HasAdmin(http));
    }

    [Theory]
    [InlineData(NoticeType.FirstBlood)]
    [InlineData(NoticeType.SecondBlood)]
    [InlineData(NoticeType.ThirdBlood)]
    public void BloodIdentityIsRemovedAtFreezeAndAfterEnd(NoticeType type)
    {
        var notice = new GameNotice { Type = type, Values = ["PrivateTeam", "Challenge"], PublishTimeUtc = Freeze };
        var settings = new DiscordSettings { FirstBloodTitle = "{team} wins", SecondBloodTitle = "{team} wins",
            ThirdBloodTitle = "{team} wins", BloodMessage = "{team}: {challenge}" };
        foreach (var now in new[] { Freeze, Freeze.AddDays(1) })
        {
            var payload = JsonSerializer.Serialize(SendWebhookService.CreateNoticeMessage(notice, Game(), settings, now));
            Assert.DoesNotContain("PrivateTeam", payload);
            Assert.Contains("Anonymous team", payload);
            Assert.Contains("Challenge", payload);
            Assert.Contains("\"allowed_mentions\":{\"parse\":[]}", payload);
        }
        notice.PublishTimeUtc = Freeze.AddSeconds(-1);
        var before = SendWebhookService.CreateNoticeMessage(notice, Game(), settings, Freeze.AddSeconds(-1));
        Assert.Contains("PrivateTeam", before!.Embeds![0].Description);
        var delayed = SendWebhookService.CreateNoticeMessage(notice, Game(), settings, Freeze);
        Assert.DoesNotContain("PrivateTeam", JsonSerializer.Serialize(delayed));
    }

    [Fact]
    public void CheatDetailsAreWithheldAndTemplatesCanBeDisabled()
    {
        var evt = new GameEvent { Type = EventType.CheatDetected, Team = new Team { Name = "SecretTeam" },
            Values = ["SecretTeam copied OtherTeam flag"], PublishTimeUtc = Freeze };
        var settings = new DiscordSettings { AvatarUrl = "https://example.org/bot.png", Username = "Custom bot" };
        var payload = JsonSerializer.Serialize(SendWebhookService.CreateEventMessage(evt, Game(), settings, Freeze));
        Assert.DoesNotContain("SecretTeam", payload);
        Assert.DoesNotContain("OtherTeam", payload);
        Assert.Contains("withheld", payload);
        Assert.Contains("avatar_url", payload);
        Assert.Contains("Custom bot", payload);
        settings.CheatMessage = "";
        Assert.Null(SendWebhookService.CreateEventMessage(evt, Game(), settings, Freeze));
        settings.Enabled = false;
        Assert.Null(SendWebhookService.CreateNoticeMessage(new GameNotice { Type = NoticeType.FirstBlood }, Game(), settings, Freeze));
        settings.Enabled = true;
        settings.Bloods = false;
        Assert.Null(SendWebhookService.CreateNoticeMessage(new GameNotice { Type = NoticeType.FirstBlood }, Game(), settings, Freeze));
    }

    [Fact]
    public void SubstitutionDoesNotExpandTokensInsidePlayerValues()
        => Assert.Equal("\\*{details}\\*", SendWebhookService.Render("{team}", "*{details}*", "", "", "", "private"));

    [Fact]
    public async Task SettingsPersistPerGameWithoutChangingOtherConfigs()
    {
        await using var db = Database();
        db.Games.Add(Game());
        await db.SaveChangesAsync();
        var controller = new DiscordSettingsController(db);
        Assert.IsType<OkObjectResult>(await controller.Save(1, new DiscordSettings { Username = "Saved bot", Bloods = false }, CancellationToken.None));
        var saved = await DiscordSettings.Read(db, 1);
        Assert.Equal("Saved bot", saved.Username);
        Assert.False(saved.Bloods);
        Assert.True((await DiscordSettings.Read(db, 2)).Bloods);
        Assert.IsType<NotFoundResult>(await controller.Save(99, new(), CancellationToken.None));
    }

    [Theory]
    [InlineData("flag{test}", AnswerResult.Accepted)]
    [InlineData("wrong", AnswerResult.WrongAnswer)]
    public async Task PreStartFlagChecksDoNotAwardSolvesOrConsumeLiveAttempts(string answer, AnswerResult expected)
    {
        await using var db = Database();
        var game = Game();
        var challenge = new GameChallenge { Id = 2, Game = game, Title = "Test", Type = ChallengeType.StaticAttachment };
        var user = new UserInfo { UserName = "admin" };
        var team = new Team { Id = 3, Name = "Test team", Captain = user };
        var part = new Participation { Id = 4, Game = game, Team = team, Status = ParticipationStatus.Accepted };
        var flag = new FlagContext { Challenge = challenge, Flag = "flag{test}" };
        var submission = new Submission { Id = 5, Game = game, GameChallenge = challenge, Participation = part,
            Team = team, User = user, Answer = answer, SubmitTimeUtc = game.StartTimeUtc.AddMinutes(-1) };
        db.AddRange(submission, flag, new GameInstance { Challenge = challenge, Participation = part });
        await db.SaveChangesAsync();
        var repository = new GameInstanceRepository(db, null!, null!, null!, null!, null!,
            NullLogger<GameInstanceRepository>.Instance, null!, null!);
        await repository.VerifyAnswer(submission);
        Assert.Equal(expected, submission.Status);
        Assert.Empty(await db.FirstSolves.ToArrayAsync());
        Assert.Single(await db.Submissions.ToArrayAsync());
        Assert.Equal(0, await new SubmissionRepository(null!, null!, null!, db).CountSubmissions(4, 2));
    }
}
