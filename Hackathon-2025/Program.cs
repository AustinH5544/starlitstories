using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Hackathon_2025.Data;
using Hackathon_2025.Models;
using Hackathon_2025.Services;
using Stripe.Checkout;
using OpenAI;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    WebRootPath = "ClientApp/dist",
    Args = args
});

// Load OpenAI API Key
var apiKey = builder.Configuration["OpenAI:ApiKey"]
           ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("OpenAI API key is missing.");
}

// Database (SQL Server)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Password hashing
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// OpenAI config
builder.Services.Configure<OpenAISettings>(builder.Configuration.GetSection("OpenAI"));
builder.Services.AddSingleton(_ => new OpenAIClient(apiKey));
builder.Services.AddHttpClient();

// Custom services
builder.Services.AddScoped<IImageGeneratorService, OpenAIImageGeneratorService>();
builder.Services.AddScoped<IStoryGeneratorService, StoryGenerator>();
builder.Services.AddSingleton<BlobUploadService>();

// Stripe config
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));
builder.Services.AddScoped<SessionService>();

// JWT Authentication setup
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

// CORS for frontend dev servers
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy
            .WithOrigins("http://localhost:5173", "http://localhost:5174")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer(); // for Swagger or minimal APIs

var app = builder.Build();

// Middleware pipeline
app.UseCors("AllowFrontend");

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication(); //  MUST come before UseAuthorization
app.UseAuthorization();

app.MapControllers();
app.MapFallbackToFile("/index.html");

app.Run();