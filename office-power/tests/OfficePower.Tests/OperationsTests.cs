using OfficePower.Core;

namespace OfficePower.Tests;

public class OperationsTests
{
    [Fact]
    public async Task DeferredLocalControllerIsProcessedAfterRemoteComputers()
    {
        var pcs = Enumerable.Range(1, 3).Select(i => new PcRecord(Guid.NewGuid(), "PC"+i, "192.168.0."+i,
            "00:11:22:33:44:55", "255.255.255.255", 18765, new string('A',64), Tokens.Create())).ToArray();
        var order = new List<Guid>();
        await PowerOperations.RunAsync(pcs, pc => { order.Add(pc.Id); return Task.FromResult("sent"); }, pc => pc.Id == pcs[0].Id);
        Assert.Equal(new[] { pcs[1].Id, pcs[2].Id, pcs[0].Id }, order);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OneOfflineComputerDoesNotPreventOtherOperations(bool socketError)
    {
        var pcs = Enumerable.Range(1, 3).Select(i => new PcRecord(Guid.NewGuid(), "PC"+i, "192.168.0."+i,
            "00:11:22:33:44:55", "255.255.255.255", 18765, new string('A',64), Tokens.Create())).ToArray();
        int calls = 0;
        var results = await PowerOperations.RunAsync(pcs, pc =>
        {
            calls++;
            if (pc.Id == pcs[0].Id)
            {
                if (socketError) throw new System.Net.Sockets.SocketException();
                throw new HttpRequestException("offline");
            }
            return Task.FromResult("켜기 요청 전송");
        });
        Assert.Equal(3, calls);
        Assert.False(results[0].Success);
        Assert.True(results[1].Success);
        Assert.True(results[2].Success);
        Assert.Equal("켜기 요청 전송", results[1].Message);
    }
}
