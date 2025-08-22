public record CheckoutSession(string Url);
public record PortalSession(string Url);

public interface IPaymentGateway
{
    Task<CheckoutSession> CreateCheckoutSessionAsync(
        int userId, string userEmail, string planKey, string successUrl, string cancelUrl);

    Task<PortalSession> CreatePortalSessionAsync(int userId);

    Task CancelAtPeriodEndAsync(int userId);

    // Normalized webhook payload for your DB update
    Task<(int? userId, string? customerRef, string? subscriptionRef,
          string? planKey, string status, DateTime? periodEndUtc)>
        HandleWebhookAsync(HttpRequest request);
}
