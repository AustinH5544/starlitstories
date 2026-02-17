using System.ComponentModel.DataAnnotations;

namespace Hackathon_2025.Models;

public class User
{
    public int Id { get; set; }

    [EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Username { get; set; } = string.Empty;

    [MaxLength(32)]
    public string UsernameNormalized { get; set; } = string.Empty;

    [MaxLength(256)]
    public string PasswordHash { get; set; } = string.Empty;

    public MembershipPlan Membership { get; set; } = MembershipPlan.Free;

    public int BooksGenerated { get; set; }
    public int AddOnBalance { get; set; }
    public int AddOnSpentThisPeriod { get; set; }
    public DateTime LastReset { get; set; } = DateTime.UtcNow;

    public string? ProfileImage { get; set; }

    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetExpires { get; set; }

    public bool IsEmailVerified { get; set; }
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationExpires { get; set; }

    public string? BillingProvider { get; set; }
    public string? BillingCustomerRef { get; set; }
    public string? BillingSubscriptionRef { get; set; }

    [MaxLength(32)] public string PlanKey { get; set; } = "free";
    [MaxLength(32)] public string PlanStatus { get; set; } = "none";

    public DateTime? CurrentPeriodEndUtc { get; set; }
    public DateTime? CurrentPeriodStartUtc { get; set; }
    public DateTime? CancelAtUtc { get; set; }

    public ICollection<Story> Stories { get; set; } = new List<Story>();
}