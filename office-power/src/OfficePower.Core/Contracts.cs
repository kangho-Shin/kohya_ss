using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

namespace OfficePower.Core;

public static class JsonFormat
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
}

public sealed record PcRecord(Guid Id, string Name, string Ip, string Mac, string Broadcast,
    int Port, string CertificateSha256, string Token)
{
    public static IPAddress ValidateAddress(string text)
    {
        var parts = text.Split('.');
        if (parts.Length != 4 || parts.Any(p => !byte.TryParse(p, out var b) || b.ToString() != p)
            || !IPAddress.TryParse(text, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork
            || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.Broadcast) || ip.GetAddressBytes()[0] >= 224)
            throw new ArgumentException("올바른 IPv4 주소를 입력하세요.");
        return ip;
    }

    public void Validate()
    {
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(Name) || Name.Length > 80)
            throw new ArgumentException("PC 이름을 1~80자로 입력하세요.");
        ValidateAddress(Ip);
        WakeOnLan.CreatePacket(Mac);
        if (!IPAddress.TryParse(Broadcast, out var broadcast) || broadcast.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("브로드캐스트 IPv4 주소가 올바르지 않습니다.");
        if (Port is < 1 or > 65535) throw new ArgumentException("포트는 1~65535 사이여야 합니다.");
        if (CertificateSha256.Length != 64 || !CertificateSha256.All(Uri.IsHexDigit))
            throw new ArgumentException("인증서 SHA-256 지문을 확인하세요. 인증 파일을 다시 가져오세요.");
        if (Token.Length != 64 || !Token.All(Uri.IsHexDigit))
            throw new ArgumentException("인증키가 올바르지 않습니다. 인증 파일을 다시 가져오세요.");
    }

    public override string ToString() => $"{Name} ({Ip})";
}

public sealed record PairingInfo(int Version, string Name, string[] IpAddresses, string[] MacAddresses,
    int Port, string CertificateSha256, string Token)
{
    public void Validate()
    {
        if (Version != 1 || IpAddresses is null || MacAddresses is null || IpAddresses.Length == 0 || MacAddresses.Length == 0)
            throw new InvalidDataException("지원하지 않거나 불완전한 인증 파일입니다.");
        new PcRecord(Guid.NewGuid(), Name, IpAddresses[0], MacAddresses[0], "255.255.255.255",
            Port, CertificateSha256, Token).Validate();
    }
    public override string ToString() => $"Pairing: {Name}";
}

public sealed record AgentStatus(string Name, string State);
public sealed record ShutdownReply(string State, int DelaySeconds);

public static class Tokens
{
    public static string Create() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static bool Equals(string expected, string supplied)
    {
        if (expected.Length != 64 || supplied.Length != 64) return false;
        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(expected), System.Text.Encoding.UTF8.GetBytes(supplied));
    }
}
