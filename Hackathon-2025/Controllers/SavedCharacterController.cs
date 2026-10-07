using System.Security.Claims;
using System.Text.Json;
using Hackathon_2025.Data;
using Hackathon_2025.Models;
using Hackathon_2025.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hackathon_2025.Controllers;

[ApiController]
[Route("api/saved-character")]
[Authorize]
public class SavedCharacterController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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

        var membership = await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.Membership)
            .FirstOrDefaultAsync();

        var maxSavedCharacters = MembershipEntitlements.SavedCharacterLimitFor(membership);

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
            maxSavedCharacters,
            savedCharacterCount = saved.Count,
            isOverLimit = saved.Count > maxSavedCharacters,
            overLimitCount = Math.Max(saved.Count - maxSavedCharacters, 0),
            downgradePolicy = "Saved characters are never deleted when a membership changes. If you are over your current plan limit, keep the extras and delete down before saving new ones.",
            items
        });
    }

    [HttpPost("me")]
    public async Task<IActionResult> CreateMine([FromBody] UpsertSavedCharacterRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized("Invalid or missing user ID.");

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
            return Unauthorized("User not found.");

        if (!TryParseCharacterRequest(request, out var character) || !TryValidateCharacterRequest(character, out var name))
            return BadRequest("Character name is required.");

        var sanitizedCharacter = SanitizeCharacterPayload(user.Membership, character);
        var maxSavedCharacters = MembershipEntitlements.SavedCharacterLimitFor(user.Membership);
        var savedCount = await _db.SavedCharacters.CountAsync(x => x.UserId == userId);
        if (savedCount >= maxSavedCharacters)
        {
            return Conflict(new
            {
                message = $"Your {user.Membership} plan includes up to {maxSavedCharacters} saved character{(maxSavedCharacters == 1 ? "" : "s")}. Delete one to save a new character."
            });
        }

        var now = DateTime.UtcNow;
        var entity = new SavedCharacter
        {
            UserId = userId,
            Name = name,
            CharacterJson = JsonSerializer.Serialize(sanitizedCharacter, JsonOptions),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _db.SavedCharacters.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            id = entity.Id,
            name = entity.Name,
            character = sanitizedCharacter,
            updatedAtUtc = entity.UpdatedAtUtc
        });
    }

    [HttpPut("me/{id:int}")]
    public async Task<IActionResult> UpdateMine(int id, [FromBody] UpsertSavedCharacterRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized("Invalid or missing user ID.");

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
            return Unauthorized("User not found.");

        if (!TryParseCharacterRequest(request, out var character) || !TryValidateCharacterRequest(character, out var name))
            return BadRequest("Character name is required.");

        var existing = await _db.SavedCharacters.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (existing is null) return NotFound();

        var sanitizedCharacter = SanitizeCharacterPayload(user.Membership, character);
        var now = DateTime.UtcNow;
        existing.Name = name;
        existing.CharacterJson = JsonSerializer.Serialize(sanitizedCharacter, JsonOptions);
        existing.UpdatedAtUtc = now;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            id = existing.Id,
            name = existing.Name,
            character = sanitizedCharacter,
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

    private sealed class SavedCharacterPayload
    {
        public string Role { get; set; } = "main";
        public string RoleCustom { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsAnimal { get; set; }
        public Dictionary<string, string> DescriptionFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryParseCharacterRequest(UpsertSavedCharacterRequest request, out SavedCharacterPayload character)
    {
        character = new SavedCharacterPayload();
        if (request.Character.ValueKind != JsonValueKind.Object) return false;

        try
        {
            var parsed = JsonSerializer.Deserialize<SavedCharacterPayload>(request.Character.GetRawText(), JsonOptions);
            if (parsed is null) return false;
            character = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryValidateCharacterRequest(SavedCharacterPayload request, out string name)
    {
        name = (request.Name ?? string.Empty).Trim();
        return !string.IsNullOrWhiteSpace(name);
    }

    private static SavedCharacterPayload SanitizeCharacterPayload(MembershipPlan membership, SavedCharacterPayload payload) =>
        new()
        {
            Role = string.IsNullOrWhiteSpace(payload.Role) ? "main" : payload.Role.Trim(),
            RoleCustom = payload.RoleCustom?.Trim() ?? string.Empty,
            Name = payload.Name.Trim(),
            IsAnimal = payload.IsAnimal,
            DescriptionFields = MembershipEntitlements.SanitizeDescriptionFields(membership, payload.DescriptionFields)
        };

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
