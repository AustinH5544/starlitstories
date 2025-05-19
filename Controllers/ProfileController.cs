using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hackathon_2025.Data;
using Hackathon_2025.Models;

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

    // GET: api/profile/{email}
    [HttpGet("{email}")]
    public async Task<IActionResult> GetProfile(string email)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
            return NotFound("User not found.");

        return Ok(new
        {
            user.Email,
            user.Membership,
            user.BooksGenerated
        });
    }

    // GET: api/profile/{email}/stories
    [HttpGet("{email}/stories")]
    public async Task<IActionResult> GetUserStories(string email)
    {
        var user = await _db.Users
            .Include(u => u.Stories)
                .ThenInclude(s => s.Pages)
            .FirstOrDefaultAsync(u => u.Email == email);

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