using Hackathon_2025.Services;

namespace Hackathon_2025.Tests.Services;

/// <summary>
/// The recovery loop must go quiet when nothing is generating: any query inside the database's auto-pause delay
/// keeps the serverless database awake and billed around the clock.
/// </summary>
[TestClass]
public class StaleDraftRecoveryServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public void Stops_When_No_Story_Is_Generating()
    {
        Assert.IsNull(StaleDraftRecoveryService.DelayBeforeNextCheck(nextCheckAt: null, Now));
    }

    [TestMethod]
    public void Waits_Until_Just_After_The_Draft_Would_Be_Abandoned()
    {
        var delay = StaleDraftRecoveryService.DelayBeforeNextCheck(Now.AddMinutes(18), Now);

        Assert.AreEqual(TimeSpan.FromMinutes(18) + StaleDraftRecoveryService.CheckMargin, delay);
    }

    [TestMethod]
    public void Never_Waits_A_Negative_Time()
    {
        var delay = StaleDraftRecoveryService.DelayBeforeNextCheck(Now.AddMinutes(-5), Now);

        Assert.AreEqual(StaleDraftRecoveryService.CheckMargin, delay);
    }
}
