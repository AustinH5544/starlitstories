using System.Text;
using System.Threading.RateLimiting;
using System.Xml;

using Azure.Identity;
using Azure.Extensions.AspNetCore.Configuration.Secrets;

using Hackathon_2025.Data;
using Hackathon_2025.Models;                 // for User (password hasher)
using Hackathon_2025.Options;
using Hackathon_2025.Services;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;         // for IPasswordHasher<User>
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.RateLimiting;     // for .DisableRateLimiting()

using OpenAI;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------
// Configuration (Key Vault first)
// -----------------------------
var env = builder.Environment.EnvironmentName; // Development | Staging | Production
var vaultName = builder.Configuration["KeyVault:VaultName"] ?? env switch
{
    "Development" => "kv-starlitstories-dev",
    "Staging" => "kv-starlitstories-dev",
    _ => "kv-starlitstories-prod"
};

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri($"https://{vaultName}.vault.azure.net/"),
        new DefaultAzureCredential());
}

// -----------------------------
// Options binding + validation
// -----------------------------
builder.Services.AddOptions<StripeOptions>()
    .Bind(builder.Configuration.GetSection("Stripe"))
    .ValidateOnStart();

builder.Services.AddOptions<AzureBlobStorageOptions>()
    .Bind(builder.Configuration.GetSection("AzureBlobStorage"))
    .ValidateOnStart();

builder.Services.AddOptions<EmailOptions>()
    .Bind(builder.Configuration.GetSection("Email"))
    .ValidateOnStart();

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .Validate(j => !string.IsNullOrWhiteSpace(j.Key), "Jwt:Key must be set")
    .ValidateOnStart();

builder.Services.AddOptions<OpenAIOptions>()
    .Bind(builder.Configuration.GetSection("OpenAI"))
    .ValidateOnStart();

builder.Services.AddOptions<AppOptions>()
    .Bind(builder.Configuration.GetSection("App"))
    .ValidateOnStart();

builder.Services.Configure<CreditsOptions>(builder.Configuration.GetSection("Credits"));
builder.Services.Configure<StoryOptions>(builder.Configuration.GetSection("Story"));

// Stripe client (required by StripeGateway)
builder.Services.AddSingleton<StripeClient>(sp =>
{
    var s = sp.GetRequiredService<IOptions<StripeOptions>>().Value;
    if (string.IsNullOrWhiteSpace(s.SecretKey))
        throw new InvalidOperationException("Stripe:SecretKey must be configured.");
    return new StripeClient(s.SecretKey);
});

// OpenAI client (with env fallback)
builder.Services.AddSingleton(sp =>
{
    var opts = sp.GetRequiredService<IOptions<OpenAIOptions>>().Value;
    var key = string.IsNullOrWhiteSpace(opts.ApiKey)
        ? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        : opts.ApiKey;
    if (string.IsNullOrWhiteSpace(key))
        throw new InvalidOperationException("OpenAI API key is missing.");
    return new OpenAIClient(key);
});
builder.Services.AddHttpClient();

// -----------------------------
// Database (EF Core)
// -----------------------------
if (builder.Environment.IsEnvironment("Testing"))
{
    // For integration tests
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseInMemoryDatabase("tests-db"));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        options.UseSqlServer(
            connectionString,
            sql => sql.EnableRetryOnFailure(
                maxRetryCount: 10,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null));
    });
}

// -----------------------------
// Auth (JWT) — no ASP.NET Identity
// -----------------------------
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
var jwtIssuer = jwtSection["Issuer"];
var jwtAudience = jwtSection["Audience"];

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrWhiteSpace(jwtIssuer),
            ValidateAudience = !string.IsNullOrWhiteSpace(jwtAudience),
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

// -----------------------------
// App services (your implementations)
// -----------------------------
builder.Services.AddScoped<IImageGeneratorService, OpenAIImageGeneratorService>();
builder.Services.AddScoped<IStoryGeneratorService, StoryGenerator>();
builder.Services.AddSingleton<BlobUploadService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddSingleton<IProgressBroker, ProgressBroker>();
builder.Services.AddScoped<IQuotaService, QuotaService>();
builder.Services.AddScoped<IPeriodService, PeriodService>();
builder.Services.AddHealthChecks();

// Payments provider toggle (default: stripe)
var billingProvider = builder.Configuration["Billing:Provider"] ?? "stripe";
if (billingProvider.Equals("stripe", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<IPaymentGateway, StripeGateway>();
}

// -----------------------------
// CORS (temporary explicit allowlist)
// -----------------------------
string[] ParseCors(string? raw) =>
    string.IsNullOrWhiteSpace(raw)
        ? Array.Empty<string>()
        : raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

// Keep reading from config, but if it's empty, fall back to these:
var appOptions = builder.Configuration.GetSection("App").Get<AppOptions>() ?? new AppOptions();
var corsOriginsFromConfig = ParseCors(appOptions.AllowedCorsOrigins);

var allowedOrigins = corsOriginsFromConfig.Length > 0
    ? corsOriginsFromConfig
    : new[] {
        "https://staging.starlitstories.app",
        "http://localhost:5173"
      };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AppCors", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// -----------------------------
// Rate limiting (your login policy + a global cap if desired)
// -----------------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, token) =>
    {
        ctx.HttpContext.Response.Headers.RetryAfter = "60";
        await ctx.HttpContext.Response.WriteAsync("Too many login attempts. Please try again shortly.", token);
    };

    // per-IP limiter for login route (match in endpoint with RequireRateLimiting("login-ip"))
    options.AddPolicy("login-ip", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    // Optional: a light global limiter
    // options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(...)
});

builder.Services.AddControllers();
// builder.Services.AddEndpointsApiExplorer(); // only needed if you add Swagger later
builder.Services.AddResponseCompression(o => o.EnableForHttps = true);

// =========================
// Build
// =========================
var app = builder.Build();

app.UseResponseCompression();

var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
logger.LogInformation("CORS origins: {origins}", string.Join(", ", allowedOrigins));

// =========================
// Security headers / HTTPS
// =========================
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();

// =========================
// Static files & routing
// =========================
//app.UseDefaultFiles();
//app.UseStaticFiles();
app.UseRouting();
app.UseCors("AppCors");

// =========================
// Rate limit + Auth
// =========================
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// =========================
// Health endpoints
// =========================
app.MapGet("/__ping", () => Results.Text("pong"));
app.MapHealthChecks("/healthz");

app.MapMethods("/api/{**catchall}", new[] { "OPTIONS" }, () => Results.Ok())
   .RequireCors("AppCors");

//app.MapGet("/healthz", () => Results.Ok("ok"));
app.MapGet("/readyz", async (AppDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();
    return canConnect ? Results.Ok("ready") : Results.StatusCode(503);
});

// =========================
// Warm-up Endpoints
// =========================

// Quick health check (pings app container)
app.MapGet("/api/healthz", () => Results.Ok(new { ok = true }))
   .AllowAnonymous();

// DB + EF Core warm-up (pings SQL + builds model)
app.MapPost("/api/warmup", async (AppDbContext db, ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("Warmup");

    try
    {
        // Touch database connection (super-cheap)
        await db.Database.ExecuteSqlRawAsync("SELECT 1");
        logger.LogInformation("SQL warm-up successful");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "SQL warm-up failed");
    }

    try
    {
        // Force EF model load (cached for lifetime)
        await db.Users.AsNoTracking().FirstOrDefaultAsync();
        logger.LogInformation("EF warm-up successful");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "EF warm-up failed");
    }

    return Results.NoContent();
}).AllowAnonymous();

// =========================
// Dynamic Sitemap
// =========================

// spec limits: 50,000 URLs or 50MB per file; chunk at 10k for safety
const int MaxUrlsPerFile = 10000;

app.MapGet("/sitemap.xml", async (AppDbContext db, IConfiguration cfg) =>
{
    var baseUrl = (cfg["Site:BaseUrl"] ?? "https://starlitstories.app").TrimEnd('/');

    // Static routes you want indexed:
    var staticUrls = new[]
    {
        $"{baseUrl}/",
        $"{baseUrl}/stories",
        $"{baseUrl}/pricing",
        $"{baseUrl}/about",
        $"{baseUrl}/contact"
    };

    var allUrls = new List<(string loc, DateTime? lastmod)>();
    allUrls.AddRange(staticUrls.Select(u => (u, (DateTime?)null)));

    // Variant A: StoryShare table (Token + optional IsPublic/IsListed + ExpiresAt)
    bool addedShares = false;
    try
    {
        var shares = await db.Set<StoryShare>()
            .AsNoTracking()
            .Include(s => s.Story) // if you need UpdatedAt from Story
            .Where(s =>
                s.Token != null &&
                (EF.Property<bool?>(s, "IsPublic") ?? EF.Property<bool?>(s, "IsListed") ?? true) &&
                (EF.Property<DateTime?>(s, "ExpiresAt") == null || EF.Property<DateTime?>(s, "ExpiresAt") > DateTime.UtcNow))
            .OrderByDescending(s => EF.Property<DateTime?>(s.Story!, "UpdatedAt") ?? DateTime.UtcNow)
            .Select(s => new
            {
                Url = $"{baseUrl}/s/{s.Token}",
                LastMod = EF.Property<DateTime?>(s.Story!, "UpdatedAt")
            })
            .ToListAsync();

        if (shares.Count > 0)
        {
            allUrls.AddRange(shares.Select(x => (x.Url, x.LastMod)));
            addedShares = true;
        }
    }
    catch
    {
        // if StoryShare doesn't exist, we fall back to Variant B
    }

    // Variant B: token on Story (ShareToken + IsPublic)
    if (!addedShares)
    {
        try
        {
            var stories = await db.Set<Story>()
                .AsNoTracking()
                .Where(s =>
                    EF.Property<string?>(s, "ShareToken") != null &&
                    (EF.Property<bool?>(s, "IsPublic") ?? true))
                .OrderByDescending(s => EF.Property<DateTime?>(s, "UpdatedAt") ?? DateTime.UtcNow)
                .Select(s => new
                {
                    Url = $"{baseUrl}/s/{EF.Property<string>(s, "ShareToken")}",
                    LastMod = EF.Property<DateTime?>(s, "UpdatedAt")
                })
                .ToListAsync();

            allUrls.AddRange(stories.Select(x => (x.Url, x.LastMod)));
        }
        catch
        {
            // neither variant present; serve static routes only
        }
    }

    // Return index if we exceed max per file
    if (allUrls.Count > MaxUrlsPerFile)
    {
        var chunks = allUrls
            .Select((u, i) => new { u, i })
            .GroupBy(x => x.i / MaxUrlsPerFile, x => x.u)
            .Select(g => g.ToList())
            .ToList();

        var indexXml = await BuildSitemapIndexXmlAsync(baseUrl, chunks.Count);
        return Results.Content(indexXml, "application/xml", Encoding.UTF8);
    }

    var xml = await BuildSitemapXmlAsync(allUrls);
    return Results.Content(xml, "application/xml", Encoding.UTF8);
})
.Produces(statusCode: 200, contentType: "application/xml")
.WithMetadata(new ResponseCacheAttribute { Duration = 3600, Location = ResponseCacheLocation.Any })
.AllowAnonymous()
.DisableRateLimiting();

app.MapGet("/sitemaps/sitemap-{index}.xml", async (int index, AppDbContext db, IConfiguration cfg) =>
{
    var baseUrl = (cfg["Site:BaseUrl"] ?? "https://starlitstories.app").TrimEnd('/');

    var staticUrls = new[]
    {
        $"{baseUrl}/",
        $"{baseUrl}/stories",
        $"{baseUrl}/pricing",
        $"{baseUrl}/about",
        $"{baseUrl}/contact"
    };

    var allUrls = new List<(string loc, DateTime? lastmod)>();
    allUrls.AddRange(staticUrls.Select(u => (u, (DateTime?)null)));

    bool addedShares = false;
    try
    {
        var shares = await db.Set<StoryShare>()
            .AsNoTracking()
            .Include(s => s.Story)
            .Where(s =>
                s.Token != null &&
                (EF.Property<bool?>(s, "IsPublic") ?? EF.Property<bool?>(s, "IsListed") ?? true) &&
                (EF.Property<DateTime?>(s, "ExpiresAt") == null || EF.Property<DateTime?>(s, "ExpiresAt") > DateTime.UtcNow))
            .OrderByDescending(s => EF.Property<DateTime?>(s.Story!, "UpdatedAt") ?? DateTime.UtcNow)
            .Select(s => new
            {
                Url = $"{baseUrl}/s/{s.Token}",
                LastMod = EF.Property<DateTime?>(s.Story!, "UpdatedAt")
            })
            .ToListAsync();

        if (shares.Count > 0)
        {
            allUrls.AddRange(shares.Select(x => (x.Url, x.LastMod)));
            addedShares = true;
        }
    }
    catch { }

    if (!addedShares)
    {
        try
        {
            var stories = await db.Set<Story>()
                .AsNoTracking()
                .Where(s =>
                    EF.Property<string?>(s, "ShareToken") != null &&
                    (EF.Property<bool?>(s, "IsPublic") ?? true))
                .OrderByDescending(s => EF.Property<DateTime?>(s, "UpdatedAt") ?? DateTime.UtcNow)
                .Select(s => new
                {
                    Url = $"{baseUrl}/s/{EF.Property<string>(s, "ShareToken")}",
                    LastMod = EF.Property<DateTime?>(s, "UpdatedAt")
                })
                .ToListAsync();

            allUrls.AddRange(stories.Select(x => (x.Url, x.LastMod)));
        }
        catch { }
    }

    var skip = index * MaxUrlsPerFile;
    var page = allUrls.Skip(skip).Take(MaxUrlsPerFile).ToList();
    if (page.Count == 0) return Results.NotFound();

    var xml = await BuildSitemapXmlAsync(page);
    return Results.Content(xml, "application/xml", Encoding.UTF8);
})
.Produces(statusCode: 200, contentType: "application/xml")
.AllowAnonymous()
.DisableRateLimiting();

// ---------- helpers ----------
static async Task<string> BuildSitemapXmlAsync(List<(string loc, DateTime? lastmod)> urls)
{
    var settings = new XmlWriterSettings { Async = true, Indent = true, Encoding = Encoding.UTF8 };
    using var sw = new StringWriter();
    using (var xw = XmlWriter.Create(sw, settings))
    {
        await xw.WriteStartDocumentAsync();
        await xw.WriteStartElementAsync(null, "urlset", "https://www.sitemaps.org/schemas/sitemap/0.9");

        foreach (var (loc, lastmod) in urls)
        {
            await xw.WriteStartElementAsync(null, "url", null);
            await xw.WriteElementStringAsync(null, "loc", null, loc);
            if (lastmod.HasValue)
                await xw.WriteElementStringAsync(null, "lastmod", null, lastmod.Value.ToString("yyyy-MM-dd"));
            await xw.WriteEndElementAsync(); // url
        }

        await xw.WriteEndElementAsync(); // urlset
        await xw.WriteEndDocumentAsync();
    }
    return sw.ToString();
}

static async Task<string> BuildSitemapIndexXmlAsync(string baseUrl, int chunks)
{
    var settings = new XmlWriterSettings { Async = true, Indent = true, Encoding = Encoding.UTF8 };
    using var sw = new StringWriter();
    using (var xw = XmlWriter.Create(sw, settings))
    {
        await xw.WriteStartDocumentAsync();
        await xw.WriteStartElementAsync(null, "sitemapindex", "https://www.sitemaps.org/schemas/sitemap/0.9");

        for (int i = 0; i < chunks; i++)
        {
            await xw.WriteStartElementAsync(null, "sitemap", null);
            await xw.WriteElementStringAsync(null, "loc", null, $"{baseUrl}/sitemaps/sitemap-{i}.xml");
            await xw.WriteElementStringAsync(null, "lastmod", null, DateTime.UtcNow.ToString("yyyy-MM-dd"));
            await xw.WriteEndElementAsync();
        }

        await xw.WriteEndElementAsync();
        await xw.WriteEndDocumentAsync();
    }
    return sw.ToString();
}

// =========================
// MVC + SPA fallback
// =========================
app.MapControllers();
//app.MapFallbackToFile("/index.html");

app.Run();

public partial class Program { }