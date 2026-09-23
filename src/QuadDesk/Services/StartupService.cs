using System.Runtime.InteropServices;
namespace QuadDesk.Services;
internal static class StartupService
{
    static string Shortcut => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "QuadDesk.lnk");
    public static bool Enabled => File.Exists(Shortcut);
    public static void Set(bool enabled)
    {
        if (!enabled)
        {
            if (!File.Exists(Shortcut)) return;
            WithShortcut(link =>
            {
                string target = link.TargetPath;
                if (!string.Equals(target, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Ярлык QuadDesk принадлежит другой копии приложения. Удалите его вручную через shell:startup.");
            });
            File.Delete(Shortcut); return;
        }
        if (File.Exists(Shortcut))
        {
            WithShortcut(link => { string target = link.TargetPath; if (!string.Equals(target, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Существующий ярлык указывает на другую копию QuadDesk."); });
        }
        WithShortcut(link => { link.TargetPath = Environment.ProcessPath!; link.WorkingDirectory = AppContext.BaseDirectory; link.Description = "QuadDesk — зоны рабочего стола"; link.Save(); });
    }
    static void WithShortcut(Action<dynamic> action)
    {
        object? shell = null, shortcut = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("WScript.Shell недоступен."));
            shortcut = ((dynamic)shell!).CreateShortcut(Shortcut); action(shortcut);
        }
        finally { if (shortcut is not null) Marshal.FinalReleaseComObject(shortcut); if (shell is not null) Marshal.FinalReleaseComObject(shell); }
    }
}
