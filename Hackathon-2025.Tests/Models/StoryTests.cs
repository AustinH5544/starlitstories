//using Microsoft.VisualStudio.TestTools.UnitTesting;
//using Hackathon_2025.Models;
//using System;
//using System.Collections.Generic;

//namespace Hackathon_2025.Tests.Models;

//[TestClass]
//public class StoryTests
//{
//    [TestMethod]
//    public void Constructor_DefaultValues_ContainExpectedDefaults()
//    {
//        // Arrange
//        var story = new Story();

//        // Act
//        var id = story.Id;
//        var title = story.Title;
//        var coverImageUrl = story.CoverImageUrl;
//        var pages = story.Pages;
//        var createdAt = story.CreatedAt;
//        var userId = story.UserId;
//        var user = story.User;

//        // Assert
//        Assert.AreEqual(0, id);
//        Assert.AreEqual("", title);
//        Assert.AreEqual("", coverImageUrl);
//        Assert.IsNotNull(pages);
//        Assert.AreEqual(0, pages.Count);
//        Assert.AreNotEqual(default(DateTime), createdAt);
//        Assert.AreEqual(0, userId);
//        Assert.IsNotNull(user); // null-forgiving operator ensures non-null
//    }

//    [TestMethod]
//    public void PropertyAssignment_ValidValues_AreStoredCorrectly()
//    {
//        // Arrange
//        var user = new User { Id = 1, Email = "test@example.com" };
//        var storyPages = new List<StoryPage>
//        {
//            new StoryPage
//            {
//                Text = "Once upon a time...",
//                ImagePrompt = "A fox in the forest",
//                ImageUrl = "https://example.com/page1.png"
//            }
//        };

//        var story = new Story
//        {
//            Id = 42,
//            Title = "The Brave Fox",
//            CoverImageUrl = "https://example.com/cover.jpg",
//            Pages = storyPages,
//            CreatedAt = new DateTime(2024, 1, 1),
//            UserId = 1,
//            User = user
//        };

//        // Act
//        var title = story.Title;
//        var cover = story.CoverImageUrl;
//        var pages = story.Pages;
//        var created = story.CreatedAt;
//        var linkedUser = story.User;

//        // Assert
//        Assert.AreEqual("The Brave Fox", title);
//        Assert.AreEqual("https://example.com/cover.jpg", cover);
//        Assert.AreEqual(1, pages.Count);
//        Assert.AreEqual("Once upon a time...", pages[0].Text);
//        Assert.AreEqual("A fox in the forest", pages[0].ImagePrompt);
//        Assert.AreEqual("https://example.com/page1.png", pages[0].ImageUrl);
//        Assert.AreEqual(new DateTime(2024, 1, 1), created);
//        Assert.AreEqual(user, linkedUser);
//        Assert.AreEqual(1, story.UserId);
//    }
//}
