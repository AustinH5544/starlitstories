using System;
using Microsoft.AspNetCore.Http;

namespace Hackathon_2025.Services;

// Simple return types used by the gateway
public record CheckoutSession(string Url);
public record PortalSession(string Url);

public interface IPaymentGateway
{
    Task<CheckoutSession> CreateCheckoutSessionAsync(
        int userId, string userEmail, string planKey, string successUrl, string cancelUrl);

    Task<PortalSession> CreatePortalSessionAsync(int userId);

    Task CancelAtPeriodEndAsync(int userId);

    // One-time checkout for add-on packs
    Task<CheckoutSession> CreateOneTimeCheckoutAsync(
        int userId, string email, string priceId, int quantity, string successUrl, string cancelUrl);

    // NOTE: eventId FIRST so the controller can do webhook idempotency
    Task<(string eventId, int? userId, string? customerRef, string? subscriptionRef,
          string? planKey, string? status, DateTime? periodEndUtc,
          DateTime? periodStartUtc, DateTime? cancelAtUtc,
          string? addOnSku, int addOnQty)>
    HandleWebhookAsync(HttpRequest request);
}