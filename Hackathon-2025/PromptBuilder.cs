using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using Hackathon_2025.Models;
using System.Linq;

namespace Hackathon_2025.Services;

public static class PromptBuilder
{
    // --- Descriptor lookup tables -------------------------------------------

    private static readonly Dictionary<string, string> SkinToneMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pale"]     = "very pale ivory skin",
        ["light"]    = "fair light skin",
        ["freckled"] = "light freckled skin",
        ["olive"]    = "olive-toned skin",
        ["tan"]      = "warm tan skin",
        ["brown"]    = "medium brown skin",
        ["dark"]     = "deep dark brown skin",
    };

    private static readonly Dictionary<string, string> HairColorMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["blonde"]       = "golden blonde",
        ["dirty blonde"] = "dirty golden blonde",
        ["auburn"]       = "auburn red-brown",
        ["red"]          = "bright natural red",
        ["brown"]        = "chestnut brown",
        ["light brown"]  = "warm light brown",
        ["dark brown"]   = "deep dark brown",
        ["black"]        = "jet black",
        ["gray"]         = "silver gray",
        ["white"]        = "snow white",
        ["blue"]         = "vivid cobalt blue",
        ["green"]        = "vivid emerald green",
        ["pink"]         = "vivid hot pink",
        ["purple"]       = "vivid violet purple",
    };

    private static readonly Dictionary<string, string> EyeColorMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["blue"]   = "bright blue",
        ["brown"]  = "warm brown",
        ["green"]  = "vivid green",
        ["hazel"]  = "hazel (green-brown)",
        ["gray"]   = "cool gray",
        ["amber"]  = "golden amber",
        ["violet"] = "deep violet",
    };

    private static readonly Dictionary<string, string> ClothingColorMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["red"]    = "red",
        ["blue"]   = "blue",
        ["green"]  = "green",
        ["yellow"] = "yellow",
        ["orange"] = "orange",
        ["pink"]   = "pink",
        ["purple"] = "purple",
        ["white"]  = "white",
        ["black"]  = "black",
        ["brown"]  = "brown",
        ["gray"]   = "gray",
        ["khaki"]  = "khaki",
        ["gold"]   = "gold",
        ["silver"] = "silver",
    };

    private static readonly Dictionary<string, string> AnimalColorMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"]   = "jet black",
        ["white"]   = "snow white",
        ["brown"]   = "warm brown",
        ["gray"]    = "silver gray",
        ["orange"]  = "vivid orange",
        ["red"]     = "bright red",
        ["blue"]    = "vivid blue",
        ["green"]   = "vivid green",
        ["pink"]    = "bubblegum pink",
        ["golden"]  = "shiny golden",
        ["glowing"] = "glowing luminescent",
        ["spotted"] = "spotted with mixed patches",
        ["striped"] = "striped with alternating bands",
    };

    private static string Map(Dictionary<string, string> map, string? value)
        => (!string.IsNullOrWhiteSpace(value) && map.TryGetValue(value, out var v)) ? v : (value ?? "");


    // Base watercolor style with strong "cover-safe" guardrails.
    private static string GetDefaultArtStyle() =>
        "Children's watercolor illustration. Soft muted pastel palette, warm harmonious tones, gentle hand-painted look. ";

    //+
    //"Portrait orientation, single full-bleed image. " +
    //"Soft lighting, gentle pastel tones, hand-painted look, minimal outlines. " +
    //"Show exactly the listed characters once each (no clones or duplicates). ";

    public static string BuildImagePrompt(List<CharacterSpec> characters, string paragraph, string? artStyleKey)
    {
        string anchors = string.Join(" and ", characters.Select(GetCharacterAnchor));
        string scene = SummarizeScene(paragraph);
        var style = GetArtStyle(artStyleKey);
        return $"{style} Character: {anchors}. Setting: {scene}.";
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
        string anchors = string.Join(" and ", characters.Select(GetCharacterAnchor));
        string scene = SummarizeScene(paragraph);
        var (visualMood, tone) = GetReadingProfile(readingLevel);
        var style = GetArtStyle(artStyleKey);

        return $"{style} Use {visualMood}. Keep the tone {tone}. Character: {anchors}. Setting: {scene}.";
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
            "comic" => "Children's comic book illustration. Bold clean outlines, flat consistent color palette, bright but not neon hues." + guardrails,
            "crayon" => "Children's crayon drawing. Waxy textured strokes, child-like proportions, soft earthy color palette." + guardrails,
            "papercut" => "Children's paper cutout collage. Flat layered shapes, soft drop shadows, limited warm pastel palette, no outlines." + guardrails,
            "toy3d" => "Children's 3D toy render. Soft warm overhead lighting, smooth plastic and plush textures, clean soft pastel color palette." + guardrails,
            "pixel" => "Children's pixel art. 16-bit retro style, limited consistent 16-color palette, clear readable silhouettes." + guardrails,
            "inkwash" => "Children's ink and wash illustration. Minimal flowing brushstrokes, soft translucent washes, calm desaturated palette." + guardrails,
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
        var style = GetArtStyle(artStyleKey);
        var scene = string.IsNullOrWhiteSpace(paragraph)
            ? "posing for a simple portrait in a calm setting"
            : SummarizeScene(paragraph);

        return $"{style} {scene}.";
    }

    private static string CleanForModel(string s, int maxLen = 800)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length > maxLen ? s[..maxLen] : s;
    }

    private static string GetCharacterAnchor(CharacterSpec character)
    {
        return character.IsAnimal
            ? BuildAnimalDescription(character.DescriptionFields)
            : BuildHumanDescription(character.DescriptionFields);
    }

    private static string BuildHumanDescription(Dictionary<string, string> fields)
    {
        var parts = new List<string>();

        fields.TryGetValue("age", out var age);
        fields.TryGetValue("gender", out var gender);
        if (!string.IsNullOrWhiteSpace(age) || !string.IsNullOrWhiteSpace(gender))
            parts.Add($"{age}-year-old {gender}".Trim(' ', '-'));

        if (fields.TryGetValue("skinTone", out var skin) && !string.IsNullOrWhiteSpace(skin))
            parts.Add($"{Map(SkinToneMap, skin)} skin");

        if (fields.TryGetValue("hairColor", out var hair) && !string.IsNullOrWhiteSpace(hair))
        {
            var mappedHair = Map(HairColorMap, hair);
            parts.Add(fields.TryGetValue("hairStyle", out var hairStyle) && !string.IsNullOrWhiteSpace(hairStyle)
                ? $"{mappedHair} {hairStyle}"
                : mappedHair);
        }

        if (fields.TryGetValue("eyeColor", out var eyes) && !string.IsNullOrWhiteSpace(eyes))
            parts.Add($"{Map(EyeColorMap, eyes)} eyes");

        if (fields.TryGetValue("shirtColor", out var shirt) && !string.IsNullOrWhiteSpace(shirt))
            parts.Add($"{Map(ClothingColorMap, shirt)} shirt");

        if (fields.TryGetValue("pantsColor", out var pants) && !string.IsNullOrWhiteSpace(pants))
            parts.Add($"{Map(ClothingColorMap, pants)} pants");

        if (fields.TryGetValue("shoeColor", out var shoes) && !string.IsNullOrWhiteSpace(shoes))
            parts.Add($"{Map(ClothingColorMap, shoes)} shoes");

        if (fields.TryGetValue("accessory", out var accessory) && !string.IsNullOrWhiteSpace(accessory))
            parts.Add(accessory);

        return string.Join(", ", parts);
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
        var parts = new List<string>();

        parts.Add(fields.TryGetValue("species", out var species) && !string.IsNullOrWhiteSpace(species)
            ? species : "animal");

        if (fields.TryGetValue("bodyColor", out var bodyColor) && !string.IsNullOrWhiteSpace(bodyColor))
        {
            var mappedColor = Map(AnimalColorMap, bodyColor);
            parts.Add(fields.TryGetValue("bodyCovering", out var covering) && !string.IsNullOrWhiteSpace(covering)
                ? $"{mappedColor} {covering}"
                : mappedColor);
        }

        if (fields.TryGetValue("accessory", out var accessory) && !string.IsNullOrWhiteSpace(accessory))
            parts.Add(accessory);

        return string.Join(", ", parts);
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

        string anchors = string.Join(" and ", characters.Select(GetCharacterAnchor));
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

        // Final prompt: character appearance comes from the reference image, not the text.
        return $"{style} {scene}.";
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
        string anchors = string.Join(" and ", characters.Select(GetCharacterAnchor));
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

        return $"{style} Character: {anchors}. Standing confidently in a centered portrait pose, facing the viewer with a warm expression. The background is a rich, atmospheric setting inspired by {theme}, with soft depth and magical detail. Book cover composition — no text, no panels.";
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