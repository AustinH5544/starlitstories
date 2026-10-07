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
        var prompt = PromptBuilder.BuildImagePrompt(Human(), "They met a wise owl.", "comic");

        StringAssert.StartsWith(prompt, "Children's comic book illustration");
        StringAssert.Contains(prompt, "Portrait orientation");
        StringAssert.Contains(prompt, "7-year-old boy");
        StringAssert.Contains(prompt, "talking to a wise owl");
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("watercolor")]
    [DataRow("not-a-real-style")]
    public void Missing_Or_Unknown_Style_Falls_Back_To_Watercolor(string? style)
        => StringAssert.StartsWith(PromptBuilder.BuildImagePrompt(Human(), "A walk.", style), "Children's watercolor illustration");

    [TestMethod]
    public void Animal_Character_Uses_Species()
        => StringAssert.Contains(PromptBuilder.BuildImagePrompt(Fox(), "A walk.", "pixel"), "fox");

    [TestMethod]
    public void Base_Character_Prompt_Is_A_Plain_Reference_Portrait()
    {
        var prompt = PromptBuilder.BuildBaseCharacterPrompt(Human(), "clay");

        StringAssert.StartsWith(prompt, "Children's clay animation style illustration");
        StringAssert.Contains(prompt, "plain white background");
        StringAssert.Contains(prompt, "no text");
    }

    [TestMethod]
    public void Cover_Prompt_Includes_Theme_And_No_Text_Rule()
    {
        var prompt = PromptBuilder.BuildCoverPrompt(Human(), "Under the Sea", "early", "gouache");

        StringAssert.Contains(prompt, "Under the Sea");
        StringAssert.Contains(prompt, "gouache");
        StringAssert.Contains(prompt, "no text");
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("network down");
    }

    // FINDING (spec open question): the retry loops use `catch when (attempt < maxAttempts)`, so the
    // last failure escapes and the keyword/static fallback below the loop is unreachable. StoryGenerator
    // calls both methods, so two OpenAI scene failures fail the whole story instead of falling back.
    // These tests pin current behavior; when the fix is approved, flip them to assert the fallback prompt.

    [TestMethod]
    public async Task Image_Prompt_Throws_Instead_Of_Falling_Back_When_Scene_Api_Fails_OpenQuestion()
    {
        await Assert.ThrowsExceptionAsync<HttpRequestException>(() => PromptBuilder.BuildImagePromptAsync(
            Human(), "They met an owl.\r\nThen they rested.", new HttpClient(new FailingHandler()), "test-key", "comic"));
    }

    [TestMethod]
    public async Task Cover_Prompt_Throws_Instead_Of_Falling_Back_When_Scene_Api_Fails_OpenQuestion()
    {
        await Assert.ThrowsExceptionAsync<HttpRequestException>(() => PromptBuilder.BuildCoverPromptAsync(
            Human(), "Under the Sea", "early", "comic", new[] { "They swam.", "They rested." },
            new HttpClient(new FailingHandler()), "test-key"));
    }
}
