namespace Hackathon_2025.Options;

public class EmailOptions
{
    public string SmtpHost { get; init; } = "";
    public int SmtpPort { get; init; }
    public string SmtpUsername { get; init; } = "";
    public string SmtpPassword { get; init; } = "";
    public string FromEmail { get; init; } = "";
    public string FromName { get; init; } = "Starlit Stories";
}