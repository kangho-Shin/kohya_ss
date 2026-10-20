namespace OfficePower.Agent;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        { Console.Error.WriteLine("이 프로그램은 Windows 서비스입니다."); return 1; }
        try
        {
            string directory = Provisioning.DefaultDirectory;
            if (args.SequenceEqual(new[] { "--initialize" }))
            {
                // Installer must create and restrict the directory before any secret is written.
                if (!Directory.Exists(directory)) throw new IOException("관리자 설치 스크립트를 먼저 실행하세요.");
                Provisioning.MigrateLegacyData(directory, Provisioning.LegacyDirectory);
                Provisioning.Initialize(directory);
                Console.WriteLine("초기화 완료. 인증 파일: " + Path.Combine(directory, "pairing.json"));
                return 0;
            }
            if (args.Length != 0) throw new ArgumentException("지원하지 않는 실행 인자입니다.");
            if (Provisioning.MigrateLegacyData(directory, Provisioning.LegacyDirectory)) Provisioning.ExportPairing(directory);
            var (settings, certificate) = Provisioning.Load(directory);
            using (certificate)
            {
                await using var app = AgentHost.Build(new AgentOptions(settings.Name, settings.Token, certificate,
                    settings.Port, System.Net.IPAddress.Any), new WindowsShutdown());
                await app.RunAsync();
            }
            return 0;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("서비스 초기화/실행 실패. 관리자 권한·네트워크·기존 설정 파일을 확인하세요. 비밀 값은 로그에 기록하지 않습니다.");
            return 1;
        }
    }
}
