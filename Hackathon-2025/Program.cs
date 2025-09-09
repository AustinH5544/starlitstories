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

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    WebRootPath = "ClientApp/dist",
    Args = args
});

// --- OpenAI ---
var apiKey = builder.Configuration["OpenAI:ApiKey"]
           ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
    throw new InvalidOperationException("OpenAI API key is missing.");

// --- Database ---
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure(
            maxRetryCount: 10,                    // retry a few times
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null));
});

// --- Identity / hashing ---
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// --- OpenAI DI ---
builder.Services.Configure<OpenAISettings>(builder.Configuration.GetSection("OpenAI"));
builder.Services.AddSingleton(_ => new OpenAIClient(apiKey));
builder.Services.AddHttpClient();

// --- App services ---
builder.Services.AddScoped<IImageGeneratorService, OpenAIImageGeneratorService>();
builder.Services.AddScoped<IStoryGeneratorService, StoryGenerator>();
builder.Services.AddSingleton<BlobUploadService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddSingleton<IProgressBroker, ProgressBroker>();

// --- Billing / Payments ---
// Bind Stripe settings (now also includes WebhookSecret and plan price IDs)
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));  // keep this

// Choose billing provider via config: "stripe" (default) or another later
var billingProvider = builder.Configuration["Billing:Provider"] ?? "stripe";
if (billingProvider.Equals("stripe", StringComparison.OrdinalIgnoreCase))
{
    // Our provider-agnostic gateway (Stripe implementation)
    builder.Services.AddScoped<IPaymentGateway, StripeGateway>();
}
//else
//{
//    // Placeholder for future provider
//    builder.Services.AddScoped<IPaymentGateway, OtherPayGateway>();
//}

// --- AuthN/Z ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
            )
        };
    });
builder.Services.AddAuthorization();

// --- CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy
            .WithOrigins("http://localhost:5173", "http://localhost:5174")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// --- Rate limiting ---
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

// --- App config / options ---
builder.Services.Configure<StoryOptions>(builder.Configuration.GetSection("Story"));

// --- Build / Pipeline ---
var app = builder.Build();

app.UseCors("AllowFrontend");
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapFallbackToFile("/index.html");

app.Run();
