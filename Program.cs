using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Hackathon_2025.Data;
using Hackathon_2025.Models;
using Hackathon_2025.Services;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);

// Load OpenAI API Key
var apiKey = builder.Configuration["OpenAI:ApiKey"]
           ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("OpenAI API key is missing.");
}

// EF Core (SQLite)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Password hashing
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// OpenAI config
builder.Services.Configure<OpenAISettings>(builder.Configuration.GetSection("OpenAI"));
builder.Services.AddSingleton(_ => new OpenAIClient(apiKey));
builder.Services.AddHttpClient();
builder.Services.AddScoped<IStoryGeneratorService, OpenAIStoryGenerator>();

// Enable CORS for React dev server
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer(); // required for minimal APIs

var app = builder.Build();

// Middleware
app.UseCors("AllowFrontend");

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.MapControllers();
app.MapFallbackToFile("/index.html");

app.Run();
