using Hackathon_2025.Data;
using Microsoft.EntityFrameworkCore;

namespace Hackathon_2025.Services;

/// <summary>
/// Refunds for story credits, shared by StoryController and <see cref="StaleDraftRecovery"/>.
/// Counters are changed with single conditional UPDATE statements that touch only the counters involved;
/// never load a user, change a counter and save the whole row (see Hackathon-2025/CLAUDE.md).
/// </summary>
public static class StoryCredits
{
    /// <summary>A page-less story younger than this may still be generating; older ones are abandoned drafts.</summary>
    public static readonly TimeSpan GenerationWindow = TimeSpan.FromMinutes(30);

    public static Task RefundReservedCreditAsync(AppDbContext db, int userId, bool usedAddOn)
    {
        var user = db.Users.Where(u => u.Id == userId);
        return usedAddOn
            ? user.ExecuteUpdateAsync(s => s
                .SetProperty(u => u.BooksGenerated, u => u.BooksGenerated > 0 ? u.BooksGenerated - 1 : 0)
                .SetProperty(u => u.AddOnBalance, u => u.AddOnBalance + 1)
                .SetProperty(u => u.AddOnSpentThisPeriod, u => u.AddOnSpentThisPeriod > 0 ? u.AddOnSpentThisPeriod - 1 : 0))
            : user.ExecuteUpdateAsync(s => s
                .SetProperty(u => u.BooksGenerated, u => u.BooksGenerated > 0 ? u.BooksGenerated - 1 : 0));
    }

    /// <summary>
    /// Removes a draft that never got pages and returns its credit, together in one transaction.
    /// Returns false (and refunds nothing) if the draft is gone or has pages by now, so callers racing each
    /// other (a failing job, recovery, the owner deleting it) refund at most once.
    /// </summary>
    public static async Task<bool> DeleteDraftAndRefundAsync(AppDbContext db, int userId, int storyId, bool usedAddOn)
    {
        var deleted = false;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var rows = await db.Stories
                .Where(s => s.Id == storyId && s.UserId == userId && !s.Pages.Any())
                .ExecuteDeleteAsync();
            if (rows == 1)
                await RefundReservedCreditAsync(db, userId, usedAddOn);
            await tx.CommitAsync();
            deleted = rows == 1;
        });
        return deleted;
    }
}
