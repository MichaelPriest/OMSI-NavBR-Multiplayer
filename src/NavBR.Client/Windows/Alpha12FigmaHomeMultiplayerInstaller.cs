using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Multiplayer;

namespace NavBR.Client.Windows;

/// <summary>
/// Replaces the shell's generic Multiplayer Home texts with a dedicated live
/// view. The original TextBlocks remain detached so the base shell timer may
/// keep refreshing them without causing visual races or fictitious values.
/// </summary>
internal static class Alpha12FigmaHomeMultiplayerInstaller
{
    private static readonly HashSet<MainWindow> Installed = new();

    public static void Install(MainWindow window)
    {
        if (!Installed.Add(window))
        {
            return;
        }

        var body = Enumerate<StackPanel>(window)
            .FirstOrDefault(panel =>
                panel.Children.OfType<TextBlock>().FirstOrDefault()?.Text == "MULTIPLAYER");
        if (body is null)
        {
            Installed.Remove(window);
            return;
        }

        var oldTexts = body.Children.OfType<TextBlock>().Skip(1).ToArray();
        foreach (var text in oldTexts)
        {
            body.Children.Remove(text);
        }

        var state = new TextBlock
        {
            Text = "Sem sessão",
            Foreground = Brushes.White,
            FontSize = 17d,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0d, 9d, 0d, 0d),
            TextWrapping = TextWrapping.Wrap
        };
        var detail = new TextBlock
        {
            Text = "Abra a Central Multiplayer",
            Foreground = Brush(151, 171, 185),
            FontSize = 12d,
            Margin = new Thickness(0d, 9d, 0d, 0d),
            TextWrapping = TextWrapping.Wrap
        };
        body.Children.Add(state);
        body.Children.Add(detail);

        void Refresh()
        {
            var central = Application.Current?.Windows
                .OfType<MultiplayerWindow>()
                .FirstOrDefault();
            if (central?.HasBackgroundSession != true)
            {
                state.Text = "Sem sessão";
                state.Foreground = Brushes.White;
                detail.Text = "Abra a Central Multiplayer";
                detail.Foreground = Brush(151, 171, 185);
                return;
            }

            var roomId = string.IsNullOrWhiteSpace(central.CurrentRoomId)
                ? "Sala ativa"
                : central.CurrentRoomId.Trim();
            state.Text = roomId;
            state.Foreground = Brush(218, 230, 238);

            var players = central.CurrentPlayerCountForShell;
            var playerText = players > 0
                ? $"{players} jogador{(players == 1 ? string.Empty : "es")}"
                : "presenças sincronizando";
            var mode = central.IsHostingRoomForShell
                ? "HOST DIRETO"
                : central.IsConnected
                    ? "CONECTADO AO HOST"
                    : "SESSÃO EM SEGUNDO PLANO";

            detail.Text = $"{mode} • {playerText}";
            detail.Foreground = central.IsHostingRoomForShell
                ? Brush(56, 201, 140)
                : Brush(113, 198, 255);
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500d) };
        timer.Tick += (_, _) => Refresh();
        timer.Start();
        Refresh();

        window.Closed += (_, _) =>
        {
            timer.Stop();
            Installed.Remove(window);
        };
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

    private static SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));
}
