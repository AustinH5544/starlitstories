using System.Collections.Generic;

namespace Hackathon_2025.Models;

/// <summary>
/// Product knobs for quotas/credits. Not secrets.
/// </summary>
public sealed class CreditsOptions
{
    public bool CarryoverEnabled { get; set; } = true;
    public bool RequirePremiumForAddons { get; set; } = true;
    public bool OnlyAllowPurchaseWhenExhausted { get; set; } = true;

    public Dictionary<string, int> BaseQuotas { get; set; }
        = new(StringComparer.OrdinalIgnoreCase) { ["free"] = 1, ["pro"] = 5, ["premium"] = 11, ["storybook"] = 11 };

    public Dictionary<string, int> SuperStoryQuotas { get; set; }
        = new(StringComparer.OrdinalIgnoreCase) { ["storybook"] = 1 };

    public int GetBaseQuota(string? membership)
        => string.IsNullOrWhiteSpace(membership) ? BaseQuotas["free"]
           : (BaseQuotas.TryGetValue(membership, out var q) ? q : 0);

    public int GetSuperStoryQuota(string? membership)
        => string.IsNullOrWhiteSpace(membership) ? 0
           : (SuperStoryQuotas.TryGetValue(membership, out var q) ? q : 0);
}
