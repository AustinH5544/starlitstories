using Hackathon_2025.Models;
using Hackathon_2025.Services;

namespace Hackathon_2025.Tests.Services;

[TestClass]
public class MembershipEntitlementsTests
{
    [DataTestMethod]
    [DataRow(MembershipPlan.Free, 1)]
    [DataRow(MembershipPlan.Pro, 5)]
    [DataRow(MembershipPlan.Premium, 10)]
    public void SavedCharacterLimit_Per_Plan(MembershipPlan plan, int expected)
        => Assert.AreEqual(expected, MembershipEntitlements.SavedCharacterLimitFor(plan));

    [TestMethod]
    public void Only_Paid_Plans_Support_Advanced_Characters()
    {
        Assert.IsFalse(MembershipEntitlements.SupportsAdvancedCharacterCreation(MembershipPlan.Free));
        Assert.IsTrue(MembershipEntitlements.SupportsAdvancedCharacterCreation(MembershipPlan.Pro));
        Assert.IsTrue(MembershipEntitlements.SupportsAdvancedCharacterCreation(MembershipPlan.Premium));
    }

    [TestMethod]
    public void Free_Fields_Are_Trimmed_To_The_Allowlist_Case_Insensitively()
    {
        var fields = new Dictionary<string, string>
        {
            ["HairColor"] = "brown",
            ["favoriteFood"] = "pizza",
            ["eyeColor"] = "  "
        };

        var result = MembershipEntitlements.SanitizeDescriptionFields(MembershipPlan.Free, fields);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("brown", result["hairColor"]);
    }

    [TestMethod]
    public void Paid_Fields_Keep_Everything_Non_Blank()
    {
        var fields = new Dictionary<string, string> { ["hairColor"] = "brown", ["favoriteFood"] = "pizza", ["blank"] = "" };

        var result = MembershipEntitlements.SanitizeDescriptionFields(MembershipPlan.Pro, fields);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("pizza", result["favoriteFood"]);
    }

    [TestMethod]
    public void Null_Fields_Become_Empty()
        => Assert.AreEqual(0, MembershipEntitlements.SanitizeDescriptionFields(MembershipPlan.Premium, null).Count);

    [TestMethod]
    public void Character_Role_Defaults_To_Main_And_Is_Trimmed()
    {
        var blankRole = new CharacterSpec { Name = "Milo", Role = " " };
        var paddedRole = new CharacterSpec { Name = "Milo", Role = "  Friend " };

        Assert.AreEqual("Main", MembershipEntitlements.SanitizeCharacterForMembership(MembershipPlan.Free, blankRole).Role);
        Assert.AreEqual("Friend", MembershipEntitlements.SanitizeCharacterForMembership(MembershipPlan.Free, paddedRole).Role);
    }
}
