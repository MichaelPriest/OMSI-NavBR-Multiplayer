using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace NavBR.Client.Omsi;

internal sealed class OmsiProfilesWindow : Window
{
    private readonly ListBox _profilesList = new();
    private readonly TextBlock _statusText = new();
    private IReadOnlyList<OmsiInstallationProfile> _profiles = Array.Empty<OmsiInstallationProfile>();

    public OmsiProfilesWindow()
    {
        Title = "OMSI — Instalações e perfis";
        Width = 820;
        Height = 560;
        MinWidth = 680;
        MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush(7, 15, 24);
        Foreground = Brushes.White;
        Icon = Application.Current?.MainWindow?.Icon;

        Content = BuildContent();
        Loaded += (_, _) => RefreshProfiles();
    }

    private UIElement BuildContent()
    {
        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        heading.Children.Add(new TextBlock
        {
            Text = "Instalações / Perfis OMSI",
            FontSize = 24,
            FontWeight = FontWeights.SemiBold
        });
        heading.Children.Add(new TextBlock
        {
            Text = "O NavBR detecta Steam, bibliotecas adicionais, registro Aerosoft e caminhos cadastrados manualmente.",
            Margin = new Thickness(0, 5, 0, 0),
            Foreground = Brush(173, 194, 215),
            TextWrapping = TextWrapping.Wrap
        });
        root.Children.Add(heading);

        _profilesList.Background = Brush(5, 12, 20);
        _profilesList.Foreground = Brushes.White;
        _profilesList.BorderBrush = Brush(42, 64, 84);
        _profilesList.BorderThickness = new Thickness(1);
        _profilesList.Padding = new Thickness(6);
        _profilesList.SelectionChanged += (_, _) => RenderStatus();
        Grid.SetRow(_profilesList, 1);
        root.Children.Add(_profilesList);

        var footer = new Grid { Margin = new Thickness(0, 16, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _statusText.Foreground = Brush(173, 194, 215);
        _statusText.TextWrapping = TextWrapping.Wrap;
        _statusText.VerticalAlignment = VerticalAlignment.Center;
        footer.Children.Add(_statusText);

        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(CreateButton("Detectar", (_, _) => RefreshProfiles(), false));
        actions.Children.Add(CreateButton("Adicionar pasta", AddFolder_Click, false));
        actions.Children.Add(CreateButton("Preferido", SetPreferred_Click, false));
        actions.Children.Add(CreateButton("Abrir pasta", OpenFolder_Click, false));
        actions.Children.Add(CreateButton("Iniciar OMSI", Launch_Click, true));
        Grid.SetColumn(actions, 1);
        footer.Children.Add(actions);

        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        return root;
    }

    private Button CreateButton(string text, RoutedEventHandler handler, bool primary)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = primary ? 118 : 96,
            Height = 34,
            Margin = new Thickness(7, 0, 0, 0),
            Padding = new Thickness(12, 4, 12, 4),
            Background = primary ? Brush(255, 132, 0) : Brush(18, 38, 56),
            Foreground = primary ? Brushes.Black : Brushes.White,
            BorderBrush = primary ? Brush(255, 165, 54) : Brush(55, 83, 106),
            BorderThickness = new Thickness(1),
            FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal
        };
        button.Click += handler;
        return button;
    }

    private void RefreshProfiles()
    {
        _profiles = OmsiInstallationProfileStore.DiscoverAndMerge();
        _profilesList.Items.Clear();

        foreach (var profile in _profiles)
        {
            var panel = new StackPanel { Margin = new Thickness(7, 6, 7, 6) };
            var title = new TextBlock
            {
                Text = profile.IsPreferred ? $"★ {profile.Name}" : profile.Name,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold
            };
            panel.Children.Add(title);
            panel.Children.Add(new TextBlock
            {
                Text = profile.InstallDirectory,
                Margin = new Thickness(0, 3, 0, 0),
                Foreground = Brush(173, 194, 215),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            });

            _profilesList.Items.Add(new ListBoxItem
            {
                Content = panel,
                Tag = profile,
                Padding = new Thickness(2),
                Margin = new Thickness(0, 0, 0, 5)
            });
        }

        if (_profilesList.Items.Count > 0)
        {
            _profilesList.SelectedIndex = 0;
        }
        else
        {
            _statusText.Text = "Nenhuma instalação do OMSI encontrada. Use “Adicionar pasta”.";
        }
    }

    private OmsiInstallationProfile? SelectedProfile =>
        (_profilesList.SelectedItem as ListBoxItem)?.Tag as OmsiInstallationProfile;

    private void RenderStatus()
    {
        var profile = SelectedProfile;
        if (profile is null)
        {
            _statusText.Text = "Selecione um perfil.";
            return;
        }

        var exeExists = File.Exists(profile.ExecutablePath);
        var running = exeExists
            ? OmsiLauncherService.FindRunningOmsiForDirectory(profile.InstallDirectory)
            : null;
        try
        {
            _statusText.Text =
                $"{(exeExists ? "OMSI ✓" : "Omsi.exe ausente")}  •  " +
                $"{(running is null ? "parado" : $"executando PID {running.Id}")}  •  " +
                $"{(profile.IsPreferred ? "perfil preferido" : "perfil disponível")}";
        }
        finally
        {
            running?.Dispose();
        }
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Selecione a pasta que contém Omsi.exe",
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var exe = Path.Combine(dialog.FolderName, "Omsi.exe");
        if (!File.Exists(exe))
        {
            MessageBox.Show(this, "Omsi.exe não foi encontrado nessa pasta.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var id = OmsiInstallationProfileStore.CreateStableId(dialog.FolderName);
        OmsiInstallationProfileStore.Upsert(new OmsiInstallationProfile(
            id,
            $"OMSI 2 — {Path.GetFileName(Path.TrimEndingDirectorySeparator(dialog.FolderName))}",
            dialog.FolderName,
            IsPreferred: _profiles.Count == 0));
        RefreshProfiles();
    }

    private void SetPreferred_Click(object sender, RoutedEventArgs e)
    {
        var profile = SelectedProfile;
        if (profile is null)
        {
            return;
        }

        OmsiInstallationProfileStore.Upsert(profile with { IsPreferred = true });
        RefreshProfiles();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var profile = SelectedProfile;
        if (profile is null || !Directory.Exists(profile.InstallDirectory))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{profile.InstallDirectory}\"",
            UseShellExecute = true
        });
    }

    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        var profile = SelectedProfile;
        if (profile is null)
        {
            return;
        }

        try
        {
            var result = OmsiLauncherService.Launch(profile);
            _statusText.Text = result.AlreadyRunning
                ? $"OMSI já estava executando (PID {result.ProcessId})."
                : $"OMSI iniciado (PID {result.ProcessId}).";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));
}
