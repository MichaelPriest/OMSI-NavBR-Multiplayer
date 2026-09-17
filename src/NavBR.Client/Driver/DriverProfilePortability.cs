using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using NavBR.Client.Localization;

namespace NavBR.Client.Driver;

internal sealed record DriverProfileExportEnvelope(
    string Schema,
    int Version,
    DateTimeOffset ExportedAtUtc,
    DriverProfileData Profile,
    IReadOnlyList<DriverTripHistoryEntry>? TripHistory = null);

internal sealed record DriverProfileImportResult(
    DriverProfileData Profile,
    IReadOnlyList<DriverTripHistoryEntry>? TripHistory,
    bool IncludesTripHistory,
    int SourceVersion);

internal static class DriverProfilePortability
{
    public const string Schema = "navbr-driver-profile";
    public const int CurrentVersion = 2;
    private const int LegacyVersion = 1;
    private const long MaxImportBytes = 4L * 1024L * 1024L;

    public static void ExportToFile(string path, DriverProfileData profile)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Invalid export path.", nameof(path));
        }

        var history = DriverTripHistoryStore.Load();
        var envelope = new DriverProfileExportEnvelope(
            Schema,
            CurrentVersion,
            DateTimeOffset.UtcNow,
            profile,
            history);

        File.WriteAllText(
            path,
            JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static DriverProfileImportResult ImportFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new FileNotFoundException("Driver profile file was not found.", path);
        }

        var info = new FileInfo(path);
        if (info.Length <= 0 || info.Length > MaxImportBytes)
        {
            throw new InvalidDataException("The selected file is not a valid NavBR driver profile.");
        }

        DriverProfileExportEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<DriverProfileExportEnvelope>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The selected file contains invalid JSON.", ex);
        }

        if (envelope is null ||
            !string.Equals(envelope.Schema, Schema, StringComparison.Ordinal) ||
            envelope.Version is not (LegacyVersion or CurrentVersion) ||
            envelope.Profile is null)
        {
            throw new InvalidDataException("Unsupported or invalid NavBR driver profile format.");
        }

        ValidateProfile(envelope.Profile);

        if (envelope.Version == LegacyVersion)
        {
            return new DriverProfileImportResult(envelope.Profile, null, false, LegacyVersion);
        }

        if (envelope.TripHistory is null)
        {
            throw new InvalidDataException("The NavBR driver profile history section is missing.");
        }

        ValidateTripHistory(envelope.TripHistory);
        return new DriverProfileImportResult(
            envelope.Profile,
            envelope.TripHistory.ToArray(),
            true,
            CurrentVersion);
    }

    public static void ApplyImport(DriverProfileImportResult import)
    {
        ArgumentNullException.ThrowIfNull(import);
        ValidateProfile(import.Profile);
        if (import.IncludesTripHistory)
        {
            if (import.TripHistory is null)
            {
                throw new InvalidDataException("The imported trip history is missing.");
            }
            ValidateTripHistory(import.TripHistory);
        }

        var previousProfile = DriverProfileStore.Load();
        var previousHistory = import.IncludesTripHistory ? DriverTripHistoryStore.Load() : null;

        try
        {
            DriverProfileStore.Save(import.Profile);
            if (import.IncludesTripHistory)
            {
                DriverTripHistoryStore.ReplaceAll(import.TripHistory!);
            }
        }
        catch
        {
            try
            {
                DriverProfileStore.Save(previousProfile);
                if (previousHistory is not null)
                {
                    DriverTripHistoryStore.ReplaceAll(previousHistory);
                }
            }
            catch
            {
                // Keep the original import failure. Rollback is best effort only.
            }
            throw;
        }
    }

    private static void ValidateProfile(DriverProfileData profile)
    {
        if (string.IsNullOrWhiteSpace(profile.DisplayName) || profile.DisplayName.Length > 80)
        {
            throw new InvalidDataException("The imported driver name is invalid.");
        }

        if (profile.CompanyName?.Length > 120)
        {
            throw new InvalidDataException("The imported company name is invalid.");
        }

        if (!double.IsFinite(profile.TotalDrivingSeconds) || profile.TotalDrivingSeconds < 0d ||
            !double.IsFinite(profile.TotalDistanceKm) || profile.TotalDistanceKm < 0d ||
            !double.IsFinite(profile.HighestSpeedKph) || profile.HighestSpeedKph < 0d || profile.HighestSpeedKph > 300d ||
            profile.Trips < 0)
        {
            throw new InvalidDataException("The imported profile contains invalid statistics.");
        }
    }

    private static void ValidateTripHistory(IReadOnlyList<DriverTripHistoryEntry> history)
    {
        if (history.Count > DriverTripHistoryStore.MaximumTrips)
        {
            throw new InvalidDataException("The imported profile contains too many trip history entries.");
        }

        foreach (var trip in history)
        {
            if (trip.EndedAtUtc < trip.StartedAtUtc ||
                !double.IsFinite(trip.DrivingSeconds) || trip.DrivingSeconds < 0d ||
                !double.IsFinite(trip.DistanceKm) || trip.DistanceKm < 0d || trip.DistanceKm > 100_000d ||
                !double.IsFinite(trip.HighestSpeedKph) || trip.HighestSpeedKph < 0d || trip.HighestSpeedKph > 300d ||
                TooLong(trip.MapName, 240) ||
                TooLong(trip.Line, 80) ||
                TooLong(trip.Route, 120) ||
                TooLong(trip.VehicleName, 260))
            {
                throw new InvalidDataException("The imported profile contains invalid trip history data.");
            }
        }
    }

    private static bool TooLong(string? value, int maximum) => value is not null && value.Length > maximum;
}

internal static class DriverProfilePortabilityInstaller
{
    private static readonly HashSet<DriverProfileWindow> Installed = new();

    public static void Attach(DriverProfileWindow window)
    {
        if (!Installed.Add(window))
        {
            return;
        }

        window.Dispatcher.BeginInvoke(new Action(() => Install(window)));
        window.Closed += (_, _) => Installed.Remove(window);
    }

    private static void Install(DriverProfileWindow window)
    {
        var saveButton = Enumerate<Button>(window).FirstOrDefault();
        if (saveButton is null || VisualTreeHelper.GetParent(saveButton) is not Grid footer)
        {
            return;
        }

        var column = Grid.GetColumn(saveButton);
        footer.Children.Remove(saveButton);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var importButton = SecondaryButton(Text("Import"));
        importButton.Click += (_, _) => Import(window);
        actions.Children.Add(importButton);

        var exportButton = SecondaryButton(Text("Export"));
        exportButton.Margin = new Thickness(8d, 0d, 8d, 0d);
        exportButton.Click += (_, _) => Export(window);
        actions.Children.Add(exportButton);

        actions.Children.Add(saveButton);
        Grid.SetColumn(actions, column);
        footer.Children.Add(actions);
    }

    private static void Export(DriverProfileWindow owner)
    {
        var profile = DriverProfileStore.Load();
        var dialog = new SaveFileDialog
        {
            Title = Text("ExportTitle"),
            Filter = "NavBR Driver Profile (*.navbr-profile.json)|*.navbr-profile.json|JSON (*.json)|*.json",
            FileName = SafeFileName(profile.DisplayName) + ".navbr-profile.json",
            AddExtension = true,
            DefaultExt = ".json",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(owner) != true)
        {
            return;
        }

        try
        {
            DriverProfilePortability.ExportToFile(dialog.FileName, profile);
            MessageBox.Show(owner, Text("ExportSuccess"), Text("ProfileTransfer"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, $"{Text("ExportError")}\n\n{ex.Message}", Text("ProfileTransfer"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void Import(DriverProfileWindow owner)
    {
        var dialog = new OpenFileDialog
        {
            Title = Text("ImportTitle"),
            Filter = "NavBR Driver Profile (*.navbr-profile.json;*.json)|*.navbr-profile.json;*.json|JSON (*.json)|*.json",
            Multiselect = false,
            CheckFileExists = true
        };

        if (dialog.ShowDialog(owner) != true)
        {
            return;
        }

        DriverProfileImportResult imported;
        try
        {
            imported = DriverProfilePortability.ImportFromFile(dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, $"{Text("ImportError")}\n\n{ex.Message}", Text("ProfileTransfer"), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var confirmationText = imported.IncludesTripHistory
            ? string.Format(
                LocalizationService.CurrentCulture,
                Text("ImportConfirmWithHistory"),
                imported.Profile.DisplayName,
                imported.TripHistory?.Count ?? 0)
            : string.Format(
                LocalizationService.CurrentCulture,
                Text("ImportConfirmLegacy"),
                imported.Profile.DisplayName);
        var confirm = MessageBox.Show(
            owner,
            confirmationText,
            Text("ProfileTransfer"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            DriverProfilePortability.ApplyImport(imported);
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, $"{Text("ImportError")}\n\n{ex.Message}", Text("ProfileTransfer"), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var boxes = Enumerate<TextBox>(owner).Take(2).ToArray();
        if (boxes.Length >= 1)
        {
            boxes[0].Text = imported.Profile.DisplayName;
        }
        if (boxes.Length >= 2)
        {
            boxes[1].Text = imported.Profile.CompanyName ?? string.Empty;
        }

        MessageBox.Show(
            owner,
            Text(imported.IncludesTripHistory ? "ImportSuccessWithHistory" : "ImportSuccessLegacy"),
            Text("ProfileTransfer"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static Button SecondaryButton(string content) => new()
    {
        Content = content,
        Height = 42d,
        MinWidth = 112d,
        Padding = new Thickness(15d, 8d, 15d, 8d),
        Background = Brush(13, 26, 36),
        Foreground = Brush(218, 230, 238),
        BorderBrush = Brush(28, 42, 51),
        BorderThickness = new Thickness(1d),
        FontWeight = FontWeights.SemiBold,
        Cursor = System.Windows.Input.Cursors.Hand
    };

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Where(character => !invalid.Contains(character)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "navbr-driver" : sanitized;
    }

    private static IEnumerable<T> Enumerate<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match)
        {
            yield return match;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            foreach (var child in Enumerate<T>(VisualTreeHelper.GetChild(root, index)))
            {
                yield return child;
            }
        }
    }

    private static string Text(string key)
    {
        var language = LocalizationService.CurrentCulture.TwoLetterISOLanguageName;
        var table = language switch
        {
            "pt" => Pt,
            "es" => Es,
            "de" => De,
            "fr" => Fr,
            _ => En
        };
        return table.TryGetValue(key, out var value) ? value : key;
    }

    private static readonly IReadOnlyDictionary<string, string> En = T(
        ("Import", "Import"), ("Export", "Export"), ("ImportTitle", "Import NavBR driver profile"),
        ("ExportTitle", "Export NavBR driver profile"), ("ProfileTransfer", "Driver profile"),
        ("ImportConfirmWithHistory", "Replace the current local profile and trip history with '{0}' ({1:N0} stored trips)?"),
        ("ImportConfirmLegacy", "Replace the current local profile with '{0}'? This older file does not contain trip history, so your current history will be kept."),
        ("ImportSuccessWithHistory", "Driver profile and trip history imported successfully."),
        ("ImportSuccessLegacy", "Driver profile imported successfully. Existing trip history was preserved."),
        ("ExportSuccess", "Driver profile and trip history exported successfully."),
        ("ImportError", "The driver profile could not be imported."), ("ExportError", "The driver profile could not be exported."));
    private static readonly IReadOnlyDictionary<string, string> Pt = T(
        ("Import", "Importar"), ("Export", "Exportar"), ("ImportTitle", "Importar perfil de motorista NavBR"),
        ("ExportTitle", "Exportar perfil de motorista NavBR"), ("ProfileTransfer", "Perfil do motorista"),
        ("ImportConfirmWithHistory", "Substituir o perfil local e o histórico de viagens atuais por '{0}' ({1:N0} viagens armazenadas)?"),
        ("ImportConfirmLegacy", "Substituir o perfil local atual por '{0}'? Este arquivo antigo não contém histórico de viagens, então o histórico atual será preservado."),
        ("ImportSuccessWithHistory", "Perfil do motorista e histórico de viagens importados com sucesso."),
        ("ImportSuccessLegacy", "Perfil do motorista importado com sucesso. O histórico de viagens atual foi preservado."),
        ("ExportSuccess", "Perfil do motorista e histórico de viagens exportados com sucesso."),
        ("ImportError", "Não foi possível importar o perfil do motorista."), ("ExportError", "Não foi possível exportar o perfil do motorista."));
    private static readonly IReadOnlyDictionary<string, string> Es = T(
        ("Import", "Importar"), ("Export", "Exportar"), ("ImportTitle", "Importar perfil de conductor NavBR"),
        ("ExportTitle", "Exportar perfil de conductor NavBR"), ("ProfileTransfer", "Perfil del conductor"),
        ("ImportConfirmWithHistory", "¿Sustituir el perfil local y el historial de viajes actuales por '{0}' ({1:N0} viajes guardados)?"),
        ("ImportConfirmLegacy", "¿Sustituir el perfil local actual por '{0}'? Este archivo antiguo no contiene historial de viajes, por lo que se conservará el historial actual."),
        ("ImportSuccessWithHistory", "Perfil del conductor e historial de viajes importados correctamente."),
        ("ImportSuccessLegacy", "Perfil del conductor importado correctamente. Se conservó el historial de viajes actual."),
        ("ExportSuccess", "Perfil del conductor e historial de viajes exportados correctamente."),
        ("ImportError", "No se pudo importar el perfil del conductor."), ("ExportError", "No se pudo exportar el perfil del conductor."));
    private static readonly IReadOnlyDictionary<string, string> De = T(
        ("Import", "Importieren"), ("Export", "Exportieren"), ("ImportTitle", "NavBR-Fahrerprofil importieren"),
        ("ExportTitle", "NavBR-Fahrerprofil exportieren"), ("ProfileTransfer", "Fahrerprofil"),
        ("ImportConfirmWithHistory", "Aktuelles lokales Profil und Fahrtenverlauf durch '{0}' ({1:N0} gespeicherte Fahrten) ersetzen?"),
        ("ImportConfirmLegacy", "Aktuelles lokales Profil durch '{0}' ersetzen? Diese ältere Datei enthält keinen Fahrtenverlauf; der aktuelle Verlauf bleibt erhalten."),
        ("ImportSuccessWithHistory", "Fahrerprofil und Fahrtenverlauf erfolgreich importiert."),
        ("ImportSuccessLegacy", "Fahrerprofil erfolgreich importiert. Der vorhandene Fahrtenverlauf wurde beibehalten."),
        ("ExportSuccess", "Fahrerprofil und Fahrtenverlauf erfolgreich exportiert."),
        ("ImportError", "Das Fahrerprofil konnte nicht importiert werden."), ("ExportError", "Das Fahrerprofil konnte nicht exportiert werden."));
    private static readonly IReadOnlyDictionary<string, string> Fr = T(
        ("Import", "Importer"), ("Export", "Exporter"), ("ImportTitle", "Importer un profil conducteur NavBR"),
        ("ExportTitle", "Exporter un profil conducteur NavBR"), ("ProfileTransfer", "Profil conducteur"),
        ("ImportConfirmWithHistory", "Remplacer le profil local et l’historique actuels par « {0} » ({1:N0} trajets enregistrés) ?"),
        ("ImportConfirmLegacy", "Remplacer le profil local actuel par « {0} » ? Cet ancien fichier ne contient pas d’historique de trajets ; l’historique actuel sera conservé."),
        ("ImportSuccessWithHistory", "Profil conducteur et historique des trajets importés avec succès."),
        ("ImportSuccessLegacy", "Profil conducteur importé avec succès. L’historique actuel a été conservé."),
        ("ExportSuccess", "Profil conducteur et historique des trajets exportés avec succès."),
        ("ImportError", "Impossible d'importer le profil conducteur."), ("ExportError", "Impossible d'exporter le profil conducteur."));

    private static IReadOnlyDictionary<string, string> T(params (string Key, string Value)[] values) =>
        values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}
