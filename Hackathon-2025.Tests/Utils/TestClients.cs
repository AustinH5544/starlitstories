using Microsoft.AspNetCore.Mvc.Testing;

namespace Hackathon_2025.Tests.Utils;

internal static class TestClients
{
    /// <summary>A client authenticated as <paramref name="userId"/> (and optionally with an email claim).</summary>
    public static HttpClient ClientFor(this WebApplicationFactory<Program> factory, int userId, string? email = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        if (email is not null)
            client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, email);
        return client;
    }

    /// <summary>A client with no authenticated user.</summary>
    public static HttpClient AnonymousClient(this WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.AnonymousHeader, "true");
        return client;
    }
}
