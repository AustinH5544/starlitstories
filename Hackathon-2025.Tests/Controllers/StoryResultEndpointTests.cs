using Hackathon_2025.Services;
using Hackathon_2025.Tests.Utils;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Runtime.CompilerServices;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class StoryResultEndpointTests
{
    private TestWebAppFactory _baseFactory = null!;
    private WebApplicationFactory<Program> _factory = null!;

    [TestInitialize]
    public void Init()
    {
        _baseFactory = new TestWebAppFactory();
        // StoryController depends on BlobUploadService, whose constructor connects to Azure.
        // The result endpoint never uses it, so an uninitialized placeholder is enough here.
        _factory = _baseFactory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.RemoveAll<BlobUploadService>();
            services.AddSingleton((BlobUploadService)RuntimeHelpers.GetUninitializedObject(typeof(BlobUploadService)));
        }));
    }

    [TestCleanup]
    public void Cleanup()
    {
        _factory.Dispose();
        _baseFactory.Dispose();
    }

    private string CreateFinishedJob(int ownerUserId)
    {
        var broker = _factory.Services.GetRequiredService<IProgressBroker>();
        var jobId = broker.CreateJob(ownerUserId);
        broker.SetResult(jobId, new { title = "A Starry Night" });
        broker.Complete(jobId);
        return jobId;
    }

    private HttpClient ClientFor(int userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        return client;
    }

    [TestMethod]
    public async Task Result_Returns_200_For_Job_Owner()
    {
        var jobId = CreateFinishedJob(ownerUserId: 5);

        var resp = await ClientFor(5).GetAsync($"/api/story/result/{jobId}");

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        StringAssert.Contains(await resp.Content.ReadAsStringAsync(), "A Starry Night");
    }

    [TestMethod]
    public async Task Result_Returns_404_For_Different_User()
    {
        var jobId = CreateFinishedJob(ownerUserId: 5);

        var resp = await ClientFor(6).GetAsync($"/api/story/result/{jobId}");

        Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
