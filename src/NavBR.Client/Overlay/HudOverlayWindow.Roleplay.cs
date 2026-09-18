using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Localization;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow
{
    public event Action? RoleplayButtonRequested;

    public void SetRoleplayState(
        bool featureEnabled,
        bool mapReady,
        bool hasSelection,
        bool active,
        string? characterName)
    {
        if (active)
        {
            RoleplayHudButton.Content = HudRpText(
                "↩ Voltar ao ônibus",
                "↩ Return to bus",
                "↩ Volver al autobús",
                "↩ Zurück zum Bus",
                "↩ Retour au bus");
            RoleplayHudButton.Background = new SolidColorBrush(Color.FromArgb(220, 43, 104, 75));
            RoleplayHudButton.BorderBrush = new SolidColorBrush(Color.FromArgb(230, 56, 201, 140));
            RoleplayHudButton.ToolTip = string.IsNullOrWhiteSpace(characterName)
                ? HudRpText(
                    "Personagem ativo. Clique para voltar ao ônibus.",
                    "Character active. Click to return to the bus.",
                    "Personaje activo. Haz clic para volver al autobús.",
                    "Charakter aktiv. Klicken, um zum Bus zurückzukehren.",
                    "Personnage actif. Cliquez pour revenir au bus.")
                : $"{characterName} • " + HudRpText(
                    "clique para voltar ao ônibus",
                    "click to return to the bus",
                    "haz clic para volver al autobús",
                    "klicken, um zum Bus zurückzukehren",
                    "cliquez pour revenir au bus");
            return;
        }

        if (featureEnabled && mapReady && hasSelection)
        {
            RoleplayHudButton.Content = HudRpText(
                "♙ Ativar personagem",
                "♙ Activate character",
                "♙ Activar personaje",
                "♙ Charakter aktivieren",
                "♙ Activer personnage");
            RoleplayHudButton.Background = new SolidColorBrush(Color.FromArgb(220, 21, 59, 87));
            RoleplayHudButton.BorderBrush = new SolidColorBrush(Color.FromArgb(230, 113, 198, 255));
            RoleplayHudButton.ToolTip = string.IsNullOrWhiteSpace(characterName)
                ? HudRpText(
                    "Sair do ônibus e controlar o personagem selecionado.",
                    "Exit the bus and control the selected character.",
                    "Salir del autobús y controlar el personaje seleccionado.",
                    "Bus verlassen und den ausgewählten Charakter steuern.",
                    "Quitter le bus et contrôler le personnage sélectionné.")
                : characterName;
            return;
        }

        RoleplayHudButton.Content = HudRpText(
            "♙ Personagem",
            "♙ Character",
            "♙ Personaje",
            "♙ Charakter",
            "♙ Personnage");
        RoleplayHudButton.Background = new SolidColorBrush(Color.FromArgb(190, 13, 26, 36));
        RoleplayHudButton.BorderBrush = new SolidColorBrush(Color.FromArgb(180, 61, 137, 196));
        RoleplayHudButton.ToolTip = !featureEnabled
            ? HudRpText(
                "Clique para configurar e ativar Personagem / RP.",
                "Click to configure and enable Character / RP.",
                "Haz clic para configurar y activar Personaje / RP.",
                "Klicken, um Charakter / RP zu konfigurieren und zu aktivieren.",
                "Cliquez pour configurer et activer Personnage / RP.")
            : !mapReady
                ? HudRpText(
                    "Aguardando mapa carregado no OMSI.",
                    "Waiting for an OMSI map.",
                    "Esperando un mapa de OMSI.",
                    "Warte auf eine OMSI-Karte.",
                    "En attente d’une carte OMSI.")
                : HudRpText(
                    "Clique para selecionar o personagem.",
                    "Click to select a character.",
                    "Haz clic para seleccionar el personaje.",
                    "Klicken, um einen Charakter auszuwählen.",
                    "Cliquez pour sélectionner le personnage.");
    }

    private void RoleplayHudButton_Click(object sender, RoutedEventArgs e)
    {
        RoleplayButtonRequested?.Invoke();
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            RestoreOmsiFocus);
    }

    private void RefreshRoleplayButtonInteraction()
    {
        if (_chatInteractive || _hudLayoutEditMode)
        {
            return;
        }

        var overButton = IsCursorOverRoleplayButton();
        SetRoleplayButtonInteractive(overButton);
    }

    private bool IsCursorOverRoleplayButton()
    {
        if (!IsVisible ||
            OverlayRoot.Visibility != Visibility.Visible ||
            RoleplayHudButton.Visibility != Visibility.Visible ||
            !RoleplayHudButton.IsVisible ||
            RoleplayHudButton.ActualWidth <= 1d ||
            RoleplayHudButton.ActualHeight <= 1d ||
            !GetCursorPos(out var cursor))
        {
            return false;
        }

        try
        {
            var topLeft = RoleplayHudButton.PointToScreen(new Point(0d, 0d));
            return cursor.X >= topLeft.X &&
                   cursor.X <= topLeft.X + RoleplayHudButton.ActualWidth &&
                   cursor.Y >= topLeft.Y &&
                   cursor.Y <= topLeft.Y + RoleplayHudButton.ActualHeight;
        }
        catch
        {
            return false;
        }
    }

    private void SetRoleplayButtonInteractive(bool interactive)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var style = GetWindowLong(handle, GwlExStyle) | WsExToolWindow | WsExNoActivate;
        if (interactive)
        {
            style &= ~WsExTransparent;
        }
        else
        {
            style |= WsExTransparent;
        }

        SetWindowLong(handle, GwlExStyle, style);
    }

    private static string HudRpText(
        string pt,
        string en,
        string es,
        string de,
        string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorPoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out CursorPoint point);
}
