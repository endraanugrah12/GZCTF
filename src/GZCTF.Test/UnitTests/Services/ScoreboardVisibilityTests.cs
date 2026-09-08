using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GZCTF.Models;
using GZCTF.Models.Data;
using GZCTF.Repositories;
using GZCTF.Services.Config;
using GZCTF.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GZCTF.Test.UnitTests.Services;

public class ScoreboardVisibilityTests
{
    [Fact]
    public async Task HidingAccountFiltersLiveAndFrozenStandingsAndCanBeReversed()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        var start = DateTimeOffset.UtcNow.AddHours(-3);
        var game = new Game { Id = 11, Title = "Visibility regression", StartTimeUtc = start,
            EndTimeUtc = start.AddHours(4), FreezeTimeUtc = start.AddHours(1) };
        var challenge = new GameChallenge { Id = 21, Game = game, Title = "Test challenge",
            Type = ChallengeType.StaticAttachment, IsEnabled = true, ReviewStatus = ChallengeReviewStatus.Active,
            DisableBloodBonus = true };
        var hidden = new UserInfo { UserName = "organizer-test" };
        var visible = new UserInfo { UserName = "player-test" };
        db.AddRange(game, challenge);
        foreach (var (user, id, time) in new[] { (hidden, 1, start.AddMinutes(10)), (visible, 2, start.AddHours(2)) })
        {
            var team = new Team { Id = id, Name = user.UserName!, Captain = user };
            var participation = new Participation { Id = id, Game = game, Team = team, Status = ParticipationStatus.Accepted };
            var membership = new UserParticipation(user, game, team) { ParticipationId = id };
            participation.Members.Add(membership);
            var submission = new Submission { Id = id, Game = game, GameChallenge = challenge,
                Participation = participation, Team = team, User = user, SubmitTimeUtc = time };
            db.AddRange(participation, submission,
                new FirstSolve { Participation = participation, Challenge = challenge, Submission = submission });
        }
        await db.SaveChangesAsync();
        var config = DispatchProxy.Create<IConfigService, ConfigStub>();
        var repository = new GameRepository(NullLogger<GameRepository>.Instance, null!, null!, null!,
            null!, config, null!, null!, db);

        var before = await repository.GenScoreboard(game);
        Assert.Equal(2, before.Items.Count);
        var frozenBefore = await repository.GenScoreboard(game, game.FreezeTimeUtc);
        Assert.Single(frozenBefore.Items[1].SolvedChallenges);
        Assert.Empty(frozenBefore.Items[2].SolvedChallenges);

        hidden.HideFromScoreboard = true;
        await db.SaveChangesAsync();
        var live = await repository.GenScoreboard(game);
        var frozen = await repository.GenScoreboard(game, game.FreezeTimeUtc);
        Assert.False(live.Items.ContainsKey(1));
        Assert.False(frozen.Items.ContainsKey(1));
        Assert.Equal(1, live.Items[2].Rank);
        Assert.Equal(500, live.Items[2].Score);
        Assert.Empty(frozen.Items[2].SolvedChallenges);
        Assert.All(live.TimeLines.Values.SelectMany(t => t), t => Assert.NotEqual(1, t.Id));
        Assert.Equal(2, await db.Submissions.CountAsync());
        Assert.Equal(2, await db.FirstSolves.CountAsync());

        hidden.HideFromScoreboard = false;
        await db.SaveChangesAsync();
        var restored = await repository.GenScoreboard(game);
        Assert.Equal(before.Items[1].Score, restored.Items[1].Score);
        Assert.Equal(before.Items[2].Score, restored.Items[2].Score);
    }

    public class ConfigStub : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method?.Name == nameof(IConfigService.GetXorKey) ? Array.Empty<byte>()
                : throw new NotSupportedException(method?.Name);
    }
}
