using System.Diagnostics;
using System.Runtime.InteropServices;

const string appName = "Neon Bomber";

try
{
    var scriptPath = FindLauncherScript(args);
    var powerShellPath = FindPowerShell();
    var startInfo = new ProcessStartInfo
    {
        FileName = powerShellPath,
        WorkingDirectory = Path.GetDirectoryName(scriptPath)!,
        UseShellExecute = false,
        CreateNoWindow = true,
        WindowStyle = ProcessWindowStyle.Hidden,
    };

    startInfo.ArgumentList.Add("-NoLogo");
    startInfo.ArgumentList.Add("-NoProfile");
    startInfo.ArgumentList.Add("-NonInteractive");
    startInfo.ArgumentList.Add("-ExecutionPolicy");
    startInfo.ArgumentList.Add("Bypass");
    startInfo.ArgumentList.Add("-WindowStyle");
    startInfo.ArgumentList.Add("Hidden");
    startInfo.ArgumentList.Add("-File");
    startInfo.ArgumentList.Add(scriptPath);
    foreach (var argument in args.Skip(1))
    {
        startInfo.ArgumentList.Add(argument);
    }

    Process.Start(startInfo);
}
catch (Exception exception)
{
    MessageBoxW(nint.Zero, exception.Message, $"{appName} could not start", 0x10);
}

static string FindLauncherScript(string[] arguments)
{
    var candidates = new List<string>();
    if (arguments.Length > 0 && !string.IsNullOrWhiteSpace(arguments[0]))
    {
        candidates.Add(arguments[0]);
    }

    candidates.Add(Path.Combine(Environment.CurrentDirectory, "Start-NeonBomber.ps1"));
    candidates.Add(Path.Combine(AppContext.BaseDirectory, "Start-NeonBomber.ps1"));

    var scriptPath = candidates
        .Select(Path.GetFullPath)
        .FirstOrDefault(File.Exists);

    return scriptPath ?? throw new FileNotFoundException(
        "Start-NeonBomber.ps1 could not be found. Reinstall the Start Menu shortcut from the project folder.");
}

static string FindPowerShell()
{
    var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
    var candidates = new[]
    {
        Path.Combine(programFiles, "PowerShell", "7", "pwsh.exe"),
        "pwsh.exe",
    };

    return candidates.FirstOrDefault(path => path == "pwsh.exe" || File.Exists(path))!;
}

[DllImport("user32.dll", CharSet = CharSet.Unicode)]
static extern int MessageBoxW(nint window, string text, string caption, uint type);
