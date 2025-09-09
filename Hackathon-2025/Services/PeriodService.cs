// File: Services/PeriodService.cs
using Hackathon_2025.Models;
using Microsoft.Extensions.Options;

namespace Hackathon_2025.Services
{
    public sealed class PeriodService : IPeriodService
    {
        private readonly IOptionsSnapshot<CreditsOptions> _opts;

        public PeriodService(IOptionsSnapshot<CreditsOptions> opts)
        {
            _opts = opts;
        }

        public (DateTime startUtc, DateTime endUtc) CurrentPeriodUtc(User user, DateTime now)
        {
            // Prefer Stripe-aligned window if we have both bounds.
            if (user.CurrentPeriodStartUtc.HasValue && user.CurrentPeriodEndUtc.HasValue)
            {
                var start = DateTime.SpecifyKind(user.CurrentPeriodStartUtc.Value, DateTimeKind.Utc);
                var end = DateTime.SpecifyKind(user.CurrentPeriodEndUtc.Value, DateTimeKind.Utc);
                if (now >= start && now < end) return (start, end);
            }

            // Fallback: calendar month [firstOfMonth, firstOfNextMonth)
            var startCal = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endCal = startCal.AddMonths(1);
            return (startCal, endCal);
        }

        public bool IsPeriodBoundary(User user, DateTime now)
        {
            // Stripe-style boundary if we have an end.
            if (user.CurrentPeriodEndUtc.HasValue)
            {
                var end = DateTime.SpecifyKind(user.CurrentPeriodEndUtc.Value, DateTimeKind.Utc);
                // Consider boundary reached once 'now' hits or exceeds end
                if (now >= end) return true;
            }

            // Fallback: calendar-month change vs LastReset
            var last = DateTime.SpecifyKind(user.LastReset, DateTimeKind.Utc);
            return last.Month != now.Month || last.Year != now.Year;
        }

        public void OnPeriodRollover(User user, DateTime now)
        {
            // Per-period counters reset
            user.BooksGenerated = 0;
            user.AddOnSpentThisPeriod = 0;

            // Keep or clear carryover wallet based on policy
            if (!_opts.Value.CarryoverEnabled)
            {
                user.AddOnBalance = 0; // flip behavior with config only
            }

            user.LastReset = now;

            // If using Stripe windows, roll the start forward.
            if (user.CurrentPeriodEndUtc.HasValue)
            {
                user.CurrentPeriodStartUtc = now; // next period start
                // NOTE: user.CurrentPeriodEndUtc should be set by your Stripe webhook on renewal.
                // If you want a fallback here, you could set end = now.AddMonths(1), but webhook is source of truth.
            }
            else
            {
                // Calendar-month fallback
                user.CurrentPeriodStartUtc = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            }
        }
    }
}