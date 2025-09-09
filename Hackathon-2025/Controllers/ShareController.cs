using Hackathon_2025.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Hackathon_2025.Data;

[ApiController]
[Route("api")]
public class ShareController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;

    public ShareController(AppDbContext db, IConfiguration cfg)
    {
        _db = db; _cfg = cfg;
    }

    private int? GetUserId()
    {
        // Try common claim names you might be issuing
        var s =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub") ??
            User.FindFirstValue("id") ??
            User.FindFirstValue("userId");

        return int.TryParse(s, out var id) ? id : (int?)null;
    }

        // Create or get a share link for a story you own
    [HttpPost("stories/{id:int}/share")]
    [Authorize]
    public async Task<IActionResult> CreateShare(int id, [FromQuery] int? days = null)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(); // no valid user id in token

        var story = await _db.Stories
            .Include(s => s.Pages)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId.Value);
        if (story == null) return NotFound();

        var active = await _db.StoryShares
            .Where(x => x.StoryId == id && x.RevokedUtc == null &&
                        (x.ExpiresUtc == null || x.ExpiresUtc > DateTime.UtcNow))
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        var share = active ?? new StoryShare { StoryId = id };
        if (days is int d && d > 0) share.ExpiresUtc = DateTime.UtcNow.AddDays(d);
        if (active == null) { _db.StoryShares.Add(share); await _db.SaveChangesAsync(); }

        var baseUrl = _cfg["PublicBaseUrl"]?.TrimEnd('/') ?? $"{Request.Scheme}://{Request.Host}";
        var url = $"{baseUrl}/s/{share.Token}";
        return Ok(new { url, token = share.Token, expiresUtc = share.ExpiresUtc });
    }

    // Revoke a share link
    [HttpDelete("share/{token}")]
    [Authorize]
    public async Task<IActionResult> Revoke(string token)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var share = await _db.StoryShares
            .Include(s => s.Story)
            .FirstOrDefaultAsync(s => s.Token == token &&
                                      s.RevokedUtc == null &&
                                      s.Story.UserId == userId.Value);
        if (share == null) return NotFound();

        share.RevokedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Public: resolve a share token to a viewable story (no auth)
    [HttpGet("share/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetShared(string token)
    {
        var share = await _db.StoryShares
            .Include(s => s.Story).ThenInclude(x => x.Pages)
            .FirstOrDefaultAsync(s => s.Token == token);
        if (share == null || share.RevokedUtc != null ||
            (share.ExpiresUtc != null && share.ExpiresUtc <= DateTime.UtcNow))
            return NotFound();

        // Return a safe DTO (only what the viewer needs)
        var pagesDto = share.Story.Pages
            .OrderBy(p => p.Id)
            .AsEnumerable() // switch to LINQ-to-Objects so we can use the index
            .Select((p, idx) => new { pageNumber = idx, text = p.Text, imageUrl = p.ImageUrl });

        var dto = new
        {
            id = share.Story.Id,
            title = share.Story.Title,
            coverImageUrl = share.Story.CoverImageUrl,
            pages = pagesDto
        };
        Response.Headers.Append("X-Robots-Tag", "noindex, nofollow");
        return Ok(dto);
    }
}
