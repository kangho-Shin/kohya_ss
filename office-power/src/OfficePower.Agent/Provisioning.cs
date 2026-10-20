using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using OfficePower.Core;

namespace OfficePower.Agent;

public sealed record AgentSettings(int Version, string Name, string Token, string PfxPassword, int Port)
{
    public override string ToString() => "Agent settings (secrets hidden)";
}

public static class Provisioning
{
    public static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "OfficePowerAgent");

    public static void Initialize(string directory)
    {
        string config = Path.Combine(directory, "agent-settings.json");
        if (File.Exists(config)) { ExportPairing(directory); return; }
        if (Directory.EnumerateFiles(directory).Any()) throw new IOException("초기화가 중단된 폴더입니다. 기존 파일을 보존하고 확인하세요.");
        var settings = new AgentSettings(1, Environment.MachineName, Tokens.Create(), Tokens.Create(), 18765);
        using var key = RSA.Create(3072);
        var request = new CertificateRequest("CN=OfficePowerAgent", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, true));
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(5));
        File.WriteAllBytes(Path.Combine(directory, "server.pfx"), cert.Export(X509ContentType.Pfx, settings.PfxPassword));
        WriteAtomic(config, JsonSerializer.Serialize(settings, JsonFormat.Options));
        ExportPairing(directory);
    }

    public static (AgentSettings Settings, X509Certificate2 Certificate) Load(string directory)
    {
        var settings = JsonSerializer.Deserialize<AgentSettings>(File.ReadAllText(Path.Combine(directory, "agent-settings.json")), JsonFormat.Options)
            ?? throw new InvalidDataException("서비스 설정이 없습니다.");
        if (settings.Version != 1 || settings.Token.Length != 64 || !settings.Token.All(Uri.IsHexDigit) || settings.Port != 18765)
            throw new InvalidDataException("서비스 설정이 올바르지 않습니다.");
        var cert = new X509Certificate2(Path.Combine(directory, "server.pfx"), settings.PfxPassword,
            // Schannel cannot serve TLS with ephemeral keys. Without PersistKeySet,
            // the temporary machine key container is cleaned when the cert is disposed.
            OperatingSystem.IsWindows() ? X509KeyStorageFlags.MachineKeySet : X509KeyStorageFlags.EphemeralKeySet);
        if (!cert.HasPrivateKey || cert.NotAfter.ToUniversalTime() <= DateTime.UtcNow)
        { cert.Dispose(); throw new InvalidDataException("서비스 인증서를 확인하세요."); }
        return (settings, cert);
    }

    public static void ExportPairing(string directory)
    {
        var (settings, cert) = Load(directory);
        using (cert)
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel
                    && n.GetPhysicalAddress().GetAddressBytes().Length == 6).ToArray();
            var ips = interfaces.SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address))
                .Select(a => a.Address.ToString()).Distinct().ToArray();
            var macs = interfaces.Select(n => n.GetPhysicalAddress().ToString()).Distinct().ToArray();
            var pairing = new PairingInfo(1, settings.Name, ips, macs, settings.Port,
                cert.GetCertHashString(HashAlgorithmName.SHA256), settings.Token);
            pairing.Validate();
            WriteAtomic(Path.Combine(directory, "pairing.json"), JsonSerializer.Serialize(pairing, JsonFormat.Options));
        }
    }

    private static void WriteAtomic(string path, string text)
    {
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, text);
        File.Move(temporary, path, true);
    }
}
