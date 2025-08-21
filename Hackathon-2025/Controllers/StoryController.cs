using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Hackathon_2025.Models;
using Hackathon_2025.Services;
using Hackathon_2025.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Hackathon_2025.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StoryController : ControllerBase
{
    private readonly IStoryGeneratorService _storyService;
    private readonly AppDbContext _db;
    private readonly BlobUploadService _blobService;
    private readonly IProgressBroker _progress;
    private readonly IServiceScopeFactory _scopeFactory;

    public StoryController(
        IStoryGeneratorService storyService,
        AppDbContext db,
        BlobUploadService blobService,
        IProgressBroker progress,
        IServiceScopeFactory scopeFactory)
    {
        _storyService = storyService;
        _db = db;
        _blobService = blobService;
        _progress = progress;
        _scopeFactory = scopeFactory;
    }

    [Authorize]
    [HttpPost("generate-full")]
    public async Task<IActionResult> GenerateFullStory([FromBody] StoryRequest request)
    {
        if (request is null || request.Characters.Count == 0)
            return BadRequest("Invalid request: At least one character is required.");

        var user = await GetAndValidateUserAsync();
        if (user is null) return Unauthorized("Invalid or missing user ID in token.");

        var now = DateTime.UtcNow;

        // Subscription limit checks / monthly reset
        var limitExceeded = await CheckAndMaybeResetLimitsAsync(user, now);
        if (limitExceeded?.exceeded == true)
            return StatusCode(403, limitExceeded.Value.message!);

        // Enforce page count by membership
        request.PageCount = (user.Membership ?? "").ToLowerInvariant() switch
        {
            "free" => 4,   // 4 story pages + 1 cover
            "pro" => 8,   // 8 story pages + 1 cover
            "premium" => 12,  // 12 story pages + 1 cover
            _ => 4
        };

        // Generate the story
        var result = await _storyService.GenerateFullStoryAsync(request);

        // Upload cover image
        var coverFileName = $"{user.Email}-cover-{Guid.NewGuid()}.png";
        var coverBlobUrl = await _blobService.UploadImageAsync(result.CoverImageUrl, coverFileName);
        result.CoverImageUrl = coverBlobUrl;

        // Upload each page image
        var uploadTasks = result.Pages.Select(async (p, i) =>
        {
            if (!string.IsNullOrEmpty(p.ImageUrl))
            {
                var pageFileName = $"{user.Email}-page-{i}-{Guid.NewGuid()}.png";
                var blobUrl = await _blobService.UploadImageAsync(p.ImageUrl!, pageFileName);
                p.ImageUrl = blobUrl;
            }
        }).ToList();

        await Task.WhenAll(uploadTasks);

        // Save story to database
        var story = new Story
        {
            Title = result.Title,
            CoverImageUrl = result.CoverImageUrl,
            CreatedAt = now,
            UserId = user.Id,
            Pages = result.Pages.Select(p => new StoryPage
            {
                Text = p.Text,
                ImagePrompt = p.ImagePrompt,
                ImageUrl = p.ImageUrl
            }).ToList()
        };

        _db.Stories.Add(story);
        user.BooksGenerated += 1;
        await _db.SaveChangesAsync();

        return Ok(result);
    }

    [Authorize]
    [HttpPost("generate-full/start")]
    public async Task<IActionResult> Start([FromBody] StoryRequest request, CancellationToken ct)
    {
        if (request is null || request.Characters.Count == 0)
            return BadRequest("Invalid request: At least one character is required.");

        var user = await GetAndValidateUserAsync();
        if (user is null) return Unauthorized("Invalid or missing user ID in token.");

        var now = DateTime.UtcNow;

        // Check/Reset monthly limits up front
        var limitExceeded = await CheckAndMaybeResetLimitsAsync(user, now);
        if (limitExceeded?.exceeded == true)
            return StatusCode(403, limitExceeded.Value.message!);

        // Enforce page count by membership
        request.PageCount = (user.Membership ?? "").ToLowerInvariant() switch
        {
            "free" => 4,
            "pro" => 8,
            "premium" => 12,
            _ => 4
        };

        // Create a job and run in background with a fresh DI scope
        var jobId = _progress.CreateJob();

        _ = Task.Run(async () =>
        {
            var progress = new ProgressUpdate { Stage = "start", Percent = 5, Message = "Starting…" };
            _progress.Publish(jobId, progress);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var scopedBlob = scope.ServiceProvider.GetRequiredService<BlobUploadService>();
                var scopedGenerator = scope.ServiceProvider.GetRequiredService<IStoryGeneratorService>();

                // Reload user within this scope
                var sUser = await scopedDb.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
                if (sUser == null)
                {
                    _progress.Publish(jobId, new ProgressUpdate { Stage = "error", Percent = 100, Message = "User not found.", Done = true });
                    _progress.Complete(jobId);
                    return;
                }

                // 1) Generate
                _progress.Publish(jobId, new ProgressUpdate { Stage = "text", Percent = 15, Message = "Writing your story…" });
                var result = await scopedGenerator.GenerateFullStoryAsync(request);

                // 2) Upload cover
                _progress.Publish(jobId, new ProgressUpdate { Stage = "image", Percent = 30, Message = "Preparing images…", Index = 0, Total = result.Pages.Count });
                var coverFileName = $"{sUser.Email}-cover-{Guid.NewGuid()}.png";
                var coverBlobUrl = await scopedBlob.UploadImageAsync(result.CoverImageUrl, coverFileName);
                result.CoverImageUrl = coverBlobUrl;

                // 3) Upload page images with incremental progress
                var total = Math.Max(1, result.Pages.Count);
                for (int i = 0; i < result.Pages.Count; i++)
                {
                    if (!string.IsNullOrEmpty(result.Pages[i].ImageUrl))
                    {
                        var pageFileName = $"{sUser.Email}-page-{i}-{Guid.NewGuid()}.png";
                        var blobUrl = await scopedBlob.UploadImageAsync(result.Pages[i].ImageUrl!, pageFileName);
                        result.Pages[i].ImageUrl = blobUrl;
                    }

                    var frac = (i + 1) / (double)total;
                    var pct = 30 + (int)Math.Round(frac * 65); // 30 → 95
                    _progress.Publish(jobId, new ProgressUpdate
                    {
                        Stage = "image",
                        Percent = Math.Min(95, pct),
                        Message = $"Generating images {i + 1}/{total}…",
                        Index = i + 1,
                        Total = total
                    });
                }

                // 4) Save to DB
                _progress.Publish(jobId, new ProgressUpdate { Stage = "db", Percent = 97, Message = "Saving your story…" });
                var story = new Story
                {
                    Title = result.Title,
                    CoverImageUrl = result.CoverImageUrl,
                    CreatedAt = DateTime.UtcNow,
                    UserId = sUser.Id,
                    Pages = result.Pages.Select(p => new StoryPage
                    {
                        Text = p.Text,
                        ImagePrompt = p.ImagePrompt,
                        ImageUrl = p.ImageUrl
                    }).ToList()
                };

                scopedDb.Stories.Add(story);
                sUser.BooksGenerated += 1;
                await scopedDb.SaveChangesAsync();

                // 5) Done
                _progress.SetResult(jobId, result);
                _progress.Publish(jobId, new ProgressUpdate { Stage = "done", Percent = 100, Message = "Done!", Done = true });
            }
            catch (Exception ex)
            {
                _progress.Publish(jobId, new ProgressUpdate { Stage = "error", Percent = 100, Message = $"Failed: {ex.Message}", Done = true });
            }
            finally
            {
                _progress.Complete(jobId);
            }
        }, ct);

        return Ok(new { jobId });
    }

    // ---------------------------
    // Stream progress via SSE
    // ---------------------------
    [HttpGet("progress/{jobId}")]
    public async Task Progress(string jobId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        Response.ContentType = "text/event-stream";

        var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        await foreach (var update in _progress.Consume(jobId, ct))
        {
            var json = JsonSerializer.Serialize(update, jsonOpts);
            await Response.WriteAsync($"data: {json}\n\n", ct);
            await Response.Body.FlushAsync(ct);
            if (update.Done) break;
        }
    }

    // ---------------------------
    // Optional result fetch
    // ---------------------------
    [Authorize]
    [HttpGet("result/{jobId}")]
    public IActionResult Result(string jobId)
    {
        var result = _progress.GetResult(jobId);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("ping")]
    public IActionResult Ping() => Ok("Story API is alive!");

    // ---------------------------
    // Helpers
    // ---------------------------
    private async Task<User?> GetAndValidateUserAsync()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            return null;

        var user = await _db.Users.FindAsync(userId);
        return user;
    }

    /// <summary>
    /// Handles monthly reset and returns (exceeded, message) if over limit.
    /// </summary>
    private async Task<(bool exceeded, string? message)?> CheckAndMaybeResetLimitsAsync(User user, DateTime now)
    {
        // Free users: one lifetime story (your existing rule)
        if (user.Membership == "free" && user.BooksGenerated >= 1)
            return (true, "Free users can only generate one story.");

        // Monthly limits for paid
        int monthlyLimit = user.Membership switch
        {
            "pro" => 10,
            "premium" => 50,
            _ => 1
        };

        // Reset monthly counters if needed
        if (user.LastReset.Month != now.Month || user.LastReset.Year != now.Year)
        {
            user.BooksGenerated = 0;
            user.LastReset = now;
            _db.Users.Update(user);
            await _db.SaveChangesAsync();
        }

        if (user.BooksGenerated >= monthlyLimit)
            return (true, $"Your {user.Membership} plan allows {monthlyLimit} books per month. You've reached your limit.");

        return (false, null);
    }
}