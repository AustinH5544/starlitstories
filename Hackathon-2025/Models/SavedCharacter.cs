using System.ComponentModel.DataAnnotations;

namespace Hackathon_2025.Models;

public class SavedCharacter
{
    public int Id { get; set; }

    public int UserId { get; set; }

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public string CharacterJson { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

