using Hackathon_2025.Data;
using Hackathon_2025.Tests.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hackathon_2025.Tests.SqlServer;

/// <summary>The normal test app, but backed by a real, freshly migrated SQL Server database.</summary>
internal sealed class SqlServerWebAppFactory : TestWebAppFactory
{
    private readonly string _connectionString;

    private SqlServerWebAppFactory(string connectionString) => _connectionString = connectionString;

    public static async Task<SqlServerWebAppFactory> CreateAsync()
    {
        var connectionString = await SqlServerTestContainer.GetConnectionStringForNewDatabaseAsync();
        var factory = new SqlServerWebAppFactory(connectionString);
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        return factory;
    }

    // Mirrors Program.cs (SqlServer + retry on transient errors such as deadlocks).
    protected override void ConfigureDatabase(IServiceCollection services)
        => services.AddDbContext<AppDbContext>(o => o.UseSqlServer(
            _connectionString,
            sql => sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(2), errorNumbersToAdd: null)));
}
