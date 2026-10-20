using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using OfficePower.Core;

namespace OfficePower.Controller;

public sealed class RegistrationWindow : Window
{
    private readonly TextBox name = new(), ip = new(), mac = new(), broadcast = new() { Text = "255.255.255.255" };
    private readonly TextBlock certificate = new() { Text = "인증 파일을 가져오세요.", TextWrapping = TextWrapping.Wrap };
    private readonly Guid id;
    private string token = "", fingerprint = "";
    private int port = 18765;
    public PcRecord? Result { get; private set; }

    public RegistrationWindow(PcRecord? existing)
    {
        Title = existing is null ? "PC 등록" : "PC 등록 수정";
        Width = 520; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        id = existing?.Id ?? Guid.NewGuid();
        var panel = new StackPanel { Margin = new Thickness(24) };
        Content = panel;
        panel.Children.Add(new TextBlock { Text = "대상 PC 설치 후 생성된 pairing.json을 가져옵니다.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        var import = new Button { Content = "인증 파일 가져오기", HorizontalAlignment = HorizontalAlignment.Left };
        import.Click += Import;
        panel.Children.Add(import);
        panel.Children.Add(certificate);
        AddField(panel, "PC 이름", name); AddField(panel, "IP 주소", ip); AddField(panel, "MAC 주소 (Wake-on-LAN용 유선 랜카드)", mac); AddField(panel, "브로드캐스트 주소", broadcast);
        panel.Children.Add(new TextBlock { Text = "IP가 변경되지 않도록 공유기 DHCP 예약 또는 고정 IP를 권장합니다.", TextWrapping = TextWrapping.Wrap });
        var save = new Button { Content = "저장", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        save.Click += Save;
        panel.Children.Add(save);
        if (existing is not null)
        {
            name.Text = existing.Name; ip.Text = existing.Ip; mac.Text = existing.Mac; broadcast.Text = existing.Broadcast;
            token = existing.Token; fingerprint = existing.CertificateSha256; port = existing.Port;
            certificate.Text = "인증서 지문: " + fingerprint;
        }
    }

    private static void AddField(Panel panel, string label, TextBox field)
    { panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 10, 0, 0) }); panel.Children.Add(field); }

    private void Import(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "PC 인증 파일 (*.json)|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 65536) throw new InvalidDataException();
            var pairing = JsonSerializer.Deserialize<PairingInfo>(File.ReadAllText(dialog.FileName), JsonFormat.Options) ?? throw new InvalidDataException();
            pairing.Validate();
            name.Text = pairing.Name; ip.Text = pairing.IpAddresses[0]; mac.Text = pairing.MacAddresses[0];
            token = pairing.Token; fingerprint = pairing.CertificateSha256; port = pairing.Port;
            certificate.Text = "인증서 지문: " + fingerprint + "\n다중 랜카드가 있으면 IP와 MAC을 확인하세요.";
        }
        catch (Exception) { MessageBox.Show(this, "인증 파일을 읽을 수 없습니다. 대상 PC에서 생성한 pairing.json인지 확인하세요."); }
    }

    private void Save(object sender, RoutedEventArgs e)
    {
        try
        {
            var record = new PcRecord(id, name.Text.Trim(), ip.Text.Trim(), mac.Text.Trim(), broadcast.Text.Trim(), port, fingerprint, token);
            record.Validate();
            Result = record;
            DialogResult = true;
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "등록 정보 확인"); }
    }
}
