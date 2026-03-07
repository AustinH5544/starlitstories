using System.Security.Claims;
using System.Text.Json;
using Hackathon_2025.Data;
using Hackathon_2025.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hackathon_2025.Controllers;

[ApiController]
[Route("api/saved-character")]
[Authorize]
public class SavedCharacterController : ControllerBase
{
    private const int MaxSavedCharactersPerUser = 5;
    private readonly AppDbContext _db;

    public SavedCharacterController(AppDbContext db)
    {
        _db = db;
    }

    private bool TryGetUserId(out int userId)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdStr, out userId);
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMine()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized("Invalid or missing user ID.");

        var saved = await _db.SavedCharacters
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToListAsync();

        var items = new List<object>(saved.Count);
        foreach (var item in saved)
        {
            if (!TryParseCharacter(item.CharacterJson, out var character))
                return StatusCode(500, "Saved character data is corrupted.");

            items.Add(new
            {
                id = item.Id,
                name = item.Name,
                character,
                updatedAtUtc = item.UpdatedAtUtc
            });
        }

        return Ok(new
        {
            maxSavedCharacters = MaxSavedCharactersPerUser,
            items
        });
    }

    [HttpPost("me")]
    public async Task<IActionResult> CreateMine([FromBody] UpsertSavedCharacterRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized("Invalid or missing user ID.");

        if (!TryValidateCharacterRequest(request, out var name))
            return BadRequest("Character name is required.");

        var savedCount = await _db.SavedCharacters.CountAsync(x => x.UserId == userId);
        if (savedCount >= MaxSavedCharactersPerUser)
        {
            return Conflict(new
            {
                message = $"You can save up to {MaxSavedCharactersPerUser} characters. Delete one to save a new character."
            });
        }

        var now = DateTime.UtcNow;
        var entity = new SavedCharacter
        {
            UserId = userId,
            Name = name,
            CharacterJson = request.Character.GetRawText(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _db.SavedCharacters.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            id = entity.Id,
            name = entity.Name,
            character = request.Character,
            updatedAtUtc = entity.UpdatedAtUtc
        });
    }

    [HttpPut("me/{id:int}")]
    public async Task<IActionResult> UpdateMine(int id, [FromBody] UpsertSavedCharacterRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized("Invalid or missing user ID.");

        if (!TryValidateCharacterRequest(request, out var name))
            return BadRequest("Character name is required.");

        var existing = await _db.SavedCharacters.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (existing is null) return NotFound();

        var now = DateTime.UtcNow;
        existing.Name = name;
        existing.CharacterJson = request.Character.GetRawText();
        existing.UpdatedAtUtc = now;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            id = existing.Id,
            name = existing.Name,
            character = request.Character,
            updatedAtUtc = existing.UpdatedAtUtc
        });
    }

    [HttpDelete("me/{id:int}")]
    public async Task<IActionResult> DeleteMine(int id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized("Invalid or missing user ID.");

        var existing = await _db.SavedCharacters.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (existing is null) return NoContent();

        _db.SavedCharacters.Remove(existing);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    public sealed class UpsertSavedCharacterRequest
    {
        public JsonElement Character { get; set; }
    }

    private static bool TryValidateCharacterRequest(UpsertSavedCharacterRequest request, out string name)
    {
        name = string.Empty;
        if (request.Character.ValueKind != JsonValueKind.Object) return false;
        if (!request.Character.TryGetProperty("name", out var nameElement)) return false;

        name = (nameElement.GetString() ?? string.Empty).Trim();
        return !string.IsNullOrWhiteSpace(name);
    }

    private static bool TryParseCharacter(string raw, out JsonElement character)
    {
        character = default;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            character = doc.RootElement.Clone();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
