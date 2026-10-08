using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace Hackathon_2025.Tests.SqlServer;

/// <summary>Stands in for a SQL Server deadlock (error 1205), which can't be constructed directly.</summary>
internal sealed class SimulatedDeadlockException : Exception
{
    public SimulatedDeadlockException() : base("Simulated deadlock (test)") { }
}

/// <summary>
/// When armed, makes the next SaveChanges fail as if the database picked it as a deadlock victim.
/// Fires once, then disarms, so the execution strategy's retry runs against a working database.
/// </summary>
internal sealed class FailNextSaveInterceptor : SaveChangesInterceptor
{
    private int _armed;
    private int _fired;

    public void Arm() => Interlocked.Exchange(ref _armed, 1);
    public int TimesFired => Volatile.Read(ref _fired);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ThrowIfArmed();
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ThrowIfArmed();
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ThrowIfArmed()
    {
        if (Interlocked.Exchange(ref _armed, 0) == 1)
        {
            Interlocked.Increment(ref _fired);
            throw new SimulatedDeadlockException();
        }
    }
}

/// <summary>The production retry strategy, plus: treat a simulated deadlock as transient.</summary>
internal sealed class RetryIncludingSimulatedDeadlocks : SqlServerRetryingExecutionStrategy
{
    public RetryIncludingSimulatedDeadlocks(ExecutionStrategyDependencies dependencies)
        : base(dependencies, maxRetryCount: 5, maxRetryDelay: TimeSpan.FromMilliseconds(200), errorNumbersToAdd: null) { }

    protected override bool ShouldRetryOn(Exception exception)
        => exception is SimulatedDeadlockException || base.ShouldRetryOn(exception);
}
