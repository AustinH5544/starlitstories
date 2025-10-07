using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hackathon_2025.Models;
using System.Collections.Generic;

namespace Hackathon_2025.Tests.Models;

[TestClass]
public class StoryRequestTests
{
    [TestMethod]
    public void Constructor_DefaultValues_ContainExpectedDefaults()
    {
        // Arrange
        var request = new StoryRequest();

        // Assert
        Assert.AreEqual("", request.Theme);
        Assert.IsNotNull(request.Characters);
        Assert.AreEqual(0, request.Characters.Count);
        Assert.IsNull(request.ReadingLevel);
        Assert.IsNull(request.ArtStyle);
        Assert.IsNull(request.PageCount);
        Assert.IsNull(request.LessonLearned);
        Assert.IsNull(request.StoryLength);
    }

    [TestMethod]
    public void PropertyAssignment_ValidValues_AreStoredCorrectly()
    {
        // Arrange
        var characterList = new List<CharacterSpec>
        {
            new CharacterSpec
            {
                Name = "Luna",
                Role = "main",
                IsAnimal = true,
                DescriptionFields = new Dictionary<string, string>
                {
                    { "species", "fox" },
                    { "color", "silver" }
                }
            }
        };

        var request = new StoryRequest
        {
            ReadingLevel = "early",
            ArtStyle = "watercolor",
            Theme = "friendship",
            Characters = characterList,
            PageCount = 12,
            LessonLearned = "teamwork matters",
            StoryLength = "medium"
        };

        // Assert
        Assert.AreEqual("early", request.ReadingLevel);
        Assert.AreEqual("watercolor", request.ArtStyle);
        Assert.AreEqual("friendship", request.Theme);
        Assert.AreEqual(12, request.PageCount);
        Assert.AreEqual("teamwork matters", request.LessonLearned);
        Assert.AreEqual("medium", request.StoryLength);

        Assert.IsNotNull(request.Characters);
        Assert.AreEqual(1, request.Characters.Count);
        Assert.AreEqual("Luna", request.Characters[0].Name);
        Assert.IsTrue(request.Characters[0].IsAnimal);
        Assert.AreEqual("fox", request.Characters[0].DescriptionFields["species"]);
    }
}
