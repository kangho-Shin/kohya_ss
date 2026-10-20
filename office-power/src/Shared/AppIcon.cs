using System.Reflection;

namespace OfficePower.Desktop;

internal static class AppIcon
{
    internal static Icon Value { get; } = Load();

    private static Icon Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("OfficePower.App.ico")
            ?? throw new InvalidOperationException("프로그램 아이콘을 찾을 수 없습니다.");
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }
}
