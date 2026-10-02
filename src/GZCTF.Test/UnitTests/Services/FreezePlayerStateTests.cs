using System;
using GZCTF.Models.Data;
using GZCTF.Models.Request.Game;
using GZCTF.Utils;
using Xunit;

namespace GZCTF.Test.UnitTests.Services;

public class FreezePlayerStateTests
{
    [Theory]
    [InlineData(NoticeType.FirstBlood)]
    [InlineData(NoticeType.SecondBlood)]
    [InlineData(NoticeType.ThirdBlood)]
    public void BloodAnonymizationDoesNotMutateStoredNotice(NoticeType type)
    {
        var notice = new GameNotice { Id = 4, GameId = 2, Type = type, Values = ["Secret team", "Challenge"] };
        var safe = notice.AnonymizeBlood();
        Assert.Equal("Anonymous team", safe.Values![0]);
        Assert.Equal("Challenge", safe.Values[1]);
        Assert.Equal("Secret team", notice.Values![0]);
        Assert.Equal(notice.Id, safe.Id);
        Assert.Equal(notice.PublishTimeUtc, safe.PublishTimeUtc);
    }

    [Fact]
    public void NormalNoticesKeepTheirContent()
    {
        var notice = new GameNotice { Type = NoticeType.Normal, Values = ["Important announcement"] };
        Assert.Equal(notice.Values, notice.AnonymizeBlood().Values);
    }

    [Fact]
    public void OwnLiveSolvesDoNotMutateFrozenBoardOrExposeLiveRank()
    {
        var frozen = new ScoreboardItem { Id = 1, Score = 100, Rank = 2, SolvedChallenges = [] };
        var own = frozen.WithOwnSolves([new ChallengeItem { Id = 12, Type = SubmissionType.Normal }]);
        Assert.Empty(frozen.SolvedChallenges);
        Assert.Single(own.SolvedChallenges);
        Assert.Equal(frozen.Score, own.Score);
        Assert.Equal(frozen.Rank, own.Rank);
        Assert.Equal(SubmissionType.Normal, own.SolvedChallenges[0].Type);
    }

    [Theory]
    [InlineData(0, 500)]
    [InlineData(1, 500)]
    [InlineData(5, 496)]
    [InlineData(10, 484)]
    [InlineData(25, 400)]
    [InlineData(50, 100)]
    [InlineData(100, 100)]
    public void CtfdCurveMatchesDocumentedParabola(int count, int expected) =>
        Assert.Equal(expected, GameChallenge.CalculateChallengeScore(500, .2, 50, count, ScoreCurve.CTFd));

    [Fact]
    public void NewChallengesUseSlowCtfdDefaultsAndLegacyCurveStaysAvailable()
    {
        var challenge = new GameChallenge();
        Assert.Equal(ScoreCurve.CTFd, challenge.ScoreCurve);
        Assert.Equal(50, challenge.Difficulty);
        Assert.Equal(500, challenge.OriginalScore);
        Assert.Equal(.2, challenge.MinScoreRate);
        Assert.Equal((int)Math.Floor(500 * (.2 + .8 * Math.Exp(-1.0 / 5))),
            GameChallenge.CalculateChallengeScore(500, .2, 5, 2, ScoreCurve.Standard));
    }
}
