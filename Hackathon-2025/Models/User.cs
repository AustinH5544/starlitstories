namespace Hackathon_2025.Models;

public class User
{
    public List<Story> Stories { get; set; } = new();
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    // NEW: public handle used for login and public identity
    public string Username { get; set; } = string.Empty;
    public string UsernameNormalized { get; set; } = string.Empty; // lowercase for unique index

    public string PasswordHash { get; set; } = string.Empty;
    public string Membership { get; set; } = "free";
    public int BooksGenerated { get; set; } = 0;
    public int AddOnBalance { get; set; } = 0;
    public int AddOnSpentThisPeriod { get; set; } = 0;
    public DateTime LastReset { get; set; } = DateTime.UtcNow;
    public string? ProfileImage { get; set; }

    // Password reset
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetExpires { get; set; }

    // Email verification
    public bool IsEmailVerified { get; set; } = false;
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationExpires { get; set; }

    // Billing / subscription
    public string? BillingProvider { get; set; }
    public string? BillingCustomerRef { get; set; }
    public string? BillingSubscriptionRef { get; set; }
    public string PlanKey { get; set; } = "free";
    public string PlanStatus { get; set; } = "none";
    public DateTime? CurrentPeriodEndUtc { get; set; }
    public DateTime? CurrentPeriodStartUtc { get; set; }
}