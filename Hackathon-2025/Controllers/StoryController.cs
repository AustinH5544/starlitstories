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

    // A page-less story younger than this is still being generated; older ones are stuck drafts.
    private static readonly TimeSpan GenerationWindow = TimeSpan.FromMinutes(30);

    private readonly IStoryGeneratorService _storyService;
    private readonly AppDbContext _db;
    private readonly IBlobUploadService _blobService;
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
        IBlobUploadService blobService,
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
            await RefundReservedCreditAsync(_db, user.Id, capacity.usedAddOn);
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
            await DeletePendingStoryAndRefundReservedCreditAsync(_db, user.Id, pendingStory.Id, capacity.usedAddOn);
            throw;
        }

        // Upload images and persist — rollback credit + pending story if anything here fails
        try
        {
            // Upload cover
            var coverFileName = $"{user.Email}-cover-{Guid.NewGuid()}.png";
            var coverBlobUrl = await _blobService.UploadImageAsync(result.CoverImageUrl!, coverFileName);
            result = result with { CoverImageUrl = coverBlobUrl };

            // Upload page images in parallel; each task returns its updated page (never mutate the list being enumerated)
            var uploadTasks = result.Pages.Select(async (p, i) =>
            {
                if (string.IsNullOrEmpty(p.ImageUrl))
                    return p;

                var pageFileName = $"{user.Email}-page-{i}-{Guid.NewGuid()}.png";
                var blobUrl = await _blobService.UploadImageAsync(p.ImageUrl!, pageFileName);
                return p with { ImageUrl = blobUrl };
            }).ToList();
            var pages = (await Task.WhenAll(uploadTasks)).ToList();

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
            await DeletePendingStoryAndRefundReservedCreditAsync(_db, user.Id, pendingStory.Id, capacity.usedAddOn);
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
            await RefundReservedCreditAsync(_db, user.Id, reserved.usedAddOn);
            throw;
        }

        var pendingStoryId = pendingStory.Id;

        var jobId = _progress.CreateJob(user.Id);

        _ = Task.Run(async () =>
        {
            _progress.Publish(jobId, new ProgressUpdate { Stage = "start", Percent = 5, Message = "Starting…" });

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var scopedBlob = scope.ServiceProvider.GetRequiredService<IBlobUploadService>();
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
                    await DeletePendingStoryAndRefundReservedCreditAsync(scopedDb, sUser.Id, story.Id, reserved.usedAddOn);
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
                    await DeletePendingStoryAndRefundReservedCreditAsync(scopedDb, sUser.Id, story.Id, reserved.usedAddOn);
                    throw;
                }
            }
            catch (SuspiciousImageGenerationException ex)
            {
                _logger.LogWarning(ex, "Story generation job {JobId} failed quality checks after retry", jobId);
                _progress.Publish(jobId, new ProgressUpdate
                {
                    Stage = "error",
                    Percent = 100,
                    Message = "One of the illustrations failed our quality check twice. No credits were used. Please try generating the story again.",
                    Done = true
                });
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
        }, CancellationToken.None); // The credit is already reserved; a client disconnect must not stop the job from starting.

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
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        // 404 (not 403) for other users' jobs so job IDs can't be probed
        var result = _progress.GetResult(jobId, userId);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await GetAndValidateUserAsync();
        if (user is null) return Unauthorized("Invalid or missing user.");

        var story = await _db.Stories
            .Include(s => s.Pages)
            .Include(s => s.Shares)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == user.Id);

        if (story is null) return NotFound("Story not found.");

        // Deleting a draft mid-generation would make the background job lose track of the reserved credit.
        if (story.Pages.Count == 0 && DateTime.UtcNow - story.CreatedAt < GenerationWindow)
            return Conflict("This story is still being generated. Try again once it's finished.");

        var imageUrls = story.Pages
            .Select(p => p.ImageUrl)
            .Prepend(story.CoverImageUrl)
            // Only real blob URLs; drafts point at a shared static placeholder that must never be deleted.
            .Where(url => Uri.TryCreate(url, UriKind.Absolute, out var uri)
                          && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            .Distinct()
            .ToList();

        _db.Stories.Remove(story); // pages and shares are loaded, so they're removed with it
        await _db.SaveChangesAsync();

        // Best-effort image cleanup; the story is already gone, so a storage failure must not surface.
        foreach (var url in imageUrls)
        {
            try { await _blobService.DeleteByUrlAsync(url!); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not delete image {Url} for story {StoryId}", url, id); }
        }

        return NoContent();
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

    // Credit bookkeeping uses single conditional UPDATE statements that touch only the counters involved.
    // Read-modify-write of the whole User row let simultaneous requests spend one credit twice, and let a
    // refund written minutes later overwrite plan changes or purchases made while the story was generating.

    private async Task RolloverIfNeededAsync(User user, DateTime now)
    {
        if (!_period.IsPeriodBoundary(user, now)) return;

        var observedLastReset = user.LastReset;
        var observedPeriodEnd = user.CurrentPeriodEndUtc;
        var observedBalance = user.AddOnBalance;

        _period.OnPeriodRollover(user, now); // computes the new period values on the in-memory copy
        var clearWallet = observedBalance != 0 && user.AddOnBalance == 0; // carryover disabled by policy
        var newLastReset = user.LastReset;
        var newPeriodStart = user.CurrentPeriodStartUtc;
        var newPeriodEnd = user.CurrentPeriodEndUtc;

        // Only the first request to see this boundary applies it; the rest just pick up the result.
        await _db.Users
            .Where(u => u.Id == user.Id && u.LastReset == observedLastReset && u.CurrentPeriodEndUtc == observedPeriodEnd)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.BooksGenerated, 0)
                .SetProperty(u => u.AddOnSpentThisPeriod, 0)
                .SetProperty(u => u.AddOnBalance, u => clearWallet ? 0 : u.AddOnBalance)
                .SetProperty(u => u.LastReset, newLastReset)
                .SetProperty(u => u.CurrentPeriodStartUtc, newPeriodStart)
                .SetProperty(u => u.CurrentPeriodEndUtc, newPeriodEnd));

        await _db.Entry(user).ReloadAsync();
    }

    private async Task<(bool ok, bool usedAddOn, string? message)> EnsureCapacityAndReserveCreditAsync(User user)
    {
        var isFree = user.Membership == MembershipPlan.Free;
        var baseQuota = _quota.BaseQuotaFor(user.Membership.ToString());
        var baseLimit = isFree ? Math.Min(baseQuota, 1) : baseQuota;

        // 1) A credit from the plan's quota, only if one is still free at the moment of the update.
        var reserved = await _db.Users
            .Where(u => u.Id == user.Id && u.BooksGenerated < baseLimit)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.BooksGenerated, u => u.BooksGenerated + 1));
        if (reserved == 1)
        {
            await _db.Entry(user).ReloadAsync();
            return (true, false, null);
        }

        // 2) Otherwise an add-on credit, only if the balance is still positive. Free users can spend credits
        //    they hold too (kept after a downgrade, or a carried-over free story); Free-plan limits still apply.
        reserved = await _db.Users
            .Where(u => u.Id == user.Id && u.AddOnBalance > 0)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.AddOnBalance, u => u.AddOnBalance - 1)
                .SetProperty(u => u.AddOnSpentThisPeriod, u => u.AddOnSpentThisPeriod + 1)
                .SetProperty(u => u.BooksGenerated, u => u.BooksGenerated + 1));
        if (reserved == 1)
        {
            await _db.Entry(user).ReloadAsync();
            return (true, true, null);
        }

        return isFree
            ? (false, false, "Free users can only generate one story.")
            : (false, false, $"Your {user.Membership} plan allows {baseQuota} books this period. You've reached your limit.");
    }

    private static Task RefundReservedCreditAsync(AppDbContext db, int userId, bool usedAddOn)
    {
        var user = db.Users.Where(u => u.Id == userId);
        return usedAddOn
            ? user.ExecuteUpdateAsync(s => s
                .SetProperty(u => u.BooksGenerated, u => u.BooksGenerated > 0 ? u.BooksGenerated - 1 : 0)
                .SetProperty(u => u.AddOnBalance, u => u.AddOnBalance + 1)
                .SetProperty(u => u.AddOnSpentThisPeriod, u => u.AddOnSpentThisPeriod > 0 ? u.AddOnSpentThisPeriod - 1 : 0))
            : user.ExecuteUpdateAsync(s => s
                .SetProperty(u => u.BooksGenerated, u => u.BooksGenerated > 0 ? u.BooksGenerated - 1 : 0));
    }

    private static async Task DeletePendingStoryAndRefundReservedCreditAsync(AppDbContext db, int userId, int storyId, bool usedAddOn)
    {
        // Remove the draft and return the credit together, retrying the pair on transient SQL errors.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.Stories.Where(s => s.Id == storyId && s.UserId == userId).ExecuteDeleteAsync();
            await RefundReservedCreditAsync(db, userId, usedAddOn);
            await tx.CommitAsync();
        });
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
