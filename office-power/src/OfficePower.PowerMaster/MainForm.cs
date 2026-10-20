using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using OfficePower.Core;

namespace OfficePower.PowerMaster;

public partial class MainForm : Form
{
    private readonly BindingList<PcRow> computers = [];
    private readonly string dataPath = Path.Combine(AppContext.BaseDirectory, "RemoteData", "computers.json");
    private readonly PcStore store;
    private readonly PowerClient client = new();
    private bool busy;
    private bool loadFailed;
    private CancellationTokenSource? connectionCancellation;
    private bool closeAfterCheck;

    public MainForm()
    {
        InitializeComponent();
        Icon = OfficePower.Desktop.AppIcon.Value;
        store = new PcStore(dataPath, new WindowsSecretProtector());
        computerSource.DataSource = computers;
    }

    private async void MainForm_Shown(object? sender, EventArgs e)
    {
        SetBusy(true);
        try
        {
            foreach (var pc in await store.LoadAsync()) computers.Add(new PcRow(pc));
            statusLabel.Text = $"등록된 PC {computers.Count}대 · 체크한 PC에 전원 요청을 보냅니다.";
        }
        catch (Exception)
        {
            loadFailed = true;
            statusLabel.Text = "등록 파일 읽기 실패. 기존 파일 보호를 위해 편집을 중단했습니다.";
            Info("등록 파일을 보존했습니다. 다음 파일과 접근 권한을 확인하세요:\n" + dataPath);
        }
        finally { SetBusy(false); }

        // Startup checks every saved PC without changing power-action selection.
        // Keep network failures separate from registration-file loading failures.
        if (!loadFailed) await CheckConnections(computers.ToArray());
    }

    private void SetBusy(bool value)
    {
        busy = value;
        toolbar.Enabled = !loadFailed;
        foreach (ToolStripItem item in toolbar.Items) item.Enabled = !value && !loadFailed;
        cancelStatusButton.Enabled = value && connectionCancellation is not null;
        powerPanel.Enabled = computerGrid.Enabled = !value && !loadFailed;
        UseWaitCursor = value;
    }

    private async void AddButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new RegistrationForm();
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result is null) return;
        await PersistChange(computers.Select(r => r.Record).Append(dialog.Result).ToList());
    }

    private async void EditButton_Click(object? sender, EventArgs e)
    {
        if (computerGrid.CurrentRow?.DataBoundItem is not PcRow row) { Info("수정할 행을 클릭하세요."); return; }
        using var dialog = new RegistrationForm(row.Record);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result is null) return;
        await PersistChange(computers.Select(r => r.Record.Id == row.Record.Id ? dialog.Result : r.Record).ToList());
    }

    private async void DeleteButton_Click(object? sender, EventArgs e)
    {
        if (computerGrid.CurrentRow?.DataBoundItem is not PcRow row) { Info("삭제할 행을 클릭하세요."); return; }
        if (MessageBox.Show(this, $"'{row.Name}' 등록을 삭제할까요? PC 서비스는 제거되지 않습니다.", "등록 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        await PersistChange(computers.Where(r => r != row).Select(r => r.Record).ToList());
    }

    private async Task PersistChange(List<PcRecord> list)
    {
        SetBusy(true);
        try
        {
            await store.SaveAsync(list);
            computers.Clear();
            foreach (var pc in list) computers.Add(new PcRow(pc));
            statusLabel.Text = $"등록 정보 저장 완료 · {computers.Count}대";
        }
        catch (Exception ex) { Info("저장 실패: " + ex.Message); }
        finally { SetBusy(false); }
    }

    private void SelectAllButton_Click(object? sender, EventArgs e)
    {
        bool value = computers.Any(r => !r.Selected);
        foreach (var row in computers) row.Selected = value;
    }

    private void ComputerGrid_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
    {
        if (computerGrid.IsCurrentCellDirty) computerGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
    }

    private PcRow[] SelectedRows()
    {
        computerGrid.EndEdit();
        computerSource.EndEdit();
        var rows = computers.Where(r => r.Selected).ToArray();
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
            statusLabel.Text = $"{rows.Length}대 처리 중…";
            var results = await PowerOperations.RunAsync(rows.Select(r => r.Record), operation, deferUntilLast);
            foreach (var result in results) rows.Single(r => r.Record.Id == result.Id).Status = result.Message;
            statusLabel.Text = $"처리 완료 · 성공 {results.Count(r => r.Success)}대 / 오류 {results.Count(r => !r.Success)}대 · 요청 전송과 실제 전원 변경은 다릅니다.";
        }
        catch (Exception) { Info("처리 중 오류가 발생했습니다. 목록과 연결을 확인하세요."); }
        finally { SetBusy(false); }
    }

    private async void WakeButton_Click(object? sender, EventArgs e) => await Execute(SelectedRows(), async pc =>
    {
        await WakeOnLan.SendAsync(pc.Mac, IPAddress.Parse(pc.Broadcast));
        return "켜기 요청 전송 (부팅 미확인)";
    });

    private async Task<string> GetConnectionStatusAsync(PcRecord pc, CancellationToken cancellation)
    {
        var status = await client.GetStatusAsync(pc, cancellation);
        return status.State == "shutdown-scheduled" ? "연결됨 · 종료 예약 중" : "연결됨 · 서비스 정상";
    }

    private async void StatusButton_Click(object? sender, EventArgs e) =>
        await CheckConnections(SelectedRows());

    private void CancelStatusButton_Click(object? sender, EventArgs e)
    {
        connectionCancellation?.Cancel();
        cancelStatusButton.Enabled = false;
        statusLabel.Text = "연결 확인 취소 중…";
    }

    private async Task CheckConnections(PcRow[] rows)
    {
        if (rows.Length == 0 || busy) return;
        using var cancellation = new CancellationTokenSource();
        connectionCancellation = cancellation;
        SetBusy(true);
        var byId = new Dictionary<Guid, PcRow>();
        var completed = new HashSet<Guid>();
        int online = 0, failed = 0;
        void ApplyResult(PcResult result)
        {
            if (!completed.Add(result.Id)) return;
            byId[result.Id].Status = result.Message;
            if (result.Success) online++; else failed++;
            statusLabel.Text = $"연결 확인 {completed.Count}/{rows.Length}대 · 연결 {online}대 / 실패 {failed}대";
        }
        var progress = new Progress<PcResult>(result =>
        {
            // Ignore queued callbacks after cancellation/completion or a later check.
            if (ReferenceEquals(connectionCancellation, cancellation) && !IsDisposed) ApplyResult(result);
        });
        try
        {
            foreach (var row in rows)
            {
                if (!byId.TryAdd(row.Record.Id, row)) throw new InvalidDataException("중복된 PC 등록입니다.");
                row.Status = "연결 확인 중…";
            }
            statusLabel.Text = $"{rows.Length}대 연결 확인 중…";
            var results = await ConnectionChecks.RunAsync(rows.Select(row => row.Record), GetConnectionStatusAsync, progress, cancellation.Token);
            // Progress callbacks are queued on the UI thread; also apply any still queued results.
            foreach (var result in results) ApplyResult(result);
            foreach (var row in rows.Where(row => !completed.Contains(row.Record.Id)))
                row.Status = "확인 취소 (전원 상태 미확인)";
            statusLabel.Text = $"연결 확인 {(cancellation.IsCancellationRequested ? "취소" : "완료")} · 연결 {online}대 / 실패 {failed}대 / 미확인 {rows.Length - completed.Count}대";
        }
        catch (Exception)
        {
            foreach (var row in rows.Where(row => !completed.Contains(row.Record.Id))) row.Status = "확인 실패 (전원 상태 미확인)";
            statusLabel.Text = "연결 확인 오류. 등록 정보와 네트워크를 확인하세요.";
        }
        finally
        {
            connectionCancellation = null;
            SetBusy(false);
            if (closeAfterCheck) Close();
        }
    }

    private async void ShutdownButton_Click(object? sender, EventArgs e)
    {
        var rows = SelectedRows();
        if (rows.Length == 0) return;
        if (MessageBox.Show(this, $"선택한 {rows.Length}대에 10초 후 종료를 요청할까요?\n\n" +
            string.Join("\n", rows.Take(10).Select(r => $"• {r.Name} ({r.Ip})")) +
            "\n\n작업을 먼저 저장하세요. 저장하지 않은 앱은 종료를 막을 수 있습니다.", "PC 종료 확인",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        var localAddresses = NetworkInterface.GetAllNetworkInterfaces().SelectMany(n => n.GetIPProperties().UnicastAddresses).Select(a => a.Address).ToArray();
        bool IsLocal(PcRecord pc) => IPAddress.IsLoopback(IPAddress.Parse(pc.Ip)) || localAddresses.Contains(IPAddress.Parse(pc.Ip));
        if (rows.Any(r => IsLocal(r.Record)) && MessageBox.Show(this,
            "관리 프로그램이 실행 중인 이 PC도 선택되었습니다. 정말 종료할까요?", "현재 PC 종료",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        await Execute(rows, async pc => { var reply = await client.ShutdownAsync(pc); return $"종료 예약됨 ({reply.DelaySeconds}초)"; }, IsLocal);
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (connectionCancellation is not null)
        {
            e.Cancel = true;
            closeAfterCheck = true;
            connectionCancellation.Cancel();
            return;
        }
        if (busy) { e.Cancel = true; Info("현재 요청이 끝난 뒤 창을 닫아 주세요."); }
    }
    private void Info(string text) => MessageBox.Show(this, text, "PowerMaster", MessageBoxButtons.OK, MessageBoxIcon.Information);
}
