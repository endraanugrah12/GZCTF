using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using GZCTF.Models.Data;
using Microsoft.Extensions.Logging;
using GZCTF.Models.Request.Admin;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace GZCTF.Services.Webhook;

public class Models
{
    public class DiscordWebhookMessage
    {
        public string? Content { get; set; }
        public string? Username { get; set; }
        [JsonPropertyName("avatar_url")] public string? AvatarUrl { get; set; }
        [JsonPropertyName("allowed_mentions")] public object AllowedMentions { get; set; } = new { parse = Array.Empty<string>() };
        public List<DiscordEmbed>? Embeds { get; set; }
    }

    public class DiscordEmbed
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public int? Color { get; set; }
        public List<DiscordEmbedField>? Fields { get; set; }
        public DiscordEmbedFooter? Footer { get; set; }
        public string? Timestamp { get; set; }
    }

    public class DiscordEmbedField
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public bool? Inline { get; set; }
    }

    public class DiscordEmbedFooter
    {
        public string Text { get; set; } = string.Empty;
    }
}

public class SendWebhookService(ILogger<SendWebhookService> logger, IServiceScopeFactory scopeFactory) : ISendWebhookService
{
    private const int ContentLimit = 2000;
    private const int EmbedTitleLimit = 256;
    private const int EmbedDescriptionLimit = 4096;
    private const int EmbedFooterLimit = 2048;
    private const int EmbedFieldNameLimit = 256;
    private const int EmbedFieldValueLimit = 1024;
    private const int EmbedFieldCountLimit = 25;
    private const int EmbedTotalCharLimit = 6000;

    // The webhook URL is operator-set, but a lower-trust per-game EventManager can set
    // it too — so the outbound POST must not become an SSRF into internal services or
    // cloud metadata. We resolve the target ourselves and refuse to connect to any
    // non-public-unicast address; validating at CONNECT time (not just up front) also
    // defeats DNS rebinding, since we connect to the exact address we vetted. Redirects
    // are disabled so a benign public host can't 30x us into internal space.
    private static readonly HttpClient WebhookClient = CreateClient();

    private static HttpClient CreateClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            ConnectCallback = async (ctx, ct) =>
            {
                var host = ctx.DnsEndPoint.Host;
                var addrs = IPAddress.TryParse(host, out var literal)
                    ? [literal]
                    : await Dns.GetHostAddressesAsync(host, ct);
                var target = Array.Find(addrs, a => !IsBlockedAddress(a))
                    ?? throw new IOException($"Webhook host '{host}' does not resolve to a public address");
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(target, ctx.DnsEndPoint.Port, ct);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
    }

    /// <summary>
    /// True for any address a webhook must NOT reach: loopback, link-local (incl. the
    /// 169.254.169.254 cloud-metadata endpoint), RFC1918 / CGNAT / ULA private ranges,
    /// multicast/reserved, and the unspecified address. Only public unicast passes.
    /// </summary>
    internal static bool IsBlockedAddress(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any))
            return true;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            if (b[0] is 0 or 10 or 127) return true;                 // this-network, RFC1918 10/8, loopback
            if (b[0] == 169 && b[1] == 254) return true;             // link-local incl. cloud metadata
            if (b[0] == 172 && b[1] is >= 16 and <= 31) return true; // 172.16/12
            if (b[0] == 192 && b[1] == 168) return true;             // 192.168/16
            if (b[0] == 100 && b[1] is >= 64 and <= 127) return true;// 100.64/10 CGNAT
            return b[0] >= 224;                                       // 224/4 multicast + 240/4 reserved
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // Normalize embedded-IPv4 IPv6 forms (6to4 / NAT64 / IPv4-compatible) to their
            // IPv4 and re-classify, so an internal target can't be reached via those wrappers.
            var embedded = ExtractEmbeddedIPv4(ip);
            if (embedded is not null) return IsBlockedAddress(embedded);

            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast) return true;
            return (ip.GetAddressBytes()[0] & 0xFE) == 0xFC;         // fc00::/7 unique-local
        }

        return true; // unknown address family — refuse
    }

    /// <summary>Extract the embedded IPv4 from 6to4 (2002::/16), NAT64 (64:ff9b::/96), or
    /// IPv4-compatible (::a.b.c.d) IPv6 addresses; null if not one of those forms.</summary>
    private static IPAddress? ExtractEmbeddedIPv4(IPAddress ip)
    {
        var b = ip.GetAddressBytes(); // 16 bytes
        if (b[0] == 0x20 && b[1] == 0x02)                              // 6to4 2002:AABB:CCDD::
            return new IPAddress(new[] { b[2], b[3], b[4], b[5] });
        if (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xff && b[3] == 0x9b // NAT64 64:ff9b::/96
            && b[4] == 0 && b[5] == 0 && b[6] == 0 && b[7] == 0
            && b[8] == 0 && b[9] == 0 && b[10] == 0 && b[11] == 0)
            return new IPAddress(new[] { b[12], b[13], b[14], b[15] });
        var hiZero = true;
        for (var i = 0; i < 12 && hiZero; i++) hiZero = b[i] == 0;
        if (hiZero && b[12] != 0)                                       // IPv4-compatible ::a.b.c.d
            return new IPAddress(new[] { b[12], b[13], b[14], b[15] });
        return null;
    }

    public async Task SendGameEventAsync(GameEvent gameEvent, string webhookUrl)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var game = await db.Games.AsNoTracking().SingleOrDefaultAsync(g => g.Id == gameEvent.GameId);
            if (game is null || DateTimeOffset.UtcNow < game.StartTimeUtc || gameEvent.PublishTimeUtc < game.StartTimeUtc) return;
            var options = await DiscordSettings.Read(db, game.Id);
            var message = CreateEventMessage(gameEvent, game, options, DateTimeOffset.UtcNow);
            if (message == null) return;

            await SendAsync(webhookUrl, message, "event");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending webhook");
        }
    }

    public async Task SendNoticeAsync(GameNotice notice, string webhookUrl)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var game = await db.Games.AsNoTracking().SingleOrDefaultAsync(g => g.Id == notice.GameId);
            if (game is null) return;
            // Deliberate announcements may be sent before an event, unlike automatic test activity.
            if (notice.Type != NoticeType.Normal &&
                (DateTimeOffset.UtcNow < game.StartTimeUtc || notice.PublishTimeUtc < game.StartTimeUtc)) return;
            var options = await DiscordSettings.Read(db, game.Id);
            var message = CreateNoticeMessage(notice, game, options, DateTimeOffset.UtcNow);
            if (message == null) return;

            await SendAsync(webhookUrl, message, "notice");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending webhook notice");
        }
    }

    private async Task SendAsync(string webhookUrl, Models.DiscordWebhookMessage message, string kind)
    {
        if (!Uri.TryCreate(webhookUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            logger.LogWarning("Skip invalid Discord webhook URL for {Kind}", kind);
            return;
        }

        SanitizeMessage(message);

        using var content = new StringContent(
            JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        using var response = await WebhookClient.PostAsync(uri, content);

        if (response.IsSuccessStatusCode)
            return;

        // Deliberately NOT logging the response body: with the SSRF guard the target is
        // a public host, but echoing an arbitrary remote response into operator logs is
        // an unnecessary read primitive. Status + reason are enough to diagnose.
        logger.LogError(
            "Failed to send webhook {Kind}: {StatusCode} {Reason}",
            kind,
            (int)response.StatusCode,
            response.ReasonPhrase ?? "Unknown");
    }

    private static string Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text ?? string.Empty;

        var cut = text[..maxLength];
        // Don't slice in the middle of a backslash-escape pair (from EscapeMd) — a dangling
        // trailing backslash would render literally. Drop an orphaned (odd) trailing run.
        var bs = 0;
        for (var i = cut.Length - 1; i >= 0 && cut[i] == '\\'; i--) bs++;
        if ((bs & 1) == 1) cut = cut[..^1];
        return cut;
    }

    /// <summary>
    /// Backslash-escape Discord markdown control chars so a player-chosen team name (or a
    /// challenge title set by a lower-trust per-game EventManager) can't inject bold/links/
    /// formatting into the staff channel when interpolated into an embed.
    /// </summary>
    private static string EscapeMd(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new System.Text.StringBuilder(s.Length + 8);
        foreach (var c in s)
        {
            if (c is '\\' or '*' or '_' or '~' or '`' or '|' or '[' or ']' or '(' or ')' or '>' or '#')
                sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static void SanitizeMessage(Models.DiscordWebhookMessage message)
    {
        message.Content = Truncate(message.Content, ContentLimit);

        if (message.Embeds is not { Count: > 0 })
            return;

        foreach (var embed in message.Embeds)
        {
            embed.Title = Truncate(embed.Title, EmbedTitleLimit);
            embed.Description = Truncate(embed.Description, EmbedDescriptionLimit);

            if (embed.Footer is not null)
                embed.Footer.Text = Truncate(embed.Footer.Text, EmbedFooterLimit);

            if (embed.Fields is { Count: > 0 })
            {
                if (embed.Fields.Count > EmbedFieldCountLimit)
                    embed.Fields = embed.Fields.Take(EmbedFieldCountLimit).ToList();

                foreach (var field in embed.Fields)
                {
                    field.Name = Truncate(field.Name, EmbedFieldNameLimit);
                    field.Value = Truncate(field.Value, EmbedFieldValueLimit);
                }
            }

            var totalChars = (embed.Title?.Length ?? 0) +
                             (embed.Description?.Length ?? 0) +
                             (embed.Footer?.Text.Length ?? 0) +
                             (embed.Fields?.Sum(f => f.Name.Length + f.Value.Length) ?? 0);

            if (totalChars > EmbedTotalCharLimit && !string.IsNullOrEmpty(embed.Description))
            {
                var overflow = totalChars - EmbedTotalCharLimit;
                embed.Description = Truncate(embed.Description, Math.Max(0, embed.Description.Length - overflow));
            }
        }
    }

    internal static bool IsFrozen(Game game, DateTimeOffset published, DateTimeOffset now)
        => game.FreezeTimeUtc is { } freeze && (now >= freeze || published >= freeze);

    internal static Models.DiscordWebhookMessage? CreateEventMessage(GameEvent gameEvent, Game game,
        DiscordSettings options, DateTimeOffset now)
    {
        if (!options.Enabled || !options.CheatAlerts || string.IsNullOrEmpty(options.CheatMessage) ||
            gameEvent.Type != EventType.CheatDetected) return null;
        var frozen = IsFrozen(game, gameEvent.PublishTimeUtc, now);
        var team = frozen ? options.AnonymousTeam : gameEvent.Team?.Name ?? "Unknown team";
        var details = frozen ? "Details withheld during scoreboard freeze." : string.Join(", ", gameEvent.Values ?? []);
        var message = new Models.DiscordWebhookMessage { Embeds = [new Models.DiscordEmbed
        {
            Title = "Cheat Detected! 🚨", Color = 0xFF0000,
            Timestamp = gameEvent.PublishTimeUtc.ToString("o"),
            Description = Render(options.CheatMessage, team, "", game.Title, "", details)
        }] };
        ApplyBranding(message, options, game.Title);
        SanitizeMessage(message);
        return message;
    }

    // Substitute once: tokens embedded in player-controlled values are never expanded.
    internal static string Render(string template, string team, string challenge, string game, string message, string details = "")
        => Regex.Replace(template, @"\{(team|challenge|game|message|details)\}", match => EscapeMd(match.Groups[1].Value switch
        {
            "team" => team, "challenge" => challenge, "game" => game,
            "message" => message, "details" => details, _ => ""
        }));

    private static void ApplyBranding(Models.DiscordWebhookMessage message, DiscordSettings options, string game)
    {
        message.Username = options.Username;
        message.AvatarUrl = string.IsNullOrWhiteSpace(options.AvatarUrl) ? null : options.AvatarUrl;
        foreach (var embed in message.Embeds ?? [])
            embed.Footer = new Models.DiscordEmbedFooter { Text = Render(options.Footer, "", "", game, "") };
    }

    internal static Models.DiscordWebhookMessage? CreateNoticeMessage(GameNotice notice, Game game, DiscordSettings options, DateTimeOffset now)
    {
        if (!options.Enabled) return null;
        var blood = notice.Type is NoticeType.FirstBlood or NoticeType.SecondBlood or NoticeType.ThirdBlood;
        if (blood && !options.Bloods || notice.Type == NoticeType.Normal && !options.Announcements ||
            notice.Type == NoticeType.NewHint && !options.Hints || notice.Type == NoticeType.NewChallenge && !options.Challenges)
            return null;
        var team = blood ? notice.Values?.ElementAtOrDefault(0) ?? "Unknown team" : "";
        if (blood && IsFrozen(game, notice.PublishTimeUtc, now)) team = options.AnonymousTeam;
        var challenge = notice.Values?.ElementAtOrDefault(blood ? 1 : 0) ?? "";
        var title = notice.Type switch
        {
            NoticeType.FirstBlood => options.FirstBloodTitle,
            NoticeType.SecondBlood => options.SecondBloodTitle,
            NoticeType.ThirdBlood => options.ThirdBloodTitle,
            _ => notice.Type.ToString()
        };
        var template = notice.Type switch
        {
            NoticeType.FirstBlood or NoticeType.SecondBlood or NoticeType.ThirdBlood => options.BloodMessage,
            NoticeType.Normal => options.AnnouncementMessage,
            NoticeType.NewHint => options.HintMessage,
            NoticeType.NewChallenge => options.ChallengeMessage,
            _ => ""
        };
        if (string.IsNullOrEmpty(template)) return null;
        var embed = new Models.DiscordEmbed
        {
            Title = Render(title, team, challenge, game.Title, ""),
            Description = Render(template, team, challenge, game.Title, blood ? "" : notice.Values?.FirstOrDefault() ?? ""),
            Timestamp = notice.PublishTimeUtc.ToString("o"),
            Color = notice.Type switch
            {
                NoticeType.FirstBlood => 0xFFD700, NoticeType.SecondBlood => 0xC0C0C0,
                NoticeType.ThirdBlood => 0xCD7F32, _ => 0x3498DB
            }
        };
        var message = new Models.DiscordWebhookMessage { Embeds = [embed] };
        ApplyBranding(message, options, game.Title);
        SanitizeMessage(message);
        return message;
    }
}
