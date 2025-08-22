namespace Hackathon_2025.Models;

public class StripeSettings
{
    public required string SecretKey { get; set; }
    public required string PublishableKey { get; set; }
    public required string WebhookSecret { get; set; }

    public required string PriceIdPro { get; set; }
    public required string PriceIdPremium { get; set; }
}
