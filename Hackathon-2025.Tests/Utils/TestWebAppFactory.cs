using Hackathon_2025.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using OpenAI;
using Stripe;

namespace Hackathon_2025.Tests.Utils;

internal class TestWebAppFactory : WebApplicationFactory<Program>
{
    public Mock<IEmailService> EmailMock { get; } = new();
    public Mock<ITurnstileService> TurnstileMock { get; } = new();

    public TestWebAppFactory()
    {
        TurnstileMock
            .Setup(x => x.VerifyAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TurnstileVerificationResult.Passed());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test-secret-32-bytes-minimum-1234567890",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Email:SmtpHost"] = "localhost",
                ["Email:SmtpPort"] = "2525",
                ["Email:From"] = "noreply@test.local",
                ["Email:UseSsl"] = "false",
                ["Billing:Provider"] = "stripe",
                ["Stripe:SecretKey"] = "sk_test_dummy",
                ["OpenAI:ApiKey"] = "test-openai-key",
                ["App:AllowedCorsOrigins"] = "http://localhost:5173",
                ["Turnstile:Enabled"] = "true",
                ["Turnstile:SiteKey"] = "test-site-key",
                ["Turnstile:SecretKey"] = "test-secret-key"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(EmailMock.Object);

            services.RemoveAll<StripeClient>();
            services.AddSingleton(new StripeClient("sk_test_dummy"));

            services.RemoveAll<OpenAIClient>();
            services.AddSingleton(new OpenAIClient("test-openai-key"));

            services.RemoveAll<ITurnstileService>();
            services.AddSingleton<ITurnstileService>(TurnstileMock.Object);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.Scheme;
                options.DefaultChallengeScheme = TestAuthHandler.Scheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.Scheme, _ => { });
        });
    }
}
