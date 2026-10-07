using System.Net;
using System.Net.Http.Json;
using Hackathon_2025.Tests.Utils;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class FeedbackControllerTests
{
    // Success path is not covered: FeedbackController uses the concrete EmailService (real SMTP/ACS).

    [TestMethod]
    public async Task Feedback_Without_Enjoyment_Is_BadRequest()
    {
        using var factory = new TestWebAppFactory();
        var resp = await factory.AnonymousClient().PostAsJsonAsync("/api/feedback", new { storyTitle = "x" });
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task Feedback_Is_Rate_Limited_After_Three_Requests()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.AnonymousClient();

        for (var i = 0; i < 3; i++)
            Assert.AreEqual(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/feedback", new { })).StatusCode);

        var limited = await client.PostAsJsonAsync("/api/feedback", new { });

        Assert.AreEqual((HttpStatusCode)429, limited.StatusCode);
    }
}
