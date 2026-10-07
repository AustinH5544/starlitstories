using Hackathon_2025.Services;

namespace Hackathon_2025.Tests.Services;

[TestClass]
public class UsernameRulesTests
{
    [DataTestMethod]
    [DataRow("milo")]
    [DataRow("abc")]
    [DataRow("a.b_c-1")]
    [DataRow(" milo ")]
    [DataRow("abcdefghijklmnopqrstuvwx")] // 24 chars
    public void Valid(string username) => Assert.IsTrue(UsernameRules.IsValid(username));

    [DataTestMethod]
    [DataRow("ab")]
    [DataRow("abcdefghijklmnopqrstuvwxy")] // 25 chars
    [DataRow("Milo")]
    [DataRow("has space")]
    [DataRow("émile")]
    [DataRow("   ")]
    [DataRow(null)]
    public void Invalid(string? username) => Assert.IsFalse(UsernameRules.IsValid(username));

    [TestMethod]
    public void Normalize_Trims_And_Lowercases() => Assert.AreEqual("milo", UsernameRules.Normalize("  MiLo "));
}
