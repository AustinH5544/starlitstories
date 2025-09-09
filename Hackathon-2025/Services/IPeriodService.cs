using Hackathon_2025.Models;

namespace Hackathon_2025.Services
{
    public interface IPeriodService
    {
        /// <summary>Returns the active period window [start,end) for display/gating.</summary>
        (DateTime startUtc, DateTime endUtc) CurrentPeriodUtc(User user, DateTime now);

        /// <summary>True when we've crossed into a new billing period.</summary>
        bool IsPeriodBoundary(User user, DateTime now);

        /// <summary>Mutates the user to reset per-period counters at boundary.</summary>
        void OnPeriodRollover(User user, DateTime now);
    }
}