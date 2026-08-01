namespace LeDormeur;

/// <summary>
/// Dev-only switches (not exposed in the UI). Enabled via CLI args or env vars.
/// </summary>
public static class DevOptions
{
    /// <summary>
    /// When true, run the full flow (timer, brightness, warning) but skip actual sleep.
    /// </summary>
    public static bool DryRun { get; private set; }

    /// <summary>
    /// Parses process args and environment. Call once at startup.
    /// Supported: --dry-run, -dry-run, /dry-run
    /// Env: LEDORMEUR_DRY_RUN=1|true|yes
    /// </summary>
    public static void Initialize(string[] args)
    {
        DryRun = HasFlag(args, "dry-run")
                 || IsTruthyEnv("LEDORMEUR_DRY_RUN");

#if DEBUG
        System.Diagnostics.Debug.WriteLine(
            $"[LeDormeur] DevOptions: DryRun={DryRun}");
#endif
    }

    private static bool HasFlag(string[] args, string name)
    {
        foreach (var raw in args)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var a = raw.Trim();
            if (a.StartsWith("--", StringComparison.Ordinal))
                a = a[2..];
            else if (a.StartsWith('-') || a.StartsWith('/'))
                a = a[1..];

            if (a.Equals(name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsTruthyEnv(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
               || value.Equals("true", StringComparison.OrdinalIgnoreCase)
               || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }
}
