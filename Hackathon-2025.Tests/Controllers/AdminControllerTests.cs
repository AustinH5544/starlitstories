using System.Net;
using System.Net.Http.Json;
using Hackathon_2025.Models;
using Hackathon_2025.Tests.Utils;
using Microsoft.EntityFrameworkCore;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class AdminControllerTests
{
    private TestWebAppFactory _factory = null!;

    [TestInitialize]
    public void Init() => _factory = new TestWebAppFactory();

    [TestCleanup]
    public void Cleanup() => _factory.Dispose();

    private HttpClient Admin(int userId = 1000) => _factory.ClientFor(userId, TestWebAppFactory.AdminEmail);
    private HttpClient NonAdmin(int userId = 2000) => _factory.ClientFor(userId, "someone@test.local");

    [DataTestMethod]
    [DataRow("GET", "/api/admin/dashboard")]
    [DataRow("GET", "/api/admin/stories/1")]
    [DataRow("PATCH", "/api/admin/users/1")]
    [DataRow("DELETE", "/api/admin/shares/abc")]
    public async Task Admin_Endpoints_Forbid_Non_Admins(string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = method == "PATCH" ? JsonContent.Create(new { membership = "premium" }) : null
        };

        var resp = await NonAdmin().SendAsync(request);

        Assert.AreEqual(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [TestMethod]
    public async Task Dashboard_Works_For_Admin_With_Any_Email_Casing()
    {
        var resp = await _factory.ClientFor(1000, "ADMIN@Test.Local").GetAsync("/api/admin/dashboard");
        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
    }

    [TestMethod]
    public async Task Access_Reports_Admin_Flag()
    {
        Assert.IsTrue((await (await Admin().GetAsync("/api/admin/access")).ReadJsonAsync()).GetProperty("isAdmin").GetBoolean());
        Assert.IsFalse((await (await NonAdmin().GetAsync("/api/admin/access")).ReadJsonAsync()).GetProperty("isAdmin").GetBoolean());
    }

    [TestMethod]
    public async Task UpdateUser_Sets_Membership_And_Manual_Status()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());

        var resp = await Admin().PatchAsJsonAsync($"/api/admin/users/{user.Id}", new { membership = "premium" });

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        var saved = await _factory.QueryDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id));
        Assert.AreEqual(MembershipPlan.Premium, saved.Membership);
        Assert.AreEqual("premium", saved.PlanKey);
        Assert.AreEqual("manual", saved.PlanStatus);
    }

    [TestMethod]
    public async Task UpdateUser_To_Free_Sets_Status_None()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));

        await Admin().PatchAsJsonAsync($"/api/admin/users/{user.Id}", new { membership = "free" });

        var saved = await _factory.QueryDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id));
        Assert.AreEqual(MembershipPlan.Free, saved.Membership);
        Assert.AreEqual("none", saved.PlanStatus);
    }

    [TestMethod]
    public async Task UpdateUser_Rejects_Unknown_Membership()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        var resp = await Admin().PatchAsJsonAsync($"/api/admin/users/{user.Id}", new { membership = "gold" });
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task UpdateUser_Credit_Adjustments_Never_Go_Negative_And_Reset_Works()
    {
        var user = TestData.NewUser(MembershipPlan.Pro, booksGenerated: 4, addOnBalance: 3);
        user.AddOnSpentThisPeriod = 2;
        await _factory.SeedAsync(user);

        await Admin().PatchAsJsonAsync($"/api/admin/users/{user.Id}", new { addOnBalanceDelta = -100, resetPeriodUsage = true });

        var saved = await _factory.QueryDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id));
        Assert.AreEqual(0, saved.AddOnBalance);
        Assert.AreEqual(0, saved.BooksGenerated);
        Assert.AreEqual(0, saved.AddOnSpentThisPeriod);
    }

    [TestMethod]
    public async Task UpdateUser_Unknown_User_Is_NotFound()
    {
        var resp = await Admin().PatchAsJsonAsync("/api/admin/users/999999", new { membership = "pro" });
        Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [TestMethod]
    public async Task GetStory_Includes_Owner()
    {
        var owner = await _factory.SeedAsync(TestData.NewUser());
        var story = await _factory.SeedAsync(TestData.NewStory(owner.Id));

        var json = await (await Admin().GetAsync($"/api/admin/stories/{story.Id}")).ReadJsonAsync();

        Assert.AreEqual(owner.Email, json.GetProperty("ownerEmail").GetString());
    }

    [TestMethod]
    public async Task Admin_RevokeShare_Is_Idempotent()
    {
        var owner = await _factory.SeedAsync(TestData.NewUser());
        var story = await _factory.SeedAsync(TestData.NewStory(owner.Id));
        var share = await _factory.SeedAsync(new StoryShare { StoryId = story.Id, ExpiresUtc = DateTime.UtcNow.AddDays(5) });

        var first = await Admin().DeleteAsync($"/api/admin/shares/{share.Token}");
        var second = await Admin().DeleteAsync($"/api/admin/shares/{share.Token}");

        Assert.AreEqual(HttpStatusCode.NoContent, first.StatusCode);
        Assert.AreEqual(HttpStatusCode.NoContent, second.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await _factory.AnonymousClient().GetAsync($"/api/share/{share.Token}")).StatusCode);
    }

    [TestMethod]
    public async Task Admin_RevokeShare_Unknown_Token_Is_NotFound()
    {
        var resp = await Admin().DeleteAsync("/api/admin/shares/does-not-exist");
        Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
