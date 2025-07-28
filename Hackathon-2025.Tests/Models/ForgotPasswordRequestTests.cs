using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hackathon_2025.Models;

namespace Hackathon_2025.Tests.Models;

[TestClass]
public class ForgotPasswordRequestTests
{
    [TestMethod]
    public void DefaultConstructor_SetsExpectedEmail()
    {
        var request = new ForgotPasswordRequest();

        Assert.AreEqual(string.Empty, request.Email);
    }

    [TestMethod]
    public void CanAssignEmail()
    {
        var request = new ForgotPasswordRequest
        {
            Email = "user@example.com"
        };

        Assert.AreEqual("user@example.com", request.Email);
    }
}
