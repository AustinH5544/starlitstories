using System.Net;
using Hackathon_2025.Tests.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class EndpointAuthorizationTests
{
    /// <summary>
    /// Every endpoint reachable without login. Adding a public endpoint is a deliberate decision:
    /// add it here in the same commit. Format: "{method} {route}" lowercase ("*" = any method).
    /// </summary>
    private static readonly string[] AllowedPublic =
    {
        "post /api/auth/signup",
        "post /api/auth/verify-email",
        "post /api/auth/resend-verification",
        "post /api/auth/login",
        "post /api/auth/forgot-password",
        "post /api/auth/reset-password",
        "get /api/config",
        "post /api/feedback",
        "post /api/payments/webhook",
        "get /api/share/{token}",
        "get /api/story/progress/{jobid}",
        "get /api/story/ping",
        "get /__ping",
        "* /healthz",
        "options /api/{**catchall}",
        "get /readyz",
        "get /api/healthz",
        "post /api/warmup",
        "get /sitemap.xml",
        "get /sitemaps/sitemap-{index}.xml",
    };

    private static IEnumerable<string> Keys(RouteEndpoint endpoint)
    {
        var path = "/" + (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/');
        var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
        if (methods is null || methods.Count == 0)
            return new[] { $"* {path}".ToLowerInvariant() };
        return methods.Select(m => $"{m} {path}".ToLowerInvariant());
    }

    [TestMethod]
    public void Every_Endpoint_Requires_Login_Unless_It_Is_Explicitly_Public()
    {
        using var factory = new TestWebAppFactory();
        _ = factory.CreateClient(); // start the app so endpoints are built

        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();
        Assert.IsTrue(endpoints.Count > 30, $"Endpoint discovery looks broken: found {endpoints.Count}.");

        var publicEndpoints = endpoints
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null || !e.Metadata.GetOrderedMetadata<IAuthorizeData>().Any())
            .SelectMany(Keys)
            .Distinct()
            .ToList();

        var unexpected = publicEndpoints.Except(AllowedPublic).OrderBy(x => x).ToList();
        Assert.AreEqual(0, unexpected.Count,
            "Endpoints reachable WITHOUT login that are not on the public allowlist:\n" + string.Join("\n", unexpected));

        var stale = AllowedPublic.Except(publicEndpoints).ToList();
        Assert.AreEqual(0, stale.Count,
            "Allowlist entries that no longer match a public endpoint:\n" + string.Join("\n", stale));
    }

    [DataTestMethod]
    [DataRow("GET", "/api/profile/me")]
    [DataRow("GET", "/api/users/me/usage")]
    [DataRow("POST", "/api/story/generate-full")]
    [DataRow("POST", "/api/story/generate-full/start")]
    [DataRow("GET", "/api/saved-character/me")]
    [DataRow("GET", "/api/admin/dashboard")]
    [DataRow("POST", "/api/payments/create-checkout-session")]
    [DataRow("POST", "/api/stories/1/share")]
    public async Task Protected_Endpoints_Return_401_Without_Login(string method, string path)
    {
        using var factory = new TestWebAppFactory();

        var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = method == "POST" ? new StringContent("{}", System.Text.Encoding.UTF8, "application/json") : null
        };
        var resp = await factory.AnonymousClient().SendAsync(request);

        Assert.AreEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
