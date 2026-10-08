using Hackathon_2025.Services;

namespace Hackathon_2025.Tests.Services;

[TestClass]
public class ProgressBrokerTests
{
    private sealed class ManualTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
        public void Advance(TimeSpan by) => Now += by;
    }

    private ManualTimeProvider _time = null!;
    private ProgressBroker _broker = null!;

    [TestInitialize]
    public void Init()
    {
        _time = new ManualTimeProvider();
        _broker = new ProgressBroker(_time);
    }

    [TestMethod]
    public void GetResult_Returns_Result_For_Owner()
    {
        var jobId = _broker.CreateJob(ownerUserId: 7);
        _broker.SetResult(jobId, "story");

        Assert.AreEqual("story", _broker.GetResult(jobId, requestingUserId: 7));
    }

    [TestMethod]
    public void GetResult_Returns_Null_For_Other_User()
    {
        var jobId = _broker.CreateJob(ownerUserId: 7);
        _broker.SetResult(jobId, "story");

        Assert.IsNull(_broker.GetResult(jobId, requestingUserId: 8));
    }

    [TestMethod]
    public void Completed_Job_Is_Kept_Within_Retention_Window()
    {
        var jobId = _broker.CreateJob(ownerUserId: 1);
        _broker.SetResult(jobId, "story");
        _broker.Complete(jobId);

        _time.Advance(ProgressBroker.CompletedJobRetention - TimeSpan.FromMinutes(1));
        _broker.CreateJob(ownerUserId: 2); // triggers cleanup

        Assert.AreEqual("story", _broker.GetResult(jobId, requestingUserId: 1));
    }

    [TestMethod]
    public void Completed_Job_Is_Removed_After_Retention_Window()
    {
        var jobId = _broker.CreateJob(ownerUserId: 1);
        _broker.SetResult(jobId, "story");
        _broker.Complete(jobId);

        _time.Advance(ProgressBroker.CompletedJobRetention + TimeSpan.FromMinutes(1));
        _broker.CreateJob(ownerUserId: 2); // triggers cleanup

        Assert.IsNull(_broker.GetResult(jobId, requestingUserId: 1));
    }

    [TestMethod]
    public void Running_Job_Survives_Retention_Window_But_Not_Max_Age()
    {
        var jobId = _broker.CreateJob(ownerUserId: 1);
        _broker.SetResult(jobId, "partial");

        _time.Advance(ProgressBroker.CompletedJobRetention + TimeSpan.FromMinutes(1));
        _broker.CreateJob(ownerUserId: 2);
        Assert.AreEqual("partial", _broker.GetResult(jobId, requestingUserId: 1));

        _time.Advance(ProgressBroker.MaxJobAge);
        _broker.CreateJob(ownerUserId: 2);
        Assert.IsNull(_broker.GetResult(jobId, requestingUserId: 1));
    }
}
