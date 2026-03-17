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
    private const string PendingStoryTitle = "Your story is being generated...";
    private const string PendingStoryCoverUrl = "/story-generating-cover.png";

    private readonly IStoryGeneratorService _storyService;
    private readonly AppDbContext _db;
    private readonly BlobUploadService _blobService;
    private readonly IProgressBroker _progress;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsSnapshot<StoryOptions> _storyOpts;
    private readonly IQuotaService _quota;
    private readonly IPeriodService _period;
    private readonly ILogger<StoryController> _logger;
    private static readonly JsonSerializerOptions StoryRequestJsonOptions = new(JsonSerializerDefaults.Web);

    public StoryController(
        IStoryGeneratorService storyService,
        AppDbContext db,
        BlobUploadService blobService,
        IProgressBroker progress,
        IServiceScopeFactory scopeFactory,
        IOptionsSnapshot<StoryOptions> storyOpts,
        IQuotaService quota,
        IPeriodService period,
        ILogger<StoryController> logger)
    {
        _storyService = storyService;
        _db = db;
        _blobService = blobService;
        _progress = progress;
        _scopeFactory = scopeFactory;
        _storyOpts = storyOpts;
        _quota = quota;
        _period = period;
        _logger = logger;
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

        var capacity = await EnsureCapacityAndReserveCreditAsync(user);
        if (!capacity.ok) return StatusCode(403, capacity.message!);

        // Build immutable effective request (StoryRequest has init-only props)
        var effectiveRequest = BuildEffectiveRequest(user.Membership, request);

        var pendingStory = new Story
        {
            Title = PendingStoryTitle,
            CoverImageUrl = PendingStoryCoverUrl,
            CreatedAt = now,
            UserId = user.Id,
            RequestTheme = effectiveRequest.Theme?.Trim(),
            RequestReadingLevel = effectiveRequest.ReadingLevel?.Trim(),
            RequestArtStyle = effectiveRequest.ArtStyle?.Trim(),
            RequestStoryLength = effectiveRequest.StoryLength?.Trim(),
            RequestLessonLearned = effectiveRequest.LessonLearned?.Trim(),
            RequestCharactersJson = SerializeCharacters(effectiveRequest.Characters)
        };

        try
        {
            _db.Stories.Add(pendingStory);
            await _db.SaveChangesAsync();
        }
        catch
        {
            await RefundReservedCreditAsync(user, capacity.usedAddOn);
            throw;
        }

        // Generate; rollback reserved credit + pending story on failure
        StoryResult result;
        try
        {
            result = await _storyService.GenerateFullStoryAsync(effectiveRequest);
        }
        catch
        {
            await DeletePendingStoryAndRefundReservedCreditAsync(_db, user, pendingStory, capacity.usedAddOn);
            throw;
        }

        // Upload images and persist — rollback credit + pending story if anything here fails
        try
        {
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

            pendingStory.Title = result.Title;
            pendingStory.CoverImageUrl = result.CoverImageUrl;
            pendingStory.Pages.Clear();
            foreach (var p in pages)
            {
                pendingStory.Pages.Add(new StoryPage(p.Text, p.ImagePrompt) { ImageUrl = p.ImageUrl });
            }
            await _db.SaveChangesAsync();

            // Return final (with blob URLs)
            var finalResult = result with { Pages = pages };
            return Ok(finalResult);
        }
        catch
        {
            await DeletePendingStoryAndRefundReservedCreditAsync(_db, user, pendingStory, capacity.usedAddOn);
            throw;
        }
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
        var reserved = await EnsureCapacityAndReserveCreditAsync(user);
        if (!reserved.ok) return StatusCode(403, reserved.message!);

        var pendingStory = new Story
        {
            Title = PendingStoryTitle,
            CoverImageUrl = PendingStoryCoverUrl,
            CreatedAt = now,
            UserId = user.Id,
            RequestTheme = effectiveRequest.Theme?.Trim(),
            RequestReadingLevel = effectiveRequest.ReadingLevel?.Trim(),
            RequestArtStyle = effectiveRequest.ArtStyle?.Trim(),
            RequestStoryLength = effectiveRequest.StoryLength?.Trim(),
            RequestLessonLearned = effectiveRequest.LessonLearned?.Trim(),
            RequestCharactersJson = SerializeCharacters(effectiveRequest.Characters)
        };

        try
        {
            _db.Stories.Add(pendingStory);
            await _db.SaveChangesAsync();
        }
        catch
        {
            await RefundReservedCreditAsync(user, reserved.usedAddOn);
            throw;
        }

        var pendingStoryId = pendingStory.Id;

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
                var sUser = await scopedDb.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
                if (sUser is null)
                {
                    _progress.Publish(jobId, new ProgressUpdate { Stage = "error", Percent = 100, Message = "User not found.", Done = true });
                    _progress.Complete(jobId);
                    return;
                }

                var story = await scopedDb.Stories
                    .Include(s => s.Pages)
                    .FirstOrDefaultAsync(s => s.Id == pendingStoryId && s.UserId == sUser.Id);

                if (story is null)
                {
                    _progress.Publish(jobId, new ProgressUpdate { Stage = "error", Percent = 100, Message = "Story draft not found.", Done = true });
                    _progress.Complete(jobId);
                    return;
                }

                StoryResult result;
                try
                {
                    result = await scopedGenerator.GenerateFullStoryAsync(
                        effectiveRequest,
                        update => _progress.Publish(jobId, update));
                }
                catch
                {
                    await DeletePendingStoryAndRefundReservedCreditAsync(scopedDb, sUser, story, reserved.usedAddOn);
                    throw;
                }

                // Upload images and persist — refund add-on if anything here fails
                try
                {
                    // Upload cover
                    _progress.Publish(jobId, new ProgressUpdate { Stage = "upload", Percent = 88, Message = "Saving your cover art...", Index = 0, Total = result.Pages.Count + 1 });

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

                        var pct = 88 + (int)Math.Round(((i + 1) / (double)total) * 8); // 88 → 96
                        _progress.Publish(jobId, new ProgressUpdate
                        {
                            Stage = "upload",
                            Percent = Math.Min(96, pct),
                            Message = $"Saving artwork {i + 1}/{total}...",
                            Index = i + 2,
                            Total = total + 1
                        });
                    }

                    // Save to DB
                    _progress.Publish(jobId, new ProgressUpdate { Stage = "db", Percent = 98, Message = "Saving your story..." });

                    story.Title = result.Title;
                    story.CoverImageUrl = result.CoverImageUrl;
                    story.Pages.Clear();
                    foreach (var p in pages)
                    {
                        story.Pages.Add(new StoryPage(p.Text, p.ImagePrompt) { ImageUrl = p.ImageUrl });
                    }

                    await scopedDb.SaveChangesAsync();

                    var finalResult = result with { Pages = pages };

                    _progress.SetResult(jobId, finalResult);
                    _progress.Publish(jobId, new ProgressUpdate { Stage = "done", Percent = 100, Message = "Done!", Done = true });
                }
                catch
                {
                    await DeletePendingStoryAndRefundReservedCreditAsync(scopedDb, sUser, story, reserved.usedAddOn);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Story generation job {JobId} failed", jobId);
                _progress.Publish(jobId, new ProgressUpdate { Stage = "error", Percent = 100, Message = "Something went wrong while creating your story. Please try again.", Done = true });
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

    private async Task<(bool ok, bool usedAddOn, string? message)> EnsureCapacityAndReserveCreditAsync(User user)
    {
        if (user.Membership == MembershipPlan.Free && user.BooksGenerated >= 1)
            return (false, false, "Free users can only generate one story.");

        var baseQuota = _quota.BaseQuotaFor(user.Membership.ToString());
        var baseRemaining = Math.Max(baseQuota - user.BooksGenerated, 0);

        var usedAddOn = false;
        if (baseRemaining <= 0)
        {
            if (user.AddOnBalance <= 0)
                return (false, false, $"Your {user.Membership} plan allows {baseQuota} books this period. You've reached your limit.");

            user.AddOnBalance -= 1;
            user.AddOnSpentThisPeriod += 1;
            usedAddOn = true;
        }

        user.BooksGenerated += 1;
        _db.Users.Update(user);
        await _db.SaveChangesAsync();

        return (true, usedAddOn, null);
    }

    private async Task RefundReservedCreditAsync(User user, bool usedAddOn)
    {
        if (user.BooksGenerated > 0) user.BooksGenerated -= 1;
        if (usedAddOn)
        {
            user.AddOnBalance += 1;
            if (user.AddOnSpentThisPeriod > 0) user.AddOnSpentThisPeriod -= 1;
        }

        _db.Users.Update(user);
        await _db.SaveChangesAsync();
    }

    private async Task RefundReservedCreditAsyncScoped(AppDbContext scopedDb, User sUser, bool usedAddOn)
    {
        if (sUser.BooksGenerated > 0) sUser.BooksGenerated -= 1;
        if (usedAddOn)
        {
            sUser.AddOnBalance += 1;
            if (sUser.AddOnSpentThisPeriod > 0) sUser.AddOnSpentThisPeriod -= 1;
        }

        scopedDb.Users.Update(sUser);
        await scopedDb.SaveChangesAsync();
    }

    private async Task DeletePendingStoryAndRefundReservedCreditAsync(AppDbContext scopedDb, User sUser, Story story, bool usedAddOn)
    {
        scopedDb.Stories.Remove(story);
        await RefundReservedCreditAsyncScoped(scopedDb, sUser, usedAddOn);
    }

    // Immutable effective request according to StoryOptions + membership
    private StoryRequest BuildEffectiveRequest(MembershipPlan membership, StoryRequest request)
    {
        var sanitizedCharacters = (request.Characters ?? new List<CharacterSpec>())
            .Take(MembershipEntitlements.MaxCharactersPerStory)
            .Select(c => MembershipEntitlements.SanitizeCharacterForMembership(membership, c))
            .ToList();

        var sanitizedRequest = request with
        {
            Characters = sanitizedCharacters,
            StoryLength = null,
            PageCount = null
        };

        if (!_storyOpts.Value.LengthHintEnabled)
        {
            return sanitizedRequest;
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

        return sanitizedRequest with
        {
            StoryLength = requested,
            PageCount = lengthToCount[requested]
        };
    }

    private static string? SerializeCharacters(List<CharacterSpec>? characters)
    {
        if (characters is null || characters.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(characters, StoryRequestJsonOptions);
    }
}
