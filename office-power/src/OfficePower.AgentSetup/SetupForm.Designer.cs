namespace OfficePower.AgentSetup;

public partial class SetupForm
{
    private Label pcLabel = null!;
    private Label ipLabel = null!;
    private Label statusLabel = null!;
    private Label fileLabel = null!;
    private Button installButton = null!;
    private Button uninstallButton = null!;
    private Button saveButton = null!;
    private TextBox detailText = null!;

    private void InitializeComponent()
    {
        Text = "Office Power Agent 설치";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(700, 475);
        MinimumSize = new Size(716, 514);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("맑은 고딕", 10F);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 8 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "이 PC를 원격으로 켜고 끌 수 있도록 설정합니다.", Dock = DockStyle.Fill, AutoSize = true });
        pcLabel = new Label { Dock = DockStyle.Fill, AutoSize = true };
        ipLabel = new Label { Dock = DockStyle.Fill, AutoSize = true };
        layout.Controls.Add(pcLabel);
        layout.Controls.Add(ipLabel);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill };
        installButton = new Button { Text = "설치 / 업데이트", AutoSize = true, Height = 38, Padding = new Padding(8) };
        uninstallButton = new Button { Text = "서비스 제거", AutoSize = true, Height = 38, Padding = new Padding(8) };
        installButton.Click += InstallButton_Click;
        uninstallButton.Click += UninstallButton_Click;
        buttons.Controls.AddRange(new Control[] { installButton, uninstallButton });
        layout.Controls.Add(buttons);
        statusLabel = new Label { Text = "처음 설치하거나 기존 버전을 바꾸려면 ‘설치 / 업데이트’를 누르세요.", Dock = DockStyle.Fill, AutoSize = true };
        layout.Controls.Add(statusLabel);
        saveButton = new Button { Text = "등록 파일 저장…", AutoSize = true, Dock = DockStyle.Left, Padding = new Padding(8) };
        saveButton.Click += SaveButton_Click;
        layout.Controls.Add(saveButton);
        fileLabel = new Label { Dock = DockStyle.Fill, AutoSize = true, AutoEllipsis = true };
        layout.Controls.Add(fileLabel);
        detailText = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, WordWrap = false };
        layout.Controls.Add(detailText);
        Controls.Add(layout);
    }
}
