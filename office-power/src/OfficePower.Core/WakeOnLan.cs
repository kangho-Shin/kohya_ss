using System.Net;
using System.Net.Sockets;

namespace OfficePower.Core;

public static class WakeOnLan
{
    public static byte[] CreatePacket(string mac)
    {
        var compact = mac.Replace(":", "").Replace("-", "");
        if (compact.Length != 12 || !compact.All(Uri.IsHexDigit))
            throw new ArgumentException("MAC 주소는 00:11:22:33:44:55 형식으로 입력하세요.");
        var bytes = Convert.FromHexString(compact);
        if ((bytes[0] & 1) != 0 || bytes.All(b => b == 0))
            throw new ArgumentException("유효한 PC 랜카드의 유니캐스트 MAC 주소가 필요합니다.");
        var packet = new byte[102];
        Array.Fill(packet, (byte)255, 0, 6);
        for (int i = 0; i < 16; i++) bytes.CopyTo(packet, 6 + i * 6);
        return packet;
    }

    public static async Task SendAsync(string mac, IPAddress broadcast, int port = 9, CancellationToken cancellation = default)
    {
        if (broadcast.AddressFamily != AddressFamily.InterNetwork) throw new ArgumentException("IPv4가 필요합니다.");
        using var client = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
        await client.SendAsync(CreatePacket(mac), new IPEndPoint(broadcast, port), cancellation);
    }
}
