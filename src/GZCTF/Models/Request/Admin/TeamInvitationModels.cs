using System.ComponentModel.DataAnnotations;
using GZCTF.Models.Request.Account;

namespace GZCTF.Models.Request.Admin;

public class TeamInvitationRow
{
    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; set; } = "";
    [Required, MaxLength(Limits.MaxTeamNameLength)]
    public string TeamName { get; set; } = "";
}

public class TeamInvitationImport
{
    [Required, MinLength(1), MaxLength(500)]
    public List<TeamInvitationRow> Rows { get; set; } = [];
}

public class TeamInvitationSettings
{
    [Range(1, 3650)]
    public int LifetimeDays { get; set; } = 30;
}

public class TeamInvitationTokenModel
{
    [Required, RegularExpression("^[A-Za-z0-9_-]{43}$")]
    public string Token { get; set; } = "";
}

public class TeamInvitationRegisterModel : RegisterModel
{
    [Required, RegularExpression("^[A-Za-z0-9_-]{43}$")]
    public string Token { get; set; } = "";
}
