using Hackathon_2025.Services;
using System.Net;
using System.Net.Mail;

namespace Hackathon_2025.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendVerificationEmailAsync(string email, string verificationToken)
    {
        var subject = "Verify Your Email - Starlit Stories";
        var baseUrl = (_config["App:BaseUrl"] ?? "").TrimEnd('/');
        var verificationUrl = $"{baseUrl}/verify-email?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(verificationToken)}";

        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                <div style='text-align: center; margin-bottom: 30px;'>
                    <h1 style='color: #4f46e5; margin-bottom: 10px;'>Welcome to Starlit Stories! 📚</h1>
                    <p style='color: #6b7280; font-size: 16px;'>Please verify your email address to get started</p>
                </div>

                <div style='background: #f9fafb; padding: 30px; border-radius: 10px; margin-bottom: 30px;'>
                    <p style='color: #374151; font-size: 16px; line-height: 1.6; margin-bottom: 20px;'>
                        Hi there! 👋
                    </p>
                    <p style='color: #374151; font-size: 16px; line-height: 1.6; margin-bottom: 20px;'>
                        Thank you for signing up for Starlit Stories! To complete your registration and start creating magical stories for your little ones, please verify your email address by clicking the button below.
                    </p>

                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{verificationUrl}'
                           style='background: #4f46e5; color: white; padding: 15px 30px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block; font-size: 16px;'>
                            ✨ Verify My Email
                        </a>
                    </div>

                    <p style='color: #6b7280; font-size: 14px; line-height: 1.6;'>
                        If the button doesn't work, you can copy and paste this link into your browser:<br>
                        <a href='{verificationUrl}' style='color: #4f46e5; word-break: break-all;'>{verificationUrl}</a>
                    </p>
                </div>

                <div style='text-align: center; color: #6b7280; font-size: 14px;'>
                    <p>This verification link will expire in 24 hours.</p>
                    <p>If you didn't create an account with us, please ignore this email.</p>
                </div>
            </body>
            </html>";

        await SendEmailAsync(email, subject, body);
    }

    public async Task SendPasswordResetEmailAsync(string email, string resetToken)
    {
        var subject = "Reset Your Password - Starlit Stories";
        var baseUrl = (_config["App:BaseUrl"] ?? "").TrimEnd('/');
        var resetUrl = $"{baseUrl}/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(resetToken)}";

        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                <div style='text-align: center; margin-bottom: 30px;'>
                    <h1 style='color: #4f46e5; margin-bottom: 10px;'>Password Reset Request 🔐</h1>
                    <p style='color: #6b7280; font-size: 16px;'>Reset your Starlit Stories password</p>
                </div>

                <div style='background: #f9fafb; padding: 30px; border-radius: 10px; margin-bottom: 30px;'>
                    <p style='color: #374151; font-size: 16px; line-height: 1.6; margin-bottom: 20px;'>
                        Hi there! 👋
                    </p>
                    <p style='color: #374151; font-size: 16px; line-height: 1.6; margin-bottom: 20px;'>
                        We received a request to reset your password for your Starlit Stories account. Click the button below to create a new password.
                    </p>

                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{resetUrl}'
                           style='background: #dc2626; color: white; padding: 15px 30px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block; font-size: 16px;'>
                            🔑 Reset My Password
                        </a>
                    </div>

                    <p style='color: #6b7280; font-size: 14px; line-height: 1.6;'>
                        If the button doesn't work, you can copy and paste this link into your browser:<br>
                        <a href='{resetUrl}' style='color: #4f46e5; word-break: break-all;'>{resetUrl}</a>
                    </p>
                </div>

                <div style='text-align: center; color: #6b7280; font-size: 14px;'>
                    <p>This reset link will expire in 1 hour.</p>
                    <p>If you didn't request a password reset, please ignore this email.</p>
                </div>
            </body>
            </html>";

        await SendEmailAsync(email, subject, body);
    }

    private async Task SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            var smtpHost = _config["Email:SmtpHost"];
            var smtpPort = int.Parse(_config["Email:SmtpPort"] ?? "587");
            var smtpUsername = _config["Email:SmtpUsername"];
            var smtpPassword = _config["Email:SmtpPassword"];
            var fromEmail = _config["Email:FromEmail"];
            var fromName = _config["Email:FromName"] ?? "Starlit Stories";

            if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpUsername) || string.IsNullOrEmpty(smtpPassword))
            {
                _logger.LogWarning("Email configuration is missing. Email not sent to {Email}", to);
                _logger.LogInformation("Email would have been sent to {Email} with subject: {Subject}", to, subject);
                return;
            }

            using var client = new SmtpClient(smtpHost, smtpPort);
            client.EnableSsl = true;
            client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);

            var message = new MailMessage
            {
                From = new MailAddress(fromEmail!, fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            message.To.Add(to);

            await client.SendMailAsync(message);
            _logger.LogInformation("Verification email sent successfully to {Email}", to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", to);
            // Don't throw - we don't want email failures to break the signup process
        }
    }
}