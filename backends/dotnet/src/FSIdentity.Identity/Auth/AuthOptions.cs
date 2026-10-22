using System.ComponentModel.DataAnnotations;

namespace FSIdentity.Identity.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required, MinLength(32)]
    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = "FSIdentity";

    public string Audience { get; set; } = "FSIdentity";

    [Range(1, 24 * 60)]
    public int AccessMinutes { get; set; } = 15;

    [Range(1, 365)]
    public int RefreshDays { get; set; } = 14;
}

public class LoginOptions
{
    public const string SectionName = "Login";

    [Range(1, 100)]
    public int MaxFailedAttempts { get; set; } = 5;

    [Range(1, 24 * 60)]
    public int WindowMinutes { get; set; } = 15;

    [Range(1, 24 * 60)]
    public int LockoutMinutes { get; set; } = 15;
}

public class PasswordOptions
{
    public const string SectionName = "Passwords";

    [Range(4, 20)]
    public int WorkFactor { get; set; } = 11;

    [Range(6, 128)]
    public int MinLength { get; set; } = 8;
}
