using System.Net;
using System.Net.Http.Json;
using Hackathon_2025.Services;
using Hackathon_2025.Tests.Utils;
using Moq;

namespace Hackathon_2025.Tests.Controllers;

[TestClass]
public class SupportControllerTests
{
    private TestWebAppFactory _factory = null!;

    [TestInitialize]
    public void Init() => _factory = new TestWebAppFactory();

    [TestCleanup]
    public void Cleanup() => _factory.Dispose();

    private static object Message(
        string? email = "parent@example.com",
        string? subject = "Can't download my story",
        string? message = "The PDF button does nothing on my phone.",
        string? name = "Sam Parent",
        string? category = "technical",
        string? priority = "medium") => new
        {
            name,
            email,
            category,
            priority,
            subject,
            message,
            turnstileToken = "test-token"
        };

    private Task<HttpResponseMessage> PostAsync(object body) =>
        _factory.AnonymousClient().PostAsJsonAsync("/api/support", body);

    private void VerifyNoEmailSent() =>
        _factory.EmailMock.Verify(
            x => x.SendCustomEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()),
            Times.Never);

    [TestMethod]
    public async Task Valid_Message_Is_Emailed_To_Support_With_Reply_To_The_Sender()
    {
        var resp = await PostAsync(Message());

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        _factory.EmailMock.Verify(x => x.SendCustomEmailAsync(
            "support@starlitstories.app",
            It.Is<string>(s => s.Contains("Can't download my story") && s.Contains("technical")),
            It.Is<string>(b => b.Contains("The PDF button does nothing on my phone.")
                               && b.Contains("Sam Parent")
                               && b.Contains("parent@example.com")),
            "parent@example.com"), Times.Once);
    }

    [TestMethod]
    public async Task Recipients_Can_Be_Overridden_By_Configuration()
    {
        using var factory = new TestWebAppFactory(new Dictionary<string, string?>
        {
            ["Support:RecipientsCsv"] = "help-a@test.local, help-b@test.local"
        });

        var resp = await factory.AnonymousClient().PostAsJsonAsync("/api/support", Message());

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        foreach (var to in new[] { "help-a@test.local", "help-b@test.local" })
            factory.EmailMock.Verify(x => x.SendCustomEmailAsync(to, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
        factory.EmailMock.Verify(x => x.SendCustomEmailAsync("support@starlitstories.app", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [DataTestMethod]
    [DataRow("", "Subject", "Message")]
    [DataRow("not-an-email", "Subject", "Message")]
    [DataRow("parent@example.com", "", "Message")]
    [DataRow("parent@example.com", "Subject", "   ")]
    public async Task Missing_Or_Invalid_Fields_Are_Rejected_Without_Sending(string email, string subject, string message)
    {
        var resp = await PostAsync(Message(email: email, subject: subject, message: message));

        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
        VerifyNoEmailSent();
    }

    [TestMethod]
    public async Task Overlong_Message_Is_Rejected_Without_Sending()
    {
        var resp = await PostAsync(Message(message: new string('a', 5001)));

        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
        VerifyNoEmailSent();
    }

    [TestMethod]
    public async Task Failed_Human_Check_Is_Rejected_Without_Sending()
    {
        _factory.TurnstileMock
            .Setup(x => x.VerifyAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TurnstileVerificationResult.Failed("Please complete the human verification challenge."));

        var resp = await PostAsync(Message());

        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
        StringAssert.Contains(await resp.Content.ReadAsStringAsync(), "human verification");
        VerifyNoEmailSent();
    }

    [TestMethod]
    public async Task Html_In_The_Message_Is_Escaped()
    {
        string? sentBody = null;
        _factory.EmailMock
            .Setup(x => x.SendCustomEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Callback<string, string, string, string?>((_, _, body, _) => sentBody = body)
            .Returns(Task.CompletedTask);

        await PostAsync(Message(message: "<script>alert('hi')</script>", name: "<b>Eve</b>"));

        Assert.IsNotNull(sentBody);
        Assert.IsFalse(sentBody!.Contains("<script>"), sentBody);
        Assert.IsFalse(sentBody.Contains("<b>Eve</b>"), sentBody);
        StringAssert.Contains(sentBody, "&lt;script&gt;");
    }

    [TestMethod]
    public async Task Line_Breaks_Cannot_Be_Injected_Into_The_Subject()
    {
        string? sentSubject = null;
        _factory.EmailMock
            .Setup(x => x.SendCustomEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Callback<string, string, string, string?>((_, subject, _, _) => sentSubject = subject)
            .Returns(Task.CompletedTask);

        await PostAsync(Message(subject: "Hello\r\nBcc: victim@example.com"));

        Assert.IsNotNull(sentSubject);
        Assert.IsFalse(sentSubject!.Contains('\r') || sentSubject.Contains('\n'), sentSubject);
    }

    [TestMethod]
    public async Task Email_Failure_Returns_An_Error_Not_A_Fake_Success()
    {
        _factory.EmailMock
            .Setup(x => x.SendCustomEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("Email:FromEmail is not configured."));

        var resp = await PostAsync(Message());

        Assert.AreEqual(HttpStatusCode.BadGateway, resp.StatusCode);
        StringAssert.Contains(await resp.Content.ReadAsStringAsync(), "support@starlitstories.app");
    }

    [TestMethod]
    public async Task Sixth_Message_In_Ten_Minutes_From_One_Address_Is_Rate_Limited()
    {
        for (var i = 0; i < 5; i++)
            Assert.AreEqual(HttpStatusCode.OK, (await PostAsync(Message())).StatusCode, $"message {i + 1}");

        var sixth = await PostAsync(Message());

        Assert.AreEqual(HttpStatusCode.TooManyRequests, sixth.StatusCode);
    }
}
