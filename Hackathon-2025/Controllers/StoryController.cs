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

    private readonly IQuotaService _quota;
    private readonly IPeriodService _period;

    public StoryController(
        IStoryGeneratorService storyService,
        AppDbContext db,
        BlobUploadService blobService,
        IProgressBroker progress,
        IServiceScopeFactory scopeFactory,
        IOptionsSnapshot<StoryOptions> storyOpts,
        IQuotaService quota,
        IPeriodService period)
    {
        _storyService = storyService;
        _db = db;
        _blobService = blobService;
        _progress = progress;
        _scopeFactory = scopeFactory;
        _storyOpts = storyOpts;
        _quota = quota;
        _period = period;
    }

    [Authorize]
    [HttpPost("generate-full")]
    public async Task<IActionResult> GenerateFullStory([FromBody] StoryRequest request)
    {
        if (request?.Characters is null || request.Characters.Count == 0)
            return BadRequest("Invalid request: At least one character is required.");

        var user = await GetAndValidateUserAsync();
        if (user is null) return Unauthorized("Invalid or missing user ID in token.");

        var now = DateTime.UtcNow;

        await RolloverIfNeededAsync(user, now);

        var capacity = await EnsureCapacityAndMaybeReserveAddOnAsync(user);
        if (!capacity.ok) return StatusCode(403, capacity.message!);

        // Build immutable effective request (StoryRequest has init-only props)
        var effectiveRequest = BuildEffectiveRequest(user.Membership, request);

        // Generate; ensure add-on refund on failure
        StoryResult result;
        try
        {
            result = await _storyService.GenerateFullStoryAsync(effectiveRequest);
        }
        catch
        {
            if (capacity.usedAddOn) await RefundReservedAddOnAsync(user);
            throw;
        }

        // Upload cover
        var coverFileName = $"{user.Email}-cover-{Guid.NewGuid()}.png";
        var coverBlobUrl = await _blobService.UploadImageAsync(result.CoverImageUrl!, coverFileName);
        result = result with { CoverImageUrl = coverBlobUrl };

        // Upload page images
        var pages = result.Pages.ToList();
        var uploadTasks = pages.Select(async (p, i) =>
        {
            if (!string.IsNullOrEmpty(p.ImageUrl))
            {
                var pageFileName = $"{user.Email}-page-{i}-{Guid.NewGuid()}.png";
                var blobUrl = await _blobService.UploadImageAsync(p.ImageUrl!, pageFileName);
                pages[i] = p with { ImageUrl = blobUrl };
            }
        }).ToList();
        await Task.WhenAll(uploadTasks);

        // Persist story (requires ImagePrompt)
        var story = new Story
        {
            Title = result.Title,
            CoverImageUrl = result.CoverImageUrl,
            CreatedAt = now,
            UserId = user.Id,
            Pages = pages
                .Select(p => new StoryPage(p.Text, p.ImagePrompt) { ImageUrl = p.ImageUrl })
                .ToList()
        };

        _db.Stories.Add(story);
        user.BooksGenerated += 1;
        await _db.SaveChangesAsync();

        // Return final (with blob URLs)
        var finalResult = result with { Pages = pages };
        return Ok(finalResult);
    }

    [Authorize]
    [HttpPost("generate-full/start")]
    public async Task<IActionResult> Start([FromBody] StoryRequest request, CancellationToken ct)
    {
        if (request?.Characters is null || request.Characters.Count == 0)
            return BadRequest("Invalid request: At least one character is required.");

        var user = await GetAndValidateUserAsync();
        if (user is null) return Unauthorized("Invalid or missing user ID in token.");

        var now = DateTime.UtcNow;
        await RolloverIfNeededAsync(user, now);

        var effectiveRequest = BuildEffectiveRequest(user.Membership, request);

        var jobId = _progress.CreateJob();

        _ = Task.Run(async () =>
        {
            _progress.Publish(jobId, new ProgressUpdate { Stage = "start", Percent = 5, Message = "Starting…" });

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var scopedBlob = scope.ServiceProvider.GetRequiredService<BlobUploadService>();
                var scopedGenerator = scope.ServiceProvider.GetRequiredService<IStoryGeneratorService>();
                var scopedQuota = scope.ServiceProvider.GetRequiredService<IQuotaService>();
                var scopedPeriod = scope.ServiceProvider.GetRequiredService<IPeriodService>();

                var sUser = await scopedDb.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
                if (sUser is null)
                {
                    _progress.Publish(jobId, new ProgressUpdate { Stage = "error", Percent = 100, Message = "User not found.", Done = true });
                    _progress.Complete(jobId);
                    return;
                }

                if (scopedPeriod.IsPeriodBoundary(sUser, DateTime.UtcNow))
                {
                    scopedPeriod.OnPeriodRollover(sUser, DateTime.UtcNow);
                    scopedDb.Users.Update(sUser);
                    await scopedDb.SaveChangesAsync();
                }

                var canProceed = await EnsureCapacityAndMaybeReserveAddOnAsyncScoped(scopedDb, scopedQuota, sUser);
                if (!canProceed.ok)
                {
                    _progress.Publish(jobId, new ProgressUpdate { Stage = "error", Percent = 100, Message = canProceed.message, Done = true });
                    _progress.Complete(jobId);
                    return;
                }

                _progress.Publish(jobId, new ProgressUpdate { Stage = "text", Percent = 15, Message = "Writing your story…" });

                StoryResult result;
                try
                {
                    result = await scopedGenerator.GenerateFullStoryAsync(effectiveRequest);
                }
                catch
                {
                    if (canProceed.usedAddOn)
                        await RefundReservedAddOnAsyncScoped(scopedDb, sUser);
                    throw;
                }

                // Upload cover
                _progress.Publish(jobId, new ProgressUpdate { Stage = "image", Percent = 30, Message = "Preparing images…", Index = 0, Total = result.Pages.Count });

                var coverFileName = $"{sUser.Email}-cover-{Guid.NewGuid()}.png";
                var coverBlobUrl = await scopedBlob.UploadImageAsync(result.CoverImageUrl!, coverFileName);
                result = result with { CoverImageUrl = coverBlobUrl };

                // Upload page images with progress
                var pages = result.Pages.ToList();
                var total = Math.Max(1, pages.Count);
                for (int i = 0; i < pages.Count; i++)
                {
                    if (!string.IsNullOrEmpty(pages[i].ImageUrl))
                    {
                        var pageFileName = $"{sUser.Email}-page-{i}-{Guid.NewGuid()}.png";
                        var blobUrl = await scopedBlob.UploadImageAsync(pages[i].ImageUrl!, pageFileName);
                        pages[i] = pages[i] with { ImageUrl = blobUrl };
                    }

                    var pct = 30 + (int)Math.Round(((i + 1) / (double)total) * 65); // 30 → 95
                    _progress.Publish(jobId, new ProgressUpdate
                    {
                        Stage = "image",
                        Percent = Math.Min(95, pct),
                        Message = $"Generating images {i + 1}/{total}…",
                        Index = i + 1,
                        Total = total
                    });
                }

                // Save to DB
                _progress.Publish(jobId, new ProgressUpdate { Stage = "db", Percent = 97, Message = "Saving your story…" });

                var story = new Story
                {
                    Title = result.Title,
                    CoverImageUrl = result.CoverImageUrl,
                    CreatedAt = DateTime.UtcNow,
                    UserId = sUser.Id,
                    Pages = pages
                        .Select(p => new StoryPage(p.Text, p.ImagePrompt) { ImageUrl = p.ImageUrl })
                        .ToList()
                };

                scopedDb.Stories.Add(story);
                sUser.BooksGenerated += 1;
                await scopedDb.SaveChangesAsync();

                var finalResult = result with { Pages = pages };

                _progress.SetResult(jobId, finalResult);
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

        return await _db.Users.FindAsync(userId);
    }

    private async Task RolloverIfNeededAsync(User user, DateTime now)
    {
        if (_period.IsPeriodBoundary(user, now))
        {
            _period.OnPeriodRollover(user, now);
            _db.Users.Update(user);
            await _db.SaveChangesAsync();
        }
    }

    private async Task<(bool ok, bool usedAddOn, string? message)> EnsureCapacityAndMaybeReserveAddOnAsync(User user)
    {
        if (user.Membership == MembershipPlan.Free && user.BooksGenerated >= 1)
            return (false, false, "Free users can only generate one story.");

        var baseQuota = _quota.BaseQuotaFor(user.Membership.ToString());
        var baseRemaining = Math.Max(baseQuota - user.BooksGenerated, 0);

        if (baseRemaining > 0)
            return (true, false, null);

        if (user.AddOnBalance <= 0)
            return (false, false, $"Your {user.Membership} plan allows {baseQuota} books this period. You've reached your limit.");

        user.AddOnBalance -= 1;
        user.AddOnSpentThisPeriod += 1;
        _db.Users.Update(user);
        await _db.SaveChangesAsync();

        return (true, true, null);
    }

    private async Task RefundReservedAddOnAsync(User user)
    {
        user.AddOnBalance += 1;
        if (user.AddOnSpentThisPeriod > 0) user.AddOnSpentThisPeriod -= 1;
        _db.Users.Update(user);
        await _db.SaveChangesAsync();
    }

    private async Task<(bool ok, bool usedAddOn, string? message)> EnsureCapacityAndMaybeReserveAddOnAsyncScoped(
        AppDbContext scopedDb, IQuotaService scopedQuota, User sUser)
    {
        if (sUser.Membership == MembershipPlan.Free && sUser.BooksGenerated >= 1)
            return (false, false, "Free users can only generate one story.");

        var baseQuota = scopedQuota.BaseQuotaFor(sUser.Membership.ToString());
        var baseRemaining = Math.Max(baseQuota - sUser.BooksGenerated, 0);

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

    private async Task RefundReservedAddOnAsyncScoped(AppDbContext scopedDb, User sUser)
    {
        sUser.AddOnBalance += 1;
        if (sUser.AddOnSpentThisPeriod > 0) sUser.AddOnSpentThisPeriod -= 1;
        scopedDb.Users.Update(sUser);
        await scopedDb.SaveChangesAsync();
    }

    // Immutable effective request according to StoryOptions + membership
    private StoryRequest BuildEffectiveRequest(MembershipPlan membership, StoryRequest request)
    {
        if (!_storyOpts.Value.LengthHintEnabled)
        {
            return request with { StoryLength = null, PageCount = null };
        }

        string[] allowed = membership switch
        {
            MembershipPlan.Free => new[] { "short" },
            MembershipPlan.Pro => new[] { "short", "medium" },
            MembershipPlan.Premium => new[] { "short", "medium", "long" },
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

        return request with
        {
            StoryLength = requested,
            PageCount = lengthToCount[requested]
        };
    }
}
