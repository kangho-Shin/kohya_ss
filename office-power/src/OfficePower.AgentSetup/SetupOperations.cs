using System.Diagnostics;
using System.Reflection;

namespace OfficePower.AgentSetup;

internal static class SetupOperations
{
    internal static string InstallDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "OfficePowerAgent");
    internal static string PairingFile => Path.Combine(InstallDirectory, Environment.MachineName + "-pairing.json");

    internal static async Task RunAsync(bool uninstall)
    {
        // Program Files prevents ordinary users from replacing elevated payloads.
        string staging = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            ".OfficePowerSetup-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(staging, "Agent"));
            string scriptName = uninstall ? "Uninstall-Agent.ps1" : "Install-Agent.ps1";
            Extract(scriptName, Path.Combine(staging, scriptName));
            if (!uninstall) Extract("OfficePower.Agent.exe", Path.Combine(staging, "Agent", "OfficePower.Agent.exe"));
            var start = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                    @"WindowsPowerShell\v1.0\powershell.exe"),
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (string argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(staging, scriptName) })
                start.ArgumentList.Add(argument);
            if (!uninstall)
            {
                start.ArgumentList.Add("-AgentSource");
                start.ArgumentList.Add(Path.Combine(staging, "Agent"));
            }
            using var process = Process.Start(start) ?? throw new IOException("설치 작업을 시작하지 못했습니다.");
            // Drain both pipes concurrently; never include credential file contents.
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> errors = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            await Task.WhenAll(output, errors);
            if (process.ExitCode != 0)
                throw new IOException("설치 작업 실패 (코드 " + process.ExitCode + ")\r\n" + output.Result + "\r\n" + errors.Result);
            if (!uninstall && !File.Exists(PairingFile))
                throw new IOException("서비스 설치 후 등록 파일을 찾지 못했습니다. 다시 설치/업데이트를 실행하세요.");
        }
        finally
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
            catch (IOException) { /* A locked staging file must not hide the installation result. */ }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static void Extract(string name, string destination)
    {
        using var source = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new IOException("설치 파일에 필요한 항목이 없습니다: " + name);
        using var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        source.CopyTo(target);
    }
}
