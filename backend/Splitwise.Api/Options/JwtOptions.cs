namespace Splitwise.Api.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    // Provided via user-secrets locally / environment variable in deployment — never committed.
    public string Key { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 60;
}
