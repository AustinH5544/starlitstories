using System.ComponentModel.DataAnnotations;

namespace Hackathon_2025.Options;

public class StripeOptions
{
    [Required] public string SecretKey { get; init; } = "";
    public string PublishableKey { get; init; } = "";
    [Required] public string WebhookSecret { get; init; } = "";
    [Required] public string PriceIdPro { get; init; } = "";
    [Required] public string PriceIdPremium { get; init; } = "";
    [Required] public string PriceIdAddon5 { get; init; } = "";
    [Required] public string PriceIdAddon11 { get; init; } = "";
}