using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;
using Hackathon_2025.Models;

namespace Hackathon_2025.Services;

public class StoryGenerator : IStoryGeneratorService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly IImageGeneratorService _imageService;
    private readonly BlobUploadService _blobUploader;
    private readonly ILogger<StoryGenerator> _logger;
    private readonly IConfiguration _config;

    public StoryGenerator(
    HttpClient httpClient,
    IConfiguration config,
    IImageGeneratorService imageService,
    BlobUploadService blobUploader,
    ILogger<StoryGenerator> logger)
    {
        _httpClient = httpClient;
        _apiKey = config["OpenAI:ApiKey"]!;
        _imageService = imageService;
        _blobUploader = blobUploader;
        _logger = logger;
        _config = config;
    }

    public async Task<StoryResult> GenerateFullStoryAsync(StoryRequest request)
    {
        var characterList = string.Join(", ", request.Characters.Select(c =>
            c.Role?.ToLowerInvariant() == "main" ? c.Name : $"{c.Name} the {c.Role}"
        ));

        var style = BuildReadingStyle(request.ReadingLevel);

        // SINGLE SOURCE OF TRUTH: page count from config only
        int pageCount = _config.GetValue<int?>("Story:DefaultParagraphs") ?? 10;
        var mustLesson = !string.IsNullOrWhiteSpace(request.LessonLearned);

        var systemContent =
        "You are a creative children's story writer. " +
        "Never describe physical appearance. " +
        "Use only the provided character names and roles; do not invent other named characters. " +
        "If a lesson is provided, weave it naturally into the plot and always conclude with a final line that begins with 'Lesson:'.";

        // Enforce the explicit page delimiter
        const string DelimiterRule =
            "Separate each page with a line containing only --- (three hyphens). " +
            "Never include --- inside a paragraph. Do not number the pages.";

        // Prevent headings or Page labels
        var formatRule = "Write plain paragraphs only. Do NOT add any headings, numbers, or 'Page' labels.";

        // Exact length line
        string lengthLine = $@"
Return exactly {pageCount} short paragraphs, one per page.
Each page MUST contain 1–3 sentences of story text.
Do not leave any page blank. Do not include bullets, numbers, or page labels.
Use '---' between pages. Do not return code fences or markdown.";

        // Lesson line (exact branch)
        var lessonLine = mustLesson
            ? $"""
Ensure the story naturally reflects this moral: "{request.LessonLearned}".
After you finish the {pageCount} paragraphs, write one extra line that begins with "Lesson:" and states this moral in simple words. Do not count this line as a paragraph.
"""
            : "If a gentle lesson emerges naturally, keep it subtle.";

        string BuildRosterLine()
        {
            var cast = request.Characters.Select(c =>
            {
                var role = string.IsNullOrWhiteSpace(c.Role) ? "character" : c.Role;
                // If your Character model has IsAnimal/species fields, include them; otherwise just name + role.
                string species = "";
                try
                {
                    // If your model exposes description fields with a "species" key:
                    var dict = (IDictionary<string, object>?)c.DescriptionFields;
                    if (dict != null && dict.TryGetValue("species", out var s) && s is string sv && !string.IsNullOrWhiteSpace(sv))
                        species = $", {sv}";
                }
                catch { /* no-op if not present */ }

                return $"{c.Name} ({role}{species})";
            });

            return "Use only these characters by name throughout the story: " +
                   string.Join(", ", cast) +
                   ". Do not introduce new named characters; unnamed extras are okay.";
        }
        var rosterLine = BuildRosterLine();

        var prompt = $"""
{lengthLine}
They go on an adventure involving {request.Theme}. The reader already knows what the characters look like,
so do not describe their appearance, clothing, or physical features.

CAST:
{rosterLine}

Rules:
- Refer to the characters by the exact names above and keep their roles consistent.
- Do not invent new named characters.
- On page 1, introduce the main character by name in the first sentence.
- Keep each page to 1–3 sentences.

{style.AudienceLine}
Use a {style.Tone} tone. Prefer {style.SentenceRule}
Use {style.VocabHint}.
{formatRule}
{DelimiterRule}
{lessonLine}

Structure the story with:
- A clear beginning that introduces the setting and the characters naturally
- A middle that includes a magical or challenging journey
- A satisfying end that resolves the conflict

Put a line containing only --- between paragraphs. Do not include any other dividers.
""";


        _logger?.LogInformation("=== STORY PROMPT ===\n{Prompt}\n====================", prompt);

        // Token budget for exact count
        var maxTokens = Math.Min(1800, pageCount * 110 + 300);

        var requestBody = new
        {
            model = "gpt-3.5-turbo",
            messages = new[]
            {
            new { role = "system", content = systemContent },
            new { role = "user",   content = prompt }
        },
            temperature = 0.6,
            max_tokens = maxTokens
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var storyText = (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("choices")[0]
            .GetProperty("message").GetProperty("content").GetString() ?? "";

        // ---- Sanitize output ----
        storyText = Regex.Replace(storyText, @"(?m)^\s*```.*$", "");                    // code fences
        storyText = Regex.Replace(storyText, @"(?mi)^\s*Page\s*\d+\s*$", "");           // "Page N" lines
        storyText = Regex.Replace(storyText, @"[ \t]+$", "", RegexOptions.Multiline);   // trailing spaces
        storyText = Regex.Replace(storyText, @"\n{3,}", "\n\n").Trim();                 // collapse blanks

        // Normalize line endings
        storyText = storyText.Replace("\r\n", "\n").Replace("\r", "\n");

        // ---- Extract (and strip) Lesson line, if present ----
        string? extractedLesson = null;
        var lessonMatch = Regex.Match(storyText, @"(?mi)^\s*Lesson:\s*(.+)\s*$");
        if (lessonMatch.Success)
        {
            extractedLesson = lessonMatch.Groups[1].Value.Trim();
            storyText = Regex.Replace(storyText, @"(?mi)^\s*Lesson:.*$", "").Trim();
        }
        if (mustLesson && string.IsNullOrWhiteSpace(extractedLesson))
            extractedLesson = request.LessonLearned?.Trim();

        // --- Split into pages ---
        static string[] SplitIntoPages(string? text)
        {
            var t = (text ?? string.Empty).Trim();
            if (t.Length == 0) return Array.Empty<string>();

            // 1) Preferred: explicit delimiter lines containing only ---
            var explicitParts = Regex
                .Split(t, @"^\s*---\s*$", RegexOptions.Multiline)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToArray();
            if (explicitParts.Length > 1)
                return explicitParts;

            // 1b) Inline delimiters: " ... --- ... "
            var inlineParts = Regex
                .Split(t, @"\s+---\s+")
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToArray();
            if (inlineParts.Length > 1)
                return inlineParts;

            // 2) Fallbacks: blank lines OR single newline after sentence end before a capitalized start
            var fallbackParts = Regex
                .Split(t, @"\r?\n\s*\r?\n+|(?<=\S[.!?])\s*\r?\n(?=\p{Lu})")
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToArray();

            return fallbackParts;
        }

        var parts = SplitIntoPages(storyText).ToList();

        // Guarantee exactly N non-empty paragraphs (server-only control)
        parts = EnsureExactlyN(parts, pageCount);

        var paragraphs = parts.ToArray();

        _logger?.LogInformation("Story paragraphs: got={Got}, target={Target}", paragraphs.Length, pageCount);

        // Use a copy WITHOUT the Lesson line for image prompts
        var paragraphsForImages = paragraphs.ToArray();

        // Append the Lesson as an extra line to the last page text only (no image comes from it)
        if (!string.IsNullOrWhiteSpace(extractedLesson) && paragraphs.Length > 0)
            paragraphs[^1] = paragraphs[^1].TrimEnd() + "\n\nLesson: " + extractedLesson;

        // Image prompts strictly from scene paragraphs (no "Lesson:" line)
        var imagePromptTasks = paragraphsForImages.Select(p =>
            PromptBuilder.BuildImagePromptAsync(request.Characters, p, _httpClient, _apiKey, request.ArtStyle));
        var imagePrompts = await Task.WhenAll(imagePromptTasks);

        _logger?.LogInformation("Image prompts generated: count={Count}", imagePrompts.Length);

        // Assemble pages (images match scene paragraphs count)
        var storyPages = new List<StoryPage>();
        for (int i = 0; i < paragraphs.Length; i++)
        {
            storyPages.Add(new StoryPage
            {
                Text = paragraphs[i],
                ImagePrompt = imagePrompts[i]
            });
        }

        var externalImageUrls = await _imageService.GenerateImagesAsync(imagePrompts.ToList());

        var title = await GenerateTitleAsync(request.Characters.FirstOrDefault()?.Name ?? "A Hero", request.Theme);

        var coverPrompt = PromptBuilder.BuildCoverPrompt(
            request.Characters, request.Theme, request.ReadingLevel, request.ArtStyle);

        var coverExternalUrl = (await _imageService.GenerateImagesAsync(new List<string> { coverPrompt }))[0];

        return new StoryResult
        {
            Title = title,
            CoverImagePrompt = coverPrompt,
            CoverImageUrl = coverExternalUrl,
            Pages = storyPages.Select((p, i) =>
            {
                p.ImageUrl = externalImageUrls[i];
                return p;
            }).ToList()
        };
    }

    // Split sentences on end punctuation followed by a capital letter (simple heuristic).
    private static readonly Regex SentenceSplit =
        new(@"(?<=[.!?])\s+(?=\p{Lu})", RegexOptions.Multiline);

    // Ensure exactly N non-empty paragraphs by splitting long ones or merging short ones.
    private static List<string> EnsureExactlyN(List<string> parts, int target)
    {
        // 0) Normalize: trim and drop empties
        parts = parts.Select(p => (p ?? string.Empty).Trim())
                     .Where(p => p.Length > 0)
                     .ToList();

        // Edge case: nothing at all -> seed a stub
        if (parts.Count == 0)
            parts.Add("The adventure begins with a vivid moment in the story.");

        // 1) If too few, split the longest paragraphs by sentences until we reach target
        while (parts.Count < target)
        {
            // pick the longest paragraph
            int idx = Enumerable.Range(0, parts.Count)
                                .OrderByDescending(i => parts[i].Length)
                                .First();

            var segs = SentenceSplit.Split(parts[idx])
                                    .Where(s => !string.IsNullOrWhiteSpace(s))
                                    .ToArray();

            if (segs.Length >= 2)
            {
                int half = (int)Math.Ceiling(segs.Length / 2.0);
                var left = string.Join(" ", segs.Take(half)).Trim();
                var right = string.Join(" ", segs.Skip(half)).Trim();

                parts[idx] = left.Length > 0 ? left : parts[idx];
                if (right.Length > 0) parts.Insert(idx + 1, right);
            }
            else
            {
                // fallback: split by midpoint if we can't find sentence boundaries
                var text = parts[idx];
                if (text.Length < 80)
                {
                    // very short; insert a safe continuation to avoid blanks
                    parts.Insert(idx + 1, "The adventure continues with a vivid moment in the story.");
                }
                else
                {
                    int mid = text.Length / 2;
                    var left = text.Substring(0, mid).Trim();
                    var right = text.Substring(mid).Trim();
                    parts[idx] = left.Length > 0 ? left : text;
                    if (right.Length > 0) parts.Insert(idx + 1, right);
                }
            }
        }

        // 2) If too many, merge the shortest neighbor pairs until we reach target
        while (parts.Count > target)
        {
            int idx = Enumerable.Range(0, parts.Count).OrderBy(i => parts[i].Length).First();

            int neighbor = (idx == parts.Count - 1) ? idx - 1
                        : (idx == 0) ? 1
                        : (parts[idx - 1].Length <= parts[idx + 1].Length ? idx - 1 : idx + 1);

            int a = Math.Min(idx, neighbor);
            int b = Math.Max(idx, neighbor);

            parts[a] = (parts[a] + " " + parts[b]).Trim();
            parts.RemoveAt(b);
        }

        // 3) Final safety: replace any accidental empties
        for (int i = 0; i < parts.Count; i++)
            if (string.IsNullOrWhiteSpace(parts[i]))
                parts[i] = "The adventure continues with a vivid moment in the story.";

        return parts;
    }

    private async Task<string> GenerateTitleAsync(string characterName, string theme)
    {
        var prompt = $"""
Suggest a creative and whimsical children's book title based on a character named {characterName}
and a theme of "{theme}". Return ONLY the title text — no quotes, no punctuation at the ends,
no extra words, no Markdown.
""";

        var requestBody = new
        {
            model = "gpt-3.5-turbo",
            messages = new[] { new { role = "user", content = prompt } },
            temperature = 0.9,
            max_tokens = 20
        };

        var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var res = await _httpClient.SendAsync(req);
        res.EnsureSuccessStatusCode();

        var json = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync());
        var raw = json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";

        // strip code fences, newlines, and surrounding quotes/backticks/smart quotes
        var cleaned = Regex.Replace(raw, @"(?m)^\s*```.*$|^\s*>.*$", "");
        cleaned = cleaned.Replace("\r", "").Replace("\n", " ").Trim();

        // remove surrounding quotes/backticks (straight or smart) and trailing period
        cleaned = Regex.Replace(cleaned, @"^[\s""'“”‘’`]+", "");
        cleaned = Regex.Replace(cleaned, @"[\s""'“”‘’`]+$", "");
        cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim();

        return cleaned;
    }

    // --- Simplified: map reading level to text style guidance (no readerAge)
    private static ReadingStyle BuildReadingStyle(string? readingLevel)
    {
        // Defaults
        var tone = "warm and imaginative";
        var sentenceRule = "short sentences (about 10 words on average)";
        var vocabHint = "simple, common words and clear phrasing";
        var audienceLine = "Aim for the selected reading level.";

        switch ((readingLevel ?? "").Trim().ToLowerInvariant())
        {
            case "pre":
                tone = "soothing, playful, and rhythmic";
                sentenceRule = "very short sentences (around 6–8 words)";
                vocabHint = "very common sight words and familiar phrases";
                break;

            case "early":
                tone = "friendly and engaging";
                sentenceRule = "short sentences (about 8–12 words)";
                vocabHint = "age-appropriate vocabulary with simple connectors";
                break;

            case "independent":
                tone = "adventurous and lively";
                sentenceRule = "mostly short sentences with occasional longer ones (10–14 words)";
                vocabHint = "age-appropriate vocabulary with a few vivid verbs";
                break;
        }

        return new ReadingStyle(tone, sentenceRule, vocabHint, audienceLine);
    }

    private readonly record struct ReadingStyle(
        string Tone,
        string SentenceRule,
        string VocabHint,
        string AudienceLine
    );
}
