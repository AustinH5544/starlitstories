using Hackathon_2025.Services;
using Hackathon_2025.Tests.Utils;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class StoryResultEndpointTests
{
    private TestWebAppFactory _factory = null!;

    [TestInitialize]
    public void Init() => _factory = new TestWebAppFactory();

    [TestCleanup]
    public void Cleanup() => _factory.Dispose();

    private string CreateFinishedJob(int ownerUserId)
    {
        var broker = _factory.Services.GetRequiredService<IProgressBroker>();
        var jobId = broker.CreateJob(ownerUserId);
        broker.SetResult(jobId, new { title = "A Starry Night" });
        broker.Complete(jobId);
        return jobId;
    }

    [TestMethod]
    public async Task Result_Returns_200_For_Job_Owner()
    {
        var jobId = CreateFinishedJob(ownerUserId: 5);

        var resp = await _factory.ClientFor(5).GetAsync($"/api/story/result/{jobId}");

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        StringAssert.Contains(await resp.Content.ReadAsStringAsync(), "A Starry Night");
    }

    [TestMethod]
    public async Task Result_Returns_404_For_Different_User()
    {
        var jobId = CreateFinishedJob(ownerUserId: 5);

        var resp = await _factory.ClientFor(6).GetAsync($"/api/story/result/{jobId}");

        Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
