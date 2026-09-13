using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace LeDormeur.Services;

/// <summary>
/// Runs <c>powercfg</c> without a console window and returns OEM-decoded output.
/// </summary>
internal static class PowerCfg
{
    static PowerCfg()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static bool TryRun(string arguments, out int exitCode, out string output)
    {
        exitCode = -1;
        output = string.Empty;

        try
        {
            var oem = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = oem,
                StandardErrorEncoding = oem
            });

            if (process is null)
                return false;

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(12000))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return false;
            }

            output = stdoutTask.GetAwaiter().GetResult();
            exitCode = process.ExitCode;
            return exitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static IReadOnlyList<string> QueryLines(string arguments)
    {
        if (!TryRun(arguments, out _, out var output) || string.IsNullOrWhiteSpace(output))
            return [];

        var lines = new List<string>();
        foreach (var raw in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0)
                continue;
            if (line.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                continue;
            lines.Add(line);
        }

        return lines;
    }
}
