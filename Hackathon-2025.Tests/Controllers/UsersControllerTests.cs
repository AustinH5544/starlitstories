using System.Net;
using Hackathon_2025.Models;
using Hackathon_2025.Tests.Utils;
using Microsoft.EntityFrameworkCore;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class UsersControllerTests
{
    private TestWebAppFactory _factory = null!;

    [TestInitialize]
    public void Init() => _factory = new TestWebAppFactory();

    [TestCleanup]
    public void Cleanup() => _factory.Dispose();

    [TestMethod]
    public async Task Usage_For_Pro_User_Counts_Base_And_AddOns()
    {
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Pro, booksGenerated: 2, addOnBalance: 3));

        var json = await (await _factory.ClientFor(user.Id).GetAsync("/api/users/me/usage")).ReadJsonAsync();

        Assert.AreEqual("pro", json.GetProperty("plan").GetString());
        Assert.AreEqual(5, json.GetProperty("baseQuota").GetInt32());
        Assert.AreEqual(2, json.GetProperty("used").GetInt32());
        Assert.AreEqual(3, json.GetProperty("baseRemaining").GetInt32());
        Assert.AreEqual(3, json.GetProperty("addOnBalance").GetInt32());
        Assert.AreEqual(6, json.GetProperty("remaining").GetInt32());
        Assert.IsFalse(json.GetProperty("canBuyAddons").GetBoolean());
    }

    [TestMethod]
    public async Task Usage_For_Exhausted_Premium_Allows_Buying_AddOns()
    {
        // Also pins that appsettings.json quotas resolve for capitalized plan names ("Premium" -> 11).
        var user = await _factory.SeedAsync(TestData.NewUser(MembershipPlan.Premium, booksGenerated: 11));

        var json = await (await _factory.ClientFor(user.Id).GetAsync("/api/users/me/usage")).ReadJsonAsync();

        Assert.AreEqual(11, json.GetProperty("baseQuota").GetInt32());
        Assert.IsTrue(json.GetProperty("canBuyAddons").GetBoolean());
    }

    [TestMethod]
    public async Task Usage_For_Free_User_Has_One_Story()
    {
        var user = await _factory.SeedAsync(TestData.NewUser());
        var json = await (await _factory.ClientFor(user.Id).GetAsync("/api/users/me/usage")).ReadJsonAsync();
        Assert.AreEqual(1, json.GetProperty("baseQuota").GetInt32());
    }

    [TestMethod]
    public async Task Usage_In_A_New_Month_Rolls_Over_Counters()
    {
        var user = TestData.NewUser(MembershipPlan.Pro, booksGenerated: 4);
        user.LastReset = DateTime.UtcNow.AddMonths(-1);
        await _factory.SeedAsync(user);

        var json = await (await _factory.ClientFor(user.Id).GetAsync("/api/users/me/usage")).ReadJsonAsync();

        Assert.AreEqual(0, json.GetProperty("used").GetInt32());
        Assert.AreEqual(0, (await _factory.QueryDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id))).BooksGenerated);
    }
}
