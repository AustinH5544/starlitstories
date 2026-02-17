using System;
using System.Security.Claims;
using Hackathon_2025.Data;
using Hackathon_2025.Models;
using Hackathon_2025.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hackathon_2025.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IQuotaService _quota;
    private readonly IPeriodService _period;

    public UsersController(AppDbContext db, IQuotaService quota, IPeriodService period)
    {
        _db = db;
        _quota = quota;
        _period = period;
    }

    [Authorize]
    [HttpGet("me/usage")]
    public async Task<IActionResult> GetMyUsage()
    {
        // Resolve user id from common claim shapes
        var userIdStr =
            User.FindFirst("sub")?.Value ??
            User.FindFirst("id")?.Value ??
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdStr, out var userId))
            return Unauthorized("No user id claim on the request.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return Unauthorized("User not found.");

        // Roll the period if a boundary was crossed (keeps counters correct)
        var now = DateTime.UtcNow;
        if (_period.IsPeriodBoundary(user, now))
        {
            _period.OnPeriodRollover(user, now);
            _db.Users.Update(user);
            await _db.SaveChangesAsync();
        }

        // Derive usage numbers
        var (periodStart, periodEnd) = _period.CurrentPeriodUtc(user, now);

        // Membership as enum → string key for services that still take strings
        var planKey = user.Membership.ToString(); // e.g., "Free", "Pro", "Premium"

        var baseQuota = _quota.BaseQuotaFor(planKey);
        var used = user.BooksGenerated;
        var baseRemaining = Math.Max(baseQuota - used, 0);

        var addOnBalance = user.AddOnBalance;              // carryover wallet
        var addOnSpentThisPeriod = user.AddOnSpentThisPeriod;
        var remaining = baseRemaining + addOnBalance;

        // Helpful for UI: whether to show/enable "Buy credits" button
        var canBuyAddons = _quota.CanBuyAddons(planKey, baseRemaining, addOnBalance);

        return Ok(new
        {
            plan = planKey.ToLowerInvariant(),
            baseQuota,
            used,
            baseRemaining,
            addOnBalance,
            addOnSpentThisPeriod,
            remaining,
            periodStart = periodStart.ToString("o"),
            periodEnd = periodEnd.ToString("o"),
            canBuyAddons
        });
    }
}
