using System.ComponentModel.DataAnnotations;

namespace Hackathon_2025.Options;

public sealed class BillingOptions
{
    public string Provider { get; init; } = "stripe";
    public BillingPricingOptions Pricing { get; init; } = new();
    public LaunchSaleOptions LaunchSale { get; init; } = new();
}

public sealed class BillingPricingOptions
{
    public PlanPriceDisplayOptions Free { get; init; } = new() { Price = "$0/month" };
    public PlanPriceDisplayOptions Pro { get; init; } = new() { Price = "$4.99/month" };
    public PlanPriceDisplayOptions Premium { get; init; } = new() { Price = "$9.99/month" };
}

public sealed class PlanPriceDisplayOptions
{
    [Required]
    public string Price { get; init; } = "";
}

public sealed class LaunchSaleOptions
{
    public bool Enabled { get; init; }
    public string BadgeText { get; init; } = "Launch Sale";
    public string DiscountMode { get; init; } = "forever";
    public int DurationInMonths { get; init; } = 1;
    public string ProCouponIdForever { get; init; } = "";
    public string ProCouponIdLimited { get; init; } = "";
    public string PremiumCouponIdForever { get; init; } = "";
    public string PremiumCouponIdLimited { get; init; } = "";
    public string ProSalePrice { get; init; } = "";
    public string PremiumSalePrice { get; init; } = "";
}
