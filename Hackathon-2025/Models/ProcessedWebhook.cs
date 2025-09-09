namespace Hackathon_2025.Models
{
    /// <summary>Tracks Stripe event IDs that we've already processed.</summary>
    public class ProcessedWebhook
    {
        // Primary key (unique); Stripe's Event.Id
        public string EventId { get; set; } = default!;

        public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;
    }
}