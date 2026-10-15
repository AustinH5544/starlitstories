using System.Text.RegularExpressions;
using Hackathon_2025.Services;

namespace Hackathon_2025.Tests.Services;

[TestClass]
public class StoryImageNamesTests
{
    [TestMethod]
    public void Cover_Name_Uses_The_User_Id_And_Is_Unique()
    {
        var first = StoryImageNames.Cover(42);
        var second = StoryImageNames.Cover(42);

        Assert.IsTrue(Regex.IsMatch(first, @"^u42-cover-[0-9a-f-]{36}\.png$"), first);
        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void Page_Name_Uses_The_User_Id_And_Page_Index()
    {
        var name = StoryImageNames.Page(42, 3);

        Assert.IsTrue(Regex.IsMatch(name, @"^u42-page-3-[0-9a-f-]{36}\.png$"), name);
        Assert.IsFalse(name.Contains('@'), "Image URLs are public, so names must never contain an email.");
    }
}
