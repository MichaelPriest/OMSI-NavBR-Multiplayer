using System.Reflection;

namespace NavBR.Client.Windows;

internal static class NavBRVersionInfo
{
    public static string Current { get; } = ResolveCurrent();

    public static string Display => $"v{Current}";

    private static string ResolveCurrent()
    {
        var assembly = typeof(NavBRVersionInfo).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            var metadataIndex = informational.IndexOf('+');
            return metadataIndex >= 0
                ? informational[..metadataIndex]
                : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.3.0";
    }
}
