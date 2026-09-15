using System.Windows;
using System.Windows.Media;

namespace NavBR.Client.Windows;

internal static class Alpha11VisualTuning
{
    private static readonly HashSet<MainWindow> Applied = new();

    public static void Apply(MainWindow window)
    {
        if (!Applied.Add(window))
        {
            return;
        }

        CompactMainMapVehicleMarker(window);
    }

    private static void CompactMainMapVehicleMarker(MainWindow window)
    {
        // MainWindow calculates the marker's logical size from the source
        // roadmap so positioning remains resolution-independent. Alpha.11
        // keeps that logical geometry for centering but renders the complete
        // marker (orange disc + arrow + outline) at a compact scale.
        // This avoids changing the GPS coordinate math or the heading updates.
        var headingRotation = window.VehicleHeadingTransform;
        var transforms = new TransformGroup();
        transforms.Children.Add(new ScaleTransform(0.32d, 0.32d));
        transforms.Children.Add(headingRotation);

        window.VehicleMarker.RenderTransformOrigin = new Point(0.5d, 0.5d);
        window.VehicleMarker.RenderTransform = transforms;
    }
}
