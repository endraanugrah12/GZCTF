namespace GZCTF.Models.Response.Game;

/// <summary>Player-facing status of the evidence needed for the next flag submission.</summary>
public class SubmissionEvidenceStatusModel
{
    public bool Required { get; set; }
    public bool Ready { get; set; }
    public long MaxSolverFileSize { get; set; }
}

/// <summary>Admin-safe metadata for reviewing a solver evidence package.</summary>
public class SubmissionEvidenceReviewModel
{
    public int Id { get; set; }
    public int ChallengeId { get; set; }
    public string ChallengeTitle { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string[] LlmLinks { get; set; } = [];
    public string SolverFileName { get; set; } = string.Empty;
    public long SolverFileSize { get; set; }
    public DateTimeOffset UploadedAtUtc { get; set; }
    public int? SubmissionId { get; set; }
}
