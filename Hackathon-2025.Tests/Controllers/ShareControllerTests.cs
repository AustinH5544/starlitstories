using System.Net;
using Hackathon_2025.Models;
using Hackathon_2025.Tests.Utils;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class ShareControllerTests
{
    private TestWebAppFactory _factory = null!;

    [TestInitialize]
    public void Init() => _factory = new TestWebAppFactory();

    [TestCleanup]
    public void Cleanup() => _factory.Dispose();

    private async Task<(User owner, Story story)> SeedStoryAsync()
    {
        var owner = await _factory.SeedAsync(TestData.NewUser());
        var story = await _factory.SeedAsync(TestData.NewStory(owner.Id));
        return (owner, story);
    }

    [TestMethod]
    public async Task Owner_Creates_Share_With_Default_30_Day_Expiry()
    {
        var (owner, story) = await SeedStoryAsync();

        var json = await (await _factory.ClientFor(owner.Id).PostAsync($"/api/stories/{story.Id}/share", null)).ReadJsonAsync();

        var expires = json.GetProperty("expiresUtc").GetDateTime();
        Assert.IsTrue(Math.Abs((expires - DateTime.UtcNow.AddDays(30)).TotalMinutes) < 2, $"expires {expires:o}");
        StringAssert.Contains(json.GetProperty("url").GetString()!, "/s/" + json.GetProperty("token").GetString());
    }

    [TestMethod]
    public async Task Requested_Days_Override_Default()
    {
        var (owner, story) = await SeedStoryAsync();

        var json = await (await _factory.ClientFor(owner.Id).PostAsync($"/api/stories/{story.Id}/share?days=7", null)).ReadJsonAsync();

        var expires = json.GetProperty("expiresUtc").GetDateTime();
        Assert.IsTrue(Math.Abs((expires - DateTime.UtcNow.AddDays(7)).TotalMinutes) < 2);
    }

    [TestMethod]
    public async Task Creating_Again_Reuses_The_Active_Link()
    {
        var (owner, story) = await SeedStoryAsync();
        var client = _factory.ClientFor(owner.Id);

        var first = (await (await client.PostAsync($"/api/stories/{story.Id}/share", null)).ReadJsonAsync()).GetProperty("token").GetString();
        var second = (await (await client.PostAsync($"/api/stories/{story.Id}/share", null)).ReadJsonAsync()).GetProperty("token").GetString();

        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public async Task Anyone_Can_View_A_Shared_Story_And_It_Is_Not_Indexed()
    {
        var (owner, story) = await SeedStoryAsync();
        var token = (await (await _factory.ClientFor(owner.Id).PostAsync($"/api/stories/{story.Id}/share", null)).ReadJsonAsync()).GetProperty("token").GetString();

        var resp = await _factory.AnonymousClient().GetAsync($"/api/share/{token}");

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        StringAssert.Contains(string.Join(",", resp.Headers.GetValues("X-Robots-Tag")), "noindex");
        Assert.AreEqual(2, (await resp.ReadJsonAsync()).GetProperty("pages").GetArrayLength());
    }

    [TestMethod]
    public async Task Expired_Link_Is_NotFound()
    {
        var (_, story) = await SeedStoryAsync();
        var share = await _factory.SeedAsync(new StoryShare { StoryId = story.Id, ExpiresUtc = DateTime.UtcNow.AddMinutes(-1) });

        var resp = await _factory.AnonymousClient().GetAsync($"/api/share/{share.Token}");

        Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [TestMethod]
    public async Task Revoked_Link_Is_NotFound()
    {
        var (owner, story) = await SeedStoryAsync();
        var share = await _factory.SeedAsync(new StoryShare { StoryId = story.Id, ExpiresUtc = DateTime.UtcNow.AddDays(5) });

        Assert.AreEqual(HttpStatusCode.NoContent, (await _factory.ClientFor(owner.Id).DeleteAsync($"/api/share/{share.Token}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await _factory.AnonymousClient().GetAsync($"/api/share/{share.Token}")).StatusCode);
    }

    [TestMethod]
    public async Task Revoke_Twice_Second_Time_Returns_NotFound()
    {
        var (owner, story) = await SeedStoryAsync();
        var share = await _factory.SeedAsync(new StoryShare { StoryId = story.Id, ExpiresUtc = DateTime.UtcNow.AddDays(5) });
        var client = _factory.ClientFor(owner.Id);

        Assert.AreEqual(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/share/{share.Token}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/share/{share.Token}")).StatusCode);
    }

    [TestMethod]
    public async Task Cannot_Share_Or_Revoke_Someone_Elses_Story()
    {
        var (_, story) = await SeedStoryAsync();
        var share = await _factory.SeedAsync(new StoryShare { StoryId = story.Id, ExpiresUtc = DateTime.UtcNow.AddDays(5) });
        var stranger = await _factory.SeedAsync(TestData.NewUser());
        var client = _factory.ClientFor(stranger.Id);

        Assert.AreEqual(HttpStatusCode.NotFound, (await client.PostAsync($"/api/stories/{story.Id}/share", null)).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/share/{share.Token}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await _factory.AnonymousClient().GetAsync($"/api/share/{share.Token}")).StatusCode);
    }

    [DataTestMethod]
    [DataRow(3_000_000)]
    [DataRow(366)]
    public async Task CreateShare_Longer_Than_A_Year_Is_Capped_At_365_Days(int days)
    {
        // Decided 2026-10-08: links last at most a year (huge values used to overflow DateTime and 500).
        var (owner, story) = await SeedStoryAsync();

        var resp = await _factory.ClientFor(owner.Id).PostAsync($"/api/stories/{story.Id}/share?days={days}", null);

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        var expires = (await resp.ReadJsonAsync()).GetProperty("expiresUtc").GetDateTime();
        Assert.AreEqual(365, (expires - DateTime.UtcNow).TotalDays, 0.01);
    }

    [TestMethod]
    public async Task CreateShare_Within_A_Year_Keeps_The_Requested_Days()
    {
        var (owner, story) = await SeedStoryAsync();

        var resp = await _factory.ClientFor(owner.Id).PostAsync($"/api/stories/{story.Id}/share?days=365", null);

        var expires = (await resp.ReadJsonAsync()).GetProperty("expiresUtc").GetDateTime();
        Assert.AreEqual(365, (expires - DateTime.UtcNow).TotalDays, 0.01);
    }
}
