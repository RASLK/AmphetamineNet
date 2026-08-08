using System.Diagnostics;
using System.Text;

namespace AmphetamineNet.Services;

/// <summary>
/// Like Amphetamine's Power Protect: installs a sudoers entry once → pmset then runs without a password.
/// </summary>
public static class PowerProtect
{
    public const string SudoersPath = "/etc/sudoers.d/amphetamine-net";

    public static bool IsSudoersInstalled()
    {
        try
        {
            return File.Exists(SudoersPath);
        }
        catch
        {
            return false;
        }
    }

    public static bool TryPmsetDisableSleep(bool disable, out string error)
    {
        error = "";
        var value = disable ? "1" : "0";
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "/usr/bin/sudo",
                ArgumentList = { "-n", "/usr/bin/pmset", "-a", "disablesleep", value },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p is null)
            {
                error = "sudo failed to start";
                return false;
            }

            error = p.StandardError.ReadToEnd().Trim();
            p.WaitForExit(8000);
            return p.ExitCode == 0;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static void Install(Action? prepareUi)
    {
        prepareUi?.Invoke();

        var user = Environment.UserName;
        if (string.IsNullOrWhiteSpace(user) || user.Any(c => c is '"' or '\'' or '\\' or ';' or '$' or '`'))
            throw new InvalidOperationException($"Invalid username: {user}");

        var body =
            $"# AmphetamineNet Power Protect — passwordless pmset disablesleep\n" +
            $"Cmnd_Alias AMPHETAMINE_NET_PMSET = /usr/bin/pmset -a disablesleep 1, /usr/bin/pmset -a disablesleep 0\n" +
            $"{user} ALL=(root) NOPASSWD: AMPHETAMINE_NET_PMSET\n";

        var sudoersTmp = Path.Combine(Path.GetTempPath(), $"amphetamine-net-sudoers-{Guid.NewGuid():N}");
        var scriptTmp = Path.Combine(Path.GetTempPath(), $"amphetamine-net-install-{Guid.NewGuid():N}.sh");
        File.WriteAllText(sudoersTmp, body, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.WriteAllText(scriptTmp, $$"""
            #!/bin/bash
            set -euo pipefail
            cp "{{sudoersTmp}}" "{{SudoersPath}}"
            chmod 440 "{{SudoersPath}}"
            /usr/sbin/visudo -cf "{{SudoersPath}}"
            """);
        try
        {
            var chmod = new ProcessStartInfo
            {
                FileName = "/bin/chmod",
                ArgumentList = { "+x", scriptTmp },
                UseShellExecute = false,
            };
            Process.Start(chmod)?.WaitForExit(3000);

            var script = $"""
                tell application "System Events" to activate
                delay 0.2
                do shell script "{EscapeForAppleScript(scriptTmp)}" with administrator privileges
                """;

            var psi = new ProcessStartInfo
            {
                FileName = "/usr/bin/osascript",
                ArgumentList = { "-e", script },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi)
                          ?? throw new InvalidOperationException("osascript failed to start");
            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            p.WaitForExit(180_000);
            if (p.ExitCode != 0)
                throw new InvalidOperationException($"Power Protect installation failed: {stderr} {stdout}".Trim());

            if (!IsSudoersInstalled())
                throw new InvalidOperationException("The sudoers file did not appear after installation.");

            if (!TryPmsetDisableSleep(false, out var testErr))
                throw new InvalidOperationException($"sudoers installed, but sudo -n pmset isn't working: {testErr}");
        }
        finally
        {
            try { File.Delete(sudoersTmp); } catch { /* ignore */ }
            try { File.Delete(scriptTmp); } catch { /* ignore */ }
        }
    }

    public static void Uninstall(Action? prepareUi)
    {
        prepareUi?.Invoke();
        var script = $"""
            tell application "System Events" to activate
            delay 0.2
            do shell script "rm -f {SudoersPath}" with administrator privileges
            """;
        var psi = new ProcessStartInfo
        {
            FileName = "/usr/bin/osascript",
            ArgumentList = { "-e", script },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi)
                      ?? throw new InvalidOperationException("osascript failed to start");
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit(180_000);
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"Removing Power Protect failed: {stderr}");
    }

    private static string EscapeForAppleScript(string s)
    {
        var sb = new StringBuilder(s.Length + 8);
        foreach (var c in s)
        {
            if (c is '\\' or '"')
                sb.Append('\\');
            sb.Append(c);
        }

        return sb.ToString();
    }

}
