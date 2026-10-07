using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hackathon_2025.Tests.Utils;

internal class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string Scheme = "TestAuth";
    public const string UserIdHeader = "X-Test-UserId";
    public const string EmailHeader = "X-Test-Email";
    public const string AnonymousHeader = "X-Test-Anonymous";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock) : base(options, logger, encoder, clock) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.TryGetValue(AnonymousHeader, out var anonymous) &&
            string.Equals(anonymous.FirstOrDefault(), "true", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Default stays user 1 so existing tests are unaffected.
        var userId = 1;
        if (Request.Headers.TryGetValue(UserIdHeader, out var values) &&
            int.TryParse(values.FirstOrDefault(), out var parsed))
        {
            userId = parsed;
        }

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        var email = Request.Headers.TryGetValue(EmailHeader, out var emailValues) ? emailValues.FirstOrDefault() : null;
        if (!string.IsNullOrWhiteSpace(email))
            claims.Add(new Claim(ClaimTypes.Email, email));

        var identity = new ClaimsIdentity(claims, Scheme);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme)));
    }
}
