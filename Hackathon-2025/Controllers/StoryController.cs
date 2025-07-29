using Microsoft.AspNetCore.Mvc;
using Hackathon_2025.Models;
using Hackathon_2025.Services;
using Hackathon_2025.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackathon_2025.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StoryController : ControllerBase
{
    private readonly IStoryGeneratorService _storyService;
    private readonly AppDbContext _db;
    private readonly BlobUploadService _blobService;

    public StoryController(IStoryGeneratorService storyService, AppDbContext db, BlobUploadService blobService)
    {
        _storyService = storyService;
        _db = db;
        _blobService = blobService;
    }

    [HttpPost("generate-full")]
    public async Task<IActionResult> GenerateFullStory([FromBody] StoryRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Email) || request.Characters.Count == 0)
        {
            return BadRequest("Invalid request: Email and at least one character are required.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null)
        {
            return Unauthorized("User not found.");
        }

        var now = DateTime.UtcNow;

        // Validation for free users
        if (user.Membership == "free" && user.BooksGenerated >= 1)
        {
            return StatusCode(403, "Free users can only generate one story.");
        }
        else
        {
            int monthlyLimit = user.Membership switch
            {
                "pro" => 10,
                "premium" => 50,
                _ => 1
            };

            if (user.LastReset.Month != now.Month || user.LastReset.Year != now.Year)
            {
                user.BooksGenerated = 0;
                user.LastReset = now;
            }

            if (user.BooksGenerated >= monthlyLimit)
            {
                return StatusCode(403, $"Your {user.Membership} plan allows {monthlyLimit} books per month. You've reached your limit.");
            }
        }

        // Generate the story
        var result = await _storyService.GenerateFullStoryAsync(request);

        //  Upload cover image to Azure Blob Storage
        var coverFileName = $"{user.Email}-cover-{Guid.NewGuid()}.png";
        var coverBlobUrl = await _blobService.UploadImageAsync(result.CoverImageUrl, coverFileName);
        result.CoverImageUrl = coverBlobUrl;

        //  Upload each page image to Azure Blob Storage
        for (int i = 0; i < result.Pages.Count; i++)
        {
            if (!string.IsNullOrEmpty(result.Pages[i].ImageUrl))
            {
                var pageFileName = $"{user.Email}-page-{i}-{Guid.NewGuid()}.png";
                var blobUrl = await _blobService.UploadImageAsync(result.Pages[i].ImageUrl!, pageFileName);
                result.Pages[i].ImageUrl = blobUrl;
            }
        }

        //  Save story to database with Blob URLs
        Story story = new Story
        {
            Title = result.Title,
            CoverImageUrl = result.CoverImageUrl,
            CreatedAt = DateTime.UtcNow,
            UserId = user.Id,
            Pages = result.Pages.Select(p => new StoryPage
            {
                Text = p.Text,
                ImagePrompt = p.ImagePrompt,
                ImageUrl = p.ImageUrl
            }).ToList()
        };

        _db.Stories.Add(story);
        user.BooksGenerated += 1;
        await _db.SaveChangesAsync();

        return Ok(result);
    }

    [HttpGet("ping")]
    public IActionResult Ping() => Ok("Story API is alive!");
}