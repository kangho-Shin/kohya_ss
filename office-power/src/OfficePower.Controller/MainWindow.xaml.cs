using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using OfficePower.Core;

namespace OfficePower.Controller;

public partial class MainWindow : Window
{
    public ObservableCollection<PcRow> Computers { get; } = [];
    private readonly PcStore store = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OfficePower", "computers.json"), new WindowsSecretProtector());
    private readonly PowerClient client = new();
    private bool busy;
    private bool loadFailed;

    public MainWindow() { InitializeComponent(); DataContext = this; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        try
        {
            foreach (var pc in await store.LoadAsync()) Computers.Add(new PcRow(pc));
            StatusText.Text = $"등록된 PC {Computers.Count}대 · 체크한 PC에 전원 요청을 보냅니다.";
        }
        catch (Exception)
        {
            loadFailed = true;
            StatusText.Text = "등록 파일 읽기 실패. 기존 파일 보호를 위해 편집을 중단했습니다. %LOCALAPPDATA%\\OfficePower\\computers.json을 확인하세요.";
            MessageBox.Show(this, StatusText.Text, "등록 정보 보호", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { SetBusy(false); }
    }

    private void SetBusy(bool value)
    {
        busy = value;
        Tools.IsEnabled = PowerBar.IsEnabled = ComputerGrid.IsEnabled = !value && !loadFailed;
    }

    private async void AddClick(object sender, RoutedEventArgs e)
    {
        var dialog = new RegistrationWindow(null) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;
        await PersistChange(Computers.Select(r => r.Record).Append(dialog.Result).ToList());
    }

    private async void EditClick(object sender, RoutedEventArgs e)
    {
        if (ComputerGrid.SelectedItem is not PcRow row) { Info("수정할 행을 클릭하세요."); return; }
        var dialog = new RegistrationWindow(row.Record) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;
        await PersistChange(Computers.Select(r => r.Record.Id == row.Record.Id ? dialog.Result : r.Record).ToList());
    }

    private async void DeleteClick(object sender, RoutedEventArgs e)
    {
        if (ComputerGrid.SelectedItem is not PcRow row) { Info("삭제할 행을 클릭하세요."); return; }
        if (MessageBox.Show(this, $"'{row.Name}' 등록을 삭제할까요? PC 서비스는 제거되지 않습니다.", "등록 삭제", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await PersistChange(Computers.Where(r => r != row).Select(r => r.Record).ToList());
    }

    private async Task PersistChange(List<PcRecord> list)
    {
        SetBusy(true);
        try
        {
            await store.SaveAsync(list);
            Computers.Clear();
            foreach (var pc in list) Computers.Add(new PcRow(pc));
            StatusText.Text = $"등록 정보 저장 완료 · {Computers.Count}대";
        }
        catch (Exception ex) { Info("저장 실패: " + ex.Message); }
        finally { SetBusy(false); }
    }

    private void SelectAllClick(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox check) foreach (var row in Computers) row.Selected = check.IsChecked == true;
    }

    private PcRow[] SelectedRows()
    {
        ComputerGrid.CommitEdit();
        var rows = Computers.Where(r => r.Selected).ToArray();
        if (rows.Length == 0) Info("왼쪽 체크박스에서 PC를 선택하세요.");
        return rows;
    }

    private async Task Execute(PcRow[] rows, Func<PcRecord, Task<string>> operation, Func<PcRecord, bool>? deferUntilLast = null)
    {
        if (rows.Length == 0 || busy) return;
        SetBusy(true);
        try
        {
            foreach (var row in rows) row.Status = "처리 중…";
            StatusText.Text = $"{rows.Length}대 처리 중…";
            var results = await PowerOperations.RunAsync(rows.Select(r => r.Record), operation, deferUntilLast);
            foreach (var result in results) rows.Single(r => r.Record.Id == result.Id).Status = result.Message;
            StatusText.Text = $"처리 완료 · 성공 {results.Count(r => r.Success)}대 / 오류 {results.Count(r => !r.Success)}대. 요청 전송은 실제 전원 변경 확인과 다릅니다.";
        }
        catch (Exception) { Info("처리 중 오류가 발생했습니다. 목록과 연결을 확인하세요."); }
        finally { SetBusy(false); }
    }

    private async void WakeClick(object sender, RoutedEventArgs e) => await Execute(SelectedRows(), async pc =>
    {
        await WakeOnLan.SendAsync(pc.Mac, IPAddress.Parse(pc.Broadcast));
        return "켜기 요청 전송 (부팅 미확인)";
    });

    private async void StatusClick(object sender, RoutedEventArgs e) => await Execute(SelectedRows(), async pc =>
    {
        var status = await client.GetStatusAsync(pc);
        return status.State == "shutdown-scheduled" ? "연결됨 · 종료 예약 중" : "연결됨 · 서비스 정상";
    });

    private async void ShutdownClick(object sender, RoutedEventArgs e)
    {
        var rows = SelectedRows();
        if (rows.Length == 0) return;
        if (MessageBox.Show(this, $"선택한 {rows.Length}대에 30초 후 종료를 요청할까요?\n\n" +
            string.Join("\n", rows.Take(10).Select(r => $"• {r.Name} ({r.Ip})")) +
            "\n\n작업을 먼저 저장하세요. 저장하지 않은 앱은 종료를 막을 수 있습니다.", "PC 종료 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        var localAddresses = NetworkInterface.GetAllNetworkInterfaces().SelectMany(n => n.GetIPProperties().UnicastAddresses).Select(a => a.Address).ToArray();
        if (rows.Any(r => IPAddress.IsLoopback(IPAddress.Parse(r.Ip)) || localAddresses.Contains(IPAddress.Parse(r.Ip))) &&
            MessageBox.Show(this, "관리 프로그램이 실행 중인 이 PC도 선택되었습니다. 정말 종료할까요?", "현재 PC 종료", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        await Execute(rows, async pc => { await client.ShutdownAsync(pc); return "종료 예약됨 (30초)"; },
            pc => IPAddress.IsLoopback(IPAddress.Parse(pc.Ip)) || localAddresses.Contains(IPAddress.Parse(pc.Ip)));
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (busy) { e.Cancel = true; Info("현재 요청이 끝난 뒤 창을 닫아 주세요."); }
    }
    private void Info(string text) => MessageBox.Show(this, text, "사무실 PC 전원 관리", MessageBoxButton.OK, MessageBoxImage.Information);
}
