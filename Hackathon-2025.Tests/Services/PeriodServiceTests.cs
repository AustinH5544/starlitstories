using Hackathon_2025.Models;
using Hackathon_2025.Services;
using Microsoft.Extensions.Options;
using Moq;

namespace Hackathon_2025.Tests.Services;

[TestClass]
public class PeriodServiceTests
{
    private static readonly DateTime Now = new(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

    private static PeriodService Create(bool carryoverEnabled = true)
    {
        var snapshot = new Mock<IOptionsSnapshot<CreditsOptions>>();
        snapshot.Setup(s => s.Value).Returns(new CreditsOptions { CarryoverEnabled = carryoverEnabled });
        return new PeriodService(snapshot.Object);
    }

    [TestMethod]
    public void CurrentPeriod_Uses_Stripe_Window_When_Now_Is_Inside_It()
    {
        var user = new User
        {
            CurrentPeriodStartUtc = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            CurrentPeriodEndUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc)
        };

        var (start, end) = Create().CurrentPeriodUtc(user, Now);

        Assert.AreEqual(new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc), start);
        Assert.AreEqual(new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc), end);
    }

    [TestMethod]
    public void CurrentPeriod_Falls_Back_To_Calendar_Month_Without_Stripe_Window()
    {
        var (start, end) = Create().CurrentPeriodUtc(new User(), Now);

        Assert.AreEqual(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), start);
        Assert.AreEqual(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), end);
    }

    [TestMethod]
    public void CurrentPeriod_Falls_Back_To_Calendar_Month_When_Window_Is_Stale()
    {
        var user = new User
        {
            CurrentPeriodStartUtc = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            CurrentPeriodEndUtc = new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc)
        };

        var (start, _) = Create().CurrentPeriodUtc(user, Now);

        Assert.AreEqual(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), start);
    }

    [TestMethod]
    public void IsPeriodBoundary_True_When_Stripe_End_Has_Passed()
    {
        var user = new User { LastReset = Now, CurrentPeriodEndUtc = Now.AddSeconds(-1) };
        Assert.IsTrue(Create().IsPeriodBoundary(user, Now));
    }

    [TestMethod]
    public void IsPeriodBoundary_False_Inside_Stripe_Window_Same_Month()
    {
        var user = new User { LastReset = Now.AddDays(-2), CurrentPeriodEndUtc = Now.AddDays(5) };
        Assert.IsFalse(Create().IsPeriodBoundary(user, Now));
    }

    [TestMethod]
    public void IsPeriodBoundary_True_When_Calendar_Month_Changed()
    {
        var user = new User { LastReset = new DateTime(2026, 2, 28, 23, 0, 0, DateTimeKind.Utc) };
        Assert.IsTrue(Create().IsPeriodBoundary(user, Now));
    }

    [TestMethod]
    public void IsPeriodBoundary_True_Same_Month_Previous_Year()
    {
        var user = new User { LastReset = new DateTime(2025, 3, 20, 0, 0, 0, DateTimeKind.Utc) };
        Assert.IsTrue(Create().IsPeriodBoundary(user, Now));
    }

    [TestMethod]
    public void IsPeriodBoundary_False_Same_Calendar_Month()
    {
        var user = new User { LastReset = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc) };
        Assert.IsFalse(Create().IsPeriodBoundary(user, Now));
    }

    [TestMethod]
    public void Rollover_Resets_Counters_And_Keeps_AddOns_When_Carryover_Enabled()
    {
        var user = new User { BooksGenerated = 4, AddOnSpentThisPeriod = 2, AddOnBalance = 7 };

        Create(carryoverEnabled: true).OnPeriodRollover(user, Now);

        Assert.AreEqual(0, user.BooksGenerated);
        Assert.AreEqual(0, user.AddOnSpentThisPeriod);
        Assert.AreEqual(7, user.AddOnBalance);
        Assert.AreEqual(Now, user.LastReset);
    }

    [TestMethod]
    public void Rollover_Clears_AddOns_When_Carryover_Disabled()
    {
        var user = new User { AddOnBalance = 7 };

        Create(carryoverEnabled: false).OnPeriodRollover(user, Now);

        Assert.AreEqual(0, user.AddOnBalance);
    }

    [TestMethod]
    public void Rollover_With_Stripe_Window_Moves_Start_And_Clears_Stale_End()
    {
        var user = new User { CurrentPeriodEndUtc = Now.AddDays(-1) };

        Create().OnPeriodRollover(user, Now);

        Assert.AreEqual(Now, user.CurrentPeriodStartUtc);
        Assert.IsNull(user.CurrentPeriodEndUtc);
        Assert.IsFalse(Create().IsPeriodBoundary(user, Now), "must not keep firing on every request after rollover");
    }

    [TestMethod]
    public void Rollover_Without_Stripe_Window_Sets_Start_To_First_Of_Month()
    {
        var user = new User();

        Create().OnPeriodRollover(user, Now);

        Assert.AreEqual(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), user.CurrentPeriodStartUtc);
    }
}
