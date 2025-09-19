namespace Hackathon_2025.Options;

public class JwtOptions
{
    public string Key { get; init; } = "";      // keep your existing name
    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";
    public int ExpiresInMinutes { get; init; } = 60;
}