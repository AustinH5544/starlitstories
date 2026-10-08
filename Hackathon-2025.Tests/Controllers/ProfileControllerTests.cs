using System.Net;
using System.Net.Http.Json;
using Hackathon_2025.Models;
using Hackathon_2025.Tests.Utils;
using Microsoft.EntityFrameworkCore;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class ProfileControllerTests
{
    private TestWebAppFactory _factory = null!;

    [TestInitialize]
    public void Init() => _factory = new TestWebAppFactory();

    [TestCleanup]
    public void Cleanup() => _factory.Dispose();

    private Task<User> ReloadAsync(int id) => _factory.QueryDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == id));

    [TestMethod]
    public async Task Me_Returns_Profile_With_Membership_Name()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro, booksGenerated: 2, addOnBalance: 4));

        var json = await (await _factory.ClientFor(user.Id).GetAsync("/api/profile/me")).ReadJsonAsync();

        Assert.AreEqual(user.Username, json.GetProperty("username").GetString());
        Assert.AreEqual("Pro", json.GetProperty("membership").GetString());
        Assert.AreEqual(2, json.GetProperty("booksGenerated").GetInt32());
        Assert.AreEqual(4, json.GetProperty("addOnBalance").GetInt32());
        Assert.IsFalse(json.GetProperty("isAdmin").GetBoolean());
    }

    [TestMethod]
    public async Task Me_Flags_Admins()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(email: TestWebAppFactory.AdminEmail));
        var json = await (await _factory.ClientFor(user.Id).GetAsync("/api/profile/me")).ReadJsonAsync();
        Assert.IsTrue(json.GetProperty("isAdmin").GetBoolean());
    }

    [TestMethod]
    public async Task Avatar_Preset_Is_Saved_And_Unknown_File_Is_Rejected()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        var client = _factory.ClientFor(user.Id);

        Assert.AreEqual(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/api/profile/avatar", new { profileImage = "wizard-avatar.png" })).StatusCode);
        Assert.AreEqual("wizard-avatar.png", (await ReloadAsync(user.Id)).ProfileImage);
        Assert.AreEqual(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/profile/avatar", new { profileImage = "evil.png" })).StatusCode);
    }

    [DataTestMethod]
    [DataRow("puppy-avatar.png")]
    [DataRow("robot-avatar.png")]
    [DataRow("alien-avatar.png")]
    [DataRow("panda-avatar.png")]
    [DataRow("whimsical-mermaid-avatar.png")]
    public async Task New_And_Existing_Preset_Avatars_Are_Accepted(string profileImage)
    {
        var user = await _factory.SeedAsync(TestData.NewUser());

        var resp = await _factory.ClientFor(user.Id).PutAsJsonAsync("/api/profile/avatar", new { profileImage });

        Assert.AreEqual(HttpStatusCode.NoContent, resp.StatusCode);
        Assert.AreEqual(profileImage, (await ReloadAsync(user.Id)).ProfileImage);
    }

    [DataTestMethod]
    [DataRow("https://cdn.test/a.png")]
    [DataRow("http://tracker.test/pixel.gif")]
    [DataRow("../avatars/wizard-avatar.png")]
    [DataRow("")]
    public async Task Avatar_Must_Be_One_Of_The_Built_In_Pictures(string profileImage)
    {
        // Decided 2026-10-08: only the built-in presets; outside URLs were never used by the app.
        var user = await _factory.SeedAsync(TestData.NewUser());

        var resp = await _factory.ClientFor(user.Id).PutAsJsonAsync("/api/profile/avatar", new { profileImage });

        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.AreNotEqual(profileImage, (await ReloadAsync(user.Id)).ProfileImage);
    }

    [TestMethod]
    public async Task UpdateUsername_Valid_Name_Is_Saved_Normalized()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());

        var resp = await _factory.ClientFor(user.Id).PutAsJsonAsync("/api/profile/username", new { username = "  new_name  " });

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        var saved = await ReloadAsync(user.Id);
        Assert.AreEqual("new_name", saved.Username);
        Assert.AreEqual("new_name", saved.UsernameNormalized);
    }

    [DataTestMethod]
    [DataRow("ab")]
    [DataRow("has space")]
    [DataRow("this-name-is-way-too-long-x")]
    public async Task UpdateUsername_Invalid_Is_Rejected(string username)
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        var resp = await _factory.ClientFor(user.Id).PutAsJsonAsync("/api/profile/username", new { username });
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task UpdateUsername_Uppercase_Is_Rejected()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());

        var resp = await _factory.ClientFor(user.Id).PutAsJsonAsync("/api/profile/username", new { username = "MILO" });

        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
        StringAssert.Contains(await resp.Content.ReadAsStringAsync(), "3-24 chars");
    }

    [TestMethod]
    public async Task UpdateUsername_Taken_Is_Conflict_But_Own_Name_Is_Fine()
    {
        var taken = await _factory.SeedAsync(TestData.NewUser(username: "takenname"));
        var user = await _factory.SeedAsync(TestData.NewUser(username: "myname"));
        var client = _factory.ClientFor(user.Id);

        Assert.AreEqual(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/profile/username", new { username = "takenname" })).StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/profile/username", new { username = "myname" })).StatusCode);
        Assert.AreEqual("takenname", (await ReloadAsync(taken.Id)).Username);
    }

    [TestMethod]
    public async Task MyStories_Pages_Results_And_Excludes_Others()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        var other = await _factory.SeedAsync(TestData.NewUser());
        for (var i = 0; i < 7; i++) await _factory.SeedAsync(TestData.NewStory(user.Id, title: $"S{i}"));
        await _factory.SeedAsync(TestData.NewStory(other.Id));
        var client = _factory.ClientFor(user.Id);

        var page1 = await (await client.GetAsync("/api/profile/me/stories?page=1&pageSize=6")).ReadJsonAsync();
        var page2 = await (await client.GetAsync("/api/profile/me/stories?page=2&pageSize=6")).ReadJsonAsync();
        var clamped = await (await client.GetAsync("/api/profile/me/stories?page=0&pageSize=500")).ReadJsonAsync();

        Assert.AreEqual(7, page1.GetProperty("total").GetInt32());
        Assert.AreEqual(6, page1.GetProperty("items").GetArrayLength());
        Assert.AreEqual(1, page2.GetProperty("items").GetArrayLength());
        Assert.AreEqual(1, clamped.GetProperty("page").GetInt32());
        Assert.AreEqual(50, clamped.GetProperty("pageSize").GetInt32());
    }

    [TestMethod]
    public async Task MyStory_Returns_Own_Story_And_404_For_Others()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        var other = await _factory.SeedAsync(TestData.NewUser());
        var mine = await _factory.SeedAsync(TestData.NewStory(user.Id));
        var theirs = await _factory.SeedAsync(TestData.NewStory(other.Id));
        var client = _factory.ClientFor(user.Id);

        var own = await client.GetAsync($"/api/profile/me/stories/{mine.Id}");
        Assert.AreEqual(HttpStatusCode.OK, own.StatusCode);
        Assert.AreEqual(2, (await own.ReadJsonAsync()).GetProperty("pages").GetArrayLength());
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/api/profile/me/stories/{theirs.Id}")).StatusCode);
    }
}
