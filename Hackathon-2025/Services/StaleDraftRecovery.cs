using Hackathon_2025.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackathon_2025.Services;

/// <summary>
/// Stories generate in-process, so an API restart (every deploy) kills any job mid-story: the draft never gets
/// pages and its reserved credit stays spent. This finds drafts that are clearly abandoned and refunds them.
/// </summary>
public class StaleDraftRecovery
{
    private const int BatchSize = 200;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StaleDraftRecovery> _logger;

    public StaleDraftRecovery(IServiceScopeFactory scopeFactory, ILogger<StaleDraftRecovery> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <returns>How many drafts were removed and refunded.</returns>
    public async Task<int> RecoverAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cutoff = DateTime.UtcNow - StoryCredits.GenerationWindow;

        var abandoned = await db.Stories
            .AsNoTracking()
            .Where(s => !s.Pages.Any() && s.CreatedAt < cutoff)
            .OrderBy(s => s.Id)
            .Select(s => new { s.Id, s.UserId, s.ReservedFromAddOn, s.CreatedAt })
            .Take(BatchSize)
            .ToListAsync(ct);

        var recovered = 0;
        foreach (var draft in abandoned)
        {
            ct.ThrowIfCancellationRequested();
            if (await StoryCredits.DeleteDraftAndRefundAsync(db, draft.UserId, draft.Id, draft.ReservedFromAddOn))
            {
                recovered++;
                _logger.LogWarning(
                    "Recovered abandoned story draft {StoryId} for user {UserId} (started {CreatedAt:u}); refunded {Credit} credit.",
                    draft.Id, draft.UserId, draft.CreatedAt, draft.ReservedFromAddOn ? "an add-on" : "a plan");
            }
        }

        return recovered;
    }
}
