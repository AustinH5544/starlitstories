namespace Hackathon_2025.Services;

/// <summary>
/// Runs <see cref="StaleDraftRecovery"/> shortly after startup, then again only while stories are generating, timed
/// for when the oldest of them would count as abandoned. With nothing generating it stops: stories are only orphaned
/// by a restart, and polling on a timer kept the serverless database from ever auto-pausing (billed 24/7).
/// </summary>
public class StaleDraftRecoveryService : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(10);

    /// <summary>Checks a little after the cutoff so the draft is safely past it.</summary>
    public static readonly TimeSpan CheckMargin = TimeSpan.FromMinutes(1);

    private readonly StaleDraftRecovery _recovery;
    private readonly ILogger<StaleDraftRecoveryService> _logger;

    public StaleDraftRecoveryService(StaleDraftRecovery recovery, ILogger<StaleDraftRecoveryService> logger)
    {
        _recovery = recovery;
        _logger = logger;
    }

    /// <returns>How long to wait before checking again, or null to stop until the next restart.</returns>
    public static TimeSpan? DelayBeforeNextCheck(DateTime? nextCheckAt, DateTime utcNow)
    {
        if (nextCheckAt is null) return null;
        var untilCutoff = nextCheckAt.Value - utcNow;
        return (untilCutoff > TimeSpan.Zero ? untilCutoff : TimeSpan.Zero) + CheckMargin;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                TimeSpan? delay;
                try
                {
                    await _recovery.RecoverAsync(stoppingToken);
                    delay = DelayBeforeNextCheck(await _recovery.NextCheckAtAsync(stoppingToken), DateTime.UtcNow);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Stale story draft recovery failed; will retry.");
                    delay = RetryDelay;
                }

                if (delay is null)
                {
                    _logger.LogInformation("No stories generating; stale draft recovery idle until the next restart.");
                    return;
                }
                await Task.Delay(delay.Value, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down
        }
    }
}
