using System.Net;
using Hackathon_2025.Tests.Utils;

namespace Hackathon_2025.Tests.SqlServer;

[TestClass]
[TestCategory("SqlServer")]
public class ReadinessTests
{
    [TestMethod]
    public async Task Readyz_Returns_Ready_When_Database_Is_Reachable()
    {
        using var factory = await SqlServerWebAppFactory.CreateAsync();

        var resp = await factory.AnonymousClient().GetAsync("/readyz");

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        StringAssert.Contains(await resp.Content.ReadAsStringAsync(), "ready");
    }

    [TestMethod]
    public async Task Warmup_Returns_NoContent()
    {
        using var factory = await SqlServerWebAppFactory.CreateAsync();

        var resp = await factory.AnonymousClient().PostAsync("/api/warmup", content: null);

        Assert.AreEqual(HttpStatusCode.NoContent, resp.StatusCode);
    }
}
