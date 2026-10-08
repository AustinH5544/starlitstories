namespace Hackathon_2025.Services;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string email, string verificationToken);
    Task SendPasswordResetEmailAsync(string email, string resetToken);

    /// <summary>Sends an HTML email and throws if it can't be sent. <paramref name="replyTo"/> overrides Email:ReplyTo.</summary>
    Task SendCustomEmailAsync(string to, string subject, string htmlBody, string? replyTo = null);
}
