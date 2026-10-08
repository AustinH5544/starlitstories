namespace Hackathon_2025.Services;

/// <summary>Runs <see cref="StaleDraftRecovery"/> shortly after startup and then every few minutes.</summary>
public class StaleDraftRecoveryService : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    private readonly StaleDraftRecovery _recovery;
    private readonly ILogger<StaleDraftRecoveryService> _logger;

    public StaleDraftRecoveryService(StaleDraftRecovery recovery, ILogger<StaleDraftRecoveryService> logger)
    {
        _recovery = recovery;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _recovery.RecoverAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Stale story draft recovery failed; will retry.");
                }
                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down
        }
    }
}
