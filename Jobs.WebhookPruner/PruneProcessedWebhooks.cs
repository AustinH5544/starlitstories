using System.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Jobs.WebhookPruner;

public class PruneProcessedWebhooks
{
    private readonly ILogger<PruneProcessedWebhooks> _log;
    private readonly IConfiguration _cfg;

    public PruneProcessedWebhooks(ILogger<PruneProcessedWebhooks> log, IConfiguration cfg)
    {
        _log = log;
        _cfg = cfg;
    }

    // Runs at 6:00 AM UTC daily by default; override with app setting "PruneCron"
    [Function(nameof(PruneProcessedWebhooks))]
    public async Task Run([TimerTrigger("%PruneCron%", RunOnStartup = false)] TimerInfo timer)
    {
        var connStr = _cfg["SQL_CONN"];
        var daysStr = _cfg["PRUNE_DAYS"];
        var days = int.TryParse(daysStr, out var d) ? d : 90;

        if (string.IsNullOrWhiteSpace(connStr))
        {
            _log.LogError("SQL_CONN not configured.");
            return;
        }

        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("dbo.usp_PruneProcessedWebhooks", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.Add(new SqlParameter("@Days", SqlDbType.Int) { Value = days });

            var affected = await cmd.ExecuteNonQueryAsync();
            _log.LogInformation("Pruned {Count} ProcessedWebhooks older than {Days} days at {Utc}.",
                affected, days, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error pruning ProcessedWebhooks.");
        }
    }
}
