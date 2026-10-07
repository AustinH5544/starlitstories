using Hackathon_2025.Models;
using Hackathon_2025.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;

namespace Hackathon_2025.Tests.Services;

[TestClass]
public class QuotaServiceTests
{
    private static QuotaService Create(CreditsOptions? options = null)
    {
        var snapshot = new Mock<IOptionsSnapshot<CreditsOptions>>();
        snapshot.Setup(s => s.Value).Returns(options ?? new CreditsOptions());
        return new QuotaService(snapshot.Object);
    }

    [DataTestMethod]
    [DataRow("Free", 1)]
    [DataRow("Pro", 5)]
    [DataRow("Premium", 11)]
    [DataRow("premium", 11)]
    [DataRow("PRO", 5)]
    public void BaseQuotaFor_Is_Case_Insensitive(string membership, int expected)
        => Assert.AreEqual(expected, Create().BaseQuotaFor(membership));

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void BaseQuotaFor_Blank_Membership_Means_Free(string? membership)
        => Assert.AreEqual(1, Create().BaseQuotaFor(membership));

    [TestMethod]
    public void BaseQuotaFor_Unknown_Plan_Is_Zero()
        => Assert.AreEqual(0, Create().BaseQuotaFor("gold"));

    [TestMethod]
    public void BaseQuotas_Bound_From_Configuration_Stay_Case_Insensitive()
    {
        // Mirrors how appsettings.json "Credits" is bound in production.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Credits:BaseQuotas:free"] = "1",
                ["Credits:BaseQuotas:pro"] = "5",
                ["Credits:BaseQuotas:premium"] = "11"
            })
            .Build();
        var bound = config.GetSection("Credits").Get<CreditsOptions>()!;

        Assert.AreEqual(11, Create(bound).BaseQuotaFor(MembershipPlan.Premium.ToString()));
        Assert.AreEqual(5, Create(bound).BaseQuotaFor(MembershipPlan.Pro.ToString()));
    }

    [DataTestMethod]
    [DataRow("Pro", 0, 0, false, DisplayName = "Pro cannot buy (premium only)")]
    [DataRow("Premium", 1, 0, false, DisplayName = "Premium with base left cannot buy")]
    [DataRow("Premium", 0, 0, true, DisplayName = "Premium exhausted can buy")]
    [DataRow("premium", 0, 5, true, DisplayName = "Existing add-ons do not block buying")]
    public void CanBuyAddons_Default_Policy(string membership, int baseRemaining, int addOnBalance, bool expected)
        => Assert.AreEqual(expected, Create().CanBuyAddons(membership, baseRemaining, addOnBalance));

    [TestMethod]
    public void CanBuyAddons_When_Premium_Not_Required_Pro_Can_Buy()
        => Assert.IsTrue(Create(new CreditsOptions { RequirePremiumForAddons = false }).CanBuyAddons("Pro", 0, 0));

    [TestMethod]
    public void CanBuyAddons_When_Exhaustion_Not_Required_Premium_Can_Buy_Early()
        => Assert.IsTrue(Create(new CreditsOptions { OnlyAllowPurchaseWhenExhausted = false }).CanBuyAddons("Premium", 3, 0));
}
