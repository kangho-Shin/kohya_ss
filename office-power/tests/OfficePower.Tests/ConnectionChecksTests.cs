using OfficePower.Core;

namespace OfficePower.Tests;

public class ConnectionChecksTests
{
    private static PcRecord[] Computers(int count) => Enumerable.Range(1, count)
        .Select(i => new PcRecord(Guid.NewGuid(), "PC" + i, "192.168.0." + i,
            "00:11:22:33:44:55", "255.255.255.255", 18765, new string('A', 64), Tokens.Create())).ToArray();

    private sealed class InlineProgress(Action<PcResult> report) : IProgress<PcResult>
    {
        public void Report(PcResult value) => report(value);
    }

    [Fact]
    public async Task HundredComputersUseAtMostTwentyConcurrentChecks()
    {
        var full = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int active = 0, maximum = 0;
        var run = ConnectionChecks.RunAsync(Computers(100), async (_, token) =>
        {
            int current = Interlocked.Increment(ref active);
            lock (full) maximum = Math.Max(maximum, current);
            if (current == 20) full.TrySetResult();
            try { await release.Task.WaitAsync(token); return "online"; }
            finally { Interlocked.Decrement(ref active); }
        });
        try
        {
            await full.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(20, maximum);
        }
        finally { release.TrySetResult(); }
        Assert.Equal(100, (await run).Count);
        Assert.Equal(20, maximum);
    }

    [Fact]
    public async Task FastResultIsReportedBeforeSlowComputerCompletes()
    {
        var pcs = Computers(2);
        var slow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstReport = new TaskCompletionSource<PcResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = ConnectionChecks.RunAsync(pcs, async (pc, token) =>
        {
            if (pc.Id == pcs[0].Id) await slow.Task.WaitAsync(token);
            return "online";
        }, new InlineProgress(result => firstReport.TrySetResult(result)));
        try
        {
            Assert.Equal(pcs[1].Id, (await firstReport.Task.WaitAsync(TimeSpan.FromSeconds(5))).Id);
            Assert.False(run.IsCompleted);
        }
        finally { slow.TrySetResult(); }
        Assert.Equal(2, (await run).Count);
    }

    [Fact]
    public async Task CancellationStopsActiveAndQueuedChecksWithoutCallingThemTimeouts()
    {
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0, reports = 0;
        var run = ConnectionChecks.RunAsync(Computers(100), async (_, token) =>
        {
            if (Interlocked.Increment(ref calls) == 20) started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return "online";
        }, new InlineProgress(_ => Interlocked.Increment(ref reports)), cancellation.Token);
        try { await started.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { cancellation.Cancel(); }
        var results = await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(results);
        Assert.Equal(0, reports);
        Assert.Equal(20, calls);
    }

    [Fact]
    public async Task TimeoutAndAuthenticationFailuresDoNotStopOtherComputers()
    {
        var pcs = Computers(4);
        var results = await ConnectionChecks.RunAsync(pcs, (pc, _) =>
        {
            if (pc.Id == pcs[0].Id) throw new TaskCanceledException();
            if (pc.Id == pcs[1].Id) throw new UnauthorizedAccessException();
            if (pc.Id == pcs[2].Id) throw new HttpRequestException();
            return Task.FromResult("online");
        });
        Assert.Equal(4, results.Count);
        Assert.Contains("시간 초과", results.Single(r => r.Id == pcs[0].Id).Message);
        Assert.Contains("인증 실패", results.Single(r => r.Id == pcs[1].Id).Message);
        Assert.Contains("연결 실패", results.Single(r => r.Id == pcs[2].Id).Message);
        Assert.True(results.Single(r => r.Id == pcs[3].Id).Success);
    }
}
