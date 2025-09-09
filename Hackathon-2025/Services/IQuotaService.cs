using Hackathon_2025.Models;

namespace Hackathon_2025.Services
{
    public interface IQuotaService
    {
        int BaseQuotaFor(string? membership);

        bool CarryoverEnabled();

        bool RequirePremiumForAddons();

        bool OnlyAllowPurchaseWhenExhausted();

        /// <summary>
        /// Should the user be allowed to buy add-on credits right now?
        /// Uses membership level and, optionally, current remaining counts.
        /// </summary>
        bool CanBuyAddons(string? membership, int baseRemaining, int addOnBalance);
    }
}