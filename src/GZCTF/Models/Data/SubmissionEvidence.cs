using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace GZCTF.Models.Data;

/// <summary>
/// A one-time, immutable evidence package supplied before a player submits a
/// flag. It is deliberately separate from the submission so an upload can be
/// validated before the flag endpoint is called.
/// </summary>
[Index(nameof(GameId), nameof(ChallengeId), nameof(UserId), nameof(SubmissionId))]
public class SubmissionEvidence
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int GameId { get; set; }

    [Required]
    public int ChallengeId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public int ParticipationId { get; set; }

    /// <summary>Validated, newline-separated LLM share URLs.</summary>
    [Required]
    public string LlmLinks { get; set; } = string.Empty;

    [Required]
    public int SolverFileId { get; set; }

    [Required]
    public DateTimeOffset UploadedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Null until this evidence package has been consumed by a submit.</summary>
    public int? SubmissionId { get; set; }

    public LocalFile SolverFile { get; set; } = null!;
    public Submission? Submission { get; set; }
    public Game Game { get; set; } = null!;
    public GameChallenge Challenge { get; set; } = null!;
    public UserInfo User { get; set; } = null!;
    public Participation Participation { get; set; } = null!;
}
