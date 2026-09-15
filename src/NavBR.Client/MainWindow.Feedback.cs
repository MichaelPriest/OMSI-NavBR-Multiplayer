using System.Windows;
using System.Windows.Controls;
using NavBR.Client.Localization;

namespace NavBR.Client;

public partial class MainWindow
{
    private Button? _feedbackButton;

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        Loaded += MainWindow_FeedbackLoaded;
    }

    private void MainWindow_FeedbackLoaded(object sender, RoutedEventArgs e)
    {
        EnsureFeedbackButton();
    }

    private void EnsureFeedbackButton()
    {
        if (_feedbackButton is not null || MultiplayerButton.Parent is not StackPanel parent)
        {
            return;
        }

        var button = new Button
        {
            MinWidth = 112,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        button.Click += FeedbackButton_Click;

        var index = parent.Children.IndexOf(MultiplayerButton);
        parent.Children.Insert(Math.Max(0, index), button);
        _feedbackButton = button;
        UpdateFeedbackButtonText();

        LanguageComboBox.SelectionChanged += (_, _) => UpdateFeedbackButtonText();
    }

    private void UpdateFeedbackButtonText()
    {
        if (_feedbackButton is null)
        {
            return;
        }

        _feedbackButton.Content = LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => "Feedback",
            "es" => "Feedback",
            "de" => "Feedback",
            "fr" => "Feedback",
            _ => "Feedback"
        };
    }

    private void FeedbackButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new FeedbackWindow
        {
            Owner = this
        };
        window.ShowDialog();
    }
}
