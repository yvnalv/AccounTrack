using System.ComponentModel.DataAnnotations;

namespace Accountrack.Identity.Infrastructure.Authentication;

/// <summary>JWT settings bound from the "Jwt" configuration section (DEPLOYMENT.md §6).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "Accountrack";

    [Required]
    public string Audience { get; set; } = "Accountrack";

    /// <summary>HMAC signing key. Must come from a secret store in real environments (SECURITY.md §6).</summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; set; } = default!;

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 14;
}
