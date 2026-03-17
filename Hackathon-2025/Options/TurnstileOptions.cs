namespace Hackathon_2025.Options;

public sealed class TurnstileOptions
{
    public bool Enabled { get; init; }

    public string SiteKey { get; init; } = string.Empty;

    public string SecretKey { get; init; } = string.Empty;
}
