using System.ComponentModel.DataAnnotations;

namespace GZCTF.Models.Request.Admin;

public class AppearanceModel : IValidatableObject
{
    [MaxLength(80)] public string Title { get; set; } = "";
    [MaxLength(200)] public string Slogan { get; set; } = "";
    [RegularExpression("^$|^#[0-9a-fA-F]{6}$")] public string PrimaryColor { get; set; } = "";
    [MaxLength(2048)] public string LogoUrl { get; set; } = "";
    [MaxLength(2048)] public string FaviconUrl { get; set; } = "";
    [MaxLength(20000)] public string HomeMarkdown { get; set; } = "";
    [MaxLength(3000)] public string BannerMarkdown { get; set; } = "";
    [MaxLength(3000)] public string FooterMarkdown { get; set; } = "";
    [MaxLength(20000)] public string CustomCss { get; set; } = "";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var (value, name) in new[] { (LogoUrl, nameof(LogoUrl)), (FaviconUrl, nameof(FaviconUrl)) })
        {
            if (string.IsNullOrEmpty(value)) continue;
            var local = value.StartsWith('/') && !value.StartsWith("//") && !value.Contains('\\') &&
                        !value.Any(char.IsControl);
            var remote = Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
                         string.IsNullOrEmpty(uri.UserInfo);
            if (!local && !remote)
                yield return new ValidationResult("Use an HTTPS image URL or a local path such as /assets/…", [name]);
        }
    }
}
