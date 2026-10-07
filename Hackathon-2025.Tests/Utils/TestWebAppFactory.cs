using Hackathon_2025.Data;
using Hackathon_2025.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using OpenAI;
using Stripe;

namespace Hackathon_2025.Tests.Utils;

internal class TestWebAppFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@test.local";
    public const string StripeWebhookSecret = "whsec_test_secret";
    public const string PriceIdPro = "price_pro_test";
    public const string PriceIdPremium = "price_premium_test";
    public const string PriceIdAddon5 = "price_addon5_test";
    public const string PriceIdAddon11 = "price_addon11_test";

    private readonly IReadOnlyDictionary<string, string?> _configOverrides;

    public Mock<IEmailService> EmailMock { get; } = new();
    public Mock<ITurnstileService> TurnstileMock { get; } = new();
    public Mock<IPaymentGateway> PaymentGatewayMock { get; } = new();
    public FakeStoryGenerator StoryGenerator { get; } = new();
    public FakeBlobUploadService BlobUploads { get; } = new();

    /// <summary>Each factory gets its own in-memory database so parallel tests never see each other's data.</summary>
    public string InMemoryDatabaseName { get; } = $"tests-{Guid.NewGuid():N}";

    public TestWebAppFactory(IReadOnlyDictionary<string, string?>? configOverrides = null)
    {
        _configOverrides = configOverrides ?? new Dictionary<string, string?>();

        TurnstileMock
            .Setup(x => x.VerifyAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TurnstileVerificationResult.Passed());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            var settings = new Dictionary<string, string?>
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
                ["Stripe:WebhookSecret"] = StripeWebhookSecret,
                ["Stripe:PriceIdPro"] = PriceIdPro,
                ["Stripe:PriceIdPremium"] = PriceIdPremium,
                ["Stripe:PriceIdAddon5"] = PriceIdAddon5,
                ["Stripe:PriceIdAddon11"] = PriceIdAddon11,
                ["OpenAI:ApiKey"] = "test-openai-key",
                ["App:AllowedCorsOrigins"] = "http://localhost:5173",
                ["App:BaseUrl"] = "https://app.test",
                ["Admin:EmailsCsv"] = AdminEmail,
                ["Turnstile:Enabled"] = "true",
                ["Turnstile:SiteKey"] = "test-site-key",
                ["Turnstile:SecretKey"] = "test-secret-key"
            };
            foreach (var (key, value) in _configOverrides)
                settings[key] = value;

            cfg.AddInMemoryCollection(settings);
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

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            ConfigureDatabase(services);

            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton(PaymentGatewayMock.Object);

            services.RemoveAll<IStoryGeneratorService>();
            services.AddSingleton<IStoryGeneratorService>(StoryGenerator);

            services.RemoveAll<IBlobUploadService>();
            services.AddSingleton<IBlobUploadService>(BlobUploads);
        });
    }

    protected virtual void ConfigureDatabase(IServiceCollection services)
        => services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(InMemoryDatabaseName));
}
