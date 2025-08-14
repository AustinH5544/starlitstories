namespace Hackathon_2025.Models;

public class ProgressUpdate
{
    public int? Percent { get; set; }       // prefer real % if you have it
    public string? Stage { get; set; }      // "text", "image", "db", "done", etc.
    public int? Index { get; set; }         // e.g., image # (0-based or 1-based)
    public int? Total { get; set; }         // e.g., total images
    public string? Message { get; set; }    // friendly text for UI
    public bool Done { get; set; }          // marks completion
}
