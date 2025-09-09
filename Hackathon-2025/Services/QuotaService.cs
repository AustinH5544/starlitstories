using Hackathon_2025.Models;
using Microsoft.Extensions.Options;

namespace Hackathon_2025.Services
{
    public sealed class QuotaService : IQuotaService
    {
        private readonly IOptionsSnapshot<CreditsOptions> _opts;

        public QuotaService(IOptionsSnapshot<CreditsOptions> opts)
        {
            _opts = opts;
        }

        public int BaseQuotaFor(string? membership)
        {
            var key = string.IsNullOrWhiteSpace(membership) ? "free" : membership!;
            return _opts.Value.BaseQuotas.TryGetValue(key, out var q) ? q : 0;
        }

        public bool CarryoverEnabled() => _opts.Value.CarryoverEnabled;

        public bool RequirePremiumForAddons() => _opts.Value.RequirePremiumForAddons;

        public bool OnlyAllowPurchaseWhenExhausted() => _opts.Value.OnlyAllowPurchaseWhenExhausted;

        public bool CanBuyAddons(string? membership, int baseRemaining, int addOnBalance)
        {
            // Require premium membership?
            if (RequirePremiumForAddons() && !string.Equals(membership, "premium", System.StringComparison.OrdinalIgnoreCase))
                return false;

            // Only allow purchase when base is exhausted?
            if (OnlyAllowPurchaseWhenExhausted() && baseRemaining > 0)
                return false;

            // If you wanted: disallow buying when user still has carryover add-ons.
            // For now, allow purchases even if addOnBalance > 0.
            return true;
        }
    }
}