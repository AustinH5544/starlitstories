using System.Security.Claims;
using Hackathon_2025.Options;
using Hackathon_2025.Services;

namespace Hackathon_2025.Tests.Services;

[TestClass]
public class AdminAccessServiceTests
{
    private static AdminAccessService Create() =>
        new(Microsoft.Extensions.Options.Options.Create(new AdminOptions
        {
            Emails = new List<string> { "A@X.com" },
            EmailsCsv = " b@y.com ; c@z.com"
        }));

    [DataTestMethod]
    [DataRow("a@x.com", true)]
    [DataRow(" B@Y.COM ", true)]
    [DataRow("c@z.com", true)]
    [DataRow("d@z.com", false)]
    [DataRow("", false)]
    [DataRow(null, false)]
    public void IsAdminEmail_Matches_List_And_Csv_Ignoring_Case_And_Spaces(string? email, bool expected)
        => Assert.AreEqual(expected, Create().IsAdminEmail(email));

    [TestMethod]
    public void IsAdmin_Reads_Standard_Or_Short_Email_Claim()
    {
        var standard = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Email, "a@x.com") }, "t"));
        var shortForm = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("email", "b@y.com") }, "t"));
        var none = new ClaimsPrincipal(new ClaimsIdentity(Array.Empty<Claim>(), "t"));

        Assert.IsTrue(Create().IsAdmin(standard));
        Assert.IsTrue(Create().IsAdmin(shortForm));
        Assert.IsFalse(Create().IsAdmin(none));
    }
}
