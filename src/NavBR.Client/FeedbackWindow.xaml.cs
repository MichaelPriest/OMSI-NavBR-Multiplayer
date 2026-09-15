using System.Diagnostics;
using System.Windows;
using NavBR.Client.Localization;

namespace NavBR.Client;

public partial class FeedbackWindow : Window
{
    private const string RepositoryIssuesUrl =
        "https://github.com/MichaelPriest/OMSI-NavBR-Multiplayer/issues";
    private const string BugUrl =
        RepositoryIssuesUrl + "/new?template=bug.yml";
    private const string SuggestionUrl =
        RepositoryIssuesUrl + "/new?template=suggestion.yml";
    private const string GeneralFeedbackUrl =
        RepositoryIssuesUrl + "/new?template=feedback.yml";

    public FeedbackWindow()
    {
        InitializeComponent();
        ApplyLocalization();
    }

    private void BugButton_Click(object sender, RoutedEventArgs e) =>
        OpenExternal(BugUrl);

    private void SuggestionButton_Click(object sender, RoutedEventArgs e) =>
        OpenExternal(SuggestionUrl);

    private void GeneralButton_Click(object sender, RoutedEventArgs e) =>
        OpenExternal(GeneralFeedbackUrl);

    private void ViewFeedbackButton_Click(object sender, RoutedEventArgs e) =>
        OpenExternal(RepositoryIssuesUrl);

    private void OpenExternal(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
            StatusText.Text = Text(
                pt: "Formulário aberto no navegador. O feedback fica vinculado ao projeto no GitHub.",
                en: "The form was opened in your browser. Feedback stays linked to the project on GitHub.",
                es: "El formulario se abrió en el navegador. El feedback queda vinculado al proyecto en GitHub.",
                de: "Das Formular wurde im Browser geöffnet. Das Feedback bleibt dem GitHub-Projekt zugeordnet.",
                fr: "Le formulaire a été ouvert dans le navigateur. Le retour reste lié au projet GitHub.");
        }
        catch (Exception ex)
        {
            StatusText.Text = Text(
                pt: $"Não foi possível abrir o navegador: {ex.Message}",
                en: $"Could not open the browser: {ex.Message}",
                es: $"No se pudo abrir el navegador: {ex.Message}",
                de: $"Der Browser konnte nicht geöffnet werden: {ex.Message}",
                fr: $"Impossible d’ouvrir le navigateur : {ex.Message}");
        }
    }

    private void ApplyLocalization()
    {
        Title = Text(
            "Feedback — OMSI NavBR Multiplayer",
            "Feedback — OMSI NavBR Multiplayer",
            "Feedback — OMSI NavBR Multiplayer",
            "Feedback — OMSI NavBR Multiplayer",
            "Feedback — OMSI NavBR Multiplayer");

        HeadingText.Text = Text(
            "Ajude a melhorar o NavBR",
            "Help improve NavBR",
            "Ayuda a mejorar NavBR",
            "Hilf mit, NavBR zu verbessern",
            "Aidez à améliorer NavBR");
        LeadText.Text = Text(
            "Escolha o tipo de feedback. O NavBR abre um formulário estruturado no GitHub para facilitar o acompanhamento pela comunidade.",
            "Choose the feedback type. NavBR opens a structured GitHub form so the community can track it easily.",
            "Elige el tipo de feedback. NavBR abre un formulario estructurado en GitHub para facilitar el seguimiento.",
            "Wähle die Art des Feedbacks. NavBR öffnet ein strukturiertes GitHub-Formular zur einfachen Nachverfolgung.",
            "Choisissez le type de retour. NavBR ouvre un formulaire GitHub structuré pour faciliter le suivi.");

        BugTitleText.Text = Text("Relatar um bug", "Report a bug", "Reportar un error", "Fehler melden", "Signaler un bug");
        BugBodyText.Text = Text(
            "Problemas de HUD, mapa, rota, chat, voz, multiplayer, instalação ou site. Prints e logs ajudam muito.",
            "Problems with HUD, map, route, chat, voice, multiplayer, installation or site. Screenshots and logs help a lot.",
            "Problemas de HUD, mapa, ruta, chat, voz, multijugador, instalación o sitio. Las capturas y logs ayudan mucho.",
            "Probleme mit HUD, Karte, Route, Chat, Sprache, Multiplayer, Installation oder Website. Screenshots und Logs helfen sehr.",
            "Problèmes de HUD, carte, itinéraire, chat, voix, multijoueur, installation ou site. Les captures et logs sont très utiles.");
        BugButton.Content = Text("Relatar problema", "Report problem", "Reportar problema", "Problem melden", "Signaler le problème");

        SuggestionTitleText.Text = Text("Sugerir uma melhoria", "Suggest an improvement", "Sugerir una mejora", "Verbesserung vorschlagen", "Suggérer une amélioration");
        SuggestionBodyText.Text = Text(
            "Ideias para HUD, navegação, multiplayer, chat, voz, interface, instalação ou site.",
            "Ideas for HUD, navigation, multiplayer, chat, voice, interface, installation or site.",
            "Ideas para HUD, navegación, multijugador, chat, voz, interfaz, instalación o sitio.",
            "Ideen für HUD, Navigation, Multiplayer, Chat, Sprache, Oberfläche, Installation oder Website.",
            "Idées pour le HUD, la navigation, le multijoueur, le chat, la voix, l’interface, l’installation ou le site.");
        SuggestionButton.Content = Text("Enviar sugestão", "Send suggestion", "Enviar sugerencia", "Vorschlag senden", "Envoyer une suggestion");

        GeneralTitleText.Text = Text("Feedback geral", "General feedback", "Feedback general", "Allgemeines Feedback", "Retour général");
        GeneralBodyText.Text = Text(
            "Conte o que gostou, o que ainda incomoda e qual área deveria receber prioridade nas próximas versões.",
            "Tell us what you liked, what still gets in the way, and what should be prioritized next.",
            "Cuéntanos qué te gustó, qué aún molesta y qué debería tener prioridad en las próximas versiones.",
            "Sag uns, was dir gefällt, was noch stört und was als Nächstes Priorität haben sollte.",
            "Dites-nous ce que vous avez aimé, ce qui gêne encore et ce qui devrait être prioritaire ensuite.");
        GeneralButton.Content = Text("Avaliar o projeto", "Review the project", "Evaluar el proyecto", "Projekt bewerten", "Évaluer le projet");

        PrivacyHeadingText.Text = Text("Feedback público", "Public feedback", "Feedback público", "Öffentliches Feedback", "Retour public");
        PrivacyText.Text = Text(
            "Os relatos ficam públicos nas Issues do GitHub para permitir acompanhamento e evitar duplicidade. Não envie senhas, tokens ou dados privados. Em bugs de rota, o navbr-route.log pode ser anexado.",
            "Reports are public in GitHub Issues so progress can be tracked and duplicates avoided. Do not send passwords, tokens or private data. For route bugs, navbr-route.log can be attached.",
            "Los reportes son públicos en GitHub Issues para permitir seguimiento y evitar duplicados. No envíes contraseñas, tokens ni datos privados. Para errores de ruta puedes adjuntar navbr-route.log.",
            "Meldungen sind öffentlich in GitHub Issues, damit der Fortschritt verfolgt und Duplikate vermieden werden können. Keine Passwörter, Tokens oder privaten Daten senden. Bei Routenfehlern kann navbr-route.log angehängt werden.",
            "Les signalements sont publics dans GitHub Issues afin de suivre leur traitement et d’éviter les doublons. N’envoyez pas de mots de passe, jetons ou données privées. Pour les bugs d’itinéraire, navbr-route.log peut être joint.");
        ViewFeedbackButton.Content = Text("Ver feedback", "View feedback", "Ver feedback", "Feedback ansehen", "Voir les retours");
    }

    private static string Text(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };
}
