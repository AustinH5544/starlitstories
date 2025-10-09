using System.Text;
using System.Threading.RateLimiting;

using Azure.Identity;
using Azure.Extensions.AspNetCore.Configuration.Secrets;

using Hackathon_2025.Data;
using Hackathon_2025.Models;                 // for User (password hasher)
using Hackathon_2025.Options;
using Hackathon_2025.Services;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;         // for IPasswordHasher<User>
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;

using OpenAI;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------
// Configuration (Key Vault first)
// -----------------------------
var vaultName =
    builder.Configuration["KeyVault:VaultName"] ??
    (builder.Environment.IsDevelopment()
        ? "kv-starlitstories-dev"    // dev vault (you set App Setting KeyVault__VaultName too)
        : "kv-starlitstories-prod"); // prod vault

builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{vaultName}.vault.azure.net/"),
    new DefaultAzureCredential());

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
builder.Services.AddSingleton<IProgressBroker, ProgressBroker>();
builder.Services.AddScoped<IQuotaService, QuotaService>();
builder.Services.AddScoped<IPeriodService, PeriodService>();
builder.Services.AddHealthChecks();

// Payments provider toggle (default: stripe)
var billingProvider = builder.Configuration["Billing:Provider"] ?? "stripe";
if (billingProvider.Equals("stripe", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<IPaymentGateway, StripeGateway>();
    // (add other providers here in the future)
}

//// -----------------------------
//// CORS (from App:AllowedCorsOrigins; semicolon-separated)
//// -----------------------------
//string[] ParseCors(string? raw) =>
//    string.IsNullOrWhiteSpace(raw)
//        ? Array.Empty<string>()
//        : raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

//var appOptions = builder.Configuration.GetSection("App").Get<AppOptions>() ?? new AppOptions();
//var corsOrigins = ParseCors(appOptions.AllowedCorsOrigins);

//builder.Services.AddCors(options =>
//{
//    options.AddPolicy("AppCors", policy =>
//        policy.WithOrigins(corsOrigins.Length > 0 ? corsOrigins : new[] { "http://localhost:5173" })
//              .AllowAnyHeader()
//              .AllowAnyMethod());
//    // .AllowCredentials()  // add if needed AND using specific origins
//});

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
              .AllowAnyMethod()
        // .AllowCredentials() // ONLY if you use cookies for auth
        );
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
    // options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    // {
    //     var key = httpContext.User.Identity?.Name
    //               ?? httpContext.Connection.RemoteIpAddress?.ToString()
    //               ?? "anon";
    //     return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
    //     {
    //         PermitLimit = 120,
    //         Window = TimeSpan.FromMinutes(1),
    //         QueueLimit = 0
    //     });
    // });
});

builder.Services.AddControllers();
// builder.Services.AddEndpointsApiExplorer(); // only needed if you add Swagger later

// =========================
// Build
// =========================
var app = builder.Build();

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
app.MapHealthChecks("/healthz");
app.MapGet("/healthz", () => Results.Ok("ok"));
app.MapGet("/readyz", async (AppDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();
    return canConnect ? Results.Ok("ready") : Results.StatusCode(503);
});

// =========================
// MVC + SPA fallback
// =========================
app.MapControllers();
//app.MapFallbackToFile("/index.html");

app.Run();