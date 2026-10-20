using System.Text.Json;
using OfficePower.Core;

namespace OfficePower.Controller;

public partial class RegistrationForm : Form
{
    private readonly Guid id;
    private string token = "", fingerprint = "";
    private int port = 18765;
    public PcRecord? Result { get; private set; }

    public RegistrationForm() : this(null) { }

    public RegistrationForm(PcRecord? existing)
    {
        InitializeComponent();
        id = existing?.Id ?? Guid.NewGuid();
        if (existing is not null)
        {
            Text = "PC 등록 수정";
            nameText.Text = existing.Name; ipText.Text = existing.Ip; macText.Text = existing.Mac;
            broadcastText.Text = existing.Broadcast; token = existing.Token;
            fingerprint = existing.CertificateSha256; port = existing.Port;
            certificateLabel.Text = "인증서 지문: " + fingerprint;
        }
    }

    private void ImportButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Filter = "PC 인증 파일 (*.json)|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 65536) throw new InvalidDataException();
            var pairing = JsonSerializer.Deserialize<PairingInfo>(File.ReadAllText(dialog.FileName), JsonFormat.Options) ?? throw new InvalidDataException();
            pairing.Validate();
            nameText.Text = pairing.Name; ipText.Text = pairing.IpAddresses[0]; macText.Text = pairing.MacAddresses[0];
            token = pairing.Token; fingerprint = pairing.CertificateSha256; port = pairing.Port;
            certificateLabel.Text = "인증서 지문: " + fingerprint + "\n다중 랜카드가 있으면 IP와 MAC을 확인하세요.";
        }
        catch (Exception) { MessageBox.Show(this, "대상 PC에서 생성한 pairing.json인지 확인하세요.", "인증 파일 읽기 실패"); }
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        try
        {
            var record = new PcRecord(id, nameText.Text.Trim(), ipText.Text.Trim(), macText.Text.Trim(), broadcastText.Text.Trim(), port, fingerprint, token);
            record.Validate();
            Result = record;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "등록 정보 확인"); }
    }
}
