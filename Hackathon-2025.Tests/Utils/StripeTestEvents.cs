using System.Security.Cryptography;
using System.Text;
using Stripe;

namespace Hackathon_2025.Tests.Utils;

internal static class StripeTestEvents
{
    /// <summary>Builds a Stripe-Signature header exactly as Stripe does: HMAC-SHA256 over "{timestamp}.{payload}".</summary>
    public static string Sign(string payload, string secret, DateTimeOffset? signedAt = null)
    {
        var timestamp = (signedAt ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}"))).ToLowerInvariant();
        return $"t={timestamp},v1={signature}";
    }

    public static string Event(string id, string type, string dataObjectJson) =>
        $$$"""
        {"id":"{{{id}}}","object":"event","api_version":"{{{StripeConfiguration.ApiVersion}}}","created":1767225600,"livemode":false,"pending_webhooks":1,"request":{"id":null,"idempotency_key":null},"type":"{{{type}}}","data":{"object":{{{dataObjectJson}}}}}
        """;
}
