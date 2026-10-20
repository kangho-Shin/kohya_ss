using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;

namespace OfficePower.Core;

public sealed class PowerClient(TimeSpan? timeout = null)
{
    private HttpClient CreateClient(PcRecord pc)
    {
        pc.Validate();
        byte[] fingerprint = Convert.FromHexString(pc.CertificateSha256);
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            ServerCertificateCustomValidationCallback = (_, cert, _, _) =>
                cert is not null && CryptographicOperations.FixedTimeEquals(
                    SHA256.HashData(cert.RawData), fingerprint)
                && DateTime.UtcNow >= cert.NotBefore.ToUniversalTime()
                && DateTime.UtcNow <= cert.NotAfter.ToUniversalTime()
        };
        var client = new HttpClient(handler) { BaseAddress = new Uri($"https://{pc.Ip}:{pc.Port}"), Timeout = timeout ?? TimeSpan.FromSeconds(5) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pc.Token);
        return client;
    }

    public async Task<AgentStatus> GetStatusAsync(PcRecord pc, CancellationToken cancellation = default)
    {
        using var client = CreateClient(pc);
        using var response = await client.GetAsync("/status", cancellation);
        EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<AgentStatus>(cancellation) ?? throw new IOException("잘못된 서비스 응답입니다.");
    }

    public async Task<ShutdownReply> ShutdownAsync(PcRecord pc, CancellationToken cancellation = default)
    {
        using var client = CreateClient(pc);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/shutdown");
        using var response = await client.SendAsync(request, cancellation);
        EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<ShutdownReply>(cancellation) ?? throw new IOException("잘못된 종료 응답입니다.");
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized) throw new UnauthorizedAccessException("인증키가 일치하지 않습니다. 인증 파일을 다시 가져오세요.");
        if (response.StatusCode == HttpStatusCode.Conflict) throw new InvalidOperationException("이미 종료가 예약되어 있습니다.");
        response.EnsureSuccessStatusCode();
    }
}
