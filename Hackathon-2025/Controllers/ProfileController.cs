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
            user.LastReset
        });
    }

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