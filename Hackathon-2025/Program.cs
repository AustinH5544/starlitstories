using Hackathon_2025.Data;
using Hackathon_2025.Models;
using Hackathon_2025.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenAI;
using System.Text;
using System.Threading.RateLimiting;

// NEW: options
using Hackathon_2025.Options;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    WebRootPath = "ClientApp/dist",
    Args = args
});

// =========================
// Options binding (fail-fast)
// =========================
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
    .ValidateOnStart();

builder.Services.AddOptions<OpenAIOptions>()
    .Bind(builder.Configuration.GetSection("OpenAI"))
    .ValidateOnStart();

builder.Services.AddOptions<AppOptions>()
    .Bind(builder.Configuration.GetSection("App"))
    .ValidateOnStart();

builder.Services.AddSingleton(sp =>
{
    var s = sp.GetRequiredService<IOptions<StripeOptions>>().Value;
    return new Stripe.StripeClient(s.SecretKey);
});

// =========================
// OpenAI client (via Options, with env fallback)
// =========================
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

// =========================
// Database
// =========================
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure(
            maxRetryCount: 10,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null));
});

// =========================
// Identity / hashing
// =========================
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// =========================
// App services
// =========================
builder.Services.AddScoped<IImageGeneratorService, OpenAIImageGeneratorService>();
builder.Services.AddScoped<IStoryGeneratorService, StoryGenerator>();
builder.Services.AddSingleton<BlobUploadService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddSingleton<IProgressBroker, ProgressBroker>();
builder.Services.AddScoped<IQuotaService, QuotaService>();
builder.Services.AddScoped<IPeriodService, PeriodService>();

// =========================
/* Billing / Payments */
// =========================
// Keep Credits options you had:
builder.Services.Configure<CreditsOptions>(builder.Configuration.GetSection("Credits"));

// Provider toggle via config (default: stripe)
var billingProvider = builder.Configuration["Billing:Provider"] ?? "stripe";
if (billingProvider.Equals("stripe", StringComparison.OrdinalIgnoreCase))
{
    // Our provider-agnostic gateway (Stripe implementation)
    builder.Services.AddScoped<IPaymentGateway, StripeGateway>();
}
// else add future providers here

// =========================
// AuthN/Z (JWT via Options)
// =========================
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException("Jwt:Key is missing.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

// =========================
// CORS (from config, not hard-coded)
// =========================
var allowedOrigins = (builder.Configuration["App:AllowedCorsOrigins"] ?? "")
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AppCors", policy =>
        policy.WithOrigins(allowedOrigins.Length > 0 ? allowedOrigins : new[] { "http://localhost:5173" })
              .AllowAnyHeader()
              .AllowAnyMethod()
              //.AllowCredentials()
              );
});

// =========================
// Controllers, API explorer
// =========================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// =========================
// Rate limiting (same as you had)
// =========================
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, token) =>
    {
        ctx.HttpContext.Response.Headers.RetryAfter = "60";
        await ctx.HttpContext.Response.WriteAsync("Too many login attempts. Please try again shortly.", token);
    };

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
});

// =========================
// App config you already had
// =========================
builder.Services.Configure<StoryOptions>(builder.Configuration.GetSection("Story"));

// =========================
// Build
// =========================
var app = builder.Build();

// =========================
// Security headers / HTTPS
// =========================
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();           // NEW
}
app.UseHttpsRedirection();   // NEW

// =========================
// Static files & routing
// =========================
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AppCors");      // CHANGED (use config-based CORS)

// =========================
// Rate limit + Auth
// =========================
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// =========================
// Health endpoints (NEW)
// =========================
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
app.MapFallbackToFile("/index.html");

app.Run();