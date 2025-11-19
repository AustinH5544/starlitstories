namespace Hackathon_2025.Models;

public sealed record StripeSettings
{
    public required string SecretKey { get; init; }
    public required string PublishableKey { get; init; }
    public required string WebhookSecret { get; init; }
    public required string PriceIdPro { get; init; }
    public required string PriceIdPremium { get; init; }
    public required string PriceIdAddon5 { get; init; }
    public required string PriceIdAddon11 { get; init; }
}