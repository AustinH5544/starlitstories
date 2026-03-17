using System.Security.Claims;
using Hackathon_2025.Data;
using Hackathon_2025.Models;
using Hackathon_2025.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Hackathon_2025.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdminController : ControllerBase
{
    private static readonly JsonSerializerOptions StoryRequestJsonOptions = new(JsonSerializerDefaults.Web);

    public sealed record UpdateUserAdminRequest
    {
        public string? Membership { get; init; }
        public int AddOnBalanceDelta { get; init; }
        public int BooksGeneratedDelta { get; init; }
        public bool ResetPeriodUsage { get; init; }
    }

    private readonly AppDbContext _db;
    private readonly IAdminAccessService _adminAccess;
    private readonly IConfiguration _config;

    public AdminController(AppDbContext db, IAdminAccessService adminAccess, IConfiguration config)
    {
        _db = db;
        _adminAccess = adminAccess;
        _config = config;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        if (!_adminAccess.IsAdmin(User))
        {
            return Forbid();
        }

        var now = DateTime.UtcNow;
        var weekAgo = now.AddDays(-7);
        var baseUrl = (_config["PublicBaseUrl"] ?? $"{Request.Scheme}://{Request.Host}").TrimEnd('/');

        var totalUsers = await _db.Users.CountAsync();
        var verifiedUsers = await _db.Users.CountAsync(u => u.IsEmailVerified);
        var paidUsers = await _db.Users.CountAsync(u => u.Membership != Models.MembershipPlan.Free);

        var totalStories = await _db.Stories.CountAsync();
        var storiesLast7Days = await _db.Stories.CountAsync(s => s.CreatedAt >= weekAgo);
        var generatingStories = await _db.Stories.CountAsync(s => !s.Pages.Any());

        var totalShares = await _db.StoryShares.CountAsync();
        var activeShares = await _db.StoryShares.CountAsync(s => s.RevokedUtc == null && (s.ExpiresUtc == null || s.ExpiresUtc > now));
        var expiredShares = await _db.StoryShares.CountAsync(s => s.RevokedUtc == null && s.ExpiresUtc != null && s.ExpiresUtc <= now);
        var revokedShares = await _db.StoryShares.CountAsync(s => s.RevokedUtc != null);

        var recentUsers = await _db.Users
            .AsNoTracking()
            .OrderByDescending(u => u.Id)
            .Take(100)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.Username,
                membership = u.Membership.ToString(),
                u.IsEmailVerified,
                u.PlanStatus,
                u.BooksGenerated,
                u.AddOnBalance,
                u.CurrentPeriodEndUtc,
                u.CancelAtUtc
            })
            .ToListAsync();

        var recentStories = await _db.Stories
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Take(100)
            .Select(s => new
            {
                s.Id,
                s.Title,
                s.CreatedAt,
                s.UserId,
                ownerEmail = s.User.Email,
                ownerUsername = s.User.Username,
                pageCount = s.Pages.Count,
                isGenerating = !s.Pages.Any(),
                shareCount = s.Shares.Count,
                requestTheme = s.RequestTheme,
                requestReadingLevel = s.RequestReadingLevel,
                requestArtStyle = s.RequestArtStyle,
                requestStoryLength = s.RequestStoryLength,
                requestLessonLearned = s.RequestLessonLearned,
                requestCharactersJson = s.RequestCharactersJson
            })
            .ToListAsync();

        var recentShares = await _db.StoryShares
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedUtc)
            .Take(100)
            .Select(s => new
            {
                s.Token,
                s.CreatedUtc,
                s.ExpiresUtc,
                s.RevokedUtc,
                storyId = s.StoryId,
                storyTitle = s.Story.Title,
                ownerEmail = s.Story.User.Email,
                ownerUsername = s.Story.User.Username
            })
            .ToListAsync();

        var shares = recentShares.Select(s => new
        {
            s.Token,
            s.CreatedUtc,
            s.ExpiresUtc,
            s.RevokedUtc,
            s.storyId,
            s.storyTitle,
            s.ownerEmail,
            s.ownerUsername,
            status = s.RevokedUtc != null
                ? "revoked"
                : (s.ExpiresUtc != null && s.ExpiresUtc <= now ? "expired" : "active"),
            url = $"{baseUrl}/s/{s.Token}"
        });

        return Ok(new
        {
            stats = new
            {
                totalUsers,
                verifiedUsers,
                paidUsers,
                totalStories,
                storiesLast7Days,
                generatingStories,
                totalShares,
                activeShares,
                expiredShares,
                revokedShares
            },
            recentUsers,
            recentStories = recentStories.Select(s => new
            {
                s.Id,
                s.Title,
                s.CreatedAt,
                s.UserId,
                s.ownerEmail,
                s.ownerUsername,
                s.pageCount,
                s.isGenerating,
                s.shareCount,
                s.requestTheme,
                s.requestReadingLevel,
                s.requestArtStyle,
                s.requestStoryLength,
                s.requestLessonLearned,
                requestCharacters = DeserializeCharacters(s.requestCharactersJson)
            }),
            recentShares = shares
        });
    }

    [HttpDelete("shares/{token}")]
    public async Task<IActionResult> RevokeShare(string token)
    {
        if (!_adminAccess.IsAdmin(User))
        {
            return Forbid();
        }

        var share = await _db.StoryShares.FirstOrDefaultAsync(s => s.Token == token);
        if (share == null)
        {
            return NotFound();
        }

        if (share.RevokedUtc == null)
        {
            share.RevokedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }

    [HttpGet("stories/{id:int}")]
    public async Task<IActionResult> GetStory(int id)
    {
        if (!_adminAccess.IsAdmin(User))
        {
            return Forbid();
        }

        var story = await _db.Stories
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new
            {
                s.Id,
                s.Title,
                s.CoverImageUrl,
                s.CreatedAt,
                ownerEmail = s.User.Email,
                ownerUsername = s.User.Username,
                requestTheme = s.RequestTheme,
                requestReadingLevel = s.RequestReadingLevel,
                requestArtStyle = s.RequestArtStyle,
                requestStoryLength = s.RequestStoryLength,
                requestLessonLearned = s.RequestLessonLearned,
                requestCharactersJson = s.RequestCharactersJson,
                IsGenerating = !s.Pages.Any(),
                Pages = s.Pages
                    .OrderBy(p => p.Id)
                    .Select(p => new { p.Text, p.ImageUrl })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (story == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            story.Id,
            story.Title,
            story.CoverImageUrl,
            story.CreatedAt,
            story.ownerEmail,
            story.ownerUsername,
            story.requestTheme,
            story.requestReadingLevel,
            story.requestArtStyle,
            story.requestStoryLength,
            story.requestLessonLearned,
            requestCharacters = DeserializeCharacters(story.requestCharactersJson),
            story.IsGenerating,
            story.Pages
        });
    }

    [HttpPatch("users/{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserAdminRequest request)
    {
        if (!_adminAccess.IsAdmin(User))
        {
            return Forbid();
        }

        var user = await _db.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Membership))
        {
            if (!Enum.TryParse<Models.MembershipPlan>(request.Membership, true, out var membership))
            {
                return BadRequest(new { message = "Invalid membership value." });
            }

            user.Membership = membership;
            user.PlanKey = membership.ToString().ToLowerInvariant();
            user.PlanStatus = membership == Models.MembershipPlan.Free
                ? "none"
                : (string.IsNullOrWhiteSpace(user.PlanStatus) || user.PlanStatus == "none" ? "manual" : user.PlanStatus);
        }

        if (request.ResetPeriodUsage)
        {
            user.BooksGenerated = 0;
            user.AddOnSpentThisPeriod = 0;
            user.LastReset = DateTime.UtcNow;
        }

        if (request.AddOnBalanceDelta != 0)
        {
            user.AddOnBalance = Math.Max(0, user.AddOnBalance + request.AddOnBalanceDelta);
        }

        if (request.BooksGeneratedDelta != 0)
        {
            user.BooksGenerated = Math.Max(0, user.BooksGenerated + request.BooksGeneratedDelta);
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            user.Id,
            user.Email,
            user.Username,
            membership = user.Membership.ToString(),
            user.PlanStatus,
            user.BooksGenerated,
            user.AddOnBalance,
            user.AddOnSpentThisPeriod,
            user.CurrentPeriodEndUtc,
            user.CancelAtUtc,
            user.LastReset
        });
    }

    [HttpGet("access")]
    public IActionResult GetAccess()
    {
        var email =
            User.FindFirstValue(ClaimTypes.Email) ??
            User.FindFirstValue("email");

        return Ok(new
        {
            isAdmin = _adminAccess.IsAdmin(User),
            email
        });
    }

    private static IReadOnlyList<object> DeserializeCharacters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<object>();
        }

        try
        {
            var characters = JsonSerializer.Deserialize<List<CharacterSpec>>(json, StoryRequestJsonOptions) ?? new();
            return characters.Select(c => new
            {
                c.Role,
                c.Name,
                c.IsAnimal,
                c.DescriptionFields
            }).Cast<object>().ToList();
        }
        catch
        {
            return Array.Empty<object>();
        }
    }
}
