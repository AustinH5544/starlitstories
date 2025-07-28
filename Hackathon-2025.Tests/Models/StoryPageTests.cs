using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hackathon_2025.Models;
using System;

namespace Hackathon_2025.Tests.Models;

[TestClass]
public class StoryPageTests
{
    [TestMethod]
    public void Constructor_CustomValues_ContainExpectedData()
    {
        // Arrange
        string text = "A dragon flies over the mountains.";
        string prompt = "dragon in the sky";

        // Act
        var page = new StoryPage(text, prompt);

        // Assert
        Assert.AreEqual(text, page.Text);
        Assert.AreEqual(prompt, page.ImagePrompt);
        Assert.IsNull(page.ImageUrl);
        Assert.IsNull(page.StoryId);
        Assert.IsNull(page.Story);
    }

    [TestMethod]
    public void Constructor_Parameterless_AllPropertiesCanBeSet()
    {
        // Arrange
        var page = new StoryPage
        {
            Text = "A knight enters the castle.",
            ImagePrompt = "knight at castle gate",
            ImageUrl = "https://example.com/image.png",
            StoryId = 99,
            Story = new Story { Id = 99, Title = "Knight's Journey" }
        };

        // Act
        var text = page.Text;
        var prompt = page.ImagePrompt;
        var imageUrl = page.ImageUrl;
        var storyId = page.StoryId;
        var story = page.Story;

        // Assert
        Assert.AreEqual("A knight enters the castle.", text);
        Assert.AreEqual("knight at castle gate", prompt);
        Assert.AreEqual("https://example.com/image.png", imageUrl);
        Assert.AreEqual(99, storyId);
        Assert.IsNotNull(story);
        Assert.AreEqual(99, story.Id);
        Assert.AreEqual("Knight's Journey", story.Title);
    }

    [TestMethod]
    public void Constructor_NullText_ThrowsArgumentNullException()
    {
        // Arrange
        string? text = null;
        string imagePrompt = "a desert oasis";

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => new StoryPage(text!, imagePrompt));
    }

    [TestMethod]
    public void Constructor_NullImagePrompt_ThrowsArgumentNullException()
    {
        // Arrange
        string text = "A wanderer walks alone";
        string? imagePrompt = null;

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => new StoryPage(text, imagePrompt!));
    }
}
