using Microsoft.EntityFrameworkCore;

namespace GZCTF.Services;

public static class SubmissionDeletion
{
    public enum Result { Deleted, NotFound, Pending }

    public static async Task<Result> DeleteAsync(AppDbContext db, int gameId, int submissionId,
        CancellationToken token = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var key = await db.Submissions.IgnoreAutoIncludes().AsNoTracking()
            .Where(s => s.GameId == gameId && s.Id == submissionId)
            .Select(s => new { s.ParticipationId, s.ChallengeId }).SingleOrDefaultAsync(token);
        if (key is null) return Result.NotFound;

        // Same lock order as flag verification: team/challenge, then challenge blood slots.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0}, {1})",
            [key.ParticipationId, key.ChallengeId], cancellationToken: token);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0}, {1})",
            [0, key.ChallengeId], cancellationToken: token);
        var submission = await db.Submissions.IgnoreAutoIncludes()
            .SingleOrDefaultAsync(s => s.GameId == gameId && s.Id == submissionId, token);
        if (submission is null) return Result.NotFound;
        // Never remove a queued/in-flight flag while the checker is judging it.
        if (submission.Status == AnswerResult.FlagSubmitted) return Result.Pending;

        var solve = await db.FirstSolves.SingleOrDefaultAsync(s => s.SubmissionId == submissionId, token);
        if (solve is not null)
        {
            // Do not resurrect submissions from before an explicit admin solve reset.
            var replacement = await db.Submissions.IgnoreAutoIncludes()
                .Where(s => s.GameId == gameId && s.ParticipationId == key.ParticipationId &&
                    s.ChallengeId == key.ChallengeId && s.Id != submissionId &&
                    s.Status == AnswerResult.Accepted && s.SubmitTimeUtc >= submission.SubmitTimeUtc)
                .OrderBy(s => s.SubmitTimeUtc).ThenBy(s => s.Id)
                .Select(s => (int?)s.Id).FirstOrDefaultAsync(token);
            if (replacement is { } next) solve.SubmissionId = next;
            else db.FirstSolves.Remove(solve);
        }
        submission.DeletedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Result.Deleted;
    }
}
