using GZCTF.Models.Internal;

namespace GZCTF.Services.Container.Manager;

internal static class ChallengeRoute
{
    internal static string NormalizeBaseDomain(string value) =>
        value.Trim().Trim('.').ToLowerInvariant();

    internal static string GetHost(ContainerConfig config, string baseDomain)
    {
        var suffix = $"-c{config.ChallengeId}-t{config.TeamId}";
        var slug = Slugify(config.ChallengeSlug);
        var maxSlugLength = Math.Max(1, 63 - suffix.Length);
        if (slug.Length > maxSlugLength)
            slug = slug[..maxSlugLength].TrimEnd('-');
        return $"{slug}{suffix}.{baseDomain}";
    }

    internal static string Slugify(string value)
    {
        var chars = new List<char>(value.Length);
        var previousDash = false;
        foreach (var ch in value.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(ch))
            {
                chars.Add(ch);
                previousDash = false;
            }
            else if (!previousDash && chars.Count > 0)
            {
                chars.Add('-');
                previousDash = true;
            }
        }

        var result = new string(chars.ToArray()).Trim('-');
        return string.IsNullOrEmpty(result) ? "challenge" : result[..Math.Min(result.Length, 40)];
    }
}
