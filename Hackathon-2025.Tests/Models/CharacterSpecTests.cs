using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hackathon_2025.Models;
using System.Collections.Generic;

namespace Hackathon_2025.Tests.Models
{
    [TestClass]
    public class CharacterSpecTests
    {
        [TestMethod]
        public void DefaultInit_SetsExpectedDefaults()
        {
            // Arrange: Name is required, so provide it (empty is fine for default test)
            var character = new CharacterSpec { Name = "" };

            // Assert
            Assert.AreEqual("Main", character.Role);
            Assert.AreEqual("", character.Name);
            Assert.IsFalse(character.IsAnimal);
            Assert.IsNotNull(character.DescriptionFields);
            Assert.AreEqual(0, character.DescriptionFields.Count);
        }

        [TestMethod]
        public void CanAssignCustomValues()
        {
            // Arrange
            var character = new CharacterSpec
            {
                Role = "Friend",
                Name = "Max",
                IsAnimal = true,
                DescriptionFields = new Dictionary<string, string>
                {
                    { "species", "dog" },
                    { "color", "brown" }
                }
            };

            // Assert
            Assert.AreEqual("Friend", character.Role);
            Assert.AreEqual("Max", character.Name);
            Assert.IsTrue(character.IsAnimal);
            Assert.AreEqual(2, character.DescriptionFields.Count);
            Assert.AreEqual("dog", character.DescriptionFields["species"]);
            Assert.AreEqual("brown", character.DescriptionFields["color"]);
        }
    }
}
