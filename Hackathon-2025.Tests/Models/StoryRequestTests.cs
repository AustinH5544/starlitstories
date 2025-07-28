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

        // Act
        var email = request.Email;
        var theme = request.Theme;
        var characters = request.Characters;

        // Assert
        Assert.AreEqual("", email);
        Assert.AreEqual("", theme);
        Assert.IsNotNull(characters);
        Assert.AreEqual(0, characters.Count);
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
            Email = "luna@example.com",
            Theme = "friendship",
            Characters = characterList
        };

        // Act
        var email = request.Email;
        var theme = request.Theme;
        var characters = request.Characters;

        // Assert
        Assert.AreEqual("luna@example.com", email);
        Assert.AreEqual("friendship", theme);
        Assert.AreEqual(1, characters.Count);
        Assert.AreEqual("Luna", characters[0].Name);
        Assert.IsTrue(characters[0].IsAnimal);
        Assert.AreEqual("fox", characters[0].DescriptionFields["species"]);
    }
}
