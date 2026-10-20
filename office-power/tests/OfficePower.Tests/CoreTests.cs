using System.Net;
using System.Net.Sockets;
using System.Text;
using OfficePower.Core;

namespace OfficePower.Tests;

public class CoreTests
{
    [Theory]
    [InlineData("00:11:22:AA:BB:CC")]
    [InlineData("00-11-22-aa-bb-cc")]
    public void MagicPacketContainsSixFFAndSixteenMacs(string mac)
    {
        var packet = WakeOnLan.CreatePacket(mac);
        Assert.Equal(102, packet.Length);
        Assert.All(packet.Take(6), b => Assert.Equal(255, b));
        for (int i = 0; i < 16; i++)
            Assert.Equal(new byte[] { 0, 17, 34, 170, 187, 204 }, packet.Skip(6 + i * 6).Take(6));
    }

    [Theory]
    [InlineData("not a mac")]
    [InlineData("00:11:22:33:44")]
    [InlineData("00:11:22:33:44:GG")]
    [InlineData("FF:FF:FF:FF:FF:FF")]
    [InlineData("00:00:00:00:00:00")]
    [InlineData("01:11:22:33:44:55")]
    public void InvalidMacIsRejected(string mac) => Assert.Throws<ArgumentException>(() => WakeOnLan.CreatePacket(mac));

    [Fact]
    public async Task RealUdpReceiverGetsMagicPacket()
    {
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)receiver.Client.LocalEndPoint!).Port;
        await WakeOnLan.SendAsync("00:11:22:33:44:55", IPAddress.Loopback, port);
        var received = await receiver.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(WakeOnLan.CreatePacket("00:11:22:33:44:55"), received.Buffer);
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("256.1.1.1")]
    [InlineData("127.0.0.1/path")]
    [InlineData("::1")]
    [InlineData("0.0.0.0")]
    public void RegistrationRejectsInvalidIPv4(string ip) =>
        Assert.Throws<ArgumentException>(() => PcRecord.ValidateAddress(ip));

    [Fact]
    public async Task StoreRoundtripDoesNotContainPlaintextToken()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".json");
        var protector = new TestProtector();
        var store = new PcStore(path, protector);
        var pc = new PcRecord(Guid.NewGuid(), "사무실 1", "192.168.0.50", "00:11:22:33:44:55", "255.255.255.255", 18765,
            new string('A', 64), new string('B', 64));
        try
        {
            await store.SaveAsync([pc]);
            Assert.DoesNotContain(pc.Token, await File.ReadAllTextAsync(path));
            var loaded = Assert.Single(await store.LoadAsync());
            Assert.Equal(pc, loaded);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task CorruptStoreIsNotSilentlyDiscarded()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".json");
        try
        {
            await File.WriteAllTextAsync(path, "broken");
            await Assert.ThrowsAsync<InvalidDataException>(() => new PcStore(path, new TestProtector()).LoadAsync());
            Assert.Equal("broken", await File.ReadAllTextAsync(path));
        }
        finally { File.Delete(path); }
    }

    private sealed class TestProtector : ISecretProtector
    {
        public string Protect(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        public string Unprotect(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));
    }
}
