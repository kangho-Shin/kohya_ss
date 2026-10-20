using System.Net;
using System.Net.Sockets;

namespace OfficePower.AgentSetup;

public partial class SetupForm : Form
{
    private bool busy;

    public SetupForm()
    {
        InitializeComponent();
        Icon = OfficePower.Desktop.AppIcon.Value;
        pcLabel.Text = "컴퓨터: " + Environment.MachineName;
        try
        {
            ipLabel.Text = "IP: " + string.Join(", ", Dns.GetHostAddresses(Dns.GetHostName())
                .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip)));
        }
        catch (SocketException) { ipLabel.Text = "IP: 네트워크 연결을 확인하세요."; }
        RefreshPairing();
    }

    private async void InstallButton_Click(object? sender, EventArgs e) => await RunOperation(false);

    private async void UninstallButton_Click(object? sender, EventArgs e)
    {
        if (MessageBox.Show(this, "이 PC의 원격 전원 제어 서비스를 제거하시겠습니까?\n기존 등록 정보는 재설치를 위해 보관합니다.",
            "Agent 제거", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            await RunOperation(true);
    }

    private async Task RunOperation(bool uninstall)
    {
        busy = true;
        installButton.Enabled = uninstallButton.Enabled = saveButton.Enabled = false;
        statusLabel.Text = uninstall ? "서비스를 제거하고 있습니다…" : "설치/업데이트 중입니다. 잠시 기다려 주세요…";
        detailText.Clear();
        try
        {
            await SetupOperations.RunAsync(uninstall);
            statusLabel.Text = uninstall
                ? "제거 완료. 이 PC는 원격 종료 요청을 받지 않습니다. 등록 정보는 보관했습니다."
                : "설치 완료. PC를 켤 때 자동 실행됩니다. 등록 파일을 저장해 관리 PC로 옮기세요.";
        }
        catch (Exception exception)
        {
            statusLabel.Text = "작업을 완료하지 못했습니다. 아래 오류를 확인하세요.";
            detailText.Text = exception.Message;
        }
        finally
        {
            busy = false;
            installButton.Enabled = uninstallButton.Enabled = true;
            RefreshPairing();
        }
    }

    private void RefreshPairing()
    {
        saveButton.Enabled = !busy && File.Exists(SetupOperations.PairingFile);
        fileLabel.Text = "등록 파일: " + SetupOperations.PairingFile;
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Title = "관리 PC로 가져갈 등록 파일 저장", Filter = "PC 등록 파일 (*.json)|*.json",
            FileName = Path.GetFileName(SetupOperations.PairingFile), AddExtension = true, DefaultExt = "json"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            if (!string.Equals(Path.GetFullPath(dialog.FileName), Path.GetFullPath(SetupOperations.PairingFile), StringComparison.OrdinalIgnoreCase))
                File.Copy(SetupOperations.PairingFile, dialog.FileName, true);
            MessageBox.Show(this, "저장했습니다. 관리 PC의 Controller → PC 등록에서 가져오세요.\n이 파일은 전원 제어 권한을 포함하므로 안전하게 전달하세요.", "등록 파일 저장");
        }
        catch (Exception) { MessageBox.Show(this, "저장하지 못했습니다. 저장할 폴더와 파일 권한을 확인하세요.", "저장 실패"); }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (busy) { e.Cancel = true; return; }
        base.OnFormClosing(e);
    }
}
