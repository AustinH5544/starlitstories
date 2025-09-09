namespace Hackathon_2025.Models;

public class StripeSettings
{
    public string SecretKey { get; set; }
    public string PublishableKey { get; set; }
    public string WebhookSecret { get; set; }

    public string PriceIdPro { get; set; }
    public string PriceIdPremium { get; set; }
    public string PriceIdAddon5 { get; set; }
    public string PriceIdAddon11 { get; set; }
}