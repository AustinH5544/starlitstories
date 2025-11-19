using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hackathon_2025.Models;
using System;

namespace Hackathon_2025.Tests.Models;

[TestClass]
public class StoryPageTests
{
    [TestMethod]
    public void Constructor_CustomValues_ContainExpectedCoreData()
    {
        // Arrange
        var text = "A dragon flies over the mountains.";
        var prompt = "dragon in the sky";

        // Act
        var page = new StoryPage(text, prompt);

        // Assert
        Assert.AreEqual(text, page.Text);
        Assert.AreEqual(prompt, page.ImagePrompt);
        Assert.IsNull(page.ImageUrl);
        Assert.AreEqual(0, page.StoryId);
        Assert.IsNull(page.Story);
    }

    [TestMethod]
    public void Properties_SetAfterConstruction_AreStoredCorrectly()
    {
        // Arrange
        var page = new StoryPage(
            text: "A knight enters the castle.",
            imagePrompt: "knight at castle gate");

        var expectedImageUrl = "https://example.com/image.png";
        var expectedStoryId = 99;
        var expectedStory = new Story
        {
            Id = expectedStoryId,
            Title = "Knight's Journey"
        };

        // Act
        page.ImageUrl = expectedImageUrl;
        page.StoryId = expectedStoryId;
        page.Story = expectedStory;

        // Assert
        Assert.AreEqual("A knight enters the castle.", page.Text);
        Assert.AreEqual("knight at castle gate", page.ImagePrompt);
        Assert.AreEqual(expectedImageUrl, page.ImageUrl);
        Assert.AreEqual(expectedStoryId, page.StoryId);
        Assert.IsNotNull(page.Story);
        Assert.AreEqual(expectedStoryId, page.Story!.Id);
        Assert.AreEqual("Knight's Journey", page.Story.Title);
    }

    [TestMethod]
    public void Constructor_NullText_ThrowsArgumentNullException()
    {
        // Arrange
        string? text = null;
        var imagePrompt = "a desert oasis";

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(
            () => new StoryPage(text!, imagePrompt));
    }

    [TestMethod]
    public void Constructor_NullImagePrompt_ThrowsArgumentNullException()
    {
        // Arrange
        var text = "A wanderer walks alone";
        string? imagePrompt = null;

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(
            () => new StoryPage(text, imagePrompt!));
    }
}
