using Microsoft.AspNetCore.Mvc;
using Hackathon_2025.Models;
using Hackathon_2025.Services; // Add this if you place the service in a separate namespace

namespace Hackathon_2025.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StoryController : ControllerBase
{
    private readonly IStoryGeneratorService _storyService;

    public StoryController(IStoryGeneratorService storyService)
    {
        _storyService = storyService;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> GenerateStory([FromBody] StoryRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.CharacterName))
        {
            return BadRequest("Invalid input");
        }

        var pages = await _storyService.GenerateFullStoryAsync(request);
        return Ok(new { pages });
    }

    [HttpPost("generate-full")]
    public async Task<IActionResult> GenerateFullStory([FromBody] StoryRequest request)
    {
        var result = await _storyService.GenerateFullStoryAsync(request);
        return Ok(result);
    }


    [HttpGet("ping")]
    public IActionResult Ping() => Ok("Story API is alive!");
}
