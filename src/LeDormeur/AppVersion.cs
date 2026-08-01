using System.Reflection;

namespace LeDormeur;

/// <summary>
/// Application version (defined in the .csproj via <Version></Version>).
/// </summary>
public static class AppVersion
{
    /// <summary>e.g. "1.0.0"</summary>
    public static string SemVer { get; } = ReadSemVer();

    /// <summary>e.g. "v1.0.0"</summary>
    public static string Display { get; } = $"v{SemVer}";

    private static string ReadSemVer()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            // May include a commit suffix ("1.0.0+abc123") — keep the semver part only
            var plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }

        var version = assembly.GetName().Version;
        if (version is null)
            return "0.0.0";

        // AssemblyVersion is often 1.0.0.0 — display as 1.0.0
        return version.Revision is 0 or -1
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : version.ToString();
    }
}
