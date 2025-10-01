namespace Hackathon_2025.Options;

public class AppOptions
{
    public string BaseUrl { get; init; } = "";          // used for links in emails, etc.
    public string AllowedCorsOrigins { get; init; } = ""; // semicolon-separated; we'll add this key
}