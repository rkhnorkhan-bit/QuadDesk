using System.Diagnostics;

var options = Options.Parse(args);

try
{
    if (!options.Valid)
    {
        Console.Error.WriteLine("Usage: QuadDesk.Updater --parent-pid <pid> --installer <setup.exe> --app-exe <QuadDesk.exe> [--silent] [--launch]");
        return 2;
    }

    if (options.ParentPid > 0)
    {
        try
        {
            using var parent = Process.GetProcessById(options.ParentPid);
            parent.WaitForExit(30000);
        }
        catch (ArgumentException) { }
        catch (InvalidOperationException) { }
    }

    if (!File.Exists(options.InstallerPath))
        throw new FileNotFoundException("Installer was not found.", options.InstallerPath);

    string installerArgs = options.Silent
        ? "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-"
        : string.Empty;

    using var installer = Process.Start(new ProcessStartInfo
    {
        FileName = options.InstallerPath,
        Arguments = installerArgs,
        UseShellExecute = true,
        WorkingDirectory = Path.GetDirectoryName(options.InstallerPath) ?? Environment.CurrentDirectory
    });

    if (installer is null)
        throw new InvalidOperationException("Installer process did not start.");

    installer.WaitForExit();
    if (installer.ExitCode != 0)
        return installer.ExitCode;

    if (options.LaunchAfterInstall && File.Exists(options.AppExePath))
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = options.AppExePath,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(options.AppExePath) ?? Environment.CurrentDirectory
        });
    }

    return 0;
}
catch (Exception ex)
{
    TryLog(ex);
    try
    {
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "QuadDesk.Updater.last-error.txt"), ex.ToString());
    }
    catch { }
    return 1;
}

static void TryLog(Exception ex)
{
    try
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuadDesk", "logs");
        Directory.CreateDirectory(dir);
        File.AppendAllText(Path.Combine(dir, "updater.log"), $"{DateTimeOffset.Now:O} {ex}{Environment.NewLine}");
    }
    catch { }
}

sealed class Options
{
    public int ParentPid { get; private init; }
    public string InstallerPath { get; private init; } = string.Empty;
    public string AppExePath { get; private init; } = string.Empty;
    public bool Silent { get; private init; }
    public bool LaunchAfterInstall { get; private init; }
    public bool Valid => !string.IsNullOrWhiteSpace(InstallerPath) && !string.IsNullOrWhiteSpace(AppExePath);

    public static Options Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
                continue;

            string key = arg[2..];
            if (key is "silent" or "launch")
            {
                flags.Add(key);
                continue;
            }

            if (i + 1 < args.Length)
                values[key] = args[++i];
        }

        _ = int.TryParse(values.GetValueOrDefault("parent-pid"), out int pid);

        return new Options
        {
            ParentPid = pid,
            InstallerPath = values.GetValueOrDefault("installer") ?? string.Empty,
            AppExePath = values.GetValueOrDefault("app-exe") ?? string.Empty,
            Silent = flags.Contains("silent"),
            LaunchAfterInstall = flags.Contains("launch")
        };
    }
}
