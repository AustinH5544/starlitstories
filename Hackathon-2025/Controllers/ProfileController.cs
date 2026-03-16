using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hackathon_2025.Data;
using Hackathon_2025.Models;
using Hackathon_2025.Services;
using System.Security.Claims;

namespace Hackathon_2025.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAdminAccessService _adminAccess;

    public ProfileController(AppDbContext db, IAdminAccessService adminAccess)
    {
        _db = db;
        _adminAccess = adminAccess;
    }

    private bool TryGetUserId(out int userId)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdStr, out userId);
    }

    // GET: api/profile/me
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized("Invalid or missing user ID.");

        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Email,
                u.Username,
                u.Membership,
                u.BooksGenerated,
                u.LastReset,
                profileImage = u.ProfileImage
            })
            .FirstOrDefaultAsync();

        if (user is null) return NotFound("User not found.");

        return Ok(new
        {
            user.Email,
            user.Username,
            user.Membership,
            user.BooksGenerated,
            user.LastReset,
            user.profileImage,
            isAdmin = _adminAccess.IsAdminEmail(user.Email)
        });
    }

    [Authorize]
    [HttpPut("avatar")]
    public async Task<IActionResult> UpdateAvatar([FromBody] AvatarDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await _db.Users.FindAsync(int.Parse(userId));
        if (user is null) return NotFound();

        // If you only allow preset avatars, validate against a whitelist
        var allowed = new HashSet<string>{
        "wizard-avatar.png","princess-avatar.png","knight-avatar.png",
        "whimsical-fairy-avatar.png","dragon-avatar.png","unicorn-avatar.png",
        "pirate-avatar.png","astronaut-avatar.png","whimsical-mermaid-avatar.png",
        "superhero-avatar.png","cat-avatar.png"
    };
        if (!allowed.Contains(dto.ProfileImage) && !Uri.IsWellFormedUriString(dto.ProfileImage, UriKind.Absolute))
            return BadRequest("Invalid avatar");

        user.ProfileImage = dto.ProfileImage;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    public record AvatarDto(string ProfileImage);
    public record UsernameDto(string Username);

    [Authorize]
    [HttpPut("username")]
    public async Task<IActionResult> UpdateUsername([FromBody] UsernameDto dto)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized("Invalid or missing user ID.");

        var username = dto.Username?.Trim() ?? string.Empty;
        if (!UsernameRules.IsValid(username))
            return BadRequest(new { message = "Use 3-24 chars: a-z, 0-9, dot, underscore, hyphen." });

        var normalized = UsernameRules.Normalize(username);
        var taken = await _db.Users.AnyAsync(u => u.Id != userId && u.UsernameNormalized == normalized);
        if (taken)
            return Conflict(new { message = "That username is taken." });

        var user = await _db.Users.FindAsync(userId);
        if (user is null) return NotFound();

        user.Username = username;
        user.UsernameNormalized = normalized;
        await _db.SaveChangesAsync();

        return Ok(new { username = user.Username });
    }

    // GET: api/profile/me/stories
    // Summaries only (no pages). Use page + pageSize with sensible defaults.
    [Authorize]
    [HttpGet("me/stories")]
    public async Task<IActionResult> GetMyStories([FromQuery] int page = 1, [FromQuery] int pageSize = 6)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized("Invalid or missing user ID.");

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _db.Stories
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt);

        var total = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.Title,
                s.CoverImageUrl,
                s.CreatedAt,
                // lightweight info about pages (counts only)
                PageCount = s.Pages.Count,
                IsGenerating = !s.Pages.Any()
            })
            .ToListAsync();

        return Ok(new
        {
            page,
            pageSize,
            total,
            items
        });
    }

    // GET: api/profile/me/stories/{id}
    // Fetch a single story WITH pages when the user actually views it.
    [Authorize]
    [HttpGet("me/stories/{id:int}")]
    public async Task<IActionResult> GetStory(int id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized("Invalid or missing user ID.");

        // Split query to avoid row explosion when including pages
        var story = await _db.Stories
            .AsNoTracking()
            .Where(s => s.Id == id && s.UserId == userId)
            .Select(s => new
            {
                s.Id,
                s.Title,
                s.CoverImageUrl,
                s.CreatedAt,
                IsGenerating = !s.Pages.Any(),
                Pages = s.Pages
                    .OrderBy(p => p.Id)
                    .Select(p => new { p.Text, p.ImageUrl })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (story is null) return NotFound("Story not found.");

        return Ok(story);
    }
}
