using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using OfficePower.Core;

namespace OfficePower.Controller;

public sealed class WindowsSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("OfficePower/Registration/v1");
    public string Protect(string value) => Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser));
    public string Unprotect(string value) => Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value), Entropy, DataProtectionScope.CurrentUser));
}

public sealed class PcRow(PcRecord record) : INotifyPropertyChanged
{
    public PcRecord Record { get; } = record;
    public string Name => Record.Name;
    public string Ip => Record.Ip;
    public string Mac => Record.Mac;
    private bool selected;
    public bool Selected { get => selected; set { selected = value; Changed(); } }
    private string status = "미확인";
    public string Status { get => status; set { status = value; Changed(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
