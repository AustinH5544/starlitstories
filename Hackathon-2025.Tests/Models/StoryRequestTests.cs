using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hackathon_2025.Models;
using System.Collections.Generic;

namespace Hackathon_2025.Tests.Models;

[TestClass]
public class StoryRequestTests
{
    [TestMethod]
    public void Constructor_ThemeOnly_ContainsExpectedDefaults()
    {
        // Arrange
        var request = new StoryRequest
        {
            Theme = string.Empty
        };

        // Act
        var theme = request.Theme;
        var characters = request.Characters;
        var readingLevel = request.ReadingLevel;
        var artStyle = request.ArtStyle;
        var pageCount = request.PageCount;
        var lessonLearned = request.LessonLearned;
        var storyLength = request.StoryLength;

        // Assert
        Assert.AreEqual(string.Empty, theme);
        Assert.IsNotNull(characters);
        Assert.AreEqual(0, characters.Count);
        Assert.IsNull(readingLevel);
        Assert.IsNull(artStyle);
        Assert.IsNull(pageCount);
        Assert.IsNull(lessonLearned);
        Assert.IsNull(storyLength);
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
                Role = CharacterRole.Main,
                IsAnimal = true,
                DescriptionFields = new Dictionary<string, string>
                {
                    { "species", "fox" },
                    { "color", "silver" }
                }
            }
        };

        // Act
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
        Assert.AreEqual(CharacterRole.Main, request.Characters[0].Role);
        Assert.IsTrue(request.Characters[0].IsAnimal);
        Assert.AreEqual("fox", request.Characters[0].DescriptionFields["species"]);
    }
}
