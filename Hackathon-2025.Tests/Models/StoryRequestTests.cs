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
                Role = "Main", // string now, not CharacterRole
                IsAnimal = false, // human-only for now
                DescriptionFields = new Dictionary<string, string>
                {
                    { "age", "7" },
                    { "hairColor", "brown" }
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

        var character = request.Characters[0];
        Assert.AreEqual("Luna", character.Name);
        Assert.AreEqual("Main", character.Role);
        Assert.IsFalse(character.IsAnimal);
        Assert.AreEqual("7", character.DescriptionFields["age"]);
        Assert.AreEqual("brown", character.DescriptionFields["hairColor"]);
    }
}
