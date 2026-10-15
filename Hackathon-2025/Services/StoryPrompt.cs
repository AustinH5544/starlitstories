using System.Text.RegularExpressions;
using Hackathon_2025.Models;

namespace Hackathon_2025.Services;

/// <summary>
/// Builds the system and user prompts for the story text. Kept separate from StoryGenerator
/// so the exact wording can be tested without calling OpenAI.
/// </summary>
public static class StoryPrompt
{
    // Only real-world risks get safety handling; imaginative adventures are left alone so the writer
    // doesn't drop "ask an adult" reminders into scenes where no adult could be.
    public const string ChildSafetyRule =
        "Keep children safe in the story: never show risky or secretive real-world behavior (like running into traffic, going off with strangers, " +
        "playing with fire, or going near deep water alone) as fun or okay. Imaginative adventures are fine as they are: don't add reminders about " +
        "staying safe or asking adults unless the story actually involves one of those real risks. If a grown-up helps, it is one the child knows " +
        "(like a parent or teacher) who is actually there in the scene.";

    private const string DelimiterRule =
        "Separate each page with a line containing only --- (three hyphens). " +
        "Never include --- inside a paragraph. Do not number the pages.";

    private const string FormatRule = "Write plain paragraphs only. Do NOT add any headings, numbers, or 'Page' labels.";

    public static string BuildSystemPrompt(bool hasLesson)
    {
        var common =
            "You are a creative children's story writer. " +
            "Never describe physical appearance. " +
            "Use only the provided character names and roles; do not invent other named characters. " +
            ChildSafetyRule + " " +
            "Every story has a clear problem that the main character solves through their own choices, and each event follows from the one before. ";

        return common + (hasLesson
            ? "Build the plot around the lesson: the story's problem comes from it, and the main character learns it through their own choices. " +
              "Always conclude with a final line that begins with 'Lesson:'."
            : "If any moral or takeaway emerges, keep it subtle and do NOT add any explicit line that starts with 'Lesson:'.");
    }

    public static string BuildUserPrompt(StoryRequest request, IReadOnlyList<CharacterSpec> characters, int pageCount)
    {
        var style = BuildReadingStyle(request.ReadingLevel);
        var lesson = request.LessonLearned?.Trim();
        var hasLesson = !string.IsNullOrWhiteSpace(lesson);

        var hasMultipleCharacters = characters.Count > 1;
        var firstName = characters.FirstOrDefault()?.Name?.Trim();
        var hero = string.IsNullOrWhiteSpace(firstName) ? "the main character" : firstName;

        var lengthLine = $@"
Return exactly {pageCount} short paragraphs, one per page.
Each page MUST contain 1–3 sentences of story text.
Do not leave any page blank. Do not include bullets, numbers, or page labels.
Use '---' between pages. Do not return code fences or markdown.";

        var referLine = hasMultipleCharacters
            ? "- Refer to the characters by the exact names above and keep their relationships consistent."
            : "- Refer to the character by the exact name above.";

        string introLine;
        if (!string.IsNullOrWhiteSpace(firstName))
        {
            introLine = hasMultipleCharacters
                ? $"- On page 1, introduce {firstName} by name in the first sentence as the main character."
                : $"- On page 1, introduce {firstName} by name in the first sentence.";
        }
        else
        {
            introLine = hasMultipleCharacters
                ? "- On page 1, introduce the main character by name in the first sentence."
                : "- On page 1, introduce the character by name in the first sentence.";
        }

        // Facts the text must respect so it matches the pictures (e.g. a hero who uses a wheelchair).
        // Empty for most stories, in which case the prompt is unchanged.
        var characterNotes = PromptBuilder.BuildStoryCharacterNotes(characters);
        var characterNotesLines = characterNotes.Count > 0
            ? "\n" + string.Join("\n", characterNotes.Select(note => "- " + note))
            : "";

        var lessonBlock = hasLesson
            ? $"""
LESSON: "{lesson}"
Build the story around this lesson; don't just mention it at the end.
- Fit the lesson into the world of {request.Theme}. If it is an everyday habit or safety rule, find the version of it that belongs in this setting (for example, a space crew brushing their teeth before a long flight).
- The problem (beat 3) comes from the lesson: {hero} faces a choice about it and struggles or gets it wrong at first (beats 4-5).
- {hero} learns the lesson from what happens and their own choices (beat 6), not because someone lectures them.
- Acting on the lesson is what solves the problem (beat 7).
- For safety lessons, {hero} never actually does the risky thing. The struggle is wanting to or almost doing it; {hero} notices the risk in time and makes the safe choice. No one gets hurt, and the risky choice is never shown as fun. If a grown-up is part of the safe choice, it is a parent or family member who came along, mentioned early in the story.
- Keep it gentle: mistakes are okay, and no one is shamed or scared.
After you finish the {pageCount} paragraphs, write one extra line that begins with "Lesson:" and states this lesson in simple words. Do not count this line as a paragraph.
"""
            : "If a gentle lesson emerges naturally, keep it subtle. Do not add any line that begins with \"Lesson:\".";

        return $"""
{lengthLine}
They go on an adventure involving {request.Theme}. The reader already knows what the characters look like,
so do not describe their appearance, clothing, or physical features.

CAST:
{BuildRosterLine(characters)}

Rules:
{referLine}
- Do not invent new named characters.
{introLine}{characterNotesLines}
- Keep each page to 1–3 sentences.

{style.AudienceLine}
Use a {style.Tone} tone. Prefer {style.SentenceRule}
Use {style.VocabHint}.
{FormatRule}
{DelimiterRule}

STORY PLAN (use it to plan the plot; never write these beat names or use them as sentence starters):
1. Introduce {hero} and the world of {request.Theme}.
2. Show what {hero} loves or wants.
3. A problem gets in the way.
4. {hero} tries to fix it, but it doesn't work.
5. Things get harder, and {hero} tries again.
6. {hero} figures out a better way from what just happened.
7. {hero} solves the problem through their own choice and effort.
8. Show how {hero} or their world is different now.
Spread these beats across the {pageCount} pages in order. With more pages, give the middle more room; with fewer, combine beats.

Make it a real story:
- Every page follows from the page before it. No random, disconnected events.
- {hero} drives the story. Helpers may appear, but {hero} makes the key choice that solves the problem; no grown-up or magic fixes it for them.
- The turning point comes from {hero} noticing what went wrong and thinking it through, not from suddenly remembering advice someone once gave.
- Show feelings through actions and dialogue, not only by naming them.
- Keep the problem kid-sized. Don't put {hero} alone in real danger (lost, near traffic, with strangers) unless the lesson is about that kind of safety, and then show the safe choice.
- Vary how pages begin. Never start two pages with the same word or phrase, and avoid stock openers like "Every day" or "Once upon a time".
- End warmly, in a way that connects back to the beginning.

{lessonBlock}

Put a line containing only --- between paragraphs. Do not include any other dividers.
""";
    }

    /// <summary>
    /// Tells the writer which pronouns to use, so it neither guesses from the name nor writes around pronouns.
    /// Boy and girl map directly; anything typed under "Other…" is passed along with they/them as the default.
    /// Returns null when no gender was given (e.g. animal characters).
    /// </summary>
    public static string? DescribePronouns(string? gender)
    {
        var g = Regex.Replace(gender ?? "", @"[\s()\[\];]+", " ").Trim();
        if (g.Length == 0)
            return null;
        if (g.Length > 40)
            g = g[..40].Trim();

        return g.ToLowerInvariant() switch
        {
            "boy" => "a boy; use he/him",
            "girl" => "a girl; use she/her",
            _ => $"{g}; use they/them unless that description clearly calls for other pronouns"
        };
    }

    private static string BuildRosterLine(IReadOnlyList<CharacterSpec> characters)
    {
        // Frontend guarantees at least 1 character
        if (characters.Count == 1)
        {
            var c = characters[0];
            var name = string.IsNullOrWhiteSpace(c.Name) ? "the child" : c.Name;

            return $"Use only this character by name throughout the story: {WithPronouns(name, c)}. " +
                   "Do not introduce new named characters; unnamed extras are okay.";
        }

        var cast = characters.Select(c =>
        {
            var pieces = new List<string>();

            if (!string.IsNullOrWhiteSpace(c.Name))
                pieces.Add(c.Name);

            var roleLabel = ToRoleString(c.Role);
            if (!string.IsNullOrWhiteSpace(roleLabel) &&
                !string.Equals(roleLabel, "character", StringComparison.OrdinalIgnoreCase))
            {
                pieces.Add($"the {roleLabel}");
            }

            // Species is only considered when you actually add it for extra characters
            if (c.DescriptionFields is not null &&
                c.DescriptionFields.TryGetValue("species", out var sv) &&
                !string.IsNullOrWhiteSpace(sv))
            {
                pieces.Add($"({sv})");
            }

            return WithPronouns(string.Join(" ", pieces.Where(p => !string.IsNullOrWhiteSpace(p))), c);
        });

        return "Use only these characters by name throughout the story: " +
               string.Join(", ", cast) +
               ". Do not introduce new named characters; unnamed extras are okay.";
    }

    private static string WithPronouns(string label, CharacterSpec character)
    {
        string? gender = null;
        character.DescriptionFields?.TryGetValue("gender", out gender);
        var pronouns = DescribePronouns(gender);
        return pronouns is null ? label : $"{label} ({pronouns})";
    }

    // string -> canonical role label used in prompts (only used if >1 character)
    private static string ToRoleString(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return "character";

        var r = role.Trim();

        return r.ToLowerInvariant() switch
        {
            "main" => "main character",
            "friend" => "friend",
            "guardian" => "guardian",
            "mom" or "mother" => "mom",
            "dad" or "father" => "dad",
            "pet" => "pet",
            "sibling" or "brother" or "sister" => "sibling",
            "teacher" => "teacher",
            _ => r   // fallback: whatever you stored on the frontend
        };
    }

    // Map reading level to text style guidance (no readerAge)
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
