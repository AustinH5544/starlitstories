using Hackathon_2025.Models;
using Hackathon_2025.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Hackathon_2025.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FeedbackController : ControllerBase
{
    private readonly ILogger<FeedbackController> _logger;
    private readonly IConfiguration _config;
    private readonly EmailService _emailService; // concrete EmailService for HTML support

    public FeedbackController(
        ILogger<FeedbackController> logger,
        IConfiguration config,
        EmailService emailService)
    {
        _logger = logger;
        _config = config;
        _emailService = emailService;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] FeedbackDto dto)
    {
        if (dto is null || dto.Enjoyment is null)
            return BadRequest("Missing required fields.");

        try
        {
            var targets = (dto.Notify is { Length: > 0 })
                ? dto.Notify
                : new[] { "austintylerdevelopment@gmail.com", "support@starlitstories.app" };

            var subject = $"⭐ Starlit Feedback — {dto.StoryTitle ?? "Untitled"} (Enjoyment {dto.Enjoyment}/5)";

            var sb = new StringBuilder();
            sb.AppendLine("<html><body style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;'>");
            sb.AppendLine("<h2 style='color:#4f46e5'>Starlit Stories Beta Feedback</h2>");
            sb.AppendLine("<table style='border-collapse:collapse;width:100%;font-size:14px;'>");

            void Row(string label, object? value)
                => sb.AppendLine($"<tr><td style='padding:6px 8px;font-weight:600;width:40%;border-bottom:1px solid #eee;'>{label}</td><td style='padding:6px 8px;border-bottom:1px solid #eee;'>{value}</td></tr>");

            Row("Story", $"{dto.StoryTitle} (ID: {dto.StoryId})");
            Row("Pages", dto.PageCount);
            Row("Estimated Read", $"~{dto.EstReadMin} min");
            Row("Actual Read", dto.ActualReadMin);
            Row("Enjoyment", $"{dto.Enjoyment}/5");
            Row("Personalization", dto.Personalization);
            Row("Illustrations", dto.Illustrations);
            Row("Navigation", dto.Navigation);
            Row("Read Time Accuracy", dto.ReadTimeAccuracy);
            Row("Performance", dto.Performance);
            Row("Bugs / Glitches", dto.Bugs);
            Row("Likes", dto.Likes);
            Row("Improvements", dto.Improvements);
            Row("Future Interest", dto.FutureInterest);
            Row("Reporter", $"{dto.Name} &lt;{dto.Email}&gt;");
            sb.AppendLine("</table>");
            sb.AppendLine("<p style='margin-top:20px;color:#6b7280;font-size:13px;'>This message was auto-sent from the feedback form on starlitstories.app.</p>");
            sb.AppendLine("</body></html>");

            var body = sb.ToString();

            foreach (var to in targets)
            {
                await _emailService.SendCustomEmailAsync(to, subject, body);
            }

            _logger.LogInformation("Feedback email sent for {Story} from {User}", dto.StoryTitle, dto.Email);
            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process feedback");
            return StatusCode(500, new { error = "Feedback email failed." });
        }
    }
}
