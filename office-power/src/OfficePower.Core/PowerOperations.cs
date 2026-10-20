namespace OfficePower.Core;

public sealed record PcResult(Guid Id, bool Success, string Message);

public static class PowerOperations
{
    public static async Task<IReadOnlyList<PcResult>> RunAsync(IEnumerable<PcRecord> computers, Func<PcRecord, Task<string>> operation, Func<PcRecord, bool>? deferUntilLast = null)
    {
        var results = new List<PcResult>();
        foreach (var pc in computers.OrderBy(pc => deferUntilLast?.Invoke(pc) == true))
        {
            try { results.Add(new PcResult(pc.Id, true, await operation(pc))); }
            catch (OperationCanceledException) { results.Add(new PcResult(pc.Id, false, "응답 시간 초과 (전원 상태 미확인)")); }
            catch (UnauthorizedAccessException) { results.Add(new PcResult(pc.Id, false, "인증 실패: 인증 파일을 확인하세요.")); }
            catch (HttpRequestException) { results.Add(new PcResult(pc.Id, false, "연결 실패: IP·서비스·인증서·방화벽 확인")); }
            catch (System.Net.Sockets.SocketException) { results.Add(new PcResult(pc.Id, false, "UDP 전송 실패: 네트워크 어댑터와 브로드캐스트 주소 확인")); }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException)
            { results.Add(new PcResult(pc.Id, false, ex.Message)); }
        }
        return results;
    }
}
