#nullable enable
namespace OfficePower.Controller;

partial class RegistrationForm
{
    private System.ComponentModel.IContainer? components;
    private TextBox nameText = null!, ipText = null!, macText = null!, broadcastText = null!;
    private Label certificateLabel = null!;
    private Label title = null!;
    private Button importButton = null!;
    private Label nameLabel = null!;
    private Label ipLabel = null!;
    private Label macLabel = null!;
    private Label broadcastLabel = null!;
    private Label hint = null!;
    private Button saveButton = null!;
    private Button cancelButton = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        nameText = new TextBox();
        ipText = new TextBox();
        macText = new TextBox();
        broadcastText = new TextBox();
        certificateLabel = new Label();
        title = new Label();
        importButton = new Button();
        nameLabel = new Label();
        ipLabel = new Label();
        macLabel = new Label();
        broadcastLabel = new Label();
        hint = new Label();
        saveButton = new Button();
        cancelButton = new Button();
        SuspendLayout();
        title.Text = "대상 PC의 컴퓨터이름-pairing.json을 가져와 등록하세요.";
        title.Name = "title";
        title.Location = new Point(20, 18);
        title.Size = new Size(480, 24);
        importButton.Text = "인증 파일 가져오기";
        importButton.Name = "importButton";
        importButton.Location = new Point(20, 50);
        importButton.Size = new Size(170, 34);
        importButton.Click += ImportButton_Click;
        certificateLabel.Text = "인증 파일을 가져오세요.";
        certificateLabel.Name = "certificateLabel";
        certificateLabel.Location = new Point(20, 96);
        certificateLabel.Size = new Size(480, 65);
        nameLabel.Text = "PC 이름";
        nameLabel.Name = "nameLabel";
        nameText.Name = "nameText";
        nameLabel.Location = new Point(20, 176);
        nameLabel.Size = new Size(140, 24);
        nameText.Location = new Point(165, 173);
        nameText.Size = new Size(335, 28);
        ipLabel.Text = "IP 주소";
        ipLabel.Name = "ipLabel";
        ipText.Name = "ipText";
        ipLabel.Location = new Point(20, 220);
        ipLabel.Size = new Size(140, 24);
        ipText.Location = new Point(165, 217);
        ipText.Size = new Size(335, 28);
        macLabel.Text = "유선 랜카드 MAC";
        macLabel.Name = "macLabel";
        macText.Name = "macText";
        macLabel.Location = new Point(20, 264);
        macLabel.Size = new Size(140, 24);
        macText.Location = new Point(165, 261);
        macText.Size = new Size(335, 28);
        broadcastLabel.Text = "브로드캐스트";
        broadcastLabel.Name = "broadcastLabel";
        broadcastText.Name = "broadcastText";
        broadcastLabel.Location = new Point(20, 308);
        broadcastLabel.Size = new Size(140, 24);
        broadcastText.Location = new Point(165, 305);
        broadcastText.Size = new Size(335, 28);
        broadcastText.Text = "255.255.255.255";
        hint.Text = "IP 변경 방지를 위해 DHCP 주소 예약 또는 고정 IP를 권장합니다.";
        hint.Name = "hint";
        hint.Location = new Point(20, 352);
        hint.Size = new Size(480, 44);
        saveButton.Text = "저장";
        saveButton.Name = "saveButton";
        saveButton.Location = new Point(294, 408);
        saveButton.Size = new Size(98, 36);
        saveButton.Click += SaveButton_Click;
        cancelButton.Text = "취소";
        cancelButton.Name = "cancelButton";
        cancelButton.Location = new Point(402, 408);
        cancelButton.Size = new Size(98, 36);
        cancelButton.DialogResult = DialogResult.Cancel;
        AcceptButton = saveButton;
        CancelButton = cancelButton;
        Controls.AddRange(new Control[] { title, importButton, certificateLabel, nameLabel, nameText, ipLabel, ipText, macLabel, macText, broadcastLabel, broadcastText, hint, saveButton, cancelButton });
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(525, 466);
        Font = new Font("Malgun Gothic", 9F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "PC 등록";
        Name = "RegistrationForm";
        ResumeLayout(false);
        PerformLayout();
    }
}
