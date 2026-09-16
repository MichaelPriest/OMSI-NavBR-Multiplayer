using System.Windows.Controls;
using System.Windows.Threading;
using NavBR.Client.Localization;

namespace NavBR.Client;

public partial class MainWindow
{
    private bool _creditsHooked;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        ApplyCreditsFooter();
        if (_creditsHooked)
        {
            return;
        }

        _creditsHooked = true;
        LanguageComboBox.SelectionChanged += CreditsLanguageComboBox_SelectionChanged;
    }

    private void CreditsLanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // The normal localization handler runs first. Reapply the credits after
        // it finishes so the footer remains present when the language changes.
        Dispatcher.BeginInvoke(
            new Action(ApplyCreditsFooter),
            DispatcherPriority.Background);
    }

    private void ApplyCreditsFooter()
    {
        var credits = LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => "Desenvolvedor: MichaelPriest • Com apoio da IA ChatGPT",
            "es" => "Desarrollador: MichaelPriest • Con apoyo de la IA ChatGPT",
            "de" => "Entwickler: MichaelPriest • Mit KI-Unterstützung durch ChatGPT",
            "fr" => "Développeur : MichaelPriest • Avec l’aide de l’IA ChatGPT",
            _ => "Developer: MichaelPriest • With AI assistance from ChatGPT"
        };

        PhaseFooterText.TextWrapping = System.Windows.TextWrapping.Wrap;
        PhaseFooterText.Text = $"{LocalizationService.Get("PhaseFooter")}{Environment.NewLine}{credits}";
    }
}
