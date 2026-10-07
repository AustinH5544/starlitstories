using System.Net;
using System.Net.Http.Json;
using Hackathon_2025.Models;
using Hackathon_2025.Tests.Utils;
using Microsoft.EntityFrameworkCore;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class SavedCharacterControllerTests
{
    private TestWebAppFactory _factory = null!;

    [TestInitialize]
    public void Init() => _factory = new TestWebAppFactory();

    [TestCleanup]
    public void Cleanup() => _factory.Dispose();

    private static object Body(string name = "Milo") => new
    {
        character = new
        {
            name,
            role = "main",
            isAnimal = false,
            descriptionFields = new Dictionary<string, string> { ["hairColor"] = "brown", ["favoriteFood"] = "pizza" }
        }
    };

    [TestMethod]
    public async Task Free_User_Save_Trims_Fields_To_Allowlist()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/saved-character/me", Body());

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        var fields = (await resp.ReadJsonAsync()).GetProperty("character").GetProperty("descriptionFields");
        Assert.IsTrue(fields.TryGetProperty("hairColor", out _));
        Assert.IsFalse(fields.TryGetProperty("favoriteFood", out _));
    }

    [TestMethod]
    public async Task Paid_User_Save_Keeps_All_Fields()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));

        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/saved-character/me", Body());

        var fields = (await resp.ReadJsonAsync()).GetProperty("character").GetProperty("descriptionFields");
        Assert.IsTrue(fields.TryGetProperty("favoriteFood", out _));
    }

    [DataTestMethod]
    [DataRow(MembershipPlan.Free, 1)]
    [DataRow(MembershipPlan.Pro, 5)]
    [DataRow(MembershipPlan.Premium, 10)]
    public async Task Save_Limit_Per_Plan_Returns_Conflict_When_Full(MembershipPlan plan, int limit)
    {
        var user = await _factory.SeedAsync(TestData.NewUser(plan));
        var client = _factory.ClientFor(user.Id);

        for (var i = 0; i < limit; i++)
            Assert.AreEqual(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/saved-character/me", Body($"C{i}"))).StatusCode);

        var overLimit = await client.PostAsJsonAsync("/api/saved-character/me", Body("One too many"));

        Assert.AreEqual(HttpStatusCode.Conflict, overLimit.StatusCode);
    }

    [TestMethod]
    public async Task Save_Without_Name_Is_BadRequest()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));
        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/saved-character/me", Body("   "));
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task Save_With_Non_Object_Character_Is_BadRequest()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));
        var resp = await _factory.ClientFor(user.Id).PostAsJsonAsync("/api/saved-character/me", new { character = "Milo" });
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task GetMine_After_Downgrade_Reports_Over_Limit_Without_Deleting()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        for (var i = 0; i < 3; i++)
            await _factory.SeedAsync(new SavedCharacter { UserId = user.Id, Name = $"C{i}", CharacterJson = "{\"name\":\"C\"}" });

        var json = await (await _factory.ClientFor(user.Id).GetAsync("/api/saved-character/me")).ReadJsonAsync();

        Assert.AreEqual(1, json.GetProperty("maxSavedCharacters").GetInt32());
        Assert.AreEqual(3, json.GetProperty("savedCharacterCount").GetInt32());
        Assert.IsTrue(json.GetProperty("isOverLimit").GetBoolean());
        Assert.AreEqual(2, json.GetProperty("overLimitCount").GetInt32());
    }

    [TestMethod]
    public async Task Users_Cannot_See_Update_Or_Delete_Each_Others_Characters()
    {
        var owner = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));
        var other = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));
        var saved = await _factory.SeedAsync(new SavedCharacter { UserId = owner.Id, Name = "Mine", CharacterJson = "{\"name\":\"Mine\"}" });
        var otherClient = _factory.ClientFor(other.Id);

        var list = await (await otherClient.GetAsync("/api/saved-character/me")).ReadJsonAsync();
        var update = await otherClient.PutAsJsonAsync($"/api/saved-character/me/{saved.Id}", Body("Stolen"));
        var delete = await otherClient.DeleteAsync($"/api/saved-character/me/{saved.Id}");

        Assert.AreEqual(0, list.GetProperty("items").GetArrayLength());
        Assert.AreEqual(HttpStatusCode.NotFound, update.StatusCode);
        Assert.AreEqual(HttpStatusCode.NoContent, delete.StatusCode); // current API: silent no-op
        var stillThere = await _factory.QueryDbAsync(db => db.SavedCharacters.AsNoTracking().SingleAsync(c => c.Id == saved.Id));
        Assert.AreEqual("Mine", stillThere.Name);
    }

    [TestMethod]
    public async Task Owner_Can_Update_And_Delete()
    {
        var owner = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro));
        var saved = await _factory.SeedAsync(new SavedCharacter { UserId = owner.Id, Name = "Old", CharacterJson = "{\"name\":\"Old\"}" });
        var client = _factory.ClientFor(owner.Id);

        var update = await client.PutAsJsonAsync($"/api/saved-character/me/{saved.Id}", Body("New"));
        Assert.AreEqual("New", (await update.ReadJsonAsync()).GetProperty("name").GetString());

        Assert.AreEqual(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/saved-character/me/{saved.Id}")).StatusCode);
        Assert.AreEqual(0, await _factory.QueryDbAsync(db => db.SavedCharacters.CountAsync(c => c.UserId == owner.Id)));
    }
}
