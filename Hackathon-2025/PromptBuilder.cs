using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using Hackathon_2025.Models;
using System.Linq;

namespace Hackathon_2025.Services;

public static class PromptBuilder
{
    // Base watercolor style with strong "cover-safe" guardrails.
    private static string GetDefaultArtStyle() =>
        "Children's watercolor art style. ";

    //+
    //"Portrait orientation, single full-bleed image. " +
    //"Soft lighting, gentle pastel tones, hand-painted look, minimal outlines. " +
    //"Show exactly the listed characters once each (no clones or duplicates). ";

    public static string BuildImagePrompt(List<CharacterSpec> characters, string paragraph, string? artStyleKey)
    {
        string anchors = string.Join(" ", characters.Select(GetCharacterAnchor));
        string scene = SummarizeScene(paragraph);
        var style = GetArtStyle(artStyleKey);
        return $"{style} {anchors} in {scene}.";
    }

    /// <summary>
    /// Image prompt that considers reading level (no reader age).
    /// </summary>
    public static string BuildImagePrompt(
        List<CharacterSpec> characters,
        string paragraph,
        string? readingLevel,
        string? artStyleKey)
    {
        string anchors = string.Join(" ", characters.Select(GetCharacterAnchor));
        string scene = SummarizeScene(paragraph);
        var (visualMood, tone) = GetReadingProfile(readingLevel);
        var style = GetArtStyle(artStyleKey);

        return $"{style} Use {visualMood}. Keep the tone {tone}. {anchors} in {scene}. ";
    }

    private static string GetArtStyle(string? key)
    {
        var k = (key ?? "watercolor").Trim().ToLowerInvariant();

        const string guardrails =
            " Portrait orientation, single full-bleed image. " +
            " Show exactly the listed characters once each (no clones). ";
        //+
        //" No character sheets, thumbnails, panels, turnarounds, labels, callouts, or sidebars. " +
        //" No color palettes, swatches, UI, logos, or design guides. " +
        //" One cohesive scene only.";

        return k switch
        {
            "comic" => "Children's comic style. Bold clean outlines. Flat bright colors." + guardrails,
            "crayon" => "Crayon drawing style. Waxy texture. Child-like strokes. Soft colors." + guardrails,
            "papercut" => "Paper cutout collage style. Flat layered shapes. Soft shadows. No outlines." + guardrails,
            "toy3d" => "3D toy render style. Soft lighting. Plush/plastic look. Gentle colors." + guardrails,
            "pixel" => "Pixel art style. Low resolution mosaic look. Clear silhouettes." + guardrails,
            "inkwash" => "Ink and wash style. Minimal lines. Soft washes. Calming mood." + guardrails,
            _ => GetDefaultArtStyle()
        };
    }

    public static async Task<string> BuildImagePromptAsync(
        List<CharacterSpec> characters,
        string paragraph,
        HttpClient httpClient,
        string apiKey,
        string? artStyleKey)
    {
        // 1) Clean once up front (used by both primary & fallback paths)
        paragraph = CleanForModel(paragraph);

        // 2) Bounded retries for transient failures
        const int maxAttempts = 2;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await BuildImagePromptWithSceneAsync(
                    characters, paragraph, httpClient, apiKey, artStyleKey);
            }
            catch when (attempt < maxAttempts)
            {
                await Task.Delay(300 * attempt); // simple backoff
            }
        }

        // 3) Fallback: keyword heuristic if API keeps failing
        string anchors = string.Join(" ", characters.Select(GetCharacterAnchor));
        var style = GetArtStyle(artStyleKey);
        var scene = string.IsNullOrWhiteSpace(paragraph)
            ? "posing for a simple portrait in a calm setting"
            : SummarizeScene(paragraph);

        return $"{style} {anchors} in {scene}.";
    }

    private static string CleanForModel(string s, int maxLen = 800)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length > maxLen ? s[..maxLen] : s;
    }

    private static string GetCharacterAnchor(CharacterSpec character)
    {
        var description = character.IsAnimal
            ? BuildAnimalDescription(character.DescriptionFields)
            : BuildHumanDescription(character.DescriptionFields);

        // Avoid labels/parentheses (e.g., "Role:") that trigger sidebars or callouts.
        return $"a consistent character named {character.Name} who is {description}";
    }

    private static string BuildHumanDescription(Dictionary<string, string> fields)
    {
        var parts = new List<string>();

        if (fields.TryGetValue("age", out var age))
            parts.Add($"a {age}-year-old");

        if (fields.TryGetValue("gender", out var gender))
            parts.Add(gender);

        if (fields.TryGetValue("skinTone", out var skin))
            parts.Add($"with {skin} skin");

        if (fields.TryGetValue("hairColor", out var hair))
        {
            if (fields.TryGetValue("hairStyle", out var style))
                parts.Add($"{style} {hair} hair");
            else
                parts.Add($"{hair} hair");
        }

        if (fields.TryGetValue("eyeColor", out var eyes))
            parts.Add($"{eyes} eyes");

        if (fields.TryGetValue("shirtColor", out var shirt))
            parts.Add($"wearing a {shirt} shirt");

        if (fields.TryGetValue("pantsColor", out var pants))
            parts.Add($"and {pants} pants");

        if (fields.TryGetValue("shoeColor", out var shoes))
            parts.Add($"with {shoes} shoes");

        if (fields.TryGetValue("accessory", out var accessory) && !string.IsNullOrWhiteSpace(accessory))
            parts.Add($"with a {accessory}");

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Maps readingLevel to guidance we can inject into prompts.
    /// </summary>
    private static (string VisualMood, string Tone) GetReadingProfile(string? readingLevel)
    {
        // Defaults (covers null/unknown)
        string visualMood = "gentle, friendly, inviting visuals";
        string tone = "warm, comforting, imaginative";

        switch ((readingLevel ?? "").Trim().ToLowerInvariant())
        {
            case "pre":
                visualMood = "very soft, simple, friendly visuals with clear shapes";
                tone = "soothing, rhythmic, very simple concepts";
                break;

            case "early":
                visualMood = "bright, engaging visuals with clear actions and expressions";
                tone = "short, clear, age-appropriate language";
                break;

            case "independent":
                visualMood = "slightly more detailed, adventurous visuals";
                tone = "engaging, varied, age-appropriate language";
                break;

            default:
                break;
        }

        return (visualMood, tone);
    }

    private static string BuildAnimalDescription(Dictionary<string, string> fields)
    {
        var species = fields.TryGetValue("species", out var s) ? s : "animal";
        string color = "";
        if (fields.TryGetValue("bodyColor", out var bodyColor) && fields.TryGetValue("bodyCovering", out var covering))
        {
            color = $"with {bodyColor} {covering}";
        }
        var accessory = fields.TryGetValue("accessory", out var a) ? $"wearing {a}" : "";
        return $"a {species} {color} {accessory}".Trim();
    }

    private static string SummarizeScene(string paragraph)
    {
        paragraph = (paragraph ?? "").ToLower();

        if (paragraph.Contains("owl")) return "talking to a wise owl in a glowing forest";
        if (paragraph.Contains("fireflies")) return "walking through a grove of glowing fireflies";
        if (paragraph.Contains("riddle")) return "solving a riddle under a large tree";
        if (paragraph.Contains("clearing")) return "standing in a forest clearing";
        if (paragraph.Contains("path")) return "walking down a winding forest path";
        if (paragraph.Contains("glow")) return "surrounded by magical glowing plants";

        return "exploring a magical forest";
    }

    public static async Task<string> BuildImagePromptWithSceneAsync(
    List<CharacterSpec> characters,
    string paragraph,
    HttpClient httpClient,
    string apiKey,
    string? artStyleKey)
    {
        // sanitize input a bit (avoid breaking quotes/newlines)
        string ParaClean(string s) =>
            (s ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();

        paragraph = ParaClean(paragraph);

        string anchors = string.Join(" ", characters.Select(GetCharacterAnchor));
        var style = GetArtStyle(artStyleKey);

        var userPrompt = $"""
Summarize the following story paragraph into a short visual scene clause for an illustration.

Requirements:
- Describe only what the characters are doing and what the environment looks like.
- Do NOT mention any subject like "a person", "a child", "the boy", "they", "he", "she", or any character names.
- Begin with a verb or a prepositional phrase, so it can follow "Show the described character".
- Examples: "stepping into a swirling, glowing portal of light..." or
  "in a cozy bedroom filled with toys and books..."

Paragraph: "{paragraph}"
""";

        var requestBody = new
        {
            model = "gpt-4.1-mini",
            messages = new[]
            {
            new
            {
                role = "system",
                content = "You are an assistant generating image prompts for a children's storybook. " +
                          "Never describe the characters' names, appearance, or clothing. " +
                          "Only describe the environment and actions, as a clause that can follow 'Show the described character'."
            },
            new { role = "user", content = userPrompt }
        },
            temperature = 0.3,
            max_tokens = 80
        };

        var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var res = await httpClient.SendAsync(req);

        if (!res.IsSuccessStatusCode)
        {
            var rawErr = await res.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Scene summarizer failed ({(int)res.StatusCode}). Body: {rawErr}");
        }

        using var stream = await res.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(stream);

        var scene = json.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        scene = ParaClean(scene ?? "");

        // Tiny safety net: if the model still gives "A person is ..." or "The child is ...",
        // strip the leading subject phrase and keep the action.
        if (!string.IsNullOrEmpty(scene))
        {
            var lower = scene.ToLowerInvariant();

            // look for patterns like "a person is", "the child is", "they are"
            string[] subjectMarkers = { " a person is ", " the child is ", " the kid is ", " they are ", " he is ", " she is " };

            foreach (var marker in subjectMarkers)
            {
                var idx = lower.IndexOf(marker, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    var after = scene.Substring(idx + marker.Length).TrimStart();
                    if (!string.IsNullOrEmpty(after))
                    {
                        scene = after;
                        break;
                    }
                }
            }
        }

        // Fallback if model returned nothing usable
        if (string.IsNullOrEmpty(scene))
        {
            scene = "stepping into a simple, calm setting where the adventure continues";
        }

        // Final prompt: we control the subject; the scene is just an action/environment clause.
        return $"{style} Show {anchors} {scene}. ";
    }

    // --- Cover prompts -------------------------------------------------------

    /// <summary>
    /// Cover prompt that considers reading level and art style.
    /// Produces a single, true cover composition with no palettes/panels/duplicates.
    /// </summary>
    public static string BuildCoverPrompt(
    List<CharacterSpec> characters,
    string theme,
    string? readingLevel,
    string? artStyleKey)
    {
        string anchors = string.Join(", ", characters.Select(GetCharacterAnchor));
        var (visualMood, tone) = GetReadingProfile(readingLevel);
        var style = GetArtStyle(artStyleKey);

        //const string coverComp =
        //    "Cover page";
        //+
        //" Cover composition: portrait 4:5 or 5:7 aspect with a clear focal subject. " +
        //" Soft, cohesive background; avoid busy layouts. Do not draw any text. " +
        //" Gentle, even lighting; keep the scene readable at thumbnail size. ";

        //const string negatives =
        //    " ";
        //" Depict the group once as a single scene. Do not repeat or mirror any character. ";

        return $"""
Depict {anchors} in a setting inspired by {theme} in a {style}
""";
        // Show only a single scene with cohesive lighting and background.
    }

    /// <summary>
    /// Overload without artStyleKey (defaults to watercolor style).
    /// </summary>
    public static string BuildCoverPrompt(
        List<CharacterSpec> characters,
        string theme,
        string? readingLevel)
    {
        return BuildCoverPrompt(characters, theme, readingLevel, null);
    }
}