using System.Diagnostics;
using System.Reflection;
using Avalonia;

namespace AmphetamineNet;

sealed class Program
{
    private const string CvFixEnv = "AMPHETAMINE_NET_CVFIX";
    private const string DylibName = "libcvdisplaylink_fix.dylib";

    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log($"Unhandled: {e.ExceptionObject}");

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log($"UnobservedTask: {e.Exception}");
            e.SetObserved();
        };

        try
        {
            if (OperatingSystem.IsMacOS() && !RelaunchWithCvDisplayLinkFixIfNeeded(args))
                return;

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log($"Main FATAL: {ex}");
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new MacOSPlatformOptions { ShowInDock = false })
            .WithInterFont()
            .LogToTrace();

    /// <summary>
    /// macOS 26: Avalonia.Native RenderTimer crashes on CVDisplayLinkCreateWithActiveCGDisplays (-6661).
    /// We relaunch ourselves with DYLD_INSERT_LIBRARIES pointing at the interpose dylib.
    /// </summary>
    /// <returns>false = this process should terminate (the child has already been started).</returns>
    private static bool RelaunchWithCvDisplayLinkFixIfNeeded(string[] args)
    {
        if (Environment.GetEnvironmentVariable(CvFixEnv) == "1")
            return true;

        var existingInsert = Environment.GetEnvironmentVariable("DYLD_INSERT_LIBRARIES");
        if (existingInsert?.Contains(DylibName, StringComparison.Ordinal) == true)
        {
            Environment.SetEnvironmentVariable(CvFixEnv, "1");
            return true;
        }

        var dylib = FindCvFixDylib();
        if (dylib is null)
        {
            Log($"WARN: {DylibName} not found — starting without the CVDisplayLink fix (crash -6661 possible)");
            return true;
        }

        var exe = ResolveExecutablePath();
        if (exe is null)
        {
            Log("WARN: could not resolve executable path for relaunch");
            return true;
        }

        Log($"Relaunch with DYLD_INSERT_LIBRARIES={dylib}");

        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory,
        };

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var key = entry.Key?.ToString();
            if (key is null || psi.Environment.ContainsKey(key))
                continue;
            psi.Environment[key] = entry.Value?.ToString() ?? "";
        }

        psi.Environment["DYLD_INSERT_LIBRARIES"] = dylib;
        psi.Environment[CvFixEnv] = "1";

        using var child = Process.Start(psi);
        if (child is null)
        {
            Log("ERROR: failed to start child process");
            return true;
        }

        child.WaitForExit();
        Environment.Exit(child.ExitCode);
        return false;
    }

    private static string? FindCvFixDylib()
    {
        var candidates = new List<string>();

        var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrEmpty(asmDir))
            candidates.Add(Path.Combine(asmDir, DylibName));

        var processDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (!string.IsNullOrEmpty(processDir))
            candidates.Add(Path.Combine(processDir, DylibName));

        // Dev: Native/ next to the project
        if (!string.IsNullOrEmpty(asmDir))
        {
            candidates.Add(Path.GetFullPath(Path.Combine(asmDir, "..", "..", "..", "..", "Native", DylibName)));
            candidates.Add(Path.GetFullPath(Path.Combine(asmDir, "..", "..", "..", "Native", DylibName)));
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? ResolveExecutablePath()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(processPath))
            return null;

        // `dotnet run` → ProcessPath = dotnet; we need the built binary next to the dll
        var fileName = Path.GetFileName(processPath);
        if (fileName.Equals("dotnet", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(asmDir))
                return null;

            var app = Path.Combine(asmDir, "AmphetamineNet");
            return File.Exists(app) ? app : null;
        }

        return processPath;
    }

    private static void Log(string message) => AppLog.Write(message);
}
