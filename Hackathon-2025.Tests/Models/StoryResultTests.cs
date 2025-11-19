using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hackathon_2025.Models;
using System.Collections.Generic;

namespace Hackathon_2025.Tests.Models
{
    [TestClass]
    public class StoryResultTests
    {
        [TestMethod]
        public void RequiredOnly_SetsDefaultsCorrectly()
        {
            // Arrange: supply required fields only
            var result = new StoryResult
            {
                Title = "",
                CoverImagePrompt = ""
                // CoverImageUrl omitted -> null
                // Pages omitted -> defaults to empty list
            };

            // Act
            var title = result.Title;
            var prompt = result.CoverImagePrompt;
            var imageUrl = result.CoverImageUrl;
            var pages = result.Pages;

            // Assert
            Assert.AreEqual("", title);
            Assert.AreEqual("", prompt);
            Assert.IsNull(imageUrl);           // nullable, default is null
            Assert.IsNotNull(pages);
            Assert.AreEqual(0, pages.Count);
        }

        [TestMethod]
        public void PropertyAssignment_ValidValues_AreStoredCorrectly()
        {
            // Arrange
            var pages = new List<StoryPageDto>
            {
                new StoryPageDto(
                    Text: "Page 1 text",
                    ImagePrompt: "A flying whale",
                    ImageUrl: "https://example.com/page1.jpg"
                )
            };

            var result = new StoryResult
            {
                Title = "Whale Adventure",
                CoverImagePrompt = "A whale flying over the sea",
                CoverImageUrl = "https://example.com/cover.jpg",
                Pages = pages
            };

            // Act
            var title = result.Title;
            var prompt = result.CoverImagePrompt;
            var imageUrl = result.CoverImageUrl;
            var resultPages = result.Pages;

            // Assert
            Assert.AreEqual("Whale Adventure", title);
            Assert.AreEqual("A whale flying over the sea", prompt);
            Assert.AreEqual("https://example.com/cover.jpg", imageUrl);
            Assert.AreEqual(1, resultPages.Count);
            Assert.AreEqual("Page 1 text", resultPages[0].Text);
            Assert.AreEqual("A flying whale", resultPages[0].ImagePrompt);
            Assert.AreEqual("https://example.com/page1.jpg", resultPages[0].ImageUrl);
        }
    }
}
