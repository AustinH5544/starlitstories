namespace Hackathon_2025.Models;

/// <summary>Tracks Stripe event IDs that we've already processed.</summary>
public sealed class ProcessedWebhook
{
    // PK
    public required string EventId { get; init; }
    public DateTime ProcessedAtUtc { get; init; } = DateTime.UtcNow;
}