using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GZCTF.Controllers;
using GZCTF.Middlewares;
using GZCTF.Models;
using GZCTF.Models.Data;
using GZCTF.Repositories;
using GZCTF.Services;
using GZCTF.Services.Config;
using GZCTF.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GZCTF.Test.UnitTests.Services;

public class SubmissionDeletionTests
{
    public class ConfigStub : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            method?.Name == nameof(IConfigService.GetXorKey) ? new byte[32] : throw new NotSupportedException();
    }

    [Fact]
    public void EndpointRequiresAdmin() =>
        Assert.NotNull(typeof(SubmissionDeletionController).GetCustomAttribute<RequireAdminAttribute>());

    [InvitationDatabaseFact]
    public async Task Delete_RecalculatesLiveFrozenScoresAndBloods_PreservesEvidence_RejectsPendingAndWrongGame()
    {
        await using var fixture = await TeamInvitationDatabaseTests.Fixture.Create();
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var start = DateTimeOffset.UtcNow.AddHours(-1);
        var game = new Game { Title = "Deletion test", StartTimeUtc = start, EndTimeUtc = start.AddHours(2),
            FreezeTimeUtc = start.AddMinutes(25) };
        var challenge = new GameChallenge { Game = game, Title = "Flag", IsEnabled = true,
            ReviewStatus = ChallengeReviewStatus.Active, Type = ChallengeType.StaticAttachment };
        var userA = new UserInfo { UserName = "A" };
        var a = new Participation { Game = game, Team = new Team { Name = "A", Captain = userA },
            Status = ParticipationStatus.Accepted };
        var b = new Participation { Game = game, Team = new Team { Name = "B", Captain = new UserInfo { UserName = "B" } },
            Status = ParticipationStatus.Accepted };
        Submission Make(Participation p, int minute, AnswerResult status = AnswerResult.Accepted) => new() {
            Game = game, GameChallenge = challenge, Participation = p, Team = p.Team,
            Status = status, SubmitTimeUtc = start.AddMinutes(minute), Answer = "flag{test}"
        };
        var first = Make(a, 10);
        var second = Make(b, 20);
        var repeat = Make(a, 30);
        var wrong = Make(a, 31, AnswerResult.WrongAnswer);
        var pending = Make(a, 32, AnswerResult.FlagSubmitted);
        // Historical accepted attempt from before a solve reset must not be revived.
        var preReset = Make(a, 5);
        db.Submissions.AddRange(preReset, first, second, repeat, wrong, pending);
        await db.SaveChangesAsync();
        db.FirstSolves.AddRange(new FirstSolve { ParticipationId = a.Id, ChallengeId = challenge.Id, SubmissionId = first.Id },
            new FirstSolve { ParticipationId = b.Id, ChallengeId = challenge.Id, SubmissionId = second.Id });
        var evidence = new SubmissionEvidence { Game = game, Challenge = challenge, User = userA, Participation = a,
            SubmissionId = first.Id, LlmLinks = "https://chatgpt.com/share/test", SolverFile = new LocalFile { Name = "solver.py", Hash = new string('a', 64) } };
        db.SubmissionEvidence.Add(evidence);
        await db.SaveChangesAsync();
        var repository = new GameRepository(NullLogger<GameRepository>.Instance, null!, null!, null!, null!,
            DispatchProxy.Create<IConfigService, ConfigStub>(), null!, null!, db);
        var before = await repository.GenScoreboard(game);
        Assert.True(before.Items[a.Id].Score > 0);
        Assert.Equal(1, before.Items[a.Id].Rank);
        Assert.Equal(SubmissionDeletion.Result.NotFound, await SubmissionDeletion.DeleteAsync(db, game.Id + 100, first.Id));
        Assert.Equal(SubmissionDeletion.Result.Pending, await SubmissionDeletion.DeleteAsync(db, game.Id, pending.Id));
        Assert.Equal(SubmissionDeletion.Result.Deleted, await SubmissionDeletion.DeleteAsync(db, game.Id, first.Id));
        Assert.False(await db.Submissions.AnyAsync(s => s.Id == first.Id));
        Assert.NotNull((await db.Submissions.IgnoreQueryFilters().SingleAsync(s => s.Id == first.Id)).DeletedAtUtc);
        Assert.Equal(first.Id, (await db.SubmissionEvidence.SingleAsync()).SubmissionId);
        Assert.Equal(repeat.Id, (await db.FirstSolves.SingleAsync(s => s.ParticipationId == a.Id)).SubmissionId);
        var after = await repository.GenScoreboard(game);
        Assert.True(after.Items[a.Id].Score > 0);
        Assert.Equal(1, after.Items[b.Id].Rank);
        var frozen = await repository.GenScoreboard(game, game.FreezeTimeUtc);
        Assert.Equal(0, frozen.Items[a.Id].Score);
        Assert.True(frozen.Items[b.Id].Score > 0);
        Assert.Equal(SubmissionDeletion.Result.NotFound, await SubmissionDeletion.DeleteAsync(db, game.Id, first.Id));
        Assert.Equal(SubmissionDeletion.Result.Deleted, await SubmissionDeletion.DeleteAsync(db, game.Id, wrong.Id));
        Assert.Equal(2, await db.FirstSolves.CountAsync());
        Assert.Equal(SubmissionDeletion.Result.Deleted, await SubmissionDeletion.DeleteAsync(db, game.Id, repeat.Id));
        var final = await repository.GenScoreboard(game);
        Assert.Equal(0, final.Items[a.Id].Score);
        Assert.Equal(0, final.Items[a.Id].SolvedCount);
        Assert.True(final.Items[b.Id].Score >= after.Items[b.Id].Score);
        Assert.Single(await db.FirstSolves.ToArrayAsync());
    }

    [InvitationDatabaseFact]
    public async Task MigrationAddsNullableTombstone()
    {
        await using var fixture = await TeamInvitationDatabaseTests.Fixture.Create();
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Submissions\" DROP COLUMN \"DeletedAtUtc\"");
        var commands = db.GetService<IMigrationsSqlGenerator>().Generate(new GZCTF.Migrations.SubmissionDeletion().UpOperations, db.Model);
        foreach (var command in commands) await db.Database.ExecuteSqlRawAsync(command.CommandText);
        Assert.Empty(await db.Submissions.ToArrayAsync());
    }
}
