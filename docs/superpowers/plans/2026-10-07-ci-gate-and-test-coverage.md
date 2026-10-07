# CI Test Gate + Production Test Coverage Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Block every deploy unless the API builds, ~240–250 risk-ranked backend test results pass (including real-SQL-Server payment tests), and the frontend lints clean.

**Architecture:** One MSTest project (`Hackathon-2025.Tests`) with a shared `TestWebAppFactory` that replaces every external dependency with a fake and gives each test its own in-memory database. A small group of `SqlServer`-category tests runs against a disposable SQL Server in Docker (Testcontainers) for the webhook idempotency SQL and migrations. CI runs build + tests (API) and lint (frontend) as gates before the existing deploy steps.

**Tech Stack:** .NET 8, ASP.NET Core, EF Core 8.0.11 (SqlServer + InMemory), MSTest 3.6.4, Moq 4.20.72, Microsoft.AspNetCore.Mvc.Testing 8.0.8, Testcontainers.MsSql (new, test project only), Stripe.net 48.1.0, React 19 + Vite 6 + ESLint 9, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-10-07-ci-gate-and-test-coverage-design.md`

## Global Constraints

- Branch: all work on `chore/tests-and-ci` (created from `staging`). Never commit on `main`, never push `main`, never force-push. A hook (`.claude/hooks/protect_main.py`) enforces this; if it blocks something, stop and ask.
- Production behavior must not change, except: (1) the `IBlobUploadService` interface refactor (Task 1), (2) `InternalsVisibleTo` for the test assembly (Task 10, build metadata only), (3) the frontend lint fixes (Task 11), (4) CI workflow changes (Task 12).
- Tests never call real Stripe, OpenAI, Azure Blob/Key Vault, or SMTP/ACS.
- **If a test exposes a real bug: do NOT change production code to make it pass.** Stop, report the bug with a proposed fix, and let the user decide. Tests that pin known open questions are named `..._OpenQuestion` and assert current behavior.
- The test assembly runs in parallel (`[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]` in `MSTestSettings.cs`). Every test creates its own factory (own in-memory DB, own SQL Server database). Never share mutable static state between tests.
- Per-factory rate limits (all requests share one "unknown" IP in TestServer): `signup-ip` 5 per 10 min, `login-ip` 10 per min, `feedback-ip` 3 per 10 min. Never exceed these inside one test unless testing the limit.
- The app has **no exception-handling middleware**: an unhandled server exception surfaces in tests as an exception from `HttpClient`, not a 500 response. Use `ServerErrors.AssertServerErrorAsync` for those cases.
- JSON: controllers use `JsonStringEnumConverter` (enums serialize as `"Pro"`, and accept `"Pro"` or numbers) and camelCase property names.
- `SqlServer` tests: locally they are **skipped (Inconclusive)** when Docker is not running; with env var `CI=true` they **fail** instead.
- Commit messages: short past-tense subject like recent history (`Added …`, `Fixed …`), body optional, and end with the trailer line `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Commands are run from the repo root unless a step says otherwise. Test command: `dotnet test Hackathon-2025.Tests/Hackathon-2025.Tests.csproj`.

## Review Focus

1. **Downgrade webhook** (`planKey "free"`, `status "canceled"`) for a Premium user holding purchased add-on credits: expected membership becomes Free and the purchased balance is untouched. Test: Task 5, `Downgrade_To_Free_Keeps_Purchased_AddOn_Balance`.
2. **Add-on checkout metadata with `qty` = "0"**: a reasonable user expects at least one pack credited, never zero. Test: Task 4, `Payment_Checkout_With_Zero_Quantity_Is_Treated_As_One`.
3. **Username with uppercase letters** (`"MILO"`): the rules are lowercase-only, so expect a clean 400 with the rules message (not a 500, not silently lowercased). Test: Task 9, `UpdateUsername_Uppercase_Is_Rejected`.
4. **Story request with zero characters**: expect 400 and **no credit consumed**. Test: Task 6, `GenerateFull_With_No_Characters_Is_Rejected_Without_Spending_Credit`.
5. **Revoking a share link twice**: the owner's second revoke returns 404 (already revoked), the admin's revoke is idempotent (204 twice). Tests: Task 8, `Revoke_Twice_Second_Time_Returns_NotFound`; Task 7, `Admin_RevokeShare_Is_Idempotent`.

---

## File Structure

**Production (modified/created):**
- Create `Hackathon-2025/Services/IBlobUploadService.cs`: interface over Blob uploads.
- Modify `Hackathon-2025/Services/BlobUploadService.cs`: implements the interface (no logic change).
- Modify `Hackathon-2025/Program.cs` (line ~192): register the interface.
- Modify `Hackathon-2025/Controllers/StoryController.cs`: depend on the interface (lines 23, 35, 206).
- Modify `Hackathon-2025/Services/StoryGenerator.cs`: depend on the interface (lines 15, 23).
- Modify `Hackathon-2025/Hackathon-2025.csproj`: `InternalsVisibleTo` (Task 10).
- Modify 12 frontend files under `Hackathon-2025/ClientApp/src/` (Task 11).
- Modify `.github/workflows/api.yml`, `.github/workflows/frontend.yml` (Task 12).

**Tests (`Hackathon-2025.Tests/`):**
- `Utils/TestWebAppFactory.cs` (modify): per-test DB, fakes, config overrides, `ConfigureDatabase` hook.
- `Utils/TestAuthHandler.cs` (modify): `X-Test-Email`, `X-Test-Anonymous`.
- `Utils/Fakes.cs`: `FakeStoryGenerator`, `FakeBlobUploadService`.
- `Utils/TestData.cs`: entity builders + `SeedAsync` / `QueryDbAsync`.
- `Utils/TestClients.cs`: `ClientFor(userId, email)`, `AnonymousClient()`.
- `Utils/TestHelpers.cs`: `ServerErrors`, `Eventually`, `JsonHelpers`.
- `Utils/WebhookEvents.cs`: typed webhook tuple builder.
- `Utils/StripeTestEvents.cs`: signed Stripe event payloads.
- `SqlServer/SqlServerTestContainer.cs`, `SqlServer/SqlServerWebAppFactory.cs`.
- `SqlServer/MigrationsTests.cs`, `SqlServer/ReadinessTests.cs`, `SqlServer/WebhookTests.cs`.
- `Services/QuotaServiceTests.cs`, `PeriodServiceTests.cs`, `MembershipEntitlementsTests.cs`, `StripeGatewayWebhookTests.cs`, `AdminAccessServiceTests.cs`, `UsernameRulesTests.cs`, `PngImageInspectorTests.cs`, `PromptBuilderTests.cs`.
- `Controllers/StoryGenerationTests.cs`, `PaymentsControllerTests.cs` (replace the commented-out file), `EndpointAuthorizationTests.cs`, `AdminControllerTests.cs`, `SavedCharacterControllerTests.cs`, `ShareControllerTests.cs`, `ProfileControllerTests.cs` (replace the commented-out file), `UsersControllerTests.cs`, `FeedbackControllerTests.cs`, `PlatformEndpointTests.cs`, `AuthControllerTests.cs` (add tests), `StoryResultEndpointTests.cs` (simplify).

---

### Task 1: Test infrastructure + `IBlobUploadService`

**Files:**
- Create: `Hackathon-2025/Services/IBlobUploadService.cs`
- Modify: `Hackathon-2025/Services/BlobUploadService.cs`, `Hackathon-2025/Program.cs:192`, `Hackathon-2025/Controllers/StoryController.cs:23,35,206`, `Hackathon-2025/Services/StoryGenerator.cs:15,23`
- Modify: `Hackathon-2025.Tests/Utils/TestWebAppFactory.cs`, `Hackathon-2025.Tests/Utils/TestAuthHandler.cs`, `Hackathon-2025.Tests/Controllers/StoryResultEndpointTests.cs`
- Create: `Hackathon-2025.Tests/Utils/Fakes.cs`, `TestData.cs`, `TestClients.cs`, `TestHelpers.cs`
- Test: `Hackathon-2025.Tests/Controllers/StoryGenerationTests.cs`

**Interfaces:**
- Produces (used by every later task):
  - `interface IBlobUploadService { Task<string> UploadImageAsync(string imageUrl, string fileName); Task<string> UploadBase64ImageAsync(string base64Data, string fileName); Task DeleteByUrlAsync(string blobUrl); }` (namespace `Hackathon_2025.Services`)
  - `TestWebAppFactory(IReadOnlyDictionary<string,string?>? configOverrides = null)` with `EmailMock`, `TurnstileMock`, `PaymentGatewayMock` (`Mock<IPaymentGateway>`), `StoryGenerator` (`FakeStoryGenerator`), `BlobUploads` (`FakeBlobUploadService`), consts `AdminEmail`, `StripeWebhookSecret`, `PriceIdPro`, `PriceIdPremium`, `PriceIdAddon5`, `PriceIdAddon11`, and `protected virtual void ConfigureDatabase(IServiceCollection services)`.
  - `TestData.NewUser(MembershipPlan plan = Free, int booksGenerated = 0, int addOnBalance = 0, string? email = null, string? username = null)`, `TestData.NewStory(int userId, int pageCount = 2, string title = "Test Story")`, extension `factory.SeedAsync<T>(T entity)`, extension `factory.QueryDbAsync<TResult>(Func<AppDbContext, Task<TResult>>)`.
  - Extensions `factory.ClientFor(int userId, string? email = null)`, `factory.AnonymousClient()`.
  - `ServerErrors.AssertServerErrorAsync(Func<Task<HttpResponseMessage>>)`, `Eventually.AssertAsync(Func<Task<bool>>, string because, int timeoutMs = 5000)`, extension `resp.ReadJsonAsync()` → `JsonElement`.
  - `FakeStoryGenerator { Exception? ThrowOnGenerate; StoryRequest? LastRequest; int CallCount; }`, `FakeBlobUploadService { ConcurrentBag<string> Uploaded; }`.

- [ ] **Step 1: Write the shared test utilities**

Create `Hackathon-2025.Tests/Utils/Fakes.cs`:

```csharp
using System.Collections.Concurrent;
using Hackathon_2025.Models;
using Hackathon_2025.Services;

namespace Hackathon_2025.Tests.Utils;

/// <summary>Stands in for the OpenAI-backed story generator. Returns a canned two-page story instantly.</summary>
internal sealed class FakeStoryGenerator : IStoryGeneratorService
{
    private int _callCount;

    public Exception? ThrowOnGenerate { get; set; }
    public StoryRequest? LastRequest { get; private set; }
    public int CallCount => _callCount;

    public Task<StoryResult> GenerateFullStoryAsync(StoryRequest request, Action<ProgressUpdate>? onProgress = null)
    {
        LastRequest = request;
        Interlocked.Increment(ref _callCount);

        if (ThrowOnGenerate is not null)
            throw ThrowOnGenerate;

        onProgress?.Invoke(new ProgressUpdate { Stage = "text", Percent = 50, Message = "Writing..." });

        return Task.FromResult(new StoryResult
        {
            Title = "Fake Story",
            CoverImagePrompt = "cover prompt",
            CoverImageUrl = "https://img.test/cover.png",
            Pages = new List<StoryPageDto>
            {
                new("Page one text", "page one prompt", "https://img.test/1.png"),
                new("Page two text", "page two prompt", "https://img.test/2.png")
            }
        });
    }
}

/// <summary>Stands in for Azure Blob Storage. Records file names and returns deterministic URLs.</summary>
internal sealed class FakeBlobUploadService : IBlobUploadService
{
    public ConcurrentBag<string> Uploaded { get; } = new();

    public Task<string> UploadImageAsync(string imageUrl, string fileName)
    {
        Uploaded.Add(fileName);
        return Task.FromResult($"https://blob.test/{fileName}");
    }

    public Task<string> UploadBase64ImageAsync(string base64Data, string fileName)
    {
        Uploaded.Add(fileName);
        return Task.FromResult($"https://blob.test/{fileName}");
    }

    public Task DeleteByUrlAsync(string blobUrl) => Task.CompletedTask;
}
```

Create `Hackathon-2025.Tests/Utils/TestData.cs`:

```csharp
using Hackathon_2025.Data;
using Hackathon_2025.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Hackathon_2025.Tests.Utils;

internal static class TestData
{
    public static User NewUser(
        MembershipPlan plan = MembershipPlan.Free,
        int booksGenerated = 0,
        int addOnBalance = 0,
        string? email = null,
        string? username = null)
    {
        var name = username ?? $"u{Guid.NewGuid():N}"[..20];
        return new User
        {
            Email = email ?? $"{name}@test.local",
            Username = name,
            UsernameNormalized = name.ToLowerInvariant(),
            PasswordHash = "not-a-real-hash",
            IsEmailVerified = true,
            Membership = plan,
            PlanKey = plan.ToString().ToLowerInvariant(),
            BooksGenerated = booksGenerated,
            AddOnBalance = addOnBalance,
            LastReset = DateTime.UtcNow
        };
    }

    public static Story NewStory(int userId, int pageCount = 2, string title = "Test Story")
    {
        var story = new Story
        {
            Title = title,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            CoverImageUrl = "https://img.test/cover.png"
        };
        for (var i = 0; i < pageCount; i++)
        {
            story.Pages.Add(new StoryPage($"Page {i + 1} text", $"prompt {i + 1}") { ImageUrl = $"https://img.test/{i + 1}.png" });
        }
        return story;
    }

    public static async Task<T> SeedAsync<T>(this WebApplicationFactory<Program> factory, T entity) where T : class
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    public static async Task<TResult> QueryDbAsync<TResult>(
        this WebApplicationFactory<Program> factory,
        Func<AppDbContext, Task<TResult>> query)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await query(db);
    }
}
```

Create `Hackathon-2025.Tests/Utils/TestClients.cs`:

```csharp
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
```

Create `Hackathon-2025.Tests/Utils/TestHelpers.cs`:

```csharp
using System.Text.Json;

namespace Hackathon_2025.Tests.Utils;

internal static class ServerErrors
{
    /// <summary>
    /// The app has no exception-handling middleware, so in TestServer an unhandled exception surfaces
    /// as an exception from HttpClient instead of a 500 response. Accept either.
    /// </summary>
    public static async Task AssertServerErrorAsync(Func<Task<HttpResponseMessage>> send)
    {
        try
        {
            var resp = await send();
            Assert.IsTrue((int)resp.StatusCode >= 500, $"Expected a server error but got {(int)resp.StatusCode}.");
        }
        catch (Exception ex) when (ex is not AssertFailedException)
        {
            // Unhandled server exception propagated by TestServer: expected.
        }
    }
}

internal static class Eventually
{
    public static async Task AssertAsync(Func<Task<bool>> condition, string because, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(50);
        }
        Assert.Fail($"Condition not met within {timeoutMs}ms: {because}");
    }
}

internal static class JsonHelpers
{
    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage resp)
    {
        var text = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.Clone();
    }
}
```

Replace `HandleAuthenticateAsync` and the constants in `Hackathon-2025.Tests/Utils/TestAuthHandler.cs` so the whole file reads:

```csharp
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
```

Replace the whole of `Hackathon-2025.Tests/Utils/TestWebAppFactory.cs` with:

```csharp
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
```

- [ ] **Step 2: Write the failing story-generation smoke test**

Create `Hackathon-2025.Tests/Controllers/StoryGenerationTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Hackathon_2025.Models;
using Hackathon_2025.Tests.Utils;
using Microsoft.EntityFrameworkCore;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class StoryGenerationTests
{
    private TestWebAppFactory _factory = null!;

    [TestInitialize]
    public void Init() => _factory = new TestWebAppFactory();

    [TestCleanup]
    public void Cleanup() => _factory.Dispose();

    private static object StoryBody(string? storyLength = null, Dictionary<string, string>? fields = null, int characterCount = 1)
    {
        var characters = Enumerable.Range(0, characterCount).Select(i => new
        {
            name = i == 0 ? "Milo" : $"Friend{i}",
            role = i == 0 ? "Main" : "Friend",
            isAnimal = false,
            descriptionFields = fields ?? new Dictionary<string, string> { ["hairColor"] = "brown" }
        }).ToArray();

        return new
        {
            theme = "Space Adventure",
            readingLevel = "early",
            artStyle = "watercolor",
            storyLength,
            characters
        };
    }

    [TestMethod]
    public async Task GenerateFull_Pro_User_Persists_Story_With_Blob_Urls()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody());

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        var story = await _factory.QueryDbAsync(db => db.Stories.Include(s => s.Pages).SingleAsync(s => s.UserId == user.Id));
        Assert.AreEqual("Fake Story", story.Title);
        Assert.AreEqual(2, story.Pages.Count);
        Assert.IsTrue(story.CoverImageUrl!.StartsWith("https://blob.test/"), story.CoverImageUrl);
        Assert.IsTrue(story.Pages.All(p => p.ImageUrl!.StartsWith("https://blob.test/")));
        Assert.AreEqual(3, _factory.BlobUploads.Uploaded.Count, "cover + 2 pages");
        var saved = await _factory.QueryDbAsync(db => db.Users.SingleAsync(u => u.Id == user.Id));
        Assert.AreEqual(1, saved.BooksGenerated);
    }
}
```

- [ ] **Step 3: Run it to verify it fails**

Run: `dotnet test Hackathon-2025.Tests/Hackathon-2025.Tests.csproj --filter "FullyQualifiedName~StoryGenerationTests"`
Expected: build FAILS with `error CS0246: The type or namespace name 'IBlobUploadService' could not be found`.

- [ ] **Step 4: Add the interface and switch consumers to it (the one approved production refactor)**

Create `Hackathon-2025/Services/IBlobUploadService.cs`:

```csharp
namespace Hackathon_2025.Services;

public interface IBlobUploadService
{
    Task<string> UploadImageAsync(string imageUrl, string fileName);
    Task<string> UploadBase64ImageAsync(string base64Data, string fileName);
    Task DeleteByUrlAsync(string blobUrl);
}
```

In `Hackathon-2025/Services/BlobUploadService.cs` add `using Hackathon_2025.Services;` as the third using line and change the class declaration from `public class BlobUploadService` to `public class BlobUploadService : IBlobUploadService`. Change nothing else (keep the class in the global namespace).

In `Hackathon-2025/Program.cs` replace `builder.Services.AddSingleton<BlobUploadService>();` with:

```csharp
builder.Services.AddSingleton<IBlobUploadService, BlobUploadService>();
```

In `Hackathon-2025/Controllers/StoryController.cs`:
- line 23: `private readonly BlobUploadService _blobService;` → `private readonly IBlobUploadService _blobService;`
- line 35: `BlobUploadService blobService,` → `IBlobUploadService blobService,`
- line 206: `scope.ServiceProvider.GetRequiredService<BlobUploadService>();` → `scope.ServiceProvider.GetRequiredService<IBlobUploadService>();`

In `Hackathon-2025/Services/StoryGenerator.cs`:
- line 15: `private readonly BlobUploadService _blobUploader;` → `private readonly IBlobUploadService _blobUploader;`
- line 23: `BlobUploadService blobUploader,` → `IBlobUploadService blobUploader,`

Then confirm nothing else references the concrete type for DI:

Run: `grep -rn "BlobUploadService" Hackathon-2025 --include=*.cs | grep -v "/obj/" | grep -v IBlobUploadService`
Expected: only `Services/BlobUploadService.cs` (the class declaration and its constructor) and the `Program.cs` registration line.

- [ ] **Step 5: Simplify the existing result-endpoint test (the factory now fakes Blob storage)**

Replace the whole of `Hackathon-2025.Tests/Controllers/StoryResultEndpointTests.cs` with:

```csharp
using Hackathon_2025.Services;
using Hackathon_2025.Tests.Utils;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class StoryResultEndpointTests
{
    private TestWebAppFactory _factory = null!;

    [TestInitialize]
    public void Init() => _factory = new TestWebAppFactory();

    [TestCleanup]
    public void Cleanup() => _factory.Dispose();

    private string CreateFinishedJob(int ownerUserId)
    {
        var broker = _factory.Services.GetRequiredService<IProgressBroker>();
        var jobId = broker.CreateJob(ownerUserId);
        broker.SetResult(jobId, new { title = "A Starry Night" });
        broker.Complete(jobId);
        return jobId;
    }

    [TestMethod]
    public async Task Result_Returns_200_For_Job_Owner()
    {
        var jobId = CreateFinishedJob(ownerUserId: 5);

        var resp = await _factory.ClientFor(5).GetAsync($"/api/story/result/{jobId}");

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        StringAssert.Contains(await resp.Content.ReadAsStringAsync(), "A Starry Night");
    }

    [TestMethod]
    public async Task Result_Returns_404_For_Different_User()
    {
        var jobId = CreateFinishedJob(ownerUserId: 5);

        var resp = await _factory.ClientFor(6).GetAsync($"/api/story/result/{jobId}");

        Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
```

- [ ] **Step 6: Run the whole suite**

Run: `dotnet build Hackathon-2025/Hackathon-2025.csproj` then `dotnet test Hackathon-2025.Tests/Hackathon-2025.Tests.csproj`
Expected: API build `0 Warning(s) 0 Error(s)`; tests `Passed! - Failed: 0, Passed: 46` (45 existing + 1 new).

- [ ] **Step 7: Commit**

```bash
git add Hackathon-2025/Services/IBlobUploadService.cs Hackathon-2025/Services/BlobUploadService.cs Hackathon-2025/Program.cs Hackathon-2025/Controllers/StoryController.cs Hackathon-2025/Services/StoryGenerator.cs Hackathon-2025.Tests/Utils Hackathon-2025.Tests/Controllers/StoryGenerationTests.cs Hackathon-2025.Tests/Controllers/StoryResultEndpointTests.cs
git commit -m "Added shared test infrastructure and IBlobUploadService" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: SQL Server test harness (Testcontainers) + migrations and readiness tests

**Files:**
- Modify: `Hackathon-2025.Tests/Hackathon-2025.Tests.csproj` (new package)
- Create: `Hackathon-2025.Tests/SqlServer/SqlServerTestContainer.cs`, `SqlServer/SqlServerWebAppFactory.cs`, `SqlServer/MigrationsTests.cs`, `SqlServer/ReadinessTests.cs`

**Interfaces:**
- Consumes: `TestWebAppFactory`, `ConfigureDatabase`, `TestData`, `TestClients` (Task 1).
- Produces: `static Task<SqlServerWebAppFactory> SqlServerWebAppFactory.CreateAsync()` (a migrated, isolated SQL Server database per call); `SqlServerTestContainer.GetConnectionStringForNewDatabaseAsync()`.

- [ ] **Step 1: Add the package (test project only)**

Run: `dotnet add Hackathon-2025.Tests/Hackathon-2025.Tests.csproj package Testcontainers.MsSql`
Expected: a `<PackageReference Include="Testcontainers.MsSql" Version="…" />` line appears in the test csproj. Record the version in the commit message body.

- [ ] **Step 2: Write the container and factory**

Create `Hackathon-2025.Tests/SqlServer/SqlServerTestContainer.cs`:

```csharp
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Hackathon_2025.Tests.SqlServer;

/// <summary>
/// One disposable SQL Server (Docker) per test run, started lazily on first use.
/// Each caller gets its own database on that server, so tests stay isolated and can run in parallel.
/// Locally, missing Docker makes these tests Inconclusive (skipped); with CI=true it fails them.
/// </summary>
internal static class SqlServerTestContainer
{
    private static readonly Lazy<Task<MsSqlContainer?>> Container = new(StartAsync);
    private static string? _startFailure;

    private static async Task<MsSqlContainer?> StartAsync()
    {
        try
        {
            // If the installed Testcontainers version marks the parameterless builder obsolete,
            // use: new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build()
            var container = new MsSqlBuilder()
                .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
                .Build();
            await container.StartAsync();
            return container;
        }
        catch (Exception ex)
        {
            _startFailure = ex.Message;
            return null;
        }
    }

    public static async Task<string> GetConnectionStringForNewDatabaseAsync()
    {
        var container = await Container.Value;
        if (container is null)
        {
            var message = $"SQL Server test container unavailable (is Docker running?): {_startFailure}";
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
                Assert.Fail(message);
            Assert.Inconclusive(message);
        }

        var builder = new SqlConnectionStringBuilder(container!.GetConnectionString())
        {
            InitialCatalog = $"t_{Guid.NewGuid():N}"
        };
        return builder.ConnectionString;
    }

    public static async Task DisposeAsync()
    {
        if (Container.IsValueCreated && await Container.Value is { } container)
            await container.DisposeAsync();
    }
}

[TestClass]
public class SqlServerAssemblyHooks
{
    [AssemblyCleanup]
    public static async Task Cleanup() => await SqlServerTestContainer.DisposeAsync();
}
```

Create `Hackathon-2025.Tests/SqlServer/SqlServerWebAppFactory.cs`:

```csharp
using Hackathon_2025.Data;
using Hackathon_2025.Tests.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hackathon_2025.Tests.SqlServer;

/// <summary>The normal test app, but backed by a real, freshly migrated SQL Server database.</summary>
internal sealed class SqlServerWebAppFactory : TestWebAppFactory
{
    private readonly string _connectionString;

    private SqlServerWebAppFactory(string connectionString) => _connectionString = connectionString;

    public static async Task<SqlServerWebAppFactory> CreateAsync()
    {
        var connectionString = await SqlServerTestContainer.GetConnectionStringForNewDatabaseAsync();
        var factory = new SqlServerWebAppFactory(connectionString);
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        return factory;
    }

    // Mirrors Program.cs (SqlServer + retry on transient errors such as deadlocks).
    protected override void ConfigureDatabase(IServiceCollection services)
        => services.AddDbContext<AppDbContext>(o => o.UseSqlServer(
            _connectionString,
            sql => sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(2), errorNumbersToAdd: null)));
}
```

- [ ] **Step 3: Write the migrations and readiness tests**

Create `Hackathon-2025.Tests/SqlServer/MigrationsTests.cs`:

```csharp
using Hackathon_2025.Models;
using Hackathon_2025.Tests.Utils;
using Microsoft.EntityFrameworkCore;

namespace Hackathon_2025.Tests.SqlServer;

[TestClass]
[TestCategory("SqlServer")]
public class MigrationsTests
{
    [TestMethod]
    public async Task All_Migrations_Apply_To_An_Empty_Database()
    {
        using var factory = await SqlServerWebAppFactory.CreateAsync();

        var (pending, applied, known) = await factory.QueryDbAsync(async db => (
            (await db.Database.GetPendingMigrationsAsync()).ToList(),
            (await db.Database.GetAppliedMigrationsAsync()).ToList(),
            db.Database.GetMigrations().ToList()));

        Assert.AreEqual(0, pending.Count, "Pending migrations: " + string.Join(", ", pending));
        CollectionAssert.AreEquivalent(known, applied);
    }

    [TestMethod]
    public async Task Migrated_Schema_Accepts_Every_Entity_The_Model_Writes()
    {
        using var factory = await SqlServerWebAppFactory.CreateAsync();

        var user = await factory.SeedAsync(TestData.NewUser(MembershipPlan.Premium, addOnBalance: 2));
        var story = TestData.NewStory(user.Id);
        story.RequestTheme = "Space";
        story.RequestReadingLevel = "early";
        story.RequestArtStyle = "watercolor";
        story.RequestStoryLength = "short";
        story.RequestLessonLearned = "Be kind";
        story.RequestCharactersJson = "[]";
        await factory.SeedAsync(story);
        await factory.SeedAsync(new StoryShare { StoryId = story.Id, ExpiresUtc = DateTime.UtcNow.AddDays(1) });
        await factory.SeedAsync(new SavedCharacter { UserId = user.Id, Name = "Milo", CharacterJson = "{}" });
        await factory.SeedAsync(new ProcessedWebhook { EventId = "evt_schema" });

        var counts = await factory.QueryDbAsync(async db => (
            await db.Users.CountAsync(),
            await db.Stories.CountAsync(),
            await db.StoryPages.CountAsync(),
            await db.StoryShares.CountAsync(),
            await db.SavedCharacters.CountAsync(),
            await db.ProcessedWebhooks.CountAsync()));

        Assert.AreEqual((1, 1, 2, 1, 1, 1), counts);
    }
}
```

Create `Hackathon-2025.Tests/SqlServer/ReadinessTests.cs`:

```csharp
using System.Net;
using Hackathon_2025.Tests.Utils;

namespace Hackathon_2025.Tests.SqlServer;

[TestClass]
[TestCategory("SqlServer")]
public class ReadinessTests
{
    [TestMethod]
    public async Task Readyz_Returns_Ready_When_Database_Is_Reachable()
    {
        using var factory = await SqlServerWebAppFactory.CreateAsync();

        var resp = await factory.AnonymousClient().GetAsync("/readyz");

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        StringAssert.Contains(await resp.Content.ReadAsStringAsync(), "ready");
    }

    [TestMethod]
    public async Task Warmup_Returns_NoContent()
    {
        using var factory = await SqlServerWebAppFactory.CreateAsync();

        var resp = await factory.AnonymousClient().PostAsync("/api/warmup", content: null);

        Assert.AreEqual(HttpStatusCode.NoContent, resp.StatusCode);
    }
}
```

- [ ] **Step 4: Run with Docker running, then without**

Make sure Docker Desktop is running (`docker info` succeeds).
Run: `dotnet test Hackathon-2025.Tests/Hackathon-2025.Tests.csproj --filter "TestCategory=SqlServer"`
Expected: `Passed: 4`. The first run pulls the SQL Server image (~1–3 minutes).

If `All_Migrations_Apply_To_An_Empty_Database` or `Migrated_Schema_…` fails, that is a real production finding (the migrations don't build the schema the app expects). Stop and report it; don't edit migrations.

Then stop Docker Desktop and run the same command. Expected: 4 tests reported as **Skipped/Inconclusive**, 0 failed. Restart Docker Desktop afterwards.

Run the whole suite: `dotnet test Hackathon-2025.Tests/Hackathon-2025.Tests.csproj` → `Failed: 0, Passed: 50`.

- [ ] **Step 5: Commit**

```bash
git add Hackathon-2025.Tests/Hackathon-2025.Tests.csproj Hackathon-2025.Tests/SqlServer
git commit -m "Added SQL Server test harness with migration and readiness tests" -m "Testcontainers.MsSql <version>" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Quota, period, and membership rule tests

**Files:**
- Test: `Hackathon-2025.Tests/Services/QuotaServiceTests.cs`, `Services/PeriodServiceTests.cs`, `Services/MembershipEntitlementsTests.cs`

**Interfaces:**
- Consumes: production `QuotaService(IOptionsSnapshot<CreditsOptions>)`, `PeriodService(IOptionsSnapshot<CreditsOptions>)`, static `MembershipEntitlements`.

These test existing, pure logic, so they should pass on first run. A failure is a finding: stop and report it.

- [ ] **Step 1: Write the tests**

Create `Hackathon-2025.Tests/Services/QuotaServiceTests.cs`:

```csharp
using Hackathon_2025.Models;
using Hackathon_2025.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;

namespace Hackathon_2025.Tests.Services;

[TestClass]
public class QuotaServiceTests
{
    private static QuotaService Create(CreditsOptions? options = null)
    {
        var snapshot = new Mock<IOptionsSnapshot<CreditsOptions>>();
        snapshot.Setup(s => s.Value).Returns(options ?? new CreditsOptions());
        return new QuotaService(snapshot.Object);
    }

    [DataTestMethod]
    [DataRow("Free", 1)]
    [DataRow("Pro", 5)]
    [DataRow("Premium", 11)]
    [DataRow("premium", 11)]
    [DataRow("PRO", 5)]
    public void BaseQuotaFor_Is_Case_Insensitive(string membership, int expected)
        => Assert.AreEqual(expected, Create().BaseQuotaFor(membership));

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void BaseQuotaFor_Blank_Membership_Means_Free(string? membership)
        => Assert.AreEqual(1, Create().BaseQuotaFor(membership));

    [TestMethod]
    public void BaseQuotaFor_Unknown_Plan_Is_Zero()
        => Assert.AreEqual(0, Create().BaseQuotaFor("gold"));

    [TestMethod]
    public void BaseQuotas_Bound_From_Configuration_Stay_Case_Insensitive()
    {
        // Mirrors how appsettings.json "Credits" is bound in production.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Credits:BaseQuotas:free"] = "1",
                ["Credits:BaseQuotas:pro"] = "5",
                ["Credits:BaseQuotas:premium"] = "11"
            })
            .Build();
        var bound = config.GetSection("Credits").Get<CreditsOptions>()!;

        Assert.AreEqual(11, Create(bound).BaseQuotaFor(MembershipPlan.Premium.ToString()));
        Assert.AreEqual(5, Create(bound).BaseQuotaFor(MembershipPlan.Pro.ToString()));
    }

    [DataTestMethod]
    [DataRow("Pro", 0, 0, false, DisplayName = "Pro cannot buy (premium only)")]
    [DataRow("Premium", 1, 0, false, DisplayName = "Premium with base left cannot buy")]
    [DataRow("Premium", 0, 0, true, DisplayName = "Premium exhausted can buy")]
    [DataRow("premium", 0, 5, true, DisplayName = "Existing add-ons do not block buying")]
    public void CanBuyAddons_Default_Policy(string membership, int baseRemaining, int addOnBalance, bool expected)
        => Assert.AreEqual(expected, Create().CanBuyAddons(membership, baseRemaining, addOnBalance));

    [TestMethod]
    public void CanBuyAddons_When_Premium_Not_Required_Pro_Can_Buy()
        => Assert.IsTrue(Create(new CreditsOptions { RequirePremiumForAddons = false }).CanBuyAddons("Pro", 0, 0));

    [TestMethod]
    public void CanBuyAddons_When_Exhaustion_Not_Required_Premium_Can_Buy_Early()
        => Assert.IsTrue(Create(new CreditsOptions { OnlyAllowPurchaseWhenExhausted = false }).CanBuyAddons("Premium", 3, 0));
}
```

Create `Hackathon-2025.Tests/Services/PeriodServiceTests.cs`:

```csharp
using Hackathon_2025.Models;
using Hackathon_2025.Services;
using Microsoft.Extensions.Options;
using Moq;

namespace Hackathon_2025.Tests.Services;

[TestClass]
public class PeriodServiceTests
{
    private static readonly DateTime Now = new(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

    private static PeriodService Create(bool carryoverEnabled = true)
    {
        var snapshot = new Mock<IOptionsSnapshot<CreditsOptions>>();
        snapshot.Setup(s => s.Value).Returns(new CreditsOptions { CarryoverEnabled = carryoverEnabled });
        return new PeriodService(snapshot.Object);
    }

    [TestMethod]
    public void CurrentPeriod_Uses_Stripe_Window_When_Now_Is_Inside_It()
    {
        var user = new User
        {
            CurrentPeriodStartUtc = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            CurrentPeriodEndUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc)
        };

        var (start, end) = Create().CurrentPeriodUtc(user, Now);

        Assert.AreEqual(new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc), start);
        Assert.AreEqual(new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc), end);
    }

    [TestMethod]
    public void CurrentPeriod_Falls_Back_To_Calendar_Month_Without_Stripe_Window()
    {
        var (start, end) = Create().CurrentPeriodUtc(new User(), Now);

        Assert.AreEqual(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), start);
        Assert.AreEqual(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), end);
    }

    [TestMethod]
    public void CurrentPeriod_Falls_Back_To_Calendar_Month_When_Window_Is_Stale()
    {
        var user = new User
        {
            CurrentPeriodStartUtc = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            CurrentPeriodEndUtc = new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc)
        };

        var (start, _) = Create().CurrentPeriodUtc(user, Now);

        Assert.AreEqual(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), start);
    }

    [TestMethod]
    public void IsPeriodBoundary_True_When_Stripe_End_Has_Passed()
    {
        var user = new User { LastReset = Now, CurrentPeriodEndUtc = Now.AddSeconds(-1) };
        Assert.IsTrue(Create().IsPeriodBoundary(user, Now));
    }

    [TestMethod]
    public void IsPeriodBoundary_False_Inside_Stripe_Window_Same_Month()
    {
        var user = new User { LastReset = Now.AddDays(-2), CurrentPeriodEndUtc = Now.AddDays(5) };
        Assert.IsFalse(Create().IsPeriodBoundary(user, Now));
    }

    [TestMethod]
    public void IsPeriodBoundary_True_When_Calendar_Month_Changed()
    {
        var user = new User { LastReset = new DateTime(2026, 2, 28, 23, 0, 0, DateTimeKind.Utc) };
        Assert.IsTrue(Create().IsPeriodBoundary(user, Now));
    }

    [TestMethod]
    public void IsPeriodBoundary_True_Same_Month_Previous_Year()
    {
        var user = new User { LastReset = new DateTime(2025, 3, 20, 0, 0, 0, DateTimeKind.Utc) };
        Assert.IsTrue(Create().IsPeriodBoundary(user, Now));
    }

    [TestMethod]
    public void IsPeriodBoundary_False_Same_Calendar_Month()
    {
        var user = new User { LastReset = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc) };
        Assert.IsFalse(Create().IsPeriodBoundary(user, Now));
    }

    [TestMethod]
    public void Rollover_Resets_Counters_And_Keeps_AddOns_When_Carryover_Enabled()
    {
        var user = new User { BooksGenerated = 4, AddOnSpentThisPeriod = 2, AddOnBalance = 7 };

        Create(carryoverEnabled: true).OnPeriodRollover(user, Now);

        Assert.AreEqual(0, user.BooksGenerated);
        Assert.AreEqual(0, user.AddOnSpentThisPeriod);
        Assert.AreEqual(7, user.AddOnBalance);
        Assert.AreEqual(Now, user.LastReset);
    }

    [TestMethod]
    public void Rollover_Clears_AddOns_When_Carryover_Disabled()
    {
        var user = new User { AddOnBalance = 7 };

        Create(carryoverEnabled: false).OnPeriodRollover(user, Now);

        Assert.AreEqual(0, user.AddOnBalance);
    }

    [TestMethod]
    public void Rollover_With_Stripe_Window_Moves_Start_And_Clears_Stale_End()
    {
        var user = new User { CurrentPeriodEndUtc = Now.AddDays(-1) };

        Create().OnPeriodRollover(user, Now);

        Assert.AreEqual(Now, user.CurrentPeriodStartUtc);
        Assert.IsNull(user.CurrentPeriodEndUtc);
        Assert.IsFalse(Create().IsPeriodBoundary(user, Now), "must not keep firing on every request after rollover");
    }

    [TestMethod]
    public void Rollover_Without_Stripe_Window_Sets_Start_To_First_Of_Month()
    {
        var user = new User();

        Create().OnPeriodRollover(user, Now);

        Assert.AreEqual(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), user.CurrentPeriodStartUtc);
    }
}
```

Create `Hackathon-2025.Tests/Services/MembershipEntitlementsTests.cs`:

```csharp
using Hackathon_2025.Models;
using Hackathon_2025.Services;

namespace Hackathon_2025.Tests.Services;

[TestClass]
public class MembershipEntitlementsTests
{
    [DataTestMethod]
    [DataRow(MembershipPlan.Free, 1)]
    [DataRow(MembershipPlan.Pro, 5)]
    [DataRow(MembershipPlan.Premium, 10)]
    public void SavedCharacterLimit_Per_Plan(MembershipPlan plan, int expected)
        => Assert.AreEqual(expected, MembershipEntitlements.SavedCharacterLimitFor(plan));

    [TestMethod]
    public void Only_Paid_Plans_Support_Advanced_Characters()
    {
        Assert.IsFalse(MembershipEntitlements.SupportsAdvancedCharacterCreation(MembershipPlan.Free));
        Assert.IsTrue(MembershipEntitlements.SupportsAdvancedCharacterCreation(MembershipPlan.Pro));
        Assert.IsTrue(MembershipEntitlements.SupportsAdvancedCharacterCreation(MembershipPlan.Premium));
    }

    [TestMethod]
    public void Free_Fields_Are_Trimmed_To_The_Allowlist_Case_Insensitively()
    {
        var fields = new Dictionary<string, string>
        {
            ["HairColor"] = "brown",
            ["favoriteFood"] = "pizza",
            ["eyeColor"] = "  "
        };

        var result = MembershipEntitlements.SanitizeDescriptionFields(MembershipPlan.Free, fields);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("brown", result["hairColor"]);
    }

    [TestMethod]
    public void Paid_Fields_Keep_Everything_Non_Blank()
    {
        var fields = new Dictionary<string, string> { ["hairColor"] = "brown", ["favoriteFood"] = "pizza", ["blank"] = "" };

        var result = MembershipEntitlements.SanitizeDescriptionFields(MembershipPlan.Pro, fields);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("pizza", result["favoriteFood"]);
    }

    [TestMethod]
    public void Null_Fields_Become_Empty()
        => Assert.AreEqual(0, MembershipEntitlements.SanitizeDescriptionFields(MembershipPlan.Premium, null).Count);

    [TestMethod]
    public void Character_Role_Defaults_To_Main_And_Is_Trimmed()
    {
        var blankRole = new CharacterSpec { Name = "Milo", Role = " " };
        var paddedRole = new CharacterSpec { Name = "Milo", Role = "  Friend " };

        Assert.AreEqual("Main", MembershipEntitlements.SanitizeCharacterForMembership(MembershipPlan.Free, blankRole).Role);
        Assert.AreEqual("Friend", MembershipEntitlements.SanitizeCharacterForMembership(MembershipPlan.Free, paddedRole).Role);
    }
}
```

- [ ] **Step 2: Run them**

Run: `dotnet test Hackathon-2025.Tests/Hackathon-2025.Tests.csproj --filter "FullyQualifiedName~Hackathon_2025.Tests.Services"`
Expected: all PASS. Pay special attention to `BaseQuotas_Bound_From_Configuration_Stay_Case_Insensitive`: if it FAILS, production quotas for paid plans may resolve to 0. Stop and report, don't "fix" the test.

- [ ] **Step 3: Commit**

```bash
git add Hackathon-2025.Tests/Services/QuotaServiceTests.cs Hackathon-2025.Tests/Services/PeriodServiceTests.cs Hackathon-2025.Tests/Services/MembershipEntitlementsTests.cs
git commit -m "Added quota, billing period, and membership rule tests" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Stripe webhook signature and parsing tests

**Files:**
- Create: `Hackathon-2025.Tests/Utils/StripeTestEvents.cs`
- Test: `Hackathon-2025.Tests/Services/StripeGatewayWebhookTests.cs`

**Interfaces:**
- Produces: `StripeTestEvents.Sign(string payload, string secret, DateTimeOffset? signedAt = null)` → `Stripe-Signature` header value; `StripeTestEvents.Event(string id, string type, string dataObjectJson)` → event JSON using `StripeConfiguration.ApiVersion`.

Why these payloads: they avoid code paths that call Stripe's API (no `subscription` on checkout sessions, no `latest_invoice` on subscriptions), so the real `StripeGateway` can be exercised with no network. `ConstructEvent` rejects events whose `api_version` differs from the SDK's, so payloads must use `StripeConfiguration.ApiVersion`.

- [ ] **Step 1: Write the signing helper**

Create `Hackathon-2025.Tests/Utils/StripeTestEvents.cs`:

```csharp
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
        $$"""
        {"id":"{{id}}","object":"event","api_version":"{{StripeConfiguration.ApiVersion}}","created":1767225600,"livemode":false,"pending_webhooks":1,"type":"{{type}}","data":{"object":{{dataObjectJson}}}}
        """;
}
```

- [ ] **Step 2: Write the gateway tests**

Create `Hackathon-2025.Tests/Services/StripeGatewayWebhookTests.cs`:

```csharp
using System.Text;
using Hackathon_2025.Data;
using Hackathon_2025.Options;
using Hackathon_2025.Tests.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Stripe;

namespace Hackathon_2025.Tests.Services;

[TestClass]
public class StripeGatewayWebhookTests
{
    private const string Secret = "whsec_unit_test";

    private static StripeGateway CreateGateway()
    {
        var stripe = Microsoft.Extensions.Options.Options.Create(new StripeOptions
        {
            SecretKey = "sk_test_dummy",
            WebhookSecret = Secret,
            PriceIdPro = "price_pro",
            PriceIdPremium = "price_premium",
            PriceIdAddon5 = "price_a5",
            PriceIdAddon11 = "price_a11"
        });
        var app = Microsoft.Extensions.Options.Options.Create(new AppOptions { BaseUrl = "https://app.test" });
        var billing = Microsoft.Extensions.Options.Options.Create(new BillingOptions());
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

        return new StripeGateway(stripe, app, billing, db, NullLogger<StripeGateway>.Instance, new StripeClient("sk_test_dummy"));
    }

    private static HttpRequest SignedRequest(string payload, string? signature = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        context.Request.Headers["Stripe-Signature"] = signature ?? StripeTestEvents.Sign(payload, Secret);
        return context.Request;
    }

    [TestMethod]
    public async Task Payment_Checkout_Maps_AddOn_Sku_Quantity_User_And_Customer()
    {
        var payload = StripeTestEvents.Event("evt_pay_1", "checkout.session.completed",
            """{"id":"cs_1","object":"checkout.session","mode":"payment","client_reference_id":"42","customer":"cus_1","metadata":{"userId":"42","sku":"addon_plus5","qty":"2","priceId":"price_a5"}}""");

        var e = await CreateGateway().HandleWebhookAsync(SignedRequest(payload));

        Assert.AreEqual("evt_pay_1", e.eventId);
        Assert.AreEqual(42, e.userId);
        Assert.AreEqual("cus_1", e.customerRef);
        Assert.AreEqual("addon_plus5", e.addOnSku);
        Assert.AreEqual(2, e.addOnQty);
        Assert.AreEqual("paid", e.status);
        Assert.IsNull(e.planKey);
    }

    [TestMethod]
    public async Task Payment_Checkout_Without_Sku_Falls_Back_To_Price_Id()
    {
        var payload = StripeTestEvents.Event("evt_pay_2", "checkout.session.completed",
            """{"id":"cs_2","object":"checkout.session","mode":"payment","client_reference_id":"42","metadata":{"priceId":"price_a11"}}""");

        var e = await CreateGateway().HandleWebhookAsync(SignedRequest(payload));

        Assert.AreEqual("addon_plus11", e.addOnSku);
        Assert.AreEqual(1, e.addOnQty);
    }

    [TestMethod]
    public async Task Payment_Checkout_With_Zero_Quantity_Is_Treated_As_One()
    {
        var payload = StripeTestEvents.Event("evt_pay_3", "checkout.session.completed",
            """{"id":"cs_3","object":"checkout.session","mode":"payment","client_reference_id":"42","metadata":{"sku":"addon_plus5","qty":"0"}}""");

        var e = await CreateGateway().HandleWebhookAsync(SignedRequest(payload));

        Assert.AreEqual(1, e.addOnQty);
    }

    [TestMethod]
    public async Task Subscription_Checkout_Maps_Plan_From_Metadata()
    {
        var payload = StripeTestEvents.Event("evt_sub_co", "checkout.session.completed",
            """{"id":"cs_4","object":"checkout.session","mode":"subscription","client_reference_id":"7","customer":"cus_7","metadata":{"plan":"premium"}}""");

        var e = await CreateGateway().HandleWebhookAsync(SignedRequest(payload));

        Assert.AreEqual(7, e.userId);
        Assert.AreEqual("premium", e.planKey);
        Assert.AreEqual("active", e.status);
        Assert.AreEqual("cus_7", e.customerRef);
        Assert.IsNull(e.addOnSku);
    }

    [TestMethod]
    public async Task Subscription_Updated_Maps_Plan_From_Price_Id()
    {
        var payload = StripeTestEvents.Event("evt_sub_upd", "customer.subscription.updated",
            """{"id":"sub_1","object":"subscription","customer":"cus_1","status":"active","items":{"object":"list","data":[{"id":"si_1","object":"subscription_item","price":{"id":"price_premium","object":"price"}}]}}""");

        var e = await CreateGateway().HandleWebhookAsync(SignedRequest(payload));

        Assert.AreEqual("premium", e.planKey);
        Assert.AreEqual("active", e.status);
        Assert.AreEqual("cus_1", e.customerRef);
        Assert.AreEqual("sub_1", e.subscriptionRef);
        Assert.IsNull(e.userId);
    }

    [TestMethod]
    public async Task Subscription_Deleted_Maps_To_Free_And_Canceled()
    {
        var payload = StripeTestEvents.Event("evt_sub_del", "customer.subscription.deleted",
            """{"id":"sub_1","object":"subscription","customer":"cus_1","status":"canceled"}""");

        var e = await CreateGateway().HandleWebhookAsync(SignedRequest(payload));

        Assert.AreEqual("free", e.planKey);
        Assert.AreEqual("canceled", e.status);
        Assert.AreEqual("sub_1", e.subscriptionRef);
    }

    [TestMethod]
    public async Task Invoice_Paid_Maps_Period_Window()
    {
        var payload = StripeTestEvents.Event("evt_inv", "invoice.payment_succeeded",
            """{"id":"in_1","object":"invoice","customer":"cus_1","lines":{"object":"list","data":[{"id":"il_1","object":"line_item","period":{"start":1767225600,"end":1769904000}}]}}""");

        var e = await CreateGateway().HandleWebhookAsync(SignedRequest(payload));

        Assert.AreEqual("active", e.status);
        Assert.AreEqual(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), e.periodStartUtc);
        Assert.AreEqual(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), e.periodEndUtc);
    }

    [TestMethod]
    public async Task Unhandled_Event_Type_Is_Ignored()
    {
        var payload = StripeTestEvents.Event("evt_other", "charge.refunded", """{"id":"ch_1","object":"charge"}""");

        var e = await CreateGateway().HandleWebhookAsync(SignedRequest(payload));

        Assert.AreEqual("ignored", e.status);
        Assert.IsNull(e.planKey);
        Assert.IsNull(e.addOnSku);
    }

    [TestMethod]
    public async Task Forged_Signature_Is_Rejected()
    {
        var payload = StripeTestEvents.Event("evt_forged", "charge.refunded", """{"id":"ch_1","object":"charge"}""");
        var forged = StripeTestEvents.Sign(payload, "whsec_attacker");

        await Assert.ThrowsExceptionAsync<StripeException>(() => CreateGateway().HandleWebhookAsync(SignedRequest(payload, forged)));
    }

    [TestMethod]
    public async Task Retry_Signed_Four_Minutes_Ago_Is_Accepted()
    {
        // Regression: a tolerance of 0 seconds rejected every Stripe retry.
        var payload = StripeTestEvents.Event("evt_retry", "charge.refunded", """{"id":"ch_1","object":"charge"}""");
        var signature = StripeTestEvents.Sign(payload, Secret, DateTimeOffset.UtcNow.AddMinutes(-4));

        var e = await CreateGateway().HandleWebhookAsync(SignedRequest(payload, signature));

        Assert.AreEqual("evt_retry", e.eventId);
    }

    [TestMethod]
    public async Task Signature_Older_Than_Five_Minutes_Is_Rejected()
    {
        var payload = StripeTestEvents.Event("evt_stale", "charge.refunded", """{"id":"ch_1","object":"charge"}""");
        var signature = StripeTestEvents.Sign(payload, Secret, DateTimeOffset.UtcNow.AddMinutes(-10));

        await Assert.ThrowsExceptionAsync<StripeException>(() => CreateGateway().HandleWebhookAsync(SignedRequest(payload, signature)));
    }
}
```

- [ ] **Step 3: Run them**

Run: `dotnet test Hackathon-2025.Tests/Hackathon-2025.Tests.csproj --filter "FullyQualifiedName~StripeGatewayWebhookTests"`
Expected: 11 PASS. If a payload fails to deserialize with a Stripe.net error naming a missing/invalid field, add only that field to the JSON (Stripe.net's model for v48 is the source of truth); never loosen an assertion.

- [ ] **Step 4: Commit**

```bash
git add Hackathon-2025.Tests/Utils/StripeTestEvents.cs Hackathon-2025.Tests/Services/StripeGatewayWebhookTests.cs
git commit -m "Added Stripe webhook signature and parsing tests" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Payments controller and webhook application tests

**Files:**
- Create: `Hackathon-2025.Tests/Utils/WebhookEvents.cs`
- Replace: `Hackathon-2025.Tests/Controllers/PaymentsControllerTests.cs` (currently 100% commented out; delete its contents)
- Test: `Hackathon-2025.Tests/SqlServer/WebhookTests.cs`

**Interfaces:**
- Consumes: `TestWebAppFactory.PaymentGatewayMock`, `SqlServerWebAppFactory.CreateAsync()`, `TestData`, `TestClients`.
- Produces: `WebhookEvents.Make(string eventId, int? userId = null, string? customerRef = null, string? subscriptionRef = null, string? planKey = null, string? status = null, DateTime? periodEndUtc = null, DateTime? periodStartUtc = null, DateTime? cancelAtUtc = null, string? addOnSku = null, int addOnQty = 0)` returning the exact tuple type of `IPaymentGateway.HandleWebhookAsync`.

- [ ] **Step 1: Write the tuple helper**

Create `Hackathon-2025.Tests/Utils/WebhookEvents.cs`:

```csharp
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
```

- [ ] **Step 2: Write the in-memory controller tests**

Replace the entire contents of `Hackathon-2025.Tests/Controllers/PaymentsControllerTests.cs` with:

```csharp
using System.Net;
using System.Net.Http.Json;
using Hackathon_2025.Models;
using Hackathon_2025.Services;
using Hackathon_2025.Tests.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Stripe;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class PaymentsControllerTests
{
    private TestWebAppFactory _factory = null!;

    [TestInitialize]
    public void Init() => _factory = new TestWebAppFactory();

    [TestCleanup]
    public void Cleanup() => _factory.Dispose();

    // ---------- create-checkout-session ----------

    [TestMethod]
    public async Task Checkout_Requires_Login()
    {
        var resp = await _factory.AnonymousClient().PostAsJsonAsync("/api/payments/create-checkout-session", new { membership = "Pro" });
        Assert.AreEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [TestMethod]
    public async Task Checkout_For_Free_Plan_Is_Rejected()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/payments/create-checkout-session", new { membership = "Free" });
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task Checkout_With_Existing_Active_Subscription_Is_Conflict()
    {
        var user = TestData.NewUser(MembershipPlan.Pro);
        user.BillingSubscriptionRef = "sub_1";
        user.PlanStatus = "active";
        await _factory.SeedAsync(user);

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/payments/create-checkout-session", new { membership = "Premium" });

        Assert.AreEqual(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [TestMethod]
    public async Task Checkout_After_Canceled_Subscription_Is_Allowed()
    {
        var user = TestData.NewUser();
        user.BillingSubscriptionRef = "sub_old";
        user.PlanStatus = "canceled";
        await _factory.SeedAsync(user);
        _factory.PaymentGatewayMock
            .Setup(g => g.CreateCheckoutSessionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new CheckoutSession("https://checkout.test/s2"));

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/payments/create-checkout-session", new { membership = "Pro" });

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
    }

    [TestMethod]
    public async Task Checkout_Passes_Plan_And_Return_Urls_To_Gateway()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        _factory.PaymentGatewayMock
            .Setup(g => g.CreateCheckoutSessionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new CheckoutSession("https://checkout.test/s1"));

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/payments/create-checkout-session", new { membership = "Pro" });

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        Assert.AreEqual("https://checkout.test/s1", (await resp.ReadJsonAsync()).GetProperty("checkoutUrl").GetString());
        _factory.PaymentGatewayMock.Verify(g => g.CreateCheckoutSessionAsync(
            user.Id, user.Email, "Pro",
            "https://app.test/profile?upgraded=1&plan=pro",
            "https://app.test/upgrade?cancelled=1"), Times.Once);
    }

    // ---------- buy-credits ----------

    [TestMethod]
    public async Task BuyCredits_Is_Premium_Only()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro, booksGenerated: 5));
        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/payments/buy-credits", new { pack = "Plus5", quantity = 1 });
        Assert.AreEqual((HttpStatusCode)403, resp.StatusCode);
    }

    [TestMethod]
    public async Task BuyCredits_Requires_Base_Quota_To_Be_Used_Up()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Premium, booksGenerated: 3));
        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/payments/buy-credits", new { pack = "Plus5", quantity = 1 });
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task BuyCredits_Exhausted_Premium_Gets_Checkout_For_The_Right_Price()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Premium, booksGenerated: 11));
        _factory.PaymentGatewayMock
            .Setup(g => g.CreateOneTimeCheckoutAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new CheckoutSession("https://checkout.test/credits"));

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/payments/buy-credits", new { pack = "Plus11", quantity = 2 });

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        _factory.PaymentGatewayMock.Verify(g => g.CreateOneTimeCheckoutAsync(
            user.Id, user.Email, TestWebAppFactory.PriceIdAddon11, 2,
            "https://app.test/profile?credits=1", "https://app.test/profile?cancelled=1"), Times.Once);
    }

    // ---------- portal / subscription / cancel ----------

    [TestMethod]
    public async Task BillingPortal_Without_Customer_Is_BadRequest()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        _factory.PaymentGatewayMock.Setup(g => g.CreatePortalSessionAsync(user.Id))
            .ThrowsAsync(new InvalidOperationException("No Stripe customer on file."));

        var resp = await _factory.ClientFor(user.Id).GetAsync("/api/payments/billing/portal");

        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task BillingPortal_Returns_Url()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));
        _factory.PaymentGatewayMock.Setup(g => g.CreatePortalSessionAsync(user.Id))
            .ReturnsAsync(new PortalSession("https://portal.test/p1"));

        var resp = await _factory.ClientFor(user.Id).GetAsync("/api/payments/billing/portal");

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        Assert.AreEqual("https://portal.test/p1", (await resp.ReadJsonAsync()).GetProperty("url").GetString());
    }

    [TestMethod]
    public async Task Subscription_Returns_Stored_Billing_State()
    {
        var user = TestData.NewUser(MembershipPlan.Pro);
        user.PlanStatus = "active";
        user.BillingSubscriptionRef = "sub_9";
        user.BillingCustomerRef = "cus_9";
        await _factory.SeedAsync(user);

        var resp = await _factory.ClientFor(user.Id).GetAsync("/api/payments/subscription");

        var sub = (await resp.ReadJsonAsync()).GetProperty("subscription");
        Assert.AreEqual("active", sub.GetProperty("status").GetString());
        Assert.AreEqual("pro", sub.GetProperty("planKey").GetString());
        Assert.AreEqual("sub_9", sub.GetProperty("subscriptionRef").GetString());
        Assert.AreEqual("cus_9", sub.GetProperty("customerRef").GetString());
    }

    [TestMethod]
    public async Task Cancel_Without_Subscription_Is_BadRequest()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        _factory.PaymentGatewayMock.Setup(g => g.CancelAtPeriodEndAsync(user.Id))
            .ThrowsAsync(new InvalidOperationException("No active subscription."));

        var resp = await _factory.ClientFor(user.Id).PostAsync("/api/payments/cancel", content: null);

        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task Cancel_Schedules_Cancellation_Through_Gateway()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));
        _factory.PaymentGatewayMock.Setup(g => g.CancelAtPeriodEndAsync(user.Id)).Returns(Task.CompletedTask);

        var resp = await _factory.ClientFor(user.Id).PostAsync("/api/payments/cancel", content: null);

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        _factory.PaymentGatewayMock.Verify(g => g.CancelAtPeriodEndAsync(user.Id), Times.Once);
    }

    // ---------- webhook paths that never reach the database ----------

    [TestMethod]
    public async Task Webhook_Bad_Signature_Returns_BadRequest()
    {
        _factory.PaymentGatewayMock.Setup(g => g.HandleWebhookAsync(It.IsAny<HttpRequest>()))
            .ThrowsAsync(new StripeException("No signatures found matching the expected signature for payload"));

        var resp = await _factory.AnonymousClient().PostAsync("/api/payments/webhook", new StringContent("{}"));

        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task Webhook_Unexpected_Error_Returns_500()
    {
        _factory.PaymentGatewayMock.Setup(g => g.HandleWebhookAsync(It.IsAny<HttpRequest>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var resp = await _factory.AnonymousClient().PostAsync("/api/payments/webhook", new StringContent("{}"));

        Assert.AreEqual(HttpStatusCode.InternalServerError, resp.StatusCode);
    }

    [TestMethod]
    public async Task Webhook_Non_Actionable_Event_Is_Acknowledged_Without_Recording()
    {
        _factory.PaymentGatewayMock.Setup(g => g.HandleWebhookAsync(It.IsAny<HttpRequest>()))
            .ReturnsAsync(WebhookEvents.Make("evt_ignored", status: "ignored"));

        var resp = await _factory.AnonymousClient().PostAsync("/api/payments/webhook", new StringContent("{}"));

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        Assert.AreEqual(0, await _factory.QueryDbAsync(db => db.ProcessedWebhooks.CountAsync()));
    }
}
```

- [ ] **Step 3: Write the SQL Server webhook tests**

Create `Hackathon-2025.Tests/SqlServer/WebhookTests.cs`:

```csharp
using System.Net;
using Hackathon_2025.Models;
using Hackathon_2025.Tests.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Hackathon_2025.Tests.SqlServer;

[TestClass]
[TestCategory("SqlServer")]
public class WebhookTests
{
    private static readonly DateTime PeriodStart = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodEnd = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    private SqlServerWebAppFactory _factory = null!;

    [TestInitialize]
    public async Task Init() => _factory = await SqlServerWebAppFactory.CreateAsync();

    [TestCleanup]
    public void Cleanup() => _factory?.Dispose();

    private void GatewayReturns(
        (string, int?, string?, string?, string?, string?, DateTime?, DateTime?, DateTime?, string?, int) evt)
        => _factory.PaymentGatewayMock.Setup(g => g.HandleWebhookAsync(It.IsAny<HttpRequest>())).ReturnsAsync(evt);

    private Task<HttpResponseMessage> PostWebhookAsync()
        => _factory.AnonymousClient().PostAsync("/api/payments/webhook", new StringContent("{}"));

    private Task<User> ReloadAsync(int id)
        => _factory.QueryDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == id));

    [TestMethod]
    public async Task Upgrade_Free_To_Pro_Sets_Plan_Status_Period_And_Billing_Refs()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        GatewayReturns(WebhookEvents.Make("evt_up_pro", userId: user.Id, customerRef: "cus_1", subscriptionRef: "sub_1",
            planKey: "pro", status: "active", periodEndUtc: PeriodEnd, periodStartUtc: PeriodStart));

        Assert.AreEqual(HttpStatusCode.OK, (await PostWebhookAsync()).StatusCode);

        var saved = await ReloadAsync(user.Id);
        Assert.AreEqual(MembershipPlan.Pro, saved.Membership);
        Assert.AreEqual("pro", saved.PlanKey);
        Assert.AreEqual("active", saved.PlanStatus);
        Assert.AreEqual("stripe", saved.BillingProvider);
        Assert.AreEqual("cus_1", saved.BillingCustomerRef);
        Assert.AreEqual("sub_1", saved.BillingSubscriptionRef);
        Assert.AreEqual(PeriodStart, saved.CurrentPeriodStartUtc);
        Assert.AreEqual(PeriodEnd, saved.CurrentPeriodEndUtc);
    }

    [TestMethod]
    public async Task Upgrade_With_Unused_Free_Story_Carries_It_Over_As_Credit()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(booksGenerated: 0));
        GatewayReturns(WebhookEvents.Make("evt_carry", userId: user.Id, planKey: "pro", status: "active"));

        await PostWebhookAsync();

        var saved = await ReloadAsync(user.Id);
        Assert.AreEqual(1, saved.AddOnBalance);
        Assert.AreEqual(0, saved.BooksGenerated);
    }

    [TestMethod]
    public async Task Upgrade_After_Using_Free_Story_Gives_No_Carryover_And_Resets_Counters()
    {
        var user = TestData.NewUser(booksGenerated: 1);
        user.AddOnSpentThisPeriod = 1;
        await _factory.SeedAsync(user);
        GatewayReturns(WebhookEvents.Make("evt_nocarry", userId: user.Id, planKey: "premium", status: "active"));

        await PostWebhookAsync();

        var saved = await ReloadAsync(user.Id);
        Assert.AreEqual(MembershipPlan.Premium, saved.Membership);
        Assert.AreEqual(0, saved.AddOnBalance);
        Assert.AreEqual(0, saved.BooksGenerated);
        Assert.AreEqual(0, saved.AddOnSpentThisPeriod);
    }

    [TestMethod]
    public async Task Unknown_Plan_Key_Is_Stored_But_Membership_Unchanged()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));
        GatewayReturns(WebhookEvents.Make("evt_gold", userId: user.Id, planKey: "gold", status: "active"));

        await PostWebhookAsync();

        var saved = await ReloadAsync(user.Id);
        Assert.AreEqual("gold", saved.PlanKey);
        Assert.AreEqual(MembershipPlan.Pro, saved.Membership);
    }

    [DataTestMethod]
    [DataRow("addon_plus5", 3, 15)]
    [DataRow("addon_plus11", 2, 22)]
    [DataRow("addon_unknown", 4, 0)]
    public async Task AddOn_Purchase_Credits_Pack_Size_Times_Quantity(string sku, int qty, int expectedCredits)
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Premium, addOnBalance: 1));
        GatewayReturns(WebhookEvents.Make($"evt_{sku}_{qty}", userId: user.Id, status: "paid", addOnSku: sku, addOnQty: qty));

        Assert.AreEqual(HttpStatusCode.OK, (await PostWebhookAsync()).StatusCode);

        Assert.AreEqual(1 + expectedCredits, (await ReloadAsync(user.Id)).AddOnBalance);
    }

    [TestMethod]
    public async Task User_Is_Found_By_Customer_Ref_When_No_User_Id()
    {
        var user = TestData.NewUser(MembershipPlan.Pro);
        user.BillingCustomerRef = "cus_lookup";
        await _factory.SeedAsync(user);
        GatewayReturns(WebhookEvents.Make("evt_by_cus", customerRef: "cus_lookup", status: "active", periodEndUtc: PeriodEnd));

        await PostWebhookAsync();

        Assert.AreEqual(PeriodEnd, (await ReloadAsync(user.Id)).CurrentPeriodEndUtc);
    }

    [TestMethod]
    public async Task User_Is_Found_By_Subscription_Ref_When_No_User_Or_Customer()
    {
        var user = TestData.NewUser(MembershipPlan.Pro);
        user.BillingSubscriptionRef = "sub_lookup";
        await _factory.SeedAsync(user);
        GatewayReturns(WebhookEvents.Make("evt_by_sub", subscriptionRef: "sub_lookup", planKey: "premium", status: "active"));

        await PostWebhookAsync();

        Assert.AreEqual(MembershipPlan.Premium, (await ReloadAsync(user.Id)).Membership);
    }

    [TestMethod]
    public async Task Unknown_User_Is_Acknowledged_And_Event_Is_Consumed()
    {
        GatewayReturns(WebhookEvents.Make("evt_nobody", userId: 999_999, planKey: "pro", status: "active"));

        Assert.AreEqual(HttpStatusCode.OK, (await PostWebhookAsync()).StatusCode);

        Assert.AreEqual(1, await _factory.QueryDbAsync(db => db.ProcessedWebhooks.CountAsync(w => w.EventId == "evt_nobody")));
    }

    [TestMethod]
    public async Task Cancellation_Records_Status_And_Cancel_Date()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));
        var cancelAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        GatewayReturns(WebhookEvents.Make("evt_cancel", userId: user.Id, status: "canceled", cancelAtUtc: cancelAt));

        await PostWebhookAsync();

        var saved = await ReloadAsync(user.Id);
        Assert.AreEqual("canceled", saved.PlanStatus);
        Assert.AreEqual(cancelAt, saved.CancelAtUtc);
    }

    [TestMethod]
    public async Task Downgrade_To_Free_Keeps_Purchased_AddOn_Balance()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Premium, addOnBalance: 7));
        GatewayReturns(WebhookEvents.Make("evt_downgrade", userId: user.Id, planKey: "free", status: "canceled"));

        await PostWebhookAsync();

        var saved = await ReloadAsync(user.Id);
        Assert.AreEqual(MembershipPlan.Free, saved.Membership);
        Assert.AreEqual(7, saved.AddOnBalance);
    }

    [TestMethod]
    public async Task Same_Event_Delivered_Twice_Is_Applied_Once()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Premium));
        GatewayReturns(WebhookEvents.Make("evt_dupe", userId: user.Id, status: "paid", addOnSku: "addon_plus5", addOnQty: 1));

        Assert.AreEqual(HttpStatusCode.OK, (await PostWebhookAsync()).StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await PostWebhookAsync()).StatusCode);

        Assert.AreEqual(5, (await ReloadAsync(user.Id)).AddOnBalance);
    }

    [TestMethod]
    public async Task Same_Event_Delivered_Concurrently_Is_Applied_Once()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Premium));
        GatewayReturns(WebhookEvents.Make("evt_race", userId: user.Id, status: "paid", addOnSku: "addon_plus5", addOnQty: 1));

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => PostWebhookAsync()));

        Assert.IsTrue(responses.All(r => r.StatusCode == HttpStatusCode.OK),
            "statuses: " + string.Join(",", responses.Select(r => (int)r.StatusCode)));
        Assert.AreEqual(5, (await ReloadAsync(user.Id)).AddOnBalance);
        Assert.AreEqual(1, await _factory.QueryDbAsync(db => db.ProcessedWebhooks.CountAsync(w => w.EventId == "evt_race")));
    }
}
```

- [ ] **Step 4: Run them (Docker running)**

Run: `dotnet test Hackathon-2025.Tests/Hackathon-2025.Tests.csproj --filter "FullyQualifiedName~PaymentsControllerTests|FullyQualifiedName~WebhookTests"`
Expected: all PASS (16 + 15 = 31; the DataTestMethod counts as 3). If `Same_Event_Delivered_Concurrently_Is_Applied_Once` fails on balance (e.g. 10 or 20), that is a real double-credit bug: stop and report. If it fails only because a response was 500 while the balance is still 5, report that too (the idempotency holds, but Stripe would see an error and retry).

- [ ] **Step 5: Commit**

```bash
git add Hackathon-2025.Tests/Utils/WebhookEvents.cs Hackathon-2025.Tests/Controllers/PaymentsControllerTests.cs Hackathon-2025.Tests/SqlServer/WebhookTests.cs
git commit -m "Added payments and webhook idempotency tests" -m "Replaces the commented-out PaymentsControllerTests." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Story generation tests (quota, refunds, gating)

**Files:**
- Modify: `Hackathon-2025.Tests/Controllers/StoryGenerationTests.cs` (add tests to the class created in Task 1)

**Interfaces:**
- Consumes: `FakeStoryGenerator` (`ThrowOnGenerate`, `LastRequest`, `CallCount`), `TestData`, `TestClients`, `ServerErrors`, `Eventually`, `ReadJsonAsync`.

- [ ] **Step 1: Add the tests**

Add these methods inside `StoryGenerationTests` (after the Task 1 test):

```csharp
    private Task<User> ReloadAsync(int id)
        => _factory.QueryDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == id));

    private Task<int> StoryCountAsync(int userId)
        => _factory.QueryDbAsync(db => db.Stories.CountAsync(s => s.UserId == userId));

    [TestMethod]
    public async Task GenerateFull_Free_User_First_Story_Succeeds()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody());

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        Assert.AreEqual(1, (await ReloadAsync(user.Id)).BooksGenerated);
    }

    [TestMethod]
    public async Task GenerateFull_Free_User_Second_Story_Is_Forbidden()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(booksGenerated: 1));

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody());

        Assert.AreEqual((HttpStatusCode)403, resp.StatusCode);
        Assert.AreEqual(0, _factory.StoryGenerator.CallCount);
        Assert.AreEqual(0, await StoryCountAsync(user.Id));
    }

    [TestMethod]
    public async Task GenerateFull_Free_User_With_AddOns_Is_Still_Forbidden_OpenQuestion()
    {
        // Current behavior: Free users are capped at one story even when holding purchased credits
        // (possible after a downgrade, since carryover keeps the balance). Open question in the spec.
        var user = await _factory.SeedAsync(TestData.NewUser(booksGenerated: 1, addOnBalance: 3));

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody());

        Assert.AreEqual((HttpStatusCode)403, resp.StatusCode);
        Assert.AreEqual(3, (await ReloadAsync(user.Id)).AddOnBalance);
    }

    [TestMethod]
    public async Task GenerateFull_Exhausted_Pro_User_Spends_An_AddOn()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro, booksGenerated: 5, addOnBalance: 2));

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody());

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        var saved = await ReloadAsync(user.Id);
        Assert.AreEqual(1, saved.AddOnBalance);
        Assert.AreEqual(1, saved.AddOnSpentThisPeriod);
        Assert.AreEqual(6, saved.BooksGenerated);
    }

    [TestMethod]
    public async Task GenerateFull_Exhausted_Pro_User_Without_AddOns_Is_Forbidden()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro, booksGenerated: 5));

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody());

        Assert.AreEqual((HttpStatusCode)403, resp.StatusCode);
    }

    [TestMethod]
    public async Task GenerateFull_Failure_Deletes_Draft_And_Refunds_Base_Credit()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));
        _factory.StoryGenerator.ThrowOnGenerate = new InvalidOperationException("OpenAI is down");

        await ServerErrors.AssertServerErrorAsync(() =>
            _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody()));

        Assert.AreEqual(0, await StoryCountAsync(user.Id));
        Assert.AreEqual(0, (await ReloadAsync(user.Id)).BooksGenerated);
    }

    [TestMethod]
    public async Task GenerateFull_Failure_Refunds_The_AddOn_It_Reserved()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro, booksGenerated: 5, addOnBalance: 1));
        _factory.StoryGenerator.ThrowOnGenerate = new InvalidOperationException("OpenAI is down");

        await ServerErrors.AssertServerErrorAsync(() =>
            _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody()));

        var saved = await ReloadAsync(user.Id);
        Assert.AreEqual(1, saved.AddOnBalance);
        Assert.AreEqual(0, saved.AddOnSpentThisPeriod);
        Assert.AreEqual(5, saved.BooksGenerated);
        Assert.AreEqual(0, await StoryCountAsync(user.Id));
    }

    [TestMethod]
    public async Task GenerateFull_New_Month_Rolls_Over_Before_Quota_Check()
    {
        var user = TestData.NewUser(MembershipPlan.Pro, booksGenerated: 5);
        user.LastReset = DateTime.UtcNow.AddMonths(-1);
        await _factory.SeedAsync(user);

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody());

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        Assert.AreEqual(1, (await ReloadAsync(user.Id)).BooksGenerated);
    }

    [TestMethod]
    public async Task GenerateFull_With_No_Characters_Is_Rejected_Without_Spending_Credit()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full",
            new { theme = "Space", characters = Array.Empty<object>() });

        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.AreEqual(0, (await ReloadAsync(user.Id)).BooksGenerated);
        Assert.AreEqual(0, _factory.StoryGenerator.CallCount);
    }

    [TestMethod]
    public async Task GenerateFull_Length_Hint_Disabled_Strips_Length_And_PageCount()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Premium));

        await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody(storyLength: "long"));

        Assert.IsNull(_factory.StoryGenerator.LastRequest!.StoryLength);
        Assert.IsNull(_factory.StoryGenerator.LastRequest!.PageCount);
    }

    [DataTestMethod]
    [DataRow(MembershipPlan.Free, "long", "short", 4)]
    [DataRow(MembershipPlan.Pro, "long", "short", 4)]
    [DataRow(MembershipPlan.Pro, "medium", "medium", 8)]
    [DataRow(MembershipPlan.Premium, "long", "long", 12)]
    public async Task GenerateFull_Length_Is_Gated_By_Plan_When_Enabled(MembershipPlan plan, string requested, string expected, int expectedPages)
    {
        using var factory = new TestWebAppFactory(new Dictionary<string, string?> { ["Story:LengthHintEnabled"] = "true" });
        var user = await factory.SeedAsync(TestData.NewUser(plan));

        var resp = await factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody(storyLength: requested));

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        Assert.AreEqual(expected, factory.StoryGenerator.LastRequest!.StoryLength);
        Assert.AreEqual(expectedPages, factory.StoryGenerator.LastRequest!.PageCount);
    }

    [TestMethod]
    public async Task GenerateFull_Free_Character_Fields_Are_Trimmed_And_Extra_Characters_Dropped()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        var fields = new Dictionary<string, string> { ["hairColor"] = "brown", ["favoriteFood"] = "pizza" };

        await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody(fields: fields, characterCount: 2));

        var sent = _factory.StoryGenerator.LastRequest!;
        Assert.AreEqual(1, sent.Characters.Count);
        Assert.IsTrue(sent.Characters[0].DescriptionFields.ContainsKey("hairColor"));
        Assert.IsFalse(sent.Characters[0].DescriptionFields.ContainsKey("favoriteFood"));
    }

    [TestMethod]
    public async Task GenerateFull_Paid_Character_Fields_Are_Kept()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));
        var fields = new Dictionary<string, string> { ["hairColor"] = "brown", ["favoriteFood"] = "pizza" };

        await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full", StoryBody(fields: fields));

        Assert.IsTrue(_factory.StoryGenerator.LastRequest!.Characters[0].DescriptionFields.ContainsKey("favoriteFood"));
    }

    // ---------- async path: generate-full/start + result ----------

    [TestMethod]
    public async Task Start_Then_Result_Returns_The_Finished_Story()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));
        var client = _factory.ClientFor(user.Id);

        var start = await client.PostAsJsonAsync("/api/story/generate-full/start", StoryBody());
        Assert.AreEqual(HttpStatusCode.OK, start.StatusCode);
        var jobId = (await start.ReadJsonAsync()).GetProperty("jobId").GetString();

        string? body = null;
        await Eventually.AssertAsync(async () =>
        {
            var resp = await client.GetAsync($"/api/story/result/{jobId}");
            if (resp.StatusCode != HttpStatusCode.OK) return false;
            body = await resp.Content.ReadAsStringAsync();
            return true;
        }, "the background job should publish a result");

        StringAssert.Contains(body!, "Fake Story");
        Assert.AreEqual(1, await StoryCountAsync(user.Id));
    }

    [TestMethod]
    public async Task Start_Failure_Deletes_Draft_And_Refunds_AddOn()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro, booksGenerated: 5, addOnBalance: 1));
        _factory.StoryGenerator.ThrowOnGenerate = new InvalidOperationException("OpenAI is down");

        var start = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/story/generate-full/start", StoryBody());
        Assert.AreEqual(HttpStatusCode.OK, start.StatusCode);

        await Eventually.AssertAsync(async () =>
        {
            var saved = await ReloadAsync(user.Id);
            return saved.AddOnBalance == 1 && saved.BooksGenerated == 5 && await StoryCountAsync(user.Id) == 0;
        }, "failed background job should refund the add-on and delete the draft");
    }
```

- [ ] **Step 2: Run them**

Run: `dotnet test Hackathon-2025.Tests/Hackathon-2025.Tests.csproj --filter "FullyQualifiedName~StoryGenerationTests"`
Expected: all PASS (1 + 13 methods; the DataTestMethod counts as 4 → 19 results).

- [ ] **Step 3: Commit**

```bash
git add Hackathon-2025.Tests/Controllers/StoryGenerationTests.cs
git commit -m "Added story generation quota, refund, and gating tests" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Endpoint authorization guard + admin tests

**Files:**
- Test: `Hackathon-2025.Tests/Controllers/EndpointAuthorizationTests.cs`, `Controllers/AdminControllerTests.cs`, `Services/AdminAccessServiceTests.cs`

**Interfaces:**
- Consumes: `TestWebAppFactory.AdminEmail`, `TestClients`, `TestData`.

- [ ] **Step 1: Write the endpoint guard**

Create `Hackathon-2025.Tests/Controllers/EndpointAuthorizationTests.cs`:

```csharp
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
```

If `Every_Endpoint_…` fails **only** because an allowlist string's formatting differs from the actual route text (for example the health check reports `get /healthz` instead of `* /healthz`), change that allowlist string to the printed actual value. If it reports an endpoint that is genuinely public but not on the list, that is a finding: stop and report it. Don't add it to the list without approval. If `GetRequiredService<EndpointDataSource>()` throws, replace that expression with `factory.Services.GetServices<EndpointDataSource>().SelectMany(s => s.Endpoints)`.

- [ ] **Step 2: Write the admin tests**

Create `Hackathon-2025.Tests/Services/AdminAccessServiceTests.cs`:

```csharp
using System.Security.Claims;
using Hackathon_2025.Options;
using Hackathon_2025.Services;

namespace Hackathon_2025.Tests.Services;

[TestClass]
public class AdminAccessServiceTests
{
    private static AdminAccessService Create() =>
        new(Microsoft.Extensions.Options.Options.Create(new AdminOptions
        {
            Emails = new List<string> { "A@X.com" },
            EmailsCsv = " b@y.com ; c@z.com"
        }));

    [DataTestMethod]
    [DataRow("a@x.com", true)]
    [DataRow(" B@Y.COM ", true)]
    [DataRow("c@z.com", true)]
    [DataRow("d@z.com", false)]
    [DataRow("", false)]
    [DataRow(null, false)]
    public void IsAdminEmail_Matches_List_And_Csv_Ignoring_Case_And_Spaces(string? email, bool expected)
        => Assert.AreEqual(expected, Create().IsAdminEmail(email));

    [TestMethod]
    public void IsAdmin_Reads_Standard_Or_Short_Email_Claim()
    {
        var standard = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Email, "a@x.com") }, "t"));
        var shortForm = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("email", "b@y.com") }, "t"));
        var none = new ClaimsPrincipal(new ClaimsIdentity(Array.Empty<Claim>(), "t"));

        Assert.IsTrue(Create().IsAdmin(standard));
        Assert.IsTrue(Create().IsAdmin(shortForm));
        Assert.IsFalse(Create().IsAdmin(none));
    }
}
```

Create `Hackathon-2025.Tests/Controllers/AdminControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Hackathon_2025.Models;
using Hackathon_2025.Tests.Utils;
using Microsoft.EntityFrameworkCore;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class AdminControllerTests
{
    private TestWebAppFactory _factory = null!;

    [TestInitialize]
    public void Init() => _factory = new TestWebAppFactory();

    [TestCleanup]
    public void Cleanup() => _factory.Dispose();

    private HttpClient Admin(int userId = 1000) => _factory.ClientFor(userId, TestWebAppFactory.AdminEmail);
    private HttpClient NonAdmin(int userId = 2000) => _factory.ClientFor(userId, "someone@test.local");

    [DataTestMethod]
    [DataRow("GET", "/api/admin/dashboard")]
    [DataRow("GET", "/api/admin/stories/1")]
    [DataRow("PATCH", "/api/admin/users/1")]
    [DataRow("DELETE", "/api/admin/shares/abc")]
    public async Task Admin_Endpoints_Forbid_Non_Admins(string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = method == "PATCH" ? JsonContent.Create(new { membership = "premium" }) : null
        };

        var resp = await NonAdmin().SendAsync(request);

        Assert.AreEqual(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [TestMethod]
    public async Task Dashboard_Works_For_Admin_With_Any_Email_Casing()
    {
        var resp = await _factory.ClientFor(1000, "ADMIN@Test.Local").GetAsync("/api/admin/dashboard");
        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
    }

    [TestMethod]
    public async Task Access_Reports_Admin_Flag()
    {
        Assert.IsTrue((await (await Admin().GetAsync("/api/admin/access")).ReadJsonAsync()).GetProperty("isAdmin").GetBoolean());
        Assert.IsFalse((await (await NonAdmin().GetAsync("/api/admin/access")).ReadJsonAsync()).GetProperty("isAdmin").GetBoolean());
    }

    [TestMethod]
    public async Task UpdateUser_Sets_Membership_And_Manual_Status()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());

        var resp = await Admin().PatchAsJsonAsync($"/api/admin/users/{user.Id}", new { membership = "premium" });

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        var saved = await _factory.QueryDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id));
        Assert.AreEqual(MembershipPlan.Premium, saved.Membership);
        Assert.AreEqual("premium", saved.PlanKey);
        Assert.AreEqual("manual", saved.PlanStatus);
    }

    [TestMethod]
    public async Task UpdateUser_To_Free_Sets_Status_None()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));

        await Admin().PatchAsJsonAsync($"/api/admin/users/{user.Id}", new { membership = "free" });

        var saved = await _factory.QueryDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id));
        Assert.AreEqual(MembershipPlan.Free, saved.Membership);
        Assert.AreEqual("none", saved.PlanStatus);
    }

    [TestMethod]
    public async Task UpdateUser_Rejects_Unknown_Membership()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        var resp = await Admin().PatchAsJsonAsync($"/api/admin/users/{user.Id}", new { membership = "gold" });
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task UpdateUser_Credit_Adjustments_Never_Go_Negative_And_Reset_Works()
    {
        var user = TestData.NewUser(MembershipPlan.Pro, booksGenerated: 4, addOnBalance: 3);
        user.AddOnSpentThisPeriod = 2;
        await _factory.SeedAsync(user);

        await Admin().PatchAsJsonAsync($"/api/admin/users/{user.Id}", new { addOnBalanceDelta = -100, resetPeriodUsage = true });

        var saved = await _factory.QueryDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id));
        Assert.AreEqual(0, saved.AddOnBalance);
        Assert.AreEqual(0, saved.BooksGenerated);
        Assert.AreEqual(0, saved.AddOnSpentThisPeriod);
    }

    [TestMethod]
    public async Task UpdateUser_Unknown_User_Is_NotFound()
    {
        var resp = await Admin().PatchAsJsonAsync("/api/admin/users/999999", new { membership = "pro" });
        Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [TestMethod]
    public async Task GetStory_Includes_Owner()
    {
        var owner = await _factory.SeedAsync(TestData.NewUser());
        var story = await _factory.SeedAsync(TestData.NewStory(owner.Id));

        var json = await (await Admin().GetAsync($"/api/admin/stories/{story.Id}")).ReadJsonAsync();

        Assert.AreEqual(owner.Email, json.GetProperty("ownerEmail").GetString());
    }

    [TestMethod]
    public async Task Admin_RevokeShare_Is_Idempotent()
    {
        var owner = await _factory.SeedAsync(TestData.NewUser());
        var story = await _factory.SeedAsync(TestData.NewStory(owner.Id));
        var share = await _factory.SeedAsync(new StoryShare { StoryId = story.Id, ExpiresUtc = DateTime.UtcNow.AddDays(5) });

        var first = await Admin().DeleteAsync($"/api/admin/shares/{share.Token}");
        var second = await Admin().DeleteAsync($"/api/admin/shares/{share.Token}");

        Assert.AreEqual(HttpStatusCode.NoContent, first.StatusCode);
        Assert.AreEqual(HttpStatusCode.NoContent, second.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await _factory.AnonymousClient().GetAsync($"/api/share/{share.Token}")).StatusCode);
    }

    [TestMethod]
    public async Task Admin_RevokeShare_Unknown_Token_Is_NotFound()
    {
        var resp = await Admin().DeleteAsync("/api/admin/shares/does-not-exist");
        Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
```

- [ ] **Step 3: Run them**

Run: `dotnet test Hackathon-2025.Tests/Hackathon-2025.Tests.csproj --filter "FullyQualifiedName~EndpointAuthorizationTests|FullyQualifiedName~AdminControllerTests|FullyQualifiedName~AdminAccessServiceTests"`
Expected: all PASS (apply only the formatting-only allowlist adjustment described in Step 1 if needed).

- [ ] **Step 4: Commit**

```bash
git add Hackathon-2025.Tests/Controllers/EndpointAuthorizationTests.cs Hackathon-2025.Tests/Controllers/AdminControllerTests.cs Hackathon-2025.Tests/Services/AdminAccessServiceTests.cs
git commit -m "Added endpoint authorization guard and admin tests" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Saved character and sharing tests

**Files:**
- Test: `Hackathon-2025.Tests/Controllers/SavedCharacterControllerTests.cs`, `Controllers/ShareControllerTests.cs`

- [ ] **Step 1: Write the saved-character tests**

Create `Hackathon-2025.Tests/Controllers/SavedCharacterControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Hackathon_2025.Models;
using Hackathon_2025.Tests.Utils;
using Microsoft.EntityFrameworkCore;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class SavedCharacterControllerTests
{
    private TestWebAppFactory _factory = null!;

    [TestInitialize]
    public void Init() => _factory = new TestWebAppFactory();

    [TestCleanup]
    public void Cleanup() => _factory.Dispose();

    private static object Body(string name = "Milo") => new
    {
        character = new
        {
            name,
            role = "main",
            isAnimal = false,
            descriptionFields = new Dictionary<string, string> { ["hairColor"] = "brown", ["favoriteFood"] = "pizza" }
        }
    };

    [TestMethod]
    public async Task Free_User_Save_Trims_Fields_To_Allowlist()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/saved-character/me", Body());

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        var fields = (await resp.ReadJsonAsync()).GetProperty("character").GetProperty("descriptionFields");
        Assert.IsTrue(fields.TryGetProperty("hairColor", out _));
        Assert.IsFalse(fields.TryGetProperty("favoriteFood", out _));
    }

    [TestMethod]
    public async Task Paid_User_Save_Keeps_All_Fields()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/saved-character/me", Body());

        var fields = (await resp.ReadJsonAsync()).GetProperty("character").GetProperty("descriptionFields");
        Assert.IsTrue(fields.TryGetProperty("favoriteFood", out _));
    }

    [DataTestMethod]
    [DataRow(MembershipPlan.Free, 1)]
    [DataRow(MembershipPlan.Pro, 5)]
    [DataRow(MembershipPlan.Premium, 10)]
    public async Task Save_Limit_Per_Plan_Returns_Conflict_When_Full(MembershipPlan plan, int limit)
    {
        var user = await _factory.SeedAsync(TestData.NewUser(plan));
        var client = _factory.ClientFor(user.Id);

        for (var i = 0; i < limit; i++)
            Assert.AreEqual(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/saved-character/me", Body($"C{i}"))).StatusCode);

        var overLimit = await client.PostAsJsonAsync("/api/saved-character/me", Body("One too many"));

        Assert.AreEqual(HttpStatusCode.Conflict, overLimit.StatusCode);
    }

    [TestMethod]
    public async Task Save_Without_Name_Is_BadRequest()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));
        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/saved-character/me", Body("   "));
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task Save_With_Non_Object_Character_Is_BadRequest()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));
        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/saved-character/me", new { character = "Milo" });
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task GetMine_After_Downgrade_Reports_Over_Limit_Without_Deleting()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        for (var i = 0; i < 3; i++)
            await _factory.SeedAsync(new SavedCharacter { UserId = user.Id, Name = $"C{i}", CharacterJson = "{\"name\":\"C\"}" });

        var json = await (await _factory.ClientFor(user.Id).GetAsync("/api/saved-character/me")).ReadJsonAsync();

        Assert.AreEqual(1, json.GetProperty("maxSavedCharacters").GetInt32());
        Assert.AreEqual(3, json.GetProperty("savedCharacterCount").GetInt32());
        Assert.IsTrue(json.GetProperty("isOverLimit").GetBoolean());
        Assert.AreEqual(2, json.GetProperty("overLimitCount").GetInt32());
    }

    [TestMethod]
    public async Task Users_Cannot_See_Update_Or_Delete_Each_Others_Characters()
    {
        var owner = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));
        var other = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));
        var saved = await _factory.SeedAsync(new SavedCharacter { UserId = owner.Id, Name = "Mine", CharacterJson = "{\"name\":\"Mine\"}" });
        var otherClient = _factory.ClientFor(other.Id);

        var list = await (await otherClient.GetAsync("/api/saved-character/me")).ReadJsonAsync();
        var update = await otherClient.PutAsJsonAsync($"/api/saved-character/me/{saved.Id}", Body("Stolen"));
        var delete = await otherClient.DeleteAsync($"/api/saved-character/me/{saved.Id}");

        Assert.AreEqual(0, list.GetProperty("items").GetArrayLength());
        Assert.AreEqual(HttpStatusCode.NotFound, update.StatusCode);
        Assert.AreEqual(HttpStatusCode.NoContent, delete.StatusCode); // current API: silent no-op
        var stillThere = await _factory.QueryDbAsync(db => db.SavedCharacters.AsNoTracking().SingleAsync(c => c.Id == saved.Id));
        Assert.AreEqual("Mine", stillThere.Name);
    }

    [TestMethod]
    public async Task Owner_Can_Update_And_Delete()
    {
        var owner = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));
        var saved = await _factory.SeedAsync(new SavedCharacter { UserId = owner.Id, Name = "Old", CharacterJson = "{\"name\":\"Old\"}" });
        var client = _factory.ClientFor(owner.Id);

        var update = await client.PutAsJsonAsync($"/api/saved-character/me/{saved.Id}", Body("New"));
        Assert.AreEqual("New", (await update.ReadJsonAsync()).GetProperty("name").GetString());

        Assert.AreEqual(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/saved-character/me/{saved.Id}")).StatusCode);
        Assert.AreEqual(0, await _factory.QueryDbAsync(db => db.SavedCharacters.CountAsync(c => c.UserId == owner.Id)));
    }
}
```

- [ ] **Step 2: Write the sharing tests**

Create `Hackathon-2025.Tests/Controllers/ShareControllerTests.cs`:

```csharp
using System.Net;
using Hackathon_2025.Models;
using Hackathon_2025.Tests.Utils;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class ShareControllerTests
{
    private TestWebAppFactory _factory = null!;

    [TestInitialize]
    public void Init() => _factory = new TestWebAppFactory();

    [TestCleanup]
    public void Cleanup() => _factory.Dispose();

    private async Task<(User owner, Story story)> SeedStoryAsync()
    {
        var owner = await _factory.SeedAsync(TestData.NewUser());
        var story = await _factory.SeedAsync(TestData.NewStory(owner.Id));
        return (owner, story);
    }

    [TestMethod]
    public async Task Owner_Creates_Share_With_Default_30_Day_Expiry()
    {
        var (owner, story) = await SeedStoryAsync();

        var json = await (await _factory.ClientFor(owner.Id).PostAsync($"/api/stories/{story.Id}/share", null)).ReadJsonAsync();

        var expires = json.GetProperty("expiresUtc").GetDateTime();
        Assert.IsTrue(Math.Abs((expires - DateTime.UtcNow.AddDays(30)).TotalMinutes) < 2, $"expires {expires:o}");
        StringAssert.Contains(json.GetProperty("url").GetString()!, "/s/" + json.GetProperty("token").GetString());
    }

    [TestMethod]
    public async Task Requested_Days_Override_Default()
    {
        var (owner, story) = await SeedStoryAsync();

        var json = await (await _factory.ClientFor(owner.Id).PostAsync($"/api/stories/{story.Id}/share?days=7", null)).ReadJsonAsync();

        var expires = json.GetProperty("expiresUtc").GetDateTime();
        Assert.IsTrue(Math.Abs((expires - DateTime.UtcNow.AddDays(7)).TotalMinutes) < 2);
    }

    [TestMethod]
    public async Task Creating_Again_Reuses_The_Active_Link()
    {
        var (owner, story) = await SeedStoryAsync();
        var client = _factory.ClientFor(owner.Id);

        var first = (await (await client.PostAsync($"/api/stories/{story.Id}/share", null)).ReadJsonAsync()).GetProperty("token").GetString();
        var second = (await (await client.PostAsync($"/api/stories/{story.Id}/share", null)).ReadJsonAsync()).GetProperty("token").GetString();

        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public async Task Anyone_Can_View_A_Shared_Story_And_It_Is_Not_Indexed()
    {
        var (owner, story) = await SeedStoryAsync();
        var token = (await (await _factory.ClientFor(owner.Id).PostAsync($"/api/stories/{story.Id}/share", null)).ReadJsonAsync()).GetProperty("token").GetString();

        var resp = await _factory.AnonymousClient().GetAsync($"/api/share/{token}");

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        StringAssert.Contains(string.Join(",", resp.Headers.GetValues("X-Robots-Tag")), "noindex");
        Assert.AreEqual(2, (await resp.ReadJsonAsync()).GetProperty("pages").GetArrayLength());
    }

    [TestMethod]
    public async Task Expired_Link_Is_NotFound()
    {
        var (_, story) = await SeedStoryAsync();
        var share = await _factory.SeedAsync(new StoryShare { StoryId = story.Id, ExpiresUtc = DateTime.UtcNow.AddMinutes(-1) });

        var resp = await _factory.AnonymousClient().GetAsync($"/api/share/{share.Token}");

        Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [TestMethod]
    public async Task Revoked_Link_Is_NotFound()
    {
        var (owner, story) = await SeedStoryAsync();
        var share = await _factory.SeedAsync(new StoryShare { StoryId = story.Id, ExpiresUtc = DateTime.UtcNow.AddDays(5) });

        Assert.AreEqual(HttpStatusCode.NoContent, (await _factory.ClientFor(owner.Id).DeleteAsync($"/api/share/{share.Token}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await _factory.AnonymousClient().GetAsync($"/api/share/{share.Token}")).StatusCode);
    }

    [TestMethod]
    public async Task Revoke_Twice_Second_Time_Returns_NotFound()
    {
        var (owner, story) = await SeedStoryAsync();
        var share = await _factory.SeedAsync(new StoryShare { StoryId = story.Id, ExpiresUtc = DateTime.UtcNow.AddDays(5) });
        var client = _factory.ClientFor(owner.Id);

        Assert.AreEqual(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/share/{share.Token}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/share/{share.Token}")).StatusCode);
    }

    [TestMethod]
    public async Task Cannot_Share_Or_Revoke_Someone_Elses_Story()
    {
        var (_, story) = await SeedStoryAsync();
        var share = await _factory.SeedAsync(new StoryShare { StoryId = story.Id, ExpiresUtc = DateTime.UtcNow.AddDays(5) });
        var stranger = await _factory.SeedAsync(TestData.NewUser());
        var client = _factory.ClientFor(stranger.Id);

        Assert.AreEqual(HttpStatusCode.NotFound, (await client.PostAsync($"/api/stories/{story.Id}/share", null)).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/share/{share.Token}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await _factory.AnonymousClient().GetAsync($"/api/share/{share.Token}")).StatusCode);
    }

    [TestMethod]
    public async Task CreateShare_With_Absurd_Days_Currently_Fails_On_Server_OpenQuestion()
    {
        // Current behavior: no cap on "days"; ~3 million days overflows DateTime and throws.
        // Open question in the spec (cap the value, e.g. 365?).
        var (owner, story) = await SeedStoryAsync();

        await ServerErrors.AssertServerErrorAsync(() =>
            _factory.ClientFor(owner.Id).PostAsync($"/api/stories/{story.Id}/share?days=3000000", null));
    }
}
```

- [ ] **Step 3: Run them**

Run: `dotnet test Hackathon-2025.Tests/Hackathon-2025.Tests.csproj --filter "FullyQualifiedName~SavedCharacterControllerTests|FullyQualifiedName~ShareControllerTests"`
Expected: all PASS.

- [ ] **Step 4: Commit**

```bash
git add Hackathon-2025.Tests/Controllers/SavedCharacterControllerTests.cs Hackathon-2025.Tests/Controllers/ShareControllerTests.cs
git commit -m "Added saved character and story sharing tests" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Profile, usage, feedback, and auth gap tests

**Files:**
- Replace: `Hackathon-2025.Tests/Controllers/ProfileControllerTests.cs` (currently fully commented out; delete its contents)
- Test: `Hackathon-2025.Tests/Controllers/UsersControllerTests.cs`, `Controllers/FeedbackControllerTests.cs`
- Modify: `Hackathon-2025.Tests/Controllers/AuthControllerTests.cs` (append two tests)

- [ ] **Step 1: Write the profile tests**

Replace the entire contents of `Hackathon-2025.Tests/Controllers/ProfileControllerTests.cs` with:

```csharp
using System.Net;
using System.Net.Http.Json;
using Hackathon_2025.Models;
using Hackathon_2025.Tests.Utils;
using Microsoft.EntityFrameworkCore;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class ProfileControllerTests
{
    private TestWebAppFactory _factory = null!;

    [TestInitialize]
    public void Init() => _factory = new TestWebAppFactory();

    [TestCleanup]
    public void Cleanup() => _factory.Dispose();

    private Task<User> ReloadAsync(int id) => _factory.QueryDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == id));

    [TestMethod]
    public async Task Me_Returns_Profile_With_Membership_Name()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro, booksGenerated: 2));

        var json = await (await _factory.ClientFor(user.Id).GetAsync("/api/profile/me")).ReadJsonAsync();

        Assert.AreEqual(user.Username, json.GetProperty("username").GetString());
        Assert.AreEqual("Pro", json.GetProperty("membership").GetString());
        Assert.AreEqual(2, json.GetProperty("booksGenerated").GetInt32());
        Assert.IsFalse(json.GetProperty("isAdmin").GetBoolean());
    }

    [TestMethod]
    public async Task Me_Flags_Admins()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(email: TestWebAppFactory.AdminEmail));
        var json = await (await _factory.ClientFor(user.Id).GetAsync("/api/profile/me")).ReadJsonAsync();
        Assert.IsTrue(json.GetProperty("isAdmin").GetBoolean());
    }

    [TestMethod]
    public async Task Avatar_Preset_Is_Saved_And_Unknown_File_Is_Rejected()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        var client = _factory.ClientFor(user.Id);

        Assert.AreEqual(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/api/profile/avatar", new { profileImage = "wizard-avatar.png" })).StatusCode);
        Assert.AreEqual("wizard-avatar.png", (await ReloadAsync(user.Id)).ProfileImage);
        Assert.AreEqual(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/profile/avatar", new { profileImage = "evil.png" })).StatusCode);
    }

    [TestMethod]
    public async Task Avatar_Absolute_Url_Is_Currently_Accepted()
    {
        // Documents current behavior: any absolute URL is allowed as an avatar.
        var user = await _factory.SeedAsync(TestData.NewUser());
        var resp = await _factory.ClientFor(user.Id).PutAsJsonAsync("/api/profile/avatar", new { profileImage = "https://cdn.test/a.png" });
        Assert.AreEqual(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [TestMethod]
    public async Task UpdateUsername_Valid_Name_Is_Saved_Normalized()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());

        var resp = await _factory.ClientFor(user.Id).PutAsJsonAsync("/api/profile/username", new { username = "  new_name  " });

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        var saved = await ReloadAsync(user.Id);
        Assert.AreEqual("new_name", saved.Username);
        Assert.AreEqual("new_name", saved.UsernameNormalized);
    }

    [DataTestMethod]
    [DataRow("ab")]
    [DataRow("has space")]
    [DataRow("this-name-is-way-too-long-x")]
    public async Task UpdateUsername_Invalid_Is_Rejected(string username)
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        var resp = await _factory.ClientFor(user.Id).PutAsJsonAsync("/api/profile/username", new { username });
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task UpdateUsername_Uppercase_Is_Rejected()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());

        var resp = await _factory.ClientFor(user.Id).PutAsJsonAsync("/api/profile/username", new { username = "MILO" });

        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
        StringAssert.Contains(await resp.Content.ReadAsStringAsync(), "3-24 chars");
    }

    [TestMethod]
    public async Task UpdateUsername_Taken_Is_Conflict_But_Own_Name_Is_Fine()
    {
        var taken = await _factory.SeedAsync(TestData.NewUser(username: "takenname"));
        var user = await _factory.SeedAsync(TestData.NewUser(username: "myname"));
        var client = _factory.ClientFor(user.Id);

        Assert.AreEqual(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/profile/username", new { username = "takenname" })).StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/profile/username", new { username = "myname" })).StatusCode);
        Assert.AreEqual("takenname", (await ReloadAsync(taken.Id)).Username);
    }

    [TestMethod]
    public async Task MyStories_Pages_Results_And_Excludes_Others()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        var other = await _factory.SeedAsync(TestData.NewUser());
        for (var i = 0; i < 7; i++) await _factory.SeedAsync(TestData.NewStory(user.Id, title: $"S{i}"));
        await _factory.SeedAsync(TestData.NewStory(other.Id));
        var client = _factory.ClientFor(user.Id);

        var page1 = await (await client.GetAsync("/api/profile/me/stories?page=1&pageSize=6")).ReadJsonAsync();
        var page2 = await (await client.GetAsync("/api/profile/me/stories?page=2&pageSize=6")).ReadJsonAsync();
        var clamped = await (await client.GetAsync("/api/profile/me/stories?page=0&pageSize=500")).ReadJsonAsync();

        Assert.AreEqual(7, page1.GetProperty("total").GetInt32());
        Assert.AreEqual(6, page1.GetProperty("items").GetArrayLength());
        Assert.AreEqual(1, page2.GetProperty("items").GetArrayLength());
        Assert.AreEqual(1, clamped.GetProperty("page").GetInt32());
        Assert.AreEqual(50, clamped.GetProperty("pageSize").GetInt32());
    }

    [TestMethod]
    public async Task MyStory_Returns_Own_Story_And_404_For_Others()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        var other = await _factory.SeedAsync(TestData.NewUser());
        var mine = await _factory.SeedAsync(TestData.NewStory(user.Id));
        var theirs = await _factory.SeedAsync(TestData.NewStory(other.Id));
        var client = _factory.ClientFor(user.Id);

        var own = await client.GetAsync($"/api/profile/me/stories/{mine.Id}");
        Assert.AreEqual(HttpStatusCode.OK, own.StatusCode);
        Assert.AreEqual(2, (await own.ReadJsonAsync()).GetProperty("pages").GetArrayLength());
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/api/profile/me/stories/{theirs.Id}")).StatusCode);
    }
}
```

- [ ] **Step 2: Write the usage and feedback tests**

Create `Hackathon-2025.Tests/Controllers/UsersControllerTests.cs`:

```csharp
using System.Net;
using Hackathon_2025.Models;
using Hackathon_2025.Tests.Utils;
using Microsoft.EntityFrameworkCore;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class UsersControllerTests
{
    private TestWebAppFactory _factory = null!;

    [TestInitialize]
    public void Init() => _factory = new TestWebAppFactory();

    [TestCleanup]
    public void Cleanup() => _factory.Dispose();

    [TestMethod]
    public async Task Usage_For_Pro_User_Counts_Base_And_AddOns()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro, booksGenerated: 2, addOnBalance: 3));

        var json = await (await _factory.ClientFor(user.Id).GetAsync("/api/users/me/usage")).ReadJsonAsync();

        Assert.AreEqual("pro", json.GetProperty("plan").GetString());
        Assert.AreEqual(5, json.GetProperty("baseQuota").GetInt32());
        Assert.AreEqual(2, json.GetProperty("used").GetInt32());
        Assert.AreEqual(3, json.GetProperty("baseRemaining").GetInt32());
        Assert.AreEqual(3, json.GetProperty("addOnBalance").GetInt32());
        Assert.AreEqual(6, json.GetProperty("remaining").GetInt32());
        Assert.IsFalse(json.GetProperty("canBuyAddons").GetBoolean());
    }

    [TestMethod]
    public async Task Usage_For_Exhausted_Premium_Allows_Buying_AddOns()
    {
        // Also pins that appsettings.json quotas resolve for capitalized plan names ("Premium" -> 11).
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Premium, booksGenerated: 11));

        var json = await (await _factory.ClientFor(user.Id).GetAsync("/api/users/me/usage")).ReadJsonAsync();

        Assert.AreEqual(11, json.GetProperty("baseQuota").GetInt32());
        Assert.IsTrue(json.GetProperty("canBuyAddons").GetBoolean());
    }

    [TestMethod]
    public async Task Usage_For_Free_User_Has_One_Story()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        var json = await (await _factory.ClientFor(user.Id).GetAsync("/api/users/me/usage")).ReadJsonAsync();
        Assert.AreEqual(1, json.GetProperty("baseQuota").GetInt32());
    }

    [TestMethod]
    public async Task Usage_In_A_New_Month_Rolls_Over_Counters()
    {
        var user = TestData.NewUser(MembershipPlan.Pro, booksGenerated: 4);
        user.LastReset = DateTime.UtcNow.AddMonths(-1);
        await _factory.SeedAsync(user);

        var json = await (await _factory.ClientFor(user.Id).GetAsync("/api/users/me/usage")).ReadJsonAsync();

        Assert.AreEqual(0, json.GetProperty("used").GetInt32());
        Assert.AreEqual(0, (await _factory.QueryDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id))).BooksGenerated);
    }
}
```

Create `Hackathon-2025.Tests/Controllers/FeedbackControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Hackathon_2025.Tests.Utils;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class FeedbackControllerTests
{
    // Success path is not covered: FeedbackController uses the concrete EmailService (real SMTP/ACS).

    [TestMethod]
    public async Task Feedback_Without_Enjoyment_Is_BadRequest()
    {
        using var factory = new TestWebAppFactory();
        var resp = await factory.AnonymousClient().PostAsJsonAsync("/api/feedback", new { storyTitle = "x" });
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task Feedback_Is_Rate_Limited_After_Three_Requests()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.AnonymousClient();

        for (var i = 0; i < 3; i++)
            Assert.AreEqual(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/feedback", new { })).StatusCode);

        var limited = await client.PostAsJsonAsync("/api/feedback", new { });

        Assert.AreEqual((HttpStatusCode)429, limited.StatusCode);
    }
}
```

- [ ] **Step 3: Append the auth gap tests**

Add these two methods inside `AuthControllerTests` (end of the class). They use the class's existing `_factory`, `_client`, and the existing `SignupRequest` / `LoginRequest` types:

```csharp
    private async Task<string> SignupVerifiedAsync(string email, string username, string password)
    {
        var signup = await _client.PostAsJsonAsync("/api/auth/signup", new SignupRequest { Email = email, Username = username, Password = password });
        Assert.AreEqual(HttpStatusCode.OK, signup.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        user.IsEmailVerified = true;
        await db.SaveChangesAsync();
        return user.Id.ToString();
    }

    [TestMethod]
    public async Task Login_Wrong_Password_And_Unknown_Account_Give_The_Same_Error()
    {
        await SignupVerifiedAsync("enum@x.com", "enumuser", "Pass123!");

        var wrongPassword = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Identifier = "enum@x.com", Password = "Wrong999!" });
        var unknownEmail = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Identifier = "nobody@x.com", Password = "Pass123!" });

        Assert.AreEqual(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.AreEqual(await wrongPassword.Content.ReadAsStringAsync(), await unknownEmail.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task Login_Token_Carries_User_Id_And_Email()
    {
        var userId = await SignupVerifiedAsync("claims@x.com", "claimsuser", "Pass123!");

        var resp = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Identifier = "claims@x.com", Password = "Pass123!" });
        var token = (await ReadJson(resp)).GetProperty("token").GetString();
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);

        // Claim names may be written short ("nameid"/"email") or as full ClaimTypes URIs, depending on how the token is built.
        Assert.AreEqual(userId, jwt.Claims.Single(c => c.Type is "nameid" or System.Security.Claims.ClaimTypes.NameIdentifier).Value);
        Assert.AreEqual("claims@x.com", jwt.Claims.Single(c => c.Type is "email" or System.Security.Claims.ClaimTypes.Email).Value);
    }
```

- [ ] **Step 4: Run them**

Run: `dotnet test Hackathon-2025.Tests/Hackathon-2025.Tests.csproj --filter "FullyQualifiedName~ProfileControllerTests|FullyQualifiedName~UsersControllerTests|FullyQualifiedName~FeedbackControllerTests|FullyQualifiedName~AuthControllerTests"`
Expected: all PASS (existing 13 Auth tests included). If `Usage_For_Exhausted_Premium_…` reports `baseQuota` 0, production quotas are broken: stop and report.

- [ ] **Step 5: Commit**

```bash
git add Hackathon-2025.Tests/Controllers/ProfileControllerTests.cs Hackathon-2025.Tests/Controllers/UsersControllerTests.cs Hackathon-2025.Tests/Controllers/FeedbackControllerTests.cs Hackathon-2025.Tests/Controllers/AuthControllerTests.cs
git commit -m "Added profile, usage, feedback, and login hardening tests" -m "Replaces the commented-out ProfileControllerTests." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Helper and platform tests

**Files:**
- Modify: `Hackathon-2025/Hackathon-2025.csproj` (`InternalsVisibleTo`, build metadata only)
- Test: `Hackathon-2025.Tests/Services/UsernameRulesTests.cs`, `Services/PngImageInspectorTests.cs`, `Services/PromptBuilderTests.cs`, `Controllers/PlatformEndpointTests.cs`

- [ ] **Step 1: Write the tests**

Create `Hackathon-2025.Tests/Services/UsernameRulesTests.cs`:

```csharp
using Hackathon_2025.Services;

namespace Hackathon_2025.Tests.Services;

[TestClass]
public class UsernameRulesTests
{
    [DataTestMethod]
    [DataRow("milo")]
    [DataRow("abc")]
    [DataRow("a.b_c-1")]
    [DataRow(" milo ")]
    [DataRow("abcdefghijklmnopqrstuvwx")] // 24 chars
    public void Valid(string username) => Assert.IsTrue(UsernameRules.IsValid(username));

    [DataTestMethod]
    [DataRow("ab")]
    [DataRow("abcdefghijklmnopqrstuvwxy")] // 25 chars
    [DataRow("Milo")]
    [DataRow("has space")]
    [DataRow("émile")]
    [DataRow("   ")]
    [DataRow(null)]
    public void Invalid(string? username) => Assert.IsFalse(UsernameRules.IsValid(username));

    [TestMethod]
    public void Normalize_Trims_And_Lowercases() => Assert.AreEqual("milo", UsernameRules.Normalize("  MiLo "));
}
```

Create `Hackathon-2025.Tests/Services/PngImageInspectorTests.cs`:

```csharp
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Hackathon_2025.Services;

namespace Hackathon_2025.Tests.Services;

[TestClass]
public class PngImageInspectorTests
{
    /// <summary>Builds a minimal valid PNG (filter 0 rows, zlib IDAT). CRCs are zero: the inspector doesn't check them.</summary>
    private static byte[] Png(int width, int height, byte colorType, Func<int, int, byte[]> pixel, byte bitDepth = 8)
    {
        using var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            for (var x = 0; x < width; x++) raw.Write(pixel(x, y));
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(raw.ToArray());

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = bitDepth;
        header[9] = colorType;

        using var png = new MemoryStream();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        stream.Write(Encoding.ASCII.GetBytes(type));
        stream.Write(data);
        stream.Write(new byte[4]);
    }

    [TestMethod]
    public void All_Black_Image_Is_Suspicious()
    {
        var ok = PngImageInspector.TryAnalyze(Png(4, 4, 2, (_, _) => new byte[] { 0, 0, 0 }), out var stats);

        Assert.IsTrue(ok);
        Assert.AreEqual(4, stats!.Width);
        Assert.AreEqual(4, stats.Height);
        Assert.AreEqual(0, stats.AverageBrightness, 0.001);
        Assert.AreEqual(1.0, stats.NearBlackRatio, 0.001);
        Assert.IsTrue(stats.IsSuspiciouslyDark);
    }

    [TestMethod]
    public void White_Image_Is_Not_Suspicious()
    {
        PngImageInspector.TryAnalyze(Png(4, 4, 2, (_, _) => new byte[] { 255, 255, 255 }), out var stats);

        Assert.AreEqual(255, stats!.AverageBrightness, 0.001);
        Assert.IsFalse(stats.IsSuspiciouslyDark);
    }

    [TestMethod]
    public void Half_Black_Half_White_Is_Not_Suspicious()
    {
        PngImageInspector.TryAnalyze(Png(4, 4, 0, (x, _) => new[] { x < 2 ? (byte)0 : (byte)255 }), out var stats);
        Assert.IsFalse(stats!.IsSuspiciouslyDark);
    }

    [TestMethod]
    public void Non_Png_Bytes_Are_Rejected() => Assert.IsFalse(PngImageInspector.TryAnalyze(Encoding.UTF8.GetBytes("not a png"), out _));

    [TestMethod]
    public void Truncated_Png_Is_Rejected() => Assert.IsFalse(PngImageInspector.TryAnalyze(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, out _));

    [TestMethod]
    public void Sixteen_Bit_Png_Is_Not_Analyzed()
        => Assert.IsFalse(PngImageInspector.TryAnalyze(Png(2, 2, 2, (_, _) => new byte[] { 0, 0, 0 }, bitDepth: 16), out _));
}
```

Create `Hackathon-2025.Tests/Services/PromptBuilderTests.cs`:

```csharp
using Hackathon_2025.Models;
using Hackathon_2025.Services;

namespace Hackathon_2025.Tests.Services;

[TestClass]
public class PromptBuilderTests
{
    private static List<CharacterSpec> Human() => new()
    {
        new CharacterSpec { Name = "Milo", DescriptionFields = new(StringComparer.OrdinalIgnoreCase) { ["age"] = "7", ["gender"] = "boy" } }
    };

    private static List<CharacterSpec> Fox() => new()
    {
        new CharacterSpec { Name = "Rusty", IsAnimal = true, DescriptionFields = new(StringComparer.OrdinalIgnoreCase) { ["species"] = "fox" } }
    };

    [TestMethod]
    public void Known_Art_Style_Is_Used_With_Guardrails()
    {
        var prompt = PromptBuilder.BuildImagePrompt(Human(), "They met a wise owl.", "comic");

        StringAssert.StartsWith(prompt, "Children's comic book illustration");
        StringAssert.Contains(prompt, "Portrait orientation");
        StringAssert.Contains(prompt, "7-year-old boy");
        StringAssert.Contains(prompt, "talking to a wise owl");
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("watercolor")]
    [DataRow("not-a-real-style")]
    public void Missing_Or_Unknown_Style_Falls_Back_To_Watercolor(string? style)
        => StringAssert.StartsWith(PromptBuilder.BuildImagePrompt(Human(), "A walk.", style), "Children's watercolor illustration");

    [TestMethod]
    public void Animal_Character_Uses_Species()
        => StringAssert.Contains(PromptBuilder.BuildImagePrompt(Fox(), "A walk.", "pixel"), "fox");

    [TestMethod]
    public void Base_Character_Prompt_Is_A_Plain_Reference_Portrait()
    {
        var prompt = PromptBuilder.BuildBaseCharacterPrompt(Human(), "clay");

        StringAssert.StartsWith(prompt, "Children's clay animation style illustration");
        StringAssert.Contains(prompt, "plain white background");
        StringAssert.Contains(prompt, "no text");
    }

    [TestMethod]
    public void Cover_Prompt_Includes_Theme_And_No_Text_Rule()
    {
        var prompt = PromptBuilder.BuildCoverPrompt(Human(), "Under the Sea", "early", "gouache");

        StringAssert.Contains(prompt, "Under the Sea");
        StringAssert.Contains(prompt, "gouache");
        StringAssert.Contains(prompt, "no text");
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("network down");
    }

    // FINDING (spec open question): the retry loops use `catch when (attempt < maxAttempts)`, so the
    // last failure escapes and the keyword/static fallback below the loop is unreachable. StoryGenerator
    // calls both methods, so two OpenAI scene failures fail the whole story instead of falling back.
    // These tests pin current behavior; when the fix is approved, flip them to assert the fallback prompt.

    [TestMethod]
    public async Task Image_Prompt_Throws_Instead_Of_Falling_Back_When_Scene_Api_Fails_OpenQuestion()
    {
        await Assert.ThrowsExceptionAsync<HttpRequestException>(() => PromptBuilder.BuildImagePromptAsync(
            Human(), "They met an owl.\r\nThen they rested.", new HttpClient(new FailingHandler()), "test-key", "comic"));
    }

    [TestMethod]
    public async Task Cover_Prompt_Throws_Instead_Of_Falling_Back_When_Scene_Api_Fails_OpenQuestion()
    {
        await Assert.ThrowsExceptionAsync<HttpRequestException>(() => PromptBuilder.BuildCoverPromptAsync(
            Human(), "Under the Sea", "early", "comic", new[] { "They swam.", "They rested." },
            new HttpClient(new FailingHandler()), "test-key"));
    }
}
```

Create `Hackathon-2025.Tests/Controllers/PlatformEndpointTests.cs`:

```csharp
using System.Net;
using System.Xml.Linq;
using Hackathon_2025.Tests.Utils;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class PlatformEndpointTests
{
    [DataTestMethod]
    [DataRow("/healthz")]
    [DataRow("/api/healthz")]
    [DataRow("/__ping")]
    [DataRow("/api/story/ping")]
    [DataRow("/api/config")]
    public async Task Public_Health_And_Config_Endpoints_Respond(string path)
    {
        using var factory = new TestWebAppFactory();
        var resp = await factory.AnonymousClient().GetAsync(path);
        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
    }

    [TestMethod]
    public async Task Sitemap_Is_Valid_Xml_Listing_Public_Pages_Only()
    {
        using var factory = new TestWebAppFactory();

        var resp = await factory.AnonymousClient().GetAsync("/sitemap.xml");

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        StringAssert.Contains(resp.Content.Headers.ContentType!.MediaType!, "xml");
        var xml = XDocument.Parse(await resp.Content.ReadAsStringAsync());
        var locs = xml.Descendants().Where(e => e.Name.LocalName == "loc").Select(e => e.Value).ToList();
        CollectionAssert.Contains(locs, "https://starlitstories.app/about");
        CollectionAssert.Contains(locs, "https://starlitstories.app/blog");
        Assert.IsFalse(locs.Any(l => l.Contains("/profile") || l.Contains("/create") || l.Contains("/admin")));
    }
}
```

- [ ] **Step 2: Run to verify the internal-type test fails to compile**

Run: `dotnet test Hackathon-2025.Tests/Hackathon-2025.Tests.csproj --filter "FullyQualifiedName~PngImageInspectorTests"`
Expected: build FAILS with `CS0122: 'PngImageInspector' is inaccessible due to its protection level`.

- [ ] **Step 3: Expose internals to the test assembly (build metadata only)**

In `Hackathon-2025/Hackathon-2025.csproj`, add this block just before the final `</Project>`:

```xml
  <ItemGroup>
    <InternalsVisibleTo Include="Hackathon-2025.Tests" />
  </ItemGroup>
```

- [ ] **Step 4: Run them**

Run: `dotnet build Hackathon-2025/Hackathon-2025.csproj` (expect `0 Warning(s) 0 Error(s)`), then
`dotnet test Hackathon-2025.Tests/Hackathon-2025.Tests.csproj --filter "FullyQualifiedName~UsernameRulesTests|FullyQualifiedName~PngImageInspectorTests|FullyQualifiedName~PromptBuilderTests|FullyQualifiedName~PlatformEndpointTests"`
Expected: all PASS.

- [ ] **Step 5: Run the whole backend suite**

Run (Docker running): `dotnet test Hackathon-2025.Tests/Hackathon-2025.Tests.csproj`
Expected: `Failed: 0`, total roughly 240–250 results (each data row counts), and no Inconclusive results.

- [ ] **Step 6: Commit**

```bash
git add Hackathon-2025/Hackathon-2025.csproj Hackathon-2025.Tests/Services/UsernameRulesTests.cs Hackathon-2025.Tests/Services/PngImageInspectorTests.cs Hackathon-2025.Tests/Services/PromptBuilderTests.cs Hackathon-2025.Tests/Controllers/PlatformEndpointTests.cs
git commit -m "Added helper and platform endpoint tests" -m "InternalsVisibleTo exposes internal helpers to the test assembly only." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Frontend lint cleanup (behavior-preserving)

**Files (all under `Hackathon-2025/ClientApp/src/`):** `components/DoodlePad.jsx`, `components/PaintingFlightGame.jsx`, `components/StoryCard.jsx`, `components/StoryForm.jsx`, `context/AuthContext.jsx`, `hooks/useWarmup.js`, `pages/CreatePage.jsx`, `pages/ProfilePage.jsx`, `pages/SignupComplete.jsx`, `pages/StoryCustomizePage.jsx`, `pages/StoryViewerPage.jsx`, `pages/SupportPage.jsx`, `utils/downloadStoryPdf.js`

Rules for this task: mechanical changes only. Do not change any hook's dependency array. Line numbers are from commit `de144e5`; locate by the quoted code if lines have shifted.

- [ ] **Step 1: Capture the baseline pre-rendered output**

From `Hackathon-2025/ClientApp`:

```bash
npm ci
npm run build:staging
rm -rf ../../../lint-baseline && mkdir -p ../../../lint-baseline && (cd dist && find . -name index.html -exec cp --parents {} ../../../../lint-baseline/ \;)
npx eslint . 2>&1 | tail -2
```

Expected: last line `✖ 41 problems (31 errors, 10 warnings)`. The baseline copies land in a `lint-baseline/` folder next to the repo (outside git).

- [ ] **Step 2: Fix the latent crash first (`no-undef`)**

`components/StoryForm.jsx:1251`: change `src={char.isAnimal ? animalIcon : personIcon}` to `src={personIcon}`. Leave the commented-out `animalIcon` import on line 14 and the `alt` ternary as they are. (`assets/ui-icons/animal.png` does not exist; any `isAnimal: true` character would throw `ReferenceError` and crash the create page.)

- [ ] **Step 3: Remove dead code and unused bindings (`no-unused-vars`)**

- `pages/ProfilePage.jsx`:
  - Delete the four username-editing state lines 57–60 (`const [editingU, …]`, `const [uname, …]`, `const [uStatus, …]`, `const [savingU, …]`).
  - Delete the whole `const saveUsername = async () => { … };` block (starts line 136, ends at its closing `};` before `const coerceBilling`).
  - Delete the whole `const loadImageAsBase64 = (url) => new Promise(…)` block (starts line 508) and the whole `const downloadAsPDF = async (story) => { … }` block (starts line 523, ends before `const downloadAsImages`). This also removes the empty catches at lines 530 and 536.
  - Line 468: `} catch (err) {` → `} catch {`.
- `pages/SignupComplete.jsx:1`: `import React, { useState, useEffect } from 'react';` → `import React, { useState } from 'react';`
- `pages/StoryViewerPage.jsx`: delete line 15 `const { user } = useAuth();` and then the now-unused import line 7 `import { useAuth } from "../context/AuthContext";`; delete line 87 `const pendingTargetAfterOpen = useRef(null);`; delete line 111 `const flipQueue = useRef([]);` (with its trailing comment); delete line 275 `const spreadNumber = isCover ? 0 : spreadOf(currentPage);`.
- `pages/StoryCustomizePage.jsx`: delete the whole `function onDragAbs(id, nextX, nextY) { … }` (starts line 383); line 1097 remove `, onTextChange` from the `DraggableBox({ … })` parameter list (the parent may keep passing the prop; it's ignored either way).
- `pages/SupportPage.jsx:166`: `({ icon: Icon, title, text })` → `({ title, text })`; line 316: `({ icon: Icon, title, detail, note, href })` → `({ title, detail, note, href })`.

After this step, run `npx eslint .`. If deleting dead code left a **newly** unused binding (for example `setUser` or an import in `ProfilePage.jsx` that was only used by `saveUsername`), remove that binding too, then re-run. Never delete anything that is still referenced.

- [ ] **Step 4: Make intentional empty catches explicit (`no-empty`)**

Replace `catch { }` with `catch { /* best-effort: ignore failure */ }` at:
`components/DoodlePad.jsx:235`, `hooks/useWarmup.js:12`, `pages/ProfilePage.jsx` (the two remaining one-line catches in `downloadAsImages`, formerly lines 557 and 562), `pages/StoryCustomizePage.jsx:1126, 1147, 1206, 1235`, `utils/downloadStoryPdf.js:108`.

- [ ] **Step 5: Remaining small fixes**

- `components/StoryCard.jsx:84`: in the template literal, change `\"${story?.title || "this story"}\"` to `"${story?.title || "this story"}"` (escaping a double quote inside backticks is unnecessary; the string is identical).
- `pages/ProfilePage.jsx` (formerly line 218, the `finally` block in the stories loader): change

  ```js
              } finally {
                  if (!alive) return;
                  setLoading(false);
                  setLoadingMore(false);
              }
  ```

  to

  ```js
              } finally {
                  if (alive) {
                      setLoading(false);
                      setLoadingMore(false);
                  }
              }
  ```

- [ ] **Step 6: Annotate intentional hook dependency lists (warnings, no behavior change)**

Insert this comment on the line directly above each listed dependency array (`}, [ … ]);` line), indented to match:

```js
    // eslint-disable-next-line react-hooks/exhaustive-deps -- intentional: adding deps would change when this effect runs
```

Locations: `components/DoodlePad.jsx:143` and `:156`, `components/PaintingFlightGame.jsx:537`, `pages/CreatePage.jsx:439`, `pages/ProfilePage.jsx` (formerly `:238` and `:294`), `pages/StoryCustomizePage.jsx:327`.

For `pages/StoryCustomizePage.jsx:340-343` (the `currentBoxes` / `selectedBox` `useMemo`), put the same comment, with the reason `-- intentional: recomputing selectedBox each render is cheap and correct`, directly above the `[currentBoxes, selectedBoxId]` line.

For `context/AuthContext.jsx`, insert directly above line 5 (`export const AuthContext = createContext();`) and directly above `export const useAuth = …`:

```js
// eslint-disable-next-line react-refresh/only-export-components -- context and hook intentionally live with the provider
```

- [ ] **Step 7: Verify lint, build, and unchanged pre-rendered pages**

From `Hackathon-2025/ClientApp`:

```bash
npm run lint
npm run build:staging
```

Expected: `npm run lint` exits 0 with no errors (warnings allowed); the build ends with `Removed dist/server (build-time only)`.

Then compare pre-rendered HTML to the baseline, ignoring hashed asset filenames (bundle hashes change whenever any JS changes):

```bash
python - <<'EOF'
import pathlib, re, sys
norm = lambda s: re.sub(r'-[A-Za-z0-9_-]{8}\.(js|css)', '-HASH.\\1', s)
base = pathlib.Path('../../../lint-baseline'); new = pathlib.Path('dist')
bad = [str(p.relative_to(base)) for p in base.rglob('index.html')
       if norm(p.read_text(encoding='utf-8')) != norm((new / p.relative_to(base)).read_text(encoding='utf-8'))]
print('DIFFERENT:', bad) if bad else print('all pre-rendered pages identical')
sys.exit(1 if bad else 0)
EOF
```

Expected: `all pre-rendered pages identical`. If any page differs, inspect the diff. Only a lint edit inside a public page component could cause it; revert that edit and annotate instead.

- [ ] **Step 8: Commit**

```bash
git add Hackathon-2025/ClientApp/src
git commit -m "Fixed frontend lint errors and a latent create-page crash" -m "Removes dead code and unused bindings, documents intentional empty catches and hook dependency lists. StoryForm no longer references the missing animalIcon. No behavior change; pre-rendered pages verified identical." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: CI gate

**Files:**
- Modify: `.github/workflows/api.yml`, `.github/workflows/frontend.yml`

- [ ] **Step 1: Gate the API workflow**

In `.github/workflows/api.yml`:

Replace the `on:` block with:

```yaml
on:
  push:
    branches: [ main, staging ]
  pull_request:
    branches: [ main, staging ]
  workflow_dispatch: {}
```

Insert this job as the first entry under `jobs:`:

```yaml
  test:
    name: Build & test (API)
    runs-on: ubuntu-latest
    env:
      CI: "true"
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore
        run: dotnet restore Hackathon-2025.Tests/Hackathon-2025.Tests.csproj

      - name: Build (warnings are errors in the API project)
        run: dotnet build Hackathon-2025.Tests/Hackathon-2025.Tests.csproj -c Release --no-restore

      - name: Test (includes SQL Server tests via Docker)
        run: dotnet test Hackathon-2025.Tests/Hackathon-2025.Tests.csproj -c Release --no-build --logger "console;verbosity=normal"
```

In `deploy-staging`, add `needs: test` and change its `if:` to:

```yaml
    needs: test
    if: ${{ github.event_name != 'pull_request' && github.ref_name == 'staging' }}
```

In `deploy-prod`, add `needs: test` and change its `if:` to:

```yaml
    needs: test
    if: ${{ github.event_name != 'pull_request' && github.ref_name == 'main' }}
```

- [ ] **Step 2: Gate the frontend workflow**

In `.github/workflows/frontend.yml`:

Replace the `on:` block with:

```yaml
on:
  push:
    branches: [main, staging]
  pull_request:
    branches: [main, staging]
  workflow_dispatch: {}
```

Insert this job as the first entry under `jobs:` (runs only for pull requests; pushes lint inside the deploy jobs):

```yaml
  verify:
    name: Lint & build (frontend)
    if: ${{ github.event_name == 'pull_request' }}
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-node@v4
        with:
          node-version: 20
          cache: npm
          cache-dependency-path: Hackathon-2025/ClientApp/package-lock.json

      - name: Install deps
        working-directory: Hackathon-2025/ClientApp
        run: npm ci

      - name: Lint
        working-directory: Hackathon-2025/ClientApp
        run: npm run lint

      - name: Build (staging config)
        working-directory: Hackathon-2025/ClientApp
        run: npm run build:staging
```

In **both** `deploy-staging` and `deploy-prod`, insert directly after their `Install deps` step:

```yaml
      - name: Lint
        working-directory: Hackathon-2025/ClientApp
        run: npm run lint
```

Leave the existing deploy `if:` conditions as they are (`github.ref == 'refs/heads/…'` is never true for pull requests).

- [ ] **Step 3: Validate the YAML and replay the CI commands locally**

```bash
npx --yes js-yaml .github/workflows/api.yml > /dev/null && npx --yes js-yaml .github/workflows/frontend.yml > /dev/null && echo "yaml ok"
dotnet build Hackathon-2025.Tests/Hackathon-2025.Tests.csproj -c Release
CI=true dotnet test Hackathon-2025.Tests/Hackathon-2025.Tests.csproj -c Release --no-build
(cd Hackathon-2025/ClientApp && npm run lint)
```

Expected: `yaml ok`; build succeeds; tests `Failed: 0` with SQL Server tests executed (not inconclusive); lint exits 0.

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/api.yml .github/workflows/frontend.yml
git commit -m "Added CI gate: build, tests, and lint must pass before deploys" -m "Also runs the same checks on pull requests to main and staging, without deploying." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 13: Integrate on staging, verify CI, report findings

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-ci-gate-and-test-coverage-design.md` (record outcomes under Open questions)

- [ ] **Step 1: Final local verification on the branch**

```bash
dotnet test Hackathon-2025.Tests/Hackathon-2025.Tests.csproj
dotnet publish Hackathon-2025/Hackathon-2025.csproj -c Release -o "$TEMP/publish-final" -p:UseAppHost=false
```

Expected: `Failed: 0`, no Inconclusive (Docker running); publish succeeds.

- [ ] **Step 2: Record findings in the spec**

Under the spec's "Open questions" heading, add one bullet per finding surfaced during Tasks 2–11 (bugs that stopped a task, plus the `_OpenQuestion` tests), each with the proposed fix. Commit:

```bash
git add docs/superpowers/specs/2026-10-07-ci-gate-and-test-coverage-design.md
git commit -m "Recorded test-coverage findings in the design spec" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 3: Merge into staging and push (deploys staging; this is the first gated run)**

Tell the user before pushing, since it deploys staging.

```bash
git switch staging
git pull --ff-only origin staging
git merge --no-ff chore/tests-and-ci -m "Merge chore/tests-and-ci into staging"
git push origin staging
```

- [ ] **Step 4: Watch the gated runs**

```bash
gh run list --branch staging --limit 2
gh run watch <api-run-id> --exit-status
gh run watch <frontend-run-id> --exit-status
```

Expected: the API run shows `Build & test (API)` passing **before** `API deploy (staging)` starts; the frontend run shows the `Lint` step passing before `Build (staging)`. Both conclude `success`. If a run fails, read `gh run view <id> --log-failed`, fix on `chore/tests-and-ci`, merge again, push again.

- [ ] **Step 5: Smoke-check staging**

```bash
API=$(grep '^VITE_API_URL=' Hackathon-2025/ClientApp/.env.staging | cut -d= -f2- | tr -d '\r')
for p in /healthz /readyz /api/healthz /api/config; do printf '%-14s ' $p; curl -s -o /dev/null -w '%{http_code}\n' --max-time 60 "$API$p"; done
```

Expected: all `200` (retry once if the first call times out while the app wakes up).

- [ ] **Step 6: Report to the user**

Summarize:
- test count before/after and how long the suite takes
- the CI gate is live, with the run links
- the frontend lint is clean, and the `animalIcon` crash is removed
- every finding and open question with its proposed fix, for their decision

Remind them to spot-check the create and profile pages on staging, and that promoting `staging` to `main` is theirs.
