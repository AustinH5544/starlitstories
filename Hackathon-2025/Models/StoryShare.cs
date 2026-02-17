using System;
using System.ComponentModel.DataAnnotations;

namespace Hackathon_2025.Models;

public class StoryShare
{
    public int Id { get; set; }

    public int StoryId { get; set; }
    public Story Story { get; set; } = null!;

    [MaxLength(64)]
    public string Token { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }
}