//using Hackathon_2025.Data;
//using Hackathon_2025.Models;
//using Hackathon_2025.Tests.Utils;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.VisualStudio.TestTools.UnitTesting;
//using System.Net;
//using System.Net.Http.Json;
//using System.Text.Json;

//namespace Hackathon_2025.Tests.Controllers;

//[TestClass]
//public class ProfileControllerTests
//{
//    private TestWebAppFactory _factory = null!;
//    private HttpClient _client = null!;
//    private int _userId;

//    [TestInitialize]
//    public void Init()
//    {
//        _factory = new TestWebAppFactory();
//        _client = _factory.CreateClient();

//        // Seed a verified user + a few stories
//        using var scope = _factory.Services.CreateScope();
//        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

//        var user = new User
//        {
//            Email = "me@x.com",
//            Username = "me",
//            UsernameNormalized = "me",
//            PasswordHash = "hash",
//            IsEmailVerified = true,
//            Membership = "free",
//            BooksGenerated = 0
//        };
//        db.Users.Add(user);
//        db.SaveChanges();
//        _userId = user.Id;

//        // Add a couple of stories with pages for this user
//        var s1 = new Story
//        {
//            UserId = _userId,
//            Title = "First Story",
//            CoverImageUrl = "https://example.com/c1.png",
//            CreatedAt = DateTime.UtcNow.AddDays(-2),
//            Pages = new List<StoryPage>
//            {
//                new StoryPage { Text = "p1", ImageUrl = "https://example.com/p1.png" },
//                new StoryPage { Text = "p2", ImageUrl = "https://example.com/p2.png" },
//            }
//        };
//        var s2 = new Story
//        {
//            UserId = _userId,
//            Title = "Second Story",
//            CoverImageUrl = "https://example.com/c2.png",
//            CreatedAt = DateTime.UtcNow.AddDays(-1),
//            Pages = new List<StoryPage>
//            {
//                new StoryPage { Text = "a", ImageUrl = "https://example.com/a.png" }
//            }
//        };
//        db.Stories.AddRange(s1, s2);
//        db.SaveChanges();
//    }

//    private static async Task<JsonElement> ReadJson(HttpResponseMessage resp)
//    {
//        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
//        Assert.IsNotNull(doc, "Response JSON was null");
//        return doc!.RootElement.Clone();
//    }

//    private HttpRequestMessage Authed(HttpMethod method, string url)
//    {
//        var req = new HttpRequestMessage(method, url);
//        req.Headers.Add(TestAuthHandler.UserIdHeader, _userId.ToString());
//        return req;
//    }

//    // =========================================================
//    // GET /api/profile/me
//    // =========================================================
//    [TestMethod]
//    public async Task GetMyProfile_Returns_Basic_Fields()
//    {
//        var resp = await _client.SendAsync(Authed(HttpMethod.Get, "/api/profile/me"));
//        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);

//        var json = await ReadJson(resp);
//        Assert.IsTrue(json.TryGetProperty("email", out _));
//        Assert.IsTrue(json.TryGetProperty("username", out _));
//        Assert.IsTrue(json.TryGetProperty("membership", out _));
//        Assert.IsTrue(json.TryGetProperty("booksGenerated", out _));
//        // profileImage can be null initially; presence not guaranteed
//    }

//    // =========================================================
//    // PUT /api/profile/avatar
//    // =========================================================
//    [TestMethod]
//    public async Task UpdateAvatar_Accepts_Allowed_Filename()
//    {
//        var body = new { profileImage = "wizard-avatar.png" };
//        var req = Authed(HttpMethod.Put, "/api/profile/avatar");
//        req.Content = JsonContent.Create(body);

//        var resp = await _client.SendAsync(req);
//        Assert.AreEqual(HttpStatusCode.NoContent, resp.StatusCode);

//        // persisted?
//        using var scope = _factory.Services.CreateScope();
//        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//        var u = await db.Users.FindAsync(_userId);
//        Assert.IsNotNull(u);
//        Assert.AreEqual("wizard-avatar.png", u!.ProfileImage);
//    }

//    [TestMethod]
//    public async Task UpdateAvatar_Rejects_Invalid_Value()
//    {
//        var body = new { profileImage = "not-in-allowlist.png" };
//        var req = Authed(HttpMethod.Put, "/api/profile/avatar");
//        req.Content = JsonContent.Create(body);

//        var resp = await _client.SendAsync(req);
//        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
//    }

//    // =========================================================
//    // GET /api/profile/me/stories
//    // =========================================================
//    [TestMethod]
//    public async Task GetMyStories_Returns_Pagination_Shape()
//    {
//        var resp = await _client.SendAsync(Authed(HttpMethod.Get, "/api/profile/me/stories?page=1&pageSize=6"));
//        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);

//        var json = await ReadJson(resp);
//        Assert.IsTrue(json.TryGetProperty("page", out _));
//        Assert.IsTrue(json.TryGetProperty("pageSize", out _));
//        Assert.IsTrue(json.TryGetProperty("total", out _));
//        Assert.IsTrue(json.TryGetProperty("items", out var items) &&
//                      items.ValueKind == JsonValueKind.Array &&
//                      items.GetArrayLength() >= 1, "Expected at least one story item");

//        // Each item should have PageCount only (not full pages)
//        var first = items[0];
//        Assert.IsTrue(first.TryGetProperty("id", out _));
//        Assert.IsTrue(first.TryGetProperty("title", out _));
//        Assert.IsTrue(first.TryGetProperty("coverImageUrl", out _));
//        Assert.IsTrue(first.TryGetProperty("createdAt", out _));
//        Assert.IsTrue(first.TryGetProperty("pageCount", out _));
//        Assert.IsFalse(first.TryGetProperty("pages", out _), "Summaries should not include pages array");
//    }

//    // =========================================================
//    // GET /api/profile/me/stories/{id}
//    // =========================================================
//    [TestMethod]
//    public async Task GetStory_Returns_Pages_For_Own_Story()
//    {
//        int storyId;
//        using (var scope = _factory.Services.CreateScope())
//        {
//            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//            storyId = await db.Stories
//                              .Where(s => s.UserId == _userId)
//                              .Select(s => s.Id)
//                              .OrderBy(id => id)
//                              .FirstAsync();
//        }

//        var resp = await _client.SendAsync(Authed(HttpMethod.Get, $"/api/profile/me/stories/{storyId}"));
//        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);

//        var json = await ReadJson(resp);
//        Assert.IsTrue(json.TryGetProperty("id", out var idEl) && idEl.GetInt32() == storyId);
//        Assert.IsTrue(json.TryGetProperty("title", out _));
//        Assert.IsTrue(json.TryGetProperty("pages", out var pages) &&
//                      pages.ValueKind == JsonValueKind.Array &&
//                      pages.GetArrayLength() >= 1, "Expected pages to be returned for single-story endpoint");

//        var page0 = pages[0];
//        Assert.IsTrue(page0.TryGetProperty("text", out _));
//        Assert.IsTrue(page0.TryGetProperty("imageUrl", out _));
//    }

//    [TestMethod]
//    public async Task GetStory_Returns_404_For_Nonexistent_Story()
//    {
//        var resp = await _client.SendAsync(Authed(HttpMethod.Get, "/api/profile/me/stories/999999"));
//        Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
//    }
//}
