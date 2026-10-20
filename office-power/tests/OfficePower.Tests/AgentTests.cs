using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using OfficePower.Agent;
using OfficePower.Core;
using System.Text.Json;

namespace OfficePower.Tests;

public class AgentTests
{
    private sealed class FakeShutdown : IShutdownExecutor
    {
        public int Calls;
        public bool Fail;
        public Task ScheduleAsync(int seconds, CancellationToken cancellation)
        {
            Assert.Equal(30, seconds);
            if (Fail) throw new IOException("test OS error");
            Interlocked.Increment(ref Calls);
            return Task.CompletedTask;
        }
    }

    private static X509Certificate2 Certificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=OfficePowerTest", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            var settings = new AgentSettings(1, "Test PC", Tokens.Create(), "test-password", 18765);
            File.WriteAllText(Path.Combine(dir, "agent-settings.json"), JsonSerializer.Serialize(settings, JsonFormat.Options));
            File.WriteAllBytes(Path.Combine(dir, "server.pfx"), generated.Export(X509ContentType.Pfx, settings.PfxPassword));
            return Provisioning.Load(dir).Certificate;
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task RealHttpsAuthenticatesAndSchedulesExactlyOnce()
    {
        using var cert = Certificate();
        var fake = new FakeShutdown();
        var token = Tokens.Create();
        await using var app = AgentHost.Build(new AgentOptions("Test PC", token, cert, 0, IPAddress.Loopback), fake);
        await app.StartAsync();
        var server = app.Services.GetRequiredService<IServer>();
        var url = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var pc = new PcRecord(Guid.NewGuid(), "테스트", "127.0.0.1", "00:11:22:33:44:55", "255.255.255.255",
            new Uri(url).Port, cert.GetCertHashString(HashAlgorithmName.SHA256), token);
        var client = new PowerClient();
        Assert.Equal("online", (await client.GetStatusAsync(pc)).State);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => client.GetStatusAsync(pc with { Token = Tokens.Create() }));
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetStatusAsync(pc with { CertificateSha256 = new string('0',64) }));
        var reply = await client.ShutdownAsync(pc);
        Assert.Equal(30, reply.DelaySeconds);
        Assert.Equal("scheduled", reply.State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ShutdownAsync(pc));
        Assert.Equal(1, fake.Calls);
        Assert.Equal("shutdown-scheduled", (await client.GetStatusAsync(pc)).State);
    }

    [Fact]
    public async Task ShutdownFailureDoesNotReportSuccessAndCanRetry()
    {
        var fake = new FakeShutdown { Fail = true };
        var coordinator = new ShutdownCoordinator(fake);
        await Assert.ThrowsAsync<IOException>(() => coordinator.ScheduleAsync(CancellationToken.None));
        Assert.False(coordinator.IsScheduled);
        fake.Fail = false;
        Assert.True(await coordinator.ScheduleAsync(CancellationToken.None));
        Assert.Equal(1, fake.Calls);
    }

    [Fact]
    public async Task ConcurrentRequestsScheduleOnlyOnce()
    {
        var fake = new FakeShutdown();
        var coordinator = new ShutdownCoordinator(fake);
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => coordinator.ScheduleAsync(CancellationToken.None)));
        Assert.Equal(1, results.Count(x => x));
        Assert.Equal(1, fake.Calls);
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task CancelledWindowsShutdownCanBeRetriedAfterDuplicateWindow()
    {
        var clock = new ManualClock();
        var executor = new FakeShutdown();
        var coordinator = new ShutdownCoordinator(executor, clock);
        Assert.True(await coordinator.ScheduleAsync(CancellationToken.None));
        Assert.False(await coordinator.ScheduleAsync(CancellationToken.None));
        clock.Now = clock.Now.AddMinutes(3);
        Assert.False(coordinator.IsScheduled);
        Assert.True(await coordinator.ScheduleAsync(CancellationToken.None));
        Assert.Equal(2, executor.Calls);
    }

    [Fact]
    public async Task RequestTimeoutIsObserved()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var pc = new PcRecord(Guid.NewGuid(), "timeout", "127.0.0.1", "00:11:22:33:44:55", "255.255.255.255", port, new string('A',64), Tokens.Create());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PowerClient(TimeSpan.FromMilliseconds(150)).GetStatusAsync(pc));
    }
}
