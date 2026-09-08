using System.ComponentModel.DataAnnotations;

namespace GZCTF.Models.Request.Edit;

/// <summary>Destructive, game-scoped cleanup selected by an organizer.</summary>
public class GameActivityResetModel
{
    /// <summary>Delete all game announcements, including automatic blood notices.</summary>
    public bool ResetNotifications { get; set; }

    /// <summary>
    /// Remove the solve facts used by the scoreboard. Submission history is retained
    /// for auditing, but no accepted submission remains a solve after this operation.
    /// </summary>
    public bool ResetSolves { get; set; }

    /// <summary>Must be exactly <c>RESET</c>.</summary>
    [Required]
    public string Confirmation { get; set; } = string.Empty;
}
