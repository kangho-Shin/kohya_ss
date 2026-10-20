using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using OfficePower.Agent;
using OfficePower.Core;

namespace OfficePower.Tests;

public class ProvisioningTests
{
    [Fact]
    public void DefaultDataFolderIsTheAgentExecutableFolder() =>
        Assert.Equal(AppContext.BaseDirectory, Provisioning.DefaultDirectory);

    [Fact]
    public void InitializationAllowsProgramFilesAndKeepsThem()
    {
        string directory = Path.Combine(Path.GetTempPath(), "office-init-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            string executable = Path.Combine(directory, "OfficePower.Agent.exe");
            File.WriteAllText(executable, "test executable placeholder");
            Provisioning.Initialize(directory);
            Assert.Equal("test executable placeholder", File.ReadAllText(executable));
            Assert.True(File.Exists(Path.Combine(directory, "pairing.json")));
            var (first, cert) = Provisioning.Load(directory);
            cert.Dispose();
            Provisioning.Initialize(directory);
            var (second, cert2) = Provisioning.Load(directory);
            cert2.Dispose();
            Assert.Equal(first.Token, second.Token);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void LegacyMigrationPreservesTokenCertificateAndOriginalFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), "office-migrate-" + Guid.NewGuid());
        string legacy = Path.Combine(root, "legacy"), target = Path.Combine(root, "program");
        Directory.CreateDirectory(legacy); Directory.CreateDirectory(target);
        try
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest("CN=migration-test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
            var settings = new AgentSettings(1, "Test", Tokens.Create(), Tokens.Create(), 18765);
            var pfx = generated.Export(X509ContentType.Pfx, settings.PfxPassword);
            File.WriteAllBytes(Path.Combine(legacy, "server.pfx"), pfx);
            File.WriteAllText(Path.Combine(legacy, "agent-settings.json"), JsonSerializer.Serialize(settings, JsonFormat.Options));
            Assert.True(Provisioning.MigrateLegacyData(target, legacy));
            var (loaded, certificate) = Provisioning.Load(target);
            using (certificate)
            {
                Assert.Equal(settings.Token, loaded.Token);
                Assert.Equal(generated.GetCertHashString(HashAlgorithmName.SHA256), certificate.GetCertHashString(HashAlgorithmName.SHA256));
            }
            Assert.Equal(pfx, File.ReadAllBytes(Path.Combine(legacy, "server.pfx")));
            Assert.False(Provisioning.MigrateLegacyData(target, legacy));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void OrphanedCertificateIsNotOverwritten()
    {
        string directory = Path.Combine(Path.GetTempPath(), "office-orphan-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "server.pfx"), "preserve me");
            Assert.Throws<IOException>(() => Provisioning.Initialize(directory));
            Assert.Equal("preserve me", File.ReadAllText(Path.Combine(directory, "server.pfx")));
        }
        finally { Directory.Delete(directory, true); }
    }
}
