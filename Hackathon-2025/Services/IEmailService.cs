namespace Hackathon_2025.Services;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string email, string verificationToken);
    Task SendPasswordResetEmailAsync(string email, string resetToken);
}
