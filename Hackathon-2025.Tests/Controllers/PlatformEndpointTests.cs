using System.Net;
using System.Xml.Linq;
using Hackathon_2025.Tests.Utils;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class PlatformEndpointTests
{
    [DataTestMethod]
    [DataRow("/healthz")]
    [DataRow("/api/healthz")]
    [DataRow("/__ping")]
    [DataRow("/api/story/ping")]
    [DataRow("/api/config")]
    public async Task Public_Health_And_Config_Endpoints_Respond(string path)
    {
        using var factory = new TestWebAppFactory();
        var resp = await factory.AnonymousClient().GetAsync(path);
        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
    }

    [TestMethod]
    public async Task Sitemap_Is_Valid_Xml_Listing_Public_Pages_Only()
    {
        using var factory = new TestWebAppFactory();

        var resp = await factory.AnonymousClient().GetAsync("/sitemap.xml");

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        StringAssert.Contains(resp.Content.Headers.ContentType!.MediaType!, "xml");
        var xml = XDocument.Parse(await resp.Content.ReadAsStringAsync());
        var locs = xml.Descendants().Where(e => e.Name.LocalName == "loc").Select(e => e.Value).ToList();
        CollectionAssert.Contains(locs, "https://starlitstories.app/about");
        CollectionAssert.Contains(locs, "https://starlitstories.app/blog");
        Assert.IsFalse(locs.Any(l => l.Contains("/profile") || l.Contains("/create") || l.Contains("/admin")));
    }
}
