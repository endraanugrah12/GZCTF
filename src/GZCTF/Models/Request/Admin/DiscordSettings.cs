using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace GZCTF.Models.Request.Admin;

public class DiscordSettings
{
    public bool Enabled { get; set; } = true;
    public bool Bloods { get; set; } = true;
    public bool Announcements { get; set; } = true;
    public bool Hints { get; set; } = true;
    public bool Challenges { get; set; } = true;
    public bool CheatAlerts { get; set; } = true;
    [MaxLength(80)] public string Username { get; set; } = "GZCTF";
    [MaxLength(2048), RegularExpression("^$|^https://[^\\s]+$")]
    public string AvatarUrl { get; set; } = "";
    [MaxLength(80)] public string AnonymousTeam { get; set; } = "Anonymous team";
    [MaxLength(256)] public string FirstBloodTitle { get; set; } = "First Blood! 🥇";
    [MaxLength(256)] public string SecondBloodTitle { get; set; } = "Second Blood! 🥈";
    [MaxLength(256)] public string ThirdBloodTitle { get; set; } = "Third Blood! 🥉";
    [MaxLength(3000)] public string BloodMessage { get; set; } = "**{team}** solved **{challenge}**";
    [MaxLength(3000)] public string AnnouncementMessage { get; set; } = "{message}";
    [MaxLength(3000)] public string HintMessage { get; set; } = "New hint for **{challenge}**";
    [MaxLength(3000)] public string ChallengeMessage { get; set; } = "New challenge: **{challenge}**";
    [MaxLength(3000)] public string CheatMessage { get; set; } = "Cheat detected for **{team}**. {details}";
    [MaxLength(500)] public string Footer { get; set; } = "{game}";
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal static string Key(int id) => $"Discord:Game:{id}";
    internal static async Task<DiscordSettings> Read(AppDbContext db, int id, CancellationToken token = default)
    {
        var json = await db.Configs.Where(c => c.ConfigKey == Key(id)).Select(c => c.Value).SingleOrDefaultAsync(token);
        return string.IsNullOrEmpty(json) ? new() : JsonSerializer.Deserialize<DiscordSettings>(json, Json) ?? new();
    }
}
