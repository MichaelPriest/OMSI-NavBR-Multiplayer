using System.Globalization;
using System.Resources;

namespace NavBR.Client.Localization;

public sealed record SupportedLanguage(string CultureName, string DisplayName);

public static class LocalizationService
{
    private static readonly ResourceManager ResourceManager =
        new("NavBR.Client.Resources.Strings", typeof(LocalizationService).Assembly);

    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OMSI NavBR Multiplayer");

    private static readonly string LanguageFile = Path.Combine(SettingsDirectory, "language.txt");

    public static IReadOnlyList<SupportedLanguage> SupportedLanguages { get; } =
    [
        new("pt-BR", "Português (Brasil)"),
        new("en-US", "English"),
        new("es-ES", "Español"),
        new("de-DE", "Deutsch"),
        new("fr-FR", "Français")
    ];

    public static CultureInfo CurrentCulture { get; private set; } = CultureInfo.GetCultureInfo("en-US");

    public static void Initialize()
    {
        var savedCulture = TryReadSavedCulture();
        var cultureName = savedCulture ?? MatchSupportedCulture(CultureInfo.CurrentUICulture);
        SetCulture(cultureName, persist: false);
    }

    public static void SetCulture(string cultureName, bool persist = true)
    {
        if (!SupportedLanguages.Any(language =>
                string.Equals(language.CultureName, cultureName, StringComparison.OrdinalIgnoreCase)))
        {
            cultureName = "en-US";
        }

        var culture = CultureInfo.GetCultureInfo(cultureName);
        CurrentCulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        if (persist)
        {
            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllText(LanguageFile, culture.Name);
        }
    }

    public static string Get(string key) =>
        ResourceManager.GetString(key, CurrentCulture) ?? $"[{key}]";

    public static string Format(string key, params object[] args) =>
        string.Format(CurrentCulture, Get(key), args);

    private static string? TryReadSavedCulture()
    {
        try
        {
            if (!File.Exists(LanguageFile))
            {
                return null;
            }

            var cultureName = File.ReadAllText(LanguageFile).Trim();
            return SupportedLanguages.Any(language =>
                string.Equals(language.CultureName, cultureName, StringComparison.OrdinalIgnoreCase))
                ? cultureName
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string MatchSupportedCulture(CultureInfo systemCulture)
    {
        var exact = SupportedLanguages.FirstOrDefault(language =>
            string.Equals(language.CultureName, systemCulture.Name, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact.CultureName;
        }

        var languageMatch = SupportedLanguages.FirstOrDefault(language =>
            string.Equals(
                CultureInfo.GetCultureInfo(language.CultureName).TwoLetterISOLanguageName,
                systemCulture.TwoLetterISOLanguageName,
                StringComparison.OrdinalIgnoreCase));

        return languageMatch?.CultureName ?? "en-US";
    }
}
