namespace OfficePower.AgentSetup;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var mutex = new Mutex(true, @"Global\OfficePowerAgentSetup", out bool created);
        if (!created)
        {
            MessageBox.Show("설치 프로그램이 이미 실행 중입니다.", "Office Power Agent");
            return;
        }
        try { Application.Run(new SetupForm()); }
        finally { mutex.ReleaseMutex(); }
    }
}
