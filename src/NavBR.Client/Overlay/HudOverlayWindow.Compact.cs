using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow
{
    private const double CompactHudUiScale = 0.86d;
    private const byte CompactDarkPanelAlpha = 150;
    private bool _compactHudPresentationApplied;

    private void ApplyCompactHudPresentation()
    {
        if (_compactHudPresentationApplied)
        {
            return;
        }

        _compactHudPresentationApplied = true;

        // Keep all coordinates and minimap math untouched. Scaling only the
        // presentation makes the HUD occupy less screen space without changing
        // route geometry, click-through behavior or saved HUD position.
        HudDock.LayoutTransform = new ScaleTransform(CompactHudUiScale, CompactHudUiScale);

        // Dark panel backgrounds become translucent while text, route lines,
        // warning colors and orange accents keep their original opacity.
        SoftenDarkPanelBackgrounds(HudDock);

        // The roadmap remains readable but no longer visually dominates OMSI.
        MiniMapImage.Opacity = Math.Min(MiniMapImage.Opacity, 0.52d);
    }

    private void SoftenDarkPanelBackgrounds(DependencyObject root)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is Border border &&
                !ReferenceEquals(border, ChatInputPanel) &&
                !ReferenceEquals(border, HotkeyWarningPanel))
            {
                border.Background = MakeDarkBrushMoreTransparent(border.Background);
            }

            SoftenDarkPanelBackgrounds(child);
        }
    }

    private static Brush? MakeDarkBrushMoreTransparent(Brush? brush)
    {
        switch (brush)
        {
            case SolidColorBrush solid when IsDark(solid.Color):
                return new SolidColorBrush(WithMaxAlpha(solid.Color, CompactDarkPanelAlpha));

            case LinearGradientBrush gradient:
            {
                var clone = gradient.CloneCurrentValue();
                foreach (var stop in clone.GradientStops)
                {
                    if (IsDark(stop.Color))
                    {
                        stop.Color = WithMaxAlpha(stop.Color, CompactDarkPanelAlpha);
                    }
                }

                return clone;
            }

            default:
                return brush;
        }
    }

    private static bool IsDark(Color color) =>
        color.R <= 80 && color.G <= 80 && color.B <= 90;

    private static Color WithMaxAlpha(Color color, byte maxAlpha) =>
        Color.FromArgb(
            Math.Min(color.A, maxAlpha),
            color.R,
            color.G,
            color.B);
}
