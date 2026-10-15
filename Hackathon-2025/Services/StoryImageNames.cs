namespace Hackathon_2025.Services;

/// <summary>
/// File names for story images in Blob Storage. Image URLs are public (share links expose them),
/// so names use the user's numeric id, never their email.
/// </summary>
public static class StoryImageNames
{
    public static string Cover(int userId) => $"u{userId}-cover-{Guid.NewGuid()}.png";

    public static string Page(int userId, int pageIndex) => $"u{userId}-page-{pageIndex}-{Guid.NewGuid()}.png";
}
