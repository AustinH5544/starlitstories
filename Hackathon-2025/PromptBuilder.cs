using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using Hackathon_2025.Models;
using System.Linq;

namespace Hackathon_2025.Services;

public static class PromptBuilder
{
    private static readonly string[] AccessoryFieldNames = new[]
    {
        "accessory",
        "accessory2",
        "accessory3",
        "accessory4",
        "accessory5"
    };

    // --- Descriptor lookup tables -------------------------------------------

    private static readonly Dictionary<string, string> SkinToneMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["almond"]   = "almond-toned skin",
        ["amber"]    = "amber-toned skin",
        ["ashen"]    = "soft ashen skin",
        ["blush"]    = "soft blush-toned skin",
        ["bronze"]   = "bronze-toned skin",
        ["pale"]     = "very pale ivory skin",
        ["light"]    = "fair light skin",
        ["freckled"] = "light freckled skin",
        ["olive"]    = "olive-toned skin",
        ["tan"]      = "warm tan skin",
        ["caramel"]  = "caramel-toned skin",
        ["brown"]    = "medium brown skin",
        ["dark"]     = "deep dark brown skin",
        ["ebony"]    = "rich ebony-toned skin",
        ["porcelain"] = "porcelain skin",
        ["rose"]     = "rose-toned skin",
        ["sun-kissed"] = "sun-kissed skin",
        ["warm beige"] = "warm beige-toned skin",
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
        ["cream"]  = "cream",
        ["cyan"]   = "cyan",
        ["charcoal"] = "charcoal",
        ["lavender"] = "lavender",
        ["maroon"] = "maroon",
        ["navy"]   = "navy",
        ["olive"]  = "olive",
        ["peach"]  = "peach",
        ["teal"]   = "teal",
        ["blush"]  = "blush",
        ["tan"]    = "tan",
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

    private static bool OnePieceAllowsTopLayer(string? onePieceWear)
    {
        if (string.IsNullOrWhiteSpace(onePieceWear)) return false;
        var normalized = onePieceWear.Trim().ToLowerInvariant();
        return normalized == "overalls" || normalized.Contains("overall");
    }


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
            "gouache" => "Children's gouache painting. Rich matte brush textures, layered color fields, storybook warmth and depth." + guardrails,
            "pastel" => "Children's soft pastel illustration. Chalky blended textures, velvety edges, dreamy gentle color transitions." + guardrails,
            "lineart" => "Children's clean line art illustration. Crisp expressive outlines, simple modern shapes, bright balanced flat colors." + guardrails,
            "clay" => "Children's clay animation style illustration. Handmade clay-like characters, tactile sculpted textures, playful studio lighting." + guardrails,
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

        if (fields.TryGetValue("ethnicity", out var ethnicity) && !string.IsNullOrWhiteSpace(ethnicity))
            parts.Add(ethnicity);
        else if (fields.TryGetValue("race", out var race) && !string.IsNullOrWhiteSpace(race))
            parts.Add(race);

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

        var hasOnePiece = fields.TryGetValue("onePieceWear", out var onePieceWear) && !string.IsNullOrWhiteSpace(onePieceWear);
        if (hasOnePiece)
        {
            if (fields.TryGetValue("onePieceColor", out var onePieceColor) && !string.IsNullOrWhiteSpace(onePieceColor))
                parts.Add($"{Map(ClothingColorMap, onePieceColor)} {onePieceWear}");
            else
                parts.Add(onePieceWear!);

            if (OnePieceAllowsTopLayer(onePieceWear))
            {
                var hasTopWear = fields.TryGetValue("topWear", out var layeredTopWear) && !string.IsNullOrWhiteSpace(layeredTopWear);
                if (hasTopWear)
                {
                    if (fields.TryGetValue("topWearColor", out var layeredTopColor) && !string.IsNullOrWhiteSpace(layeredTopColor))
                        parts.Add($"{Map(ClothingColorMap, layeredTopColor)} {layeredTopWear}");
                    else
                        parts.Add(layeredTopWear!);
                }
                else if (fields.TryGetValue("shirtColor", out var shirt) && !string.IsNullOrWhiteSpace(shirt))
                {
                    parts.Add($"{Map(ClothingColorMap, shirt)} shirt");
                }
            }
        }
        else
        {
            var hasTopWear = fields.TryGetValue("topWear", out var topWear) && !string.IsNullOrWhiteSpace(topWear);
            var hasBottomWear = fields.TryGetValue("bottomWear", out var bottomWear) && !string.IsNullOrWhiteSpace(bottomWear);

            if (hasTopWear)
            {
                if (fields.TryGetValue("topWearColor", out var topWearColor) && !string.IsNullOrWhiteSpace(topWearColor))
                    parts.Add($"{Map(ClothingColorMap, topWearColor)} {topWear}");
                else
                    parts.Add(topWear!);
            }
            else if (fields.TryGetValue("shirtColor", out var shirt) && !string.IsNullOrWhiteSpace(shirt))
            {
                parts.Add($"{Map(ClothingColorMap, shirt)} shirt");
            }

            if (hasBottomWear)
            {
                if (fields.TryGetValue("bottomWearColor", out var bottomWearColor) && !string.IsNullOrWhiteSpace(bottomWearColor))
                    parts.Add($"{Map(ClothingColorMap, bottomWearColor)} {bottomWear}");
                else
                    parts.Add(bottomWear!);
            }
            else if (fields.TryGetValue("pantsColor", out var pants) && !string.IsNullOrWhiteSpace(pants))
            {
                parts.Add($"{Map(ClothingColorMap, pants)} pants");
            }
        }

        var hasShoeStyle = fields.TryGetValue("shoeStyle", out var shoeStyle) && !string.IsNullOrWhiteSpace(shoeStyle);
        var hasShoeColor = fields.TryGetValue("shoeColor", out var shoeColor) && !string.IsNullOrWhiteSpace(shoeColor);
        if (hasShoeStyle && hasShoeColor)
            parts.Add($"{Map(ClothingColorMap, shoeColor)} {shoeStyle}");
        else if (hasShoeStyle)
            parts.Add(shoeStyle!);
        else if (hasShoeColor)
            parts.Add($"{Map(ClothingColorMap, shoeColor)} shoes");

        parts.AddRange(GetAccessories(fields));

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

        parts.AddRange(GetAccessories(fields));

        return string.Join(", ", parts);
    }

    private static IEnumerable<string> GetAccessories(IReadOnlyDictionary<string, string> fields) =>
        AccessoryFieldNames
            .Select(fieldName => fields.TryGetValue(fieldName, out var value) ? value : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))!;

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

    // --- Base character prompt -----------------------------------------------

    /// <summary>
    /// Generates a neutral full-body character portrait used as the shared reference
    /// image for all parallel story/cover image edits.
    /// </summary>
    public static string BuildBaseCharacterPrompt(List<CharacterSpec> characters, string? artStyle)
    {
        string anchor = string.Join(" and ", characters.Select(GetCharacterAnchor));
        var style = GetArtStyle(artStyle);
        return $"{style} Full-body character portrait, plain white background, " +
               $"centered composition, no scene, no props, no text. Character: {anchor}.";
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

    /// <summary>
    /// Story-aware cover prompt: asks the LLM to pick the most iconic or visually
    /// exciting moment from the full story and describe it as a cover scene.
    /// Falls back to the static BuildCoverPrompt on failure.
    /// </summary>
    public static async Task<string> BuildCoverPromptAsync(
        List<CharacterSpec> characters,
        string theme,
        string? readingLevel,
        string? artStyleKey,
        string[] storyParagraphs,
        HttpClient httpClient,
        string apiKey)
    {
        const int maxAttempts = 2;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await BuildCoverPromptWithSceneAsync(
                    characters, theme, artStyleKey, storyParagraphs, httpClient, apiKey);
            }
            catch when (attempt < maxAttempts)
            {
                await Task.Delay(300 * attempt);
            }
        }

        // Fallback: use the static prompt if the AI call keeps failing
        return BuildCoverPrompt(characters, theme, readingLevel, artStyleKey);
    }

    private static async Task<string> BuildCoverPromptWithSceneAsync(
        List<CharacterSpec> characters,
        string theme,
        string? artStyleKey,
        string[] storyParagraphs,
        HttpClient httpClient,
        string apiKey)
    {
        var style = GetArtStyle(artStyleKey);

        // Join all pages into one block (cap at ~1200 chars to stay within token budget)
        var fullStory = string.Join(" | ", storyParagraphs
            .Select(p => (p ?? "").Replace("\r", " ").Replace("\n", " ").Trim())
            .Where(p => p.Length > 0));
        if (fullStory.Length > 1200)
            fullStory = fullStory[..1200];

        var userPrompt = $"""
Read the following children's story and identify the single most visually exciting or
iconic moment — the one scene that would make the best book cover illustration.

Describe it as a short scene clause (10–20 words) that can follow "Show the described character".
- Do NOT mention character names, appearance, or clothing.
- Begin with a verb or preposition: e.g. "soaring above glowing crystal mountains at sunset" or
  "facing a giant friendly dragon in a sparkling meadow".
- Capture the story's mood, setting, and peak moment of magic or adventure.

Story: "{fullStory}"
""";

        var requestBody = new
        {
            model = "gpt-4.1-mini",
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = "You are an assistant generating book cover image prompts for a children's storybook. " +
                              "Given a full story, pick the single most visually compelling scene and describe it as a " +
                              "short clause. Never mention character names, appearance, or clothing."
                },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.4,
            max_tokens = 60
        };

        var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        req.Content = new StringContent(
            JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json");

        var res = await httpClient.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            var rawErr = await res.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Cover scene summarizer failed ({(int)res.StatusCode}). Body: {rawErr}");
        }

        using var stream = await res.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(stream);

        var scene = json.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        scene = (scene ?? "").Replace("\r", " ").Replace("\n", " ").Trim();

        if (string.IsNullOrEmpty(scene))
            scene = $"in a rich, atmospheric setting inspired by {theme}, with magical detail";

        return $"{style} {scene}. Book cover composition — no text, no panels.";
    }
}
