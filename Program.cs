using Microsoft.AspNetCore.Mvc;
using OpenAI;
using OpenAI.Chat;
using Hackathon_2025.Models; // For OpenAISettings model
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Load API key from appsettings.json or environment variable
var apiKey = builder.Configuration["OpenAI:ApiKey"]
           ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

// Optional: throw if missing
if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("OpenAI API key is missing. Set it in appsettings.json or as an environment variable.");
}

// Register OpenAI client using the API key
builder.Services.AddSingleton(_ => new OpenAIClient(apiKey));

// Register OpenAISettings configuration binding
builder.Services.Configure<OpenAISettings>(
    builder.Configuration.GetSection("OpenAI"));

// Register HttpClient for manual requests if needed
builder.Services.AddHttpClient();

// Enable CORS (for React dev server on port 5173)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

// Add MVC + API controller support
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Middleware pipeline
app.UseCors("AllowFrontend");

app.UseDefaultFiles(); // Serve index.html by default
app.UseStaticFiles();  // Serve static files from wwwroot

app.UseRouting();
app.MapControllers(); // Enable API endpoints

app.MapFallbackToFile("/index.html"); // For SPA routing fallback

app.Run();
