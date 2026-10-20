namespace OfficePower.Agent;

public interface IShutdownExecutor
{
    Task ScheduleAsync(int seconds, CancellationToken cancellation);
}

public sealed class ShutdownCoordinator(IShutdownExecutor executor, TimeProvider? clock = null)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private long scheduledUntil;
    public bool IsScheduled => Interlocked.Read(ref scheduledUntil) > time.GetUtcNow().UtcTicks;

    public async Task<bool> ScheduleAsync(CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try
        {
            if (IsScheduled) return false;
            await executor.ScheduleAsync(30, cancellation);
            // A blocked/cancelled OS shutdown must not lock this agent forever.
            Interlocked.Exchange(ref scheduledUntil, time.GetUtcNow().AddMinutes(2).UtcTicks);
            return true;
        }
        finally { gate.Release(); }
    }
}
