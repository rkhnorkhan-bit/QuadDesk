using QuadDesk.Services;
namespace QuadDesk;
internal static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, @"Local\QuadDesk-0.1", out bool created);
        if (!created) { MessageBox.Show("QuadDesk уже запущен. Откройте его через значок в трее.", "QuadDesk"); return; }
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => { Log.Write("UI exception: " + e.Exception.GetType().Name); MessageBox.Show("Ошибка QuadDesk. Завершите приложение и проверьте logs/quad-desk.log.\n" + e.Exception.Message, "QuadDesk", MessageBoxButtons.OK, MessageBoxIcon.Error); Application.Exit(); };
        try { using var context = new QuadDeskApplicationContext(); Application.Run(context); }
        catch (Exception ex) { Log.Write("Startup error: " + ex.GetType().Name); MessageBox.Show("QuadDesk не запущен. Распакуйте архив в доступную для записи папку.\n" + ex.Message, "QuadDesk", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
