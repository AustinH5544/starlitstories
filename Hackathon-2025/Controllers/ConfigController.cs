using Hackathon_2025.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hackathon_2025.Controllers;

[ApiController]
[Route("api/config")]
public class ConfigController : ControllerBase
{
    private readonly IOptionsSnapshot<StoryOptions> _story;

    public ConfigController(IOptionsSnapshot<StoryOptions> story)
        => _story = story;

    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Get()
    {
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, proxy-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";
        return Ok(new { lengthHintEnabled = _story.Value.LengthHintEnabled });
    }
}
