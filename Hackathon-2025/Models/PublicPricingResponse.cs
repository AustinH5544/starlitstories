using Hackathon_2025.Options;

namespace Hackathon_2025.Models;

public sealed class PublicPricingResponse
{
    public PublicPlanPricing Free { get; init; } = new();
    public PublicPlanPricing Pro { get; init; } = new();
    public PublicPlanPricing Premium { get; init; } = new();

    public static PublicPricingResponse From(BillingOptions billing)
    {
        var launchSale = billing.LaunchSale;
        var saleEnabled = launchSale.Enabled;
        var saleHint = saleEnabled
            ? launchSale.DiscountMode.Trim().Equals("months", StringComparison.OrdinalIgnoreCase)
                ? $"First {Math.Max(1, launchSale.DurationInMonths)} month{(Math.Max(1, launchSale.DurationInMonths) == 1 ? "" : "s")} at launch pricing"
                : "Launch pricing applies while subscribed"
            : null;

        return new PublicPricingResponse
        {
            Free = new PublicPlanPricing
            {
                Price = billing.Pricing.Free.Price
            },
            Pro = BuildPaidPlan(
                billing.Pricing.Pro.Price,
                launchSale.ProSalePrice,
                saleEnabled,
                launchSale.BadgeText,
                saleHint),
            Premium = BuildPaidPlan(
                billing.Pricing.Premium.Price,
                launchSale.PremiumSalePrice,
                saleEnabled,
                launchSale.BadgeText,
                saleHint)
        };
    }

    private static PublicPlanPricing BuildPaidPlan(
        string basePrice,
        string salePrice,
        bool saleEnabled,
        string badgeText,
        string? saleHint)
    {
        var hasSalePrice = !string.IsNullOrWhiteSpace(salePrice);
        var isOnSale = saleEnabled && hasSalePrice;

        return new PublicPlanPricing
        {
            Price = isOnSale ? salePrice : basePrice,
            OriginalPrice = isOnSale ? basePrice : null,
            IsOnSale = isOnSale,
            BadgeText = isOnSale ? badgeText : null,
            SaleHint = isOnSale ? saleHint : null
        };
    }
}

public sealed class PublicPlanPricing
{
    public string Price { get; init; } = "";
    public string? OriginalPrice { get; init; }
    public bool IsOnSale { get; init; }
    public string? BadgeText { get; init; }
    public string? SaleHint { get; init; }
}
