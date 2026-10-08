namespace Hackathon_2025.Tests.Utils;

internal static class WebhookEvents
{
    public static (string eventId, int? userId, string? customerRef, string? subscriptionRef,
                   string? planKey, string? status, DateTime? periodEndUtc,
                   DateTime? periodStartUtc, DateTime? cancelAtUtc,
                   string? addOnSku, int addOnQty)
        Make(string eventId,
             int? userId = null,
             string? customerRef = null,
             string? subscriptionRef = null,
             string? planKey = null,
             string? status = null,
             DateTime? periodEndUtc = null,
             DateTime? periodStartUtc = null,
             DateTime? cancelAtUtc = null,
             string? addOnSku = null,
             int addOnQty = 0)
        => (eventId, userId, customerRef, subscriptionRef, planKey, status, periodEndUtc, periodStartUtc, cancelAtUtc, addOnSku, addOnQty);
}
