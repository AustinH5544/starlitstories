using Hackathon_2025.Models;
using Hackathon_2025.Services;

namespace Hackathon_2025.Tests.Services;

[TestClass]
public class PromptBuilderTests
{
    private static List<CharacterSpec> Human() => new()
    {
        new CharacterSpec { Name = "Milo", DescriptionFields = new(StringComparer.OrdinalIgnoreCase) { ["age"] = "7", ["gender"] = "boy" } }
    };

    private static List<CharacterSpec> Fox() => new()
    {
        new CharacterSpec { Name = "Rusty", IsAnimal = true, DescriptionFields = new(StringComparer.OrdinalIgnoreCase) { ["species"] = "fox" } }
    };

    [TestMethod]
    public void Known_Art_Style_Is_Used_With_Guardrails()
    {
        var prompt = PromptBuilder.BuildBaseCharacterPrompt(Human(), "comic");

        StringAssert.StartsWith(prompt, "Children's comic book illustration");
        StringAssert.Contains(prompt, "Portrait orientation");
        StringAssert.Contains(prompt, "7-year-old boy");
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("watercolor")]
    [DataRow("not-a-real-style")]
    public void Missing_Or_Unknown_Style_Falls_Back_To_Watercolor(string? style)
        => StringAssert.StartsWith(PromptBuilder.BuildBaseCharacterPrompt(Human(), style), "Children's watercolor illustration");

    [TestMethod]
    public void Animal_Character_Uses_Species()
        => StringAssert.Contains(PromptBuilder.BuildBaseCharacterPrompt(Fox(), "pixel"), "fox");

    [TestMethod]
    public void Base_Character_Prompt_Is_A_Plain_Reference_Portrait()
    {
        var prompt = PromptBuilder.BuildBaseCharacterPrompt(Human(), "clay");

        StringAssert.StartsWith(prompt, "Children's clay animation style illustration");
        StringAssert.Contains(prompt, "plain white background");
        StringAssert.Contains(prompt, "no text");
    }

    /// <summary>Fails the first <c>failures</c> calls like a network/OpenAI hiccup, then answers like the chat API.</summary>
    private sealed class FlakySceneApi(int failures, string scene = "floating past glowing jellyfish") : HttpMessageHandler
    {
        private int _calls;
        public int Calls => _calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _calls) <= failures)
                throw new HttpRequestException("network down");

            var body = System.Text.Json.JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = scene } } } });
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    // Decided 2026-10-08: when the scene-writing call fails, retry patiently (4 attempts, 1s/2s/4s apart);
    // if it still fails, let the story fail and refund rather than drawing an off-topic fallback picture.

    [TestMethod]
    public async Task Image_Prompt_Rides_Out_A_Brief_Outage()
    {
        var api = new FlakySceneApi(failures: 2);

        var prompt = await PromptBuilder.BuildImagePromptAsync(Human(), "They swam with jellyfish.", new HttpClient(api), "test-key", "comic");

        StringAssert.Contains(prompt, "floating past glowing jellyfish");
        Assert.AreEqual(3, api.Calls);
    }

    [TestMethod]
    public async Task Image_Prompt_Gives_Up_After_Four_Attempts_Instead_Of_Drawing_Something_Unrelated()
    {
        var api = new FlakySceneApi(failures: int.MaxValue);

        await Assert.ThrowsExceptionAsync<HttpRequestException>(() => PromptBuilder.BuildImagePromptAsync(
            Human(), "They flew to the moon.", new HttpClient(api), "test-key", "comic"));
        Assert.AreEqual(4, api.Calls);
    }

    [TestMethod]
    public async Task Cover_Prompt_Rides_Out_A_Brief_Outage()
    {
        var api = new FlakySceneApi(failures: 2, scene: "racing a comet across a starry sky");

        var prompt = await PromptBuilder.BuildCoverPromptAsync(
            Human(), "Space", "early", "comic", new[] { "They flew.", "They landed." }, new HttpClient(api), "test-key");

        StringAssert.Contains(prompt, "racing a comet across a starry sky");
        Assert.AreEqual(3, api.Calls);
    }

    [TestMethod]
    public async Task Cover_Prompt_Gives_Up_After_Four_Attempts()
    {
        var api = new FlakySceneApi(failures: int.MaxValue);

        await Assert.ThrowsExceptionAsync<HttpRequestException>(() => PromptBuilder.BuildCoverPromptAsync(
            Human(), "Under the Sea", "early", "comic", new[] { "They swam.", "They rested." }, new HttpClient(api), "test-key"));
        Assert.AreEqual(4, api.Calls);
    }
}
