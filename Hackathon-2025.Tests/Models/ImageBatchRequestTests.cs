using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hackathon_2025.Models;

namespace Hackathon_2025.Tests.Models;

[TestClass]
public class ImageBatchRequestTests
{
    [TestMethod]
    public void CanAssignPromptsList()
    {
        var prompts = new List<string> { "a dragon flying", "a castle at sunset" };
        var request = new ImageBatchRequest
        {
            Prompts = prompts
        };

        CollectionAssert.AreEqual(prompts, request.Prompts);
    }

    [TestMethod]
    public void PromptsList_CanBeEmpty()
    {
        var request = new ImageBatchRequest
        {
            Prompts = new List<string>()
        };

        Assert.IsNotNull(request.Prompts);
        Assert.AreEqual(0, request.Prompts.Count);
    }
}
