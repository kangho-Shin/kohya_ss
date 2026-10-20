using OfficePower.Core;

namespace OfficePower.Controller;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.SequenceEqual(new[] { "--smoke-test" }))
        {
            try
            {
                var protector = new WindowsSecretProtector();
                string token = Tokens.Create();
                if (protector.Unprotect(protector.Protect(token)) != token) return 1;
                using var main = new MainForm();
                main.Show();
                Application.DoEvents();
                using var registration = new RegistrationForm();
                registration.Show(main);
                Application.DoEvents();
                registration.Close();
                main.Close();
                return 0;
            }
            catch { return 1; }
        }
        Application.Run(new MainForm());
        return 0;
    }
}
