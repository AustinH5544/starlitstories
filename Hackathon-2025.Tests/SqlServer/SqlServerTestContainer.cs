using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Hackathon_2025.Tests.SqlServer;

/// <summary>
/// One disposable SQL Server (Docker) per test run, started lazily on first use.
/// Each caller gets its own database on that server, so tests stay isolated and can run in parallel.
/// Locally, missing Docker makes these tests Inconclusive (skipped); with CI=true it fails them.
/// </summary>
internal static class SqlServerTestContainer
{
    private static readonly Lazy<Task<MsSqlContainer?>> Container = new(StartAsync);
    private static string? _startFailure;

    private static async Task<MsSqlContainer?> StartAsync()
    {
        try
        {
            var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await container.StartAsync();
            return container;
        }
        catch (Exception ex)
        {
            _startFailure = ex.Message;
            return null;
        }
    }

    public static async Task<string> GetConnectionStringForNewDatabaseAsync()
    {
        var container = await Container.Value;
        if (container is null)
        {
            var message = $"SQL Server test container unavailable (is Docker running?): {_startFailure}";
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
                Assert.Fail(message);
            Assert.Inconclusive(message);
        }

        var builder = new SqlConnectionStringBuilder(container!.GetConnectionString())
        {
            InitialCatalog = $"t_{Guid.NewGuid():N}"
        };
        return builder.ConnectionString;
    }

    public static async Task DisposeAsync()
    {
        if (Container.IsValueCreated && await Container.Value is { } container)
            await container.DisposeAsync();
    }
}

[TestClass]
public class SqlServerAssemblyHooks
{
    [AssemblyCleanup]
    public static async Task Cleanup() => await SqlServerTestContainer.DisposeAsync();
}
