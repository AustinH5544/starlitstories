using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hackathon_2025.Data;
using Hackathon_2025.Models;
using System.Security.Claims;

namespace Hackathon_2025.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProfileController(AppDbContext db)
    {
        _db = db;
    }

    // GET: api/profile/me
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            return Unauthorized("Invalid or missing user ID.");

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return NotFound("User not found.");

        return Ok(new
        {
            user.Email,
            user.Membership,
            user.BooksGenerated,
            user.LastReset,
            profileImage = user.ProfileImage
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

    // GET: api/profile/me/stories
    [Authorize]
    [HttpGet("me/stories")]
    public async Task<IActionResult> GetMyStories()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            return Unauthorized("Invalid or missing user ID.");

        var user = await _db.Users
            .Include(u => u.Stories)
                .ThenInclude(s => s.Pages)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound("User not found.");

        var stories = user.Stories
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.Id,
                s.Title,
                s.CoverImageUrl,
                s.CreatedAt,
                Pages = s.Pages.Select(p => new
                {
                    p.Text,
                    p.ImageUrl
                })
            });

        return Ok(stories);
    }
}