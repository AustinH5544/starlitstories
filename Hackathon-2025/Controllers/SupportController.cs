using System.Net;
using System.Net.Mail;
using System.Text;
using Hackathon_2025.Models;
using Hackathon_2025.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Hackathon_2025.Controllers;

/// <summary>
/// Public contact form on /support. Emails the message to the support inbox with Reply-To set to the sender.
/// Public on purpose (logged-out visitors need help too), so it is rate limited and behind Turnstile.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("support-ip")]
public class SupportController : ControllerBase
{
    private const string DefaultRecipient = "support@starlitstories.app";
    private const int MaxNameLength = 100;
    private const int MaxEmailLength = 254;
    private const int MaxShortFieldLength = 50;
    private const int MaxSubjectLength = 200;
    private const int MaxMessageLength = 5000;

    private readonly IEmailService _email;
    private readonly ITurnstileService _turnstile;
    private readonly IConfiguration _config;
    private readonly ILogger<SupportController> _logger;

    public SupportController(IEmailService email, ITurnstileService turnstile, IConfiguration config, ILogger<SupportController> logger)
    {
        _email = email;
        _turnstile = turnstile;
        _config = config;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] SupportRequest? request)
    {
        var name = request?.Name?.Trim() ?? "";
        var email = request?.Email?.Trim() ?? "";
        var category = request?.Category?.Trim() ?? "";
        var priority = request?.Priority?.Trim() ?? "";
        var subject = request?.Subject?.Trim() ?? "";
        var message = request?.Message?.Trim() ?? "";

        if (!IsValidEmail(email))
            return BadRequest(new { message = "Please enter a valid email address so we can reply." });
        if (subject.Length == 0 || message.Length == 0)
            return BadRequest(new { message = "Please add a subject and a message." });
        if (name.Length > MaxNameLength || category.Length > MaxShortFieldLength || priority.Length > MaxShortFieldLength
            || subject.Length > MaxSubjectLength || message.Length > MaxMessageLength)
            return BadRequest(new { message = $"Please keep the subject under {MaxSubjectLength} characters and the message under {MaxMessageLength}." });

        var human = await _turnstile.VerifyAsync(
            request?.TurnstileToken,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.RequestAborted);
        if (!human.Success)
            return BadRequest(new { message = human.ErrorMessage ?? "Human verification failed. Please try again." });

        var emailSubject = OneLine(category.Length > 0 ? $"[Support][{category}] {subject}" : $"[Support] {subject}");
        var body = BuildBody(name, email, category, priority, subject, message);

        try
        {
            foreach (var to in GetRecipients())
                await _email.SendCustomEmailAsync(to, emailSubject, body, replyTo: email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Support message from {Email} could not be emailed", email);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = $"We couldn't send your message right now. Please email us directly at {DefaultRecipient}."
            });
        }

        _logger.LogInformation("Support message sent from {Email} ({Category})", email, category);
        return Ok(new { ok = true });
    }

    private IEnumerable<string> GetRecipients()
    {
        var configured = (_config["Support:RecipientsCsv"] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return configured.Count > 0 ? configured : new[] { DefaultRecipient };
    }

    private static bool IsValidEmail(string email) =>
        email.Length is > 0 and <= MaxEmailLength
        && MailAddress.TryCreate(email, out var parsed)
        && string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);

    // Strip line breaks so user text can't add mail headers via the subject.
    private static string OneLine(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

    private static string BuildBody(string name, string email, string category, string priority, string subject, string message)
    {
        static string Enc(string value) => WebUtility.HtmlEncode(value);

        var sb = new StringBuilder();
        sb.AppendLine("<html><body style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;'>");
        sb.AppendLine("<h2 style='color:#4f46e5'>New support message</h2>");
        sb.AppendLine("<table style='border-collapse:collapse;width:100%;font-size:14px;'>");

        void Row(string label, string value) =>
            sb.AppendLine($"<tr><td style='padding:6px 8px;font-weight:600;width:30%;border-bottom:1px solid #eee;'>{Enc(label)}</td><td style='padding:6px 8px;border-bottom:1px solid #eee;'>{Enc(value)}</td></tr>");

        Row("From", name.Length > 0 ? $"{name} <{email}>" : email);
        Row("Category", category.Length > 0 ? category : "(none)");
        Row("Priority", priority.Length > 0 ? priority : "(none)");
        Row("Subject", subject);
        sb.AppendLine("</table>");
        sb.AppendLine($"<p style='white-space:pre-wrap;font-size:14px;line-height:1.5;margin-top:16px;'>{Enc(message)}</p>");
        sb.AppendLine("<p style='margin-top:20px;color:#6b7280;font-size:13px;'>Sent from the contact form on starlitstories.app. Reply to this email to answer the sender.</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}
