namespace Hackathon_2025.Models;

public sealed record BuyCreditsRequest
{
    public required CreditPack Pack { get; init; }
    [System.ComponentModel.DataAnnotations.Range(1, 100)]
    public int Quantity { get; init; } = 1;
}