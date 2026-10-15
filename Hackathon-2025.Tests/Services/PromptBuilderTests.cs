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

    // Decided 2026-10-13: one light-to-deep skin tone scale for every plan, no ethnicity in the form or the prompts.

    private static string PortraitFor(Dictionary<string, string> fields) => PromptBuilder.BuildBaseCharacterPrompt(
        new List<CharacterSpec> { new() { Name = "Milo", DescriptionFields = new(fields, StringComparer.OrdinalIgnoreCase) } }, "watercolor");

    private static string PortraitWithSkin(string skinTone) => PortraitFor(new() { ["age"] = "7", ["gender"] = "boy", ["skinTone"] = skinTone });

    [DataTestMethod]
    [DataRow("porcelain", "very fair porcelain skin with cool pink undertones")]
    [DataRow("fair", "fair skin")]
    [DataRow("light", "light skin with warm undertones")]
    [DataRow("light olive", "light olive skin")]
    [DataRow("golden", "warm golden skin")]
    [DataRow("olive", "olive skin")]
    [DataRow("tan", "warm tan skin")]
    [DataRow("bronze", "bronze light brown skin")]
    [DataRow("medium brown", "medium brown skin")]
    [DataRow("warm brown", "warm brown skin with golden undertones")]
    [DataRow("deep brown", "deep brown skin")]
    [DataRow("deepest brown", "very deep brown skin")]
    public void Each_Scale_Tone_Is_Described_Clearly_And_Once(string tone, string expected)
    {
        var prompt = PortraitWithSkin(tone);

        StringAssert.Contains(prompt, expected);
        Assert.IsFalse(prompt.Contains("skin skin"), prompt);
    }

    // Saved characters and old requests still carry the previous options; they map to the nearest scale tone.
    [DataTestMethod]
    [DataRow("pale", "very fair porcelain skin with cool pink undertones")]
    [DataRow("freckled", "light skin with warm undertones")]
    [DataRow("dark", "deep brown skin")]
    [DataRow("ebony", "very deep brown skin")]
    [DataRow("brown", "medium brown skin")]
    [DataRow("medium", "warm tan skin")]
    [DataRow("sun-kissed", "warm golden skin")]
    public void Previous_Tone_Options_Map_To_The_Nearest_Scale_Tone(string oldTone, string expected)
    {
        var prompt = PortraitWithSkin(oldTone);

        StringAssert.Contains(prompt, expected);
        Assert.IsFalse(prompt.Contains("skin skin"), prompt);
    }

    [DataTestMethod]
    [DataRow("light brown with freckles", "light brown with freckles skin")]
    [DataRow("pale skin with rosy cheeks", "pale skin with rosy cheeks")]
    public void A_Custom_Skin_Description_Is_Kept_And_Reads_Naturally(string custom, string expected)
    {
        var prompt = PortraitWithSkin(custom);

        // The description ends where the next part (or the sentence) begins: no stray "skin" tacked on.
        StringAssert.Matches(prompt, new System.Text.RegularExpressions.Regex(System.Text.RegularExpressions.Regex.Escape(expected) + "[,.]"));
    }

    // Decided 2026-10-13: one hairstyle list for everyone (no filtering by gender), and hair always reads as hair.
    [DataTestMethod]
    [DataRow("brown", "messy", "chestnut brown messy hair")]
    [DataRow("black", "curly", "jet black curly hair")]
    [DataRow("black", "afro", "jet black hair in an afro")]
    [DataRow("black", "locs", "jet black hair in locs")]
    [DataRow("black", "dreadlocks", "jet black hair in locs")]
    [DataRow("red", "pigtails", "bright natural red hair in pigtails")]
    [DataRow("blonde", "ponytail", "golden blonde hair in a ponytail")]
    [DataRow("dark brown", "cornrows", "deep dark brown hair in cornrows")]
    [DataRow("black", "half-up with a clip", "jet black hair, half-up with a clip")]
    [DataRow("black", "", "jet black hair")]
    [DataRow("", "curly", "curly hair")]
    [DataRow("", "afro", "hair in an afro")]
    public void Hair_Is_Described_As_Hair_With_Its_Style(string color, string style, string expected)
    {
        var fields = new Dictionary<string, string> { ["age"] = "7", ["gender"] = "girl" };
        if (color.Length > 0) fields["hairColor"] = color;
        if (style.Length > 0) fields["hairStyle"] = style;

        StringAssert.Matches(PortraitFor(fields), new System.Text.RegularExpressions.Regex(", " + System.Text.RegularExpressions.Regex.Escape(expected) + "[,.]"));
    }

    [DataTestMethod]
    [DataRow("wheelchair", "using a wheelchair")]
    [DataRow("hearing aids", "wearing hearing aids")]
    [DataRow("glasses", "glasses")]
    public void Accessories_Read_Naturally_In_Image_Prompts(string accessory, string expected)
    {
        var prompt = PortraitFor(new() { ["age"] = "7", ["gender"] = "boy", ["accessory"] = accessory });

        StringAssert.Matches(prompt, new System.Text.RegularExpressions.Regex(", " + System.Text.RegularExpressions.Regex.Escape(expected) + "[,.]"));
    }

    [TestMethod]
    public void Story_Notes_Tell_The_Writer_When_A_Character_Uses_A_Wheelchair()
    {
        var characters = new List<CharacterSpec>
        {
            new() { Name = "Milo", DescriptionFields = new(StringComparer.OrdinalIgnoreCase) { ["accessory2"] = "Wheelchair" } }
        };

        var notes = PromptBuilder.BuildStoryCharacterNotes(characters);

        Assert.AreEqual(1, notes.Count);
        StringAssert.StartsWith(notes[0], "Milo uses a wheelchair.");
    }

    [TestMethod]
    public void Story_Notes_Are_Empty_When_Nothing_Changes_How_A_Character_Moves()
        => Assert.AreEqual(0, PromptBuilder.BuildStoryCharacterNotes(Human()).Count);

    [TestMethod]
    public void Ethnicity_From_Older_Saved_Characters_Is_Not_Put_In_Image_Prompts()
    {
        var prompt = PortraitFor(new() { ["age"] = "7", ["gender"] = "boy", ["ethnicity"] = "Pacific Islander", ["race"] = "Asian", ["skinTone"] = "golden" });

        Assert.IsFalse(prompt.Contains("Pacific Islander"), prompt);
        Assert.IsFalse(prompt.Contains("Asian"), prompt);
        StringAssert.Contains(prompt, "warm golden skin");
    }

    // Found on staging 2026-10-15: each scene saw only its own page, so "the fort" on later pages was drawn
    // as a blanket fort indoors or a summer backyard fort instead of the snow fort from page 2.
    private static readonly string[] SnowFortStory =
    {
        "Vanessa loved visiting Winter Wonderland.",
        "She wanted to build the biggest snow fort anyone had ever seen.",
        "But when other children came near, Vanessa told them to stay away from the fort."
    };

    [TestMethod]
    public void Scene_Request_Includes_The_Theme_And_Whole_Story_For_Context()
    {
        var request = PromptBuilder.BuildSceneRequest(SnowFortStory[2], "Winter Wonderland", SnowFortStory, 2);

        StringAssert.Contains(request, "The story's theme is \"Winter Wonderland\".");
        StringAssert.Contains(request, "Page 2: She wanted to build the biggest snow fort anyone had ever seen.");
        StringAssert.Contains(request, "Keep the setting consistent with the rest of the story");
        StringAssert.Contains(request, $"Describe page 3: \"{SnowFortStory[2]}\"");
    }

    [DataTestMethod]
    [DataRow(-1)]
    [DataRow(3)]
    public void Scene_Request_Without_A_Valid_Page_Falls_Back_To_The_Single_Paragraph_Request(int pageIndex)
    {
        var request = PromptBuilder.BuildSceneRequest("They swam with jellyfish.", "Under the Sea", SnowFortStory, pageIndex);

        StringAssert.StartsWith(request, "Summarize the following story paragraph");
        StringAssert.Contains(request, "Paragraph: \"They swam with jellyfish.\"");
        Assert.IsFalse(request.Contains("Page 1:"));
    }

    [TestMethod]
    public async Task Image_Prompt_Sends_The_Story_Context_To_The_Scene_Writer()
    {
        var api = new CapturingSceneApi();

        await PromptBuilder.BuildImagePromptAsync(Human(), SnowFortStory[2], new HttpClient(api), "test-key", "comic",
            "Winter Wonderland", SnowFortStory, 2);

        StringAssert.Contains(api.LastBody, "snow fort anyone had ever seen");
        StringAssert.Contains(api.LastBody, "Describe page 3");
    }

    private sealed class CapturingSceneApi : HttpMessageHandler
    {
        public string LastBody { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            var body = System.Text.Json.JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = "building a snow fort" } } } });
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) };
        }
    }
}
