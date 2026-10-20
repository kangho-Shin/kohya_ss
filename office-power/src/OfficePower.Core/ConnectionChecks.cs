using System.Collections.Concurrent;

namespace OfficePower.Core;

public static class ConnectionChecks
{
    public const int MaxConcurrency = 20;

    public static async Task<IReadOnlyList<PcResult>> RunAsync(
        IEnumerable<PcRecord> computers,
        Func<PcRecord, CancellationToken, Task<string>> check,
        IProgress<PcResult>? progress = null,
        CancellationToken cancellation = default)
    {
        var results = new ConcurrentBag<PcResult>();
        try
        {
            await Parallel.ForEachAsync(computers, new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxConcurrency,
                CancellationToken = cancellation
            }, async (pc, token) =>
            {
                PcResult result;
                try { result = new PcResult(pc.Id, true, await check(pc, token)); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                catch (OperationCanceledException) { result = new PcResult(pc.Id, false, "응답 시간 초과 (전원 상태 미확인)"); }
                catch (UnauthorizedAccessException) { result = new PcResult(pc.Id, false, "인증 실패: 인증 파일을 확인하세요."); }
                catch (HttpRequestException) { result = new PcResult(pc.Id, false, "연결 실패: IP·서비스·인증서·방화벽 확인"); }
                catch (Exception) { result = new PcResult(pc.Id, false, "연결 확인 실패: 등록 정보와 서비스를 확인하세요."); }
                results.Add(result);
                progress?.Report(result);
            });
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Return completed checks; the caller marks remaining PCs as unconfirmed.
        }
        return results.ToArray();
    }
}
