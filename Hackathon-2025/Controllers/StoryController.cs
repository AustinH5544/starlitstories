using Hackathon_2025.Data;
using Hackathon_2025.Models;
using Hackathon_2025.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Json;

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
    private readonly IOptionsSnapshot<StoryOptions> _storyOpts;

    // NEW: services that centralize policy
    private readonly IQuotaService _quota;      // NEW

    private readonly IPeriodService _period;    // NEW

    public StoryController(
        IStoryGeneratorService storyService,
        AppDbContext db,
        BlobUploadService blobService,
        IProgressBroker progress,
        IServiceScopeFactory scopeFactory,
        IOptionsSnapshot<StoryOptions> storyOpts,
        IQuotaService quota,                    // NEW
        IPeriodService period)                  // NEW
    {
        _storyService = storyService;
        _db = db;
        _blobService = blobService;
        _progress = progress;
        _scopeFactory = scopeFactory;
        _storyOpts = storyOpts;
        _quota = quota;                         // NEW
        _period = period;                       // NEW
    }

    [Authorize]
    [HttpPost("generate-full")]
    public async Task<IActionResult> GenerateFullStory([FromBody] StoryRequest request)
    {
        if (request?.Characters == null || request.Characters.Count == 0)
            return BadRequest("Invalid request: At least one character is required.");

        var user = await GetAndValidateUserAsync();
        if (user is null) return Unauthorized("Invalid or missing user ID in token.");

        var now = DateTime.UtcNow;

        // NEW: Stripe/calendar-aware rollover (resets per-period counters, not wallet)
        await RolloverIfNeededAsync(user, now); // NEW

        // NEW: capacity check (base quota) + add-on reservation (decrement wallet up-front)
        var capacity = await EnsureCapacityAndMaybeReserveAddOnAsync(user); // NEW
        if (!capacity.ok) return StatusCode(403, capacity.message!);        // NEW

        // Length gating (unchanged, behind your StoryOptions flag)
        var enforceLength = _storyOpts.Value.LengthHintEnabled;
        if (enforceLength)
        {
            var membership = (user.Membership ?? "").ToLowerInvariant();
            var allowed = membership switch
            {
                "free" => new[] { "short" },
                "pro" => new[] { "short", "medium" },
                "premium" => new[] { "short", "medium", "long" },
                _ => new[] { "short" }
            };

            var requested = (request.StoryLength ?? "short").ToLowerInvariant();
            if (!allowed.Contains(requested)) requested = allowed[0];

            var lengthToCount = new Dictionary<string, int>
            {
                ["short"] = 4,
                ["medium"] = 8,
                ["long"] = 12
            };

            request.StoryLength = requested;
            request.PageCount = lengthToCount[requested];
        }
        else
        {
            request.StoryLength = null;
            request.PageCount = null;
        }

        // NEW: refund reserved add-on if generation fails
        StoryResult result; // NEW
        try                                                  // NEW
        {                                                    // NEW
            result = await _storyService.GenerateFullStoryAsync(request);
        }                                                    // NEW
        catch                                                // NEW
        {                                                    // NEW
            if (capacity.usedAddOn) await RefundReservedAddOnAsync(user); // NEW
            throw;                                           // NEW
        }                                                    // NEW

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
        user.BooksGenerated += 1; // base counter for the period
        await _db.SaveChangesAsync();

        return Ok(result);
    }

    [Authorize]
    [HttpPost("generate-full/start")]
    public async Task<IActionResult> Start([FromBody] StoryRequest request, CancellationToken ct)
    {
        if (request?.Characters == null || request.Characters.Count == 0)
            return BadRequest("Invalid request: At least one character is required.");

        var user = await GetAndValidateUserAsync();
        if (user is null) return Unauthorized("Invalid or missing user ID in token.");

        var now = DateTime.UtcNow;

        // NEW: rollover at request time; we also re-check inside the background scope
        await RolloverIfNeededAsync(user, now); // NEW

        // Length gating (unchanged, using your options)
        var enforceLength = _storyOpts.Value.LengthHintEnabled;
        if (enforceLength)
        {
            var membership = (user.Membership ?? "").ToLowerInvariant();
            var allowed = membership switch
            {
                "free" => new[] { "short" },
                "pro" => new[] { "short", "medium" },
                "premium" => new[] { "short", "medium", "long" },
                _ => new[] { "short" }
            };

            var requested = (request.StoryLength ?? "short").ToLowerInvariant();
            if (!allowed.Contains(requested)) requested = allowed[0];

            var lengthToCount = new Dictionary<string, int>
            {
                ["short"] = 4,
                ["medium"] = 8,
                ["long"] = 12
            };

            request.StoryLength = requested;
            request.PageCount = lengthToCount[requested]; // hint for generator/token budget
        }
        else
        {
            request.StoryLength = null;
            request.PageCount = null;
        }

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

                // NEW: bring services into the background scope
                var scopedQuota = scope.ServiceProvider.GetRequiredService<IQuotaService>();   // NEW
                var scopedPeriod = scope.ServiceProvider.GetRequiredService<IPeriodService>(); // NEW

                // Reload user within this scope
                var sUser = await scopedDb.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
                if (sUser == null)
                {
                    _progress.Publish(jobId, new ProgressUpdate { Stage = "error", Percent = 100, Message = "User not found.", Done = true });
                    _progress.Complete(jobId);
                    return;
                }

                // NEW: rollover again in case boundary crossed between request and job start
                if (scopedPeriod.IsPeriodBoundary(sUser, DateTime.UtcNow))                    // NEW
                {                                                                             // NEW
                    scopedPeriod.OnPeriodRollover(sUser, DateTime.UtcNow);                   // NEW
                    scopedDb.Users.Update(sUser);                                            // NEW
                    await scopedDb.SaveChangesAsync();                                       // NEW
                }                                                                             // NEW

                // NEW: capacity check + add-on reservation INSIDE the scope
                var canProceed = await EnsureCapacityAndMaybeReserveAddOnAsyncScoped(scopedDb, scopedQuota, sUser); // NEW
                if (!canProceed.ok)                                                                               // NEW
                {                                                                                                 // NEW
                    _progress.Publish(jobId, new ProgressUpdate { Stage = "error", Percent = 100, Message = canProceed.message, Done = true });
                    _progress.Complete(jobId);
                    return;
                }

                // 1) Generate (with refund on failure)
                _progress.Publish(jobId, new ProgressUpdate { Stage = "text", Percent = 15, Message = "Writing your story…" });
                StoryResult result;
                try
                {
                    result = await scopedGenerator.GenerateFullStoryAsync(request);
                }
                catch
                {
                    if (canProceed.usedAddOn)
                        await RefundReservedAddOnAsyncScoped(scopedDb, sUser); // NEW
                    throw;
                }

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

    // NEW: centralized rollover using IPeriodService
    private async Task RolloverIfNeededAsync(User user, DateTime now) // NEW
    {
        if (_period.IsPeriodBoundary(user, now))
        {
            _period.OnPeriodRollover(user, now);
            _db.Users.Update(user);
            await _db.SaveChangesAsync();
        }
    }

    // NEW: capacity check + add-on reservation (refund if generation fails)
    private async Task<(bool ok, bool usedAddOn, string? message)> EnsureCapacityAndMaybeReserveAddOnAsync(User user) // NEW
    {
        // Preserve your free plan rule: one lifetime story
        if (string.Equals(user.Membership, "free", StringComparison.OrdinalIgnoreCase) && user.BooksGenerated >= 1)
            return (false, false, "Free users can only generate one story.");

        var baseQuota = _quota.BaseQuotaFor(user.Membership);
        var used = user.BooksGenerated;
        var baseRemaining = Math.Max(baseQuota - used, 0);

        if (baseRemaining > 0)
            return (true, false, null);

        // base exhausted — try to spend wallet
        if (user.AddOnBalance <= 0)
            return (false, false, $"Your {user.Membership} plan allows {baseQuota} books this period. You've reached your limit.");

        user.AddOnBalance -= 1;
        user.AddOnSpentThisPeriod += 1;
        _db.Users.Update(user);
        await _db.SaveChangesAsync();

        return (true, true, null);
    }

    // NEW: refund helper for sync path
    private async Task RefundReservedAddOnAsync(User user) // NEW
    {
        user.AddOnBalance += 1;
        if (user.AddOnSpentThisPeriod > 0) user.AddOnSpentThisPeriod -= 1;
        _db.Users.Update(user);
        await _db.SaveChangesAsync();
    }

    // NEW: scoped versions for background job
    private async Task<(bool ok, bool usedAddOn, string? message)> EnsureCapacityAndMaybeReserveAddOnAsyncScoped( // NEW
        AppDbContext scopedDb, IQuotaService scopedQuota, User sUser)
    {
        if (string.Equals(sUser.Membership, "free", StringComparison.OrdinalIgnoreCase) && sUser.BooksGenerated >= 1)
            return (false, false, "Free users can only generate one story.");

        var baseQuota = scopedQuota.BaseQuotaFor(sUser.Membership);
        var used = sUser.BooksGenerated;
        var baseRemaining = Math.Max(baseQuota - used, 0);

        if (baseRemaining > 0)
            return (true, false, null);

        if (sUser.AddOnBalance <= 0)
            return (false, false, $"Your {sUser.Membership} plan allows {baseQuota} books this period. You've reached your limit.");

        sUser.AddOnBalance -= 1;
        sUser.AddOnSpentThisPeriod += 1;
        scopedDb.Users.Update(sUser);
        await scopedDb.SaveChangesAsync();

        return (true, true, null);
    }

    private async Task RefundReservedAddOnAsyncScoped(AppDbContext scopedDb, User sUser) // NEW
    {
        sUser.AddOnBalance += 1;
        if (sUser.AddOnSpentThisPeriod > 0) sUser.AddOnSpentThisPeriod -= 1;
        scopedDb.Users.Update(sUser);
        await scopedDb.SaveChangesAsync();
    }

    // REMOVED: old CheckAndMaybeResetLimitsAsync(...) with hardcoded monthly limits
}