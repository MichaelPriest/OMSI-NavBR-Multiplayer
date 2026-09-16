using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Localization;

namespace NavBR.Client.Overlay;

internal static class HudCustomizationWindowInstaller
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(
            typeof(HudOverlayWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnHudLoaded));
    }

    private static void OnHudLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is HudOverlayWindow window)
        {
            _ = window.Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                window.InstallHudCustomizationButton);
        }
    }
}

public partial class HudOverlayWindow
{
    private bool _hudCustomizationButtonInstalled;

    internal void InstallHudCustomizationButton()
    {
        if (_hudCustomizationButtonInstalled)
        {
            return;
        }

        if (_busDashboardHandle is null)
        {
            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                InstallHudCustomizationButton);
            return;
        }

        _hudCustomizationButtonInstalled = true;
        var previous = _busDashboardHandle.Child;
        _busDashboardHandle.Child = null;

        var dock = new DockPanel();
        var button = new Button
        {
            Content = EditorButtonText(),
            MinWidth = 86d,
            Height = 26d,
            Margin = new Thickness(10d, 0d, 0d, 0d),
            Padding = new Thickness(9d, 2d, 9d, 2d),
            Background = new SolidColorBrush(Color.FromRgb(13, 66, 101)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(45, 142, 201)),
            BorderThickness = new Thickness(1d),
            Foreground = Brushes.White,
            FontSize = 9d,
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        button.Click += (_, args) =>
        {
            args.Handled = true;
            var editor = new HudCustomizationWindow(Application.Current.MainWindow);
            editor.ShowDialog();
        };
        DockPanel.SetDock(button, Dock.Right);
        dock.Children.Add(button);

        if (previous is UIElement oldContent)
        {
            dock.Children.Add(oldContent);
        }
        else
        {
            dock.Children.Add(new TextBlock
            {
                Text = "▦  HUD",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        _busDashboardHandle.Child = dock;
    }

    private static string EditorButtonText() =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => "⚙ EDITAR HUD",
            "es" => "⚙ EDITAR HUD",
            "de" => "⚙ HUD EDITIEREN",
            "fr" => "⚙ MODIFIER HUD",
            _ => "⚙ EDIT HUD"
        };
}
