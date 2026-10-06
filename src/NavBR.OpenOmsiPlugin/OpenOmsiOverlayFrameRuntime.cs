using NavBR.Shared.PluginBridge;

namespace NavBR.OpenOmsiPlugin;

internal static class OpenOmsiOverlayFrameRuntime
{
    public static (
        OpenOmsiOverlayFrameState Full,
        OpenOmsiOverlay2DFrameState Overlay2D,
        OpenOmsiWorldGuidanceFrameState World)
        Build(
        OpenOmsiHudConfiguration hud,
        OpenOmsiMapPresentationState map,
        OpenOmsiMapVisualState visual,
        OpenOmsiCompactHudState compactHud,
        OpenOmsiTeleMatrixState teleMatrix,
        NavBR.Shared.PluginBridge.OpenOmsiGroundArrowState[] groundArrows)
    {
        var full = new OpenOmsiOverlayFrameState(
            TimestampUnixMilliseconds: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            MiniMapVisible: hud.MiniMapEnabled,
            FullMapVisible: hud.FullMapEnabled,
            CompactHudVisible: hud.RouteGuidanceEnabled && compactHud.Available,
            TeleMatrixVisible: hud.TeleMatrixEnabled,
            RouteGuidanceVisible: hud.RouteGuidanceEnabled,
            CenterX: map.CenterX,
            CenterY: map.CenterY,
            RotationDegrees: map.RotationDegrees,
            RadiusMeters: map.RadiusMeters,
            OrientationMode: map.OrientationMode,
            PrimaryText: compactHud.PrimaryText,
            SecondaryText: compactHud.SecondaryText,
            ManeuverIcon: compactHud.ManeuverIcon,
            DistanceToManeuverMeters: compactHud.DistanceToManeuverMeters,
            RouteRemainingMeters: compactHud.RouteRemainingMeters,
            OffRoute: compactHud.OffRoute,
            TraveledRoute: visual.TraveledRoute,
            ForwardRoute: visual.ForwardRoute,
            RejoinRoute: visual.RejoinRoute,
            Markers: map.Markers,
            GroundArrows: groundArrows);

        var overlay2D = new OpenOmsiOverlay2DFrameState(
            MiniMapVisible: hud.MiniMapEnabled,
            FullMapVisible: hud.FullMapEnabled,
            CompactHudVisible: hud.RouteGuidanceEnabled && compactHud.Available,
            TeleMatrixVisible: hud.TeleMatrixEnabled && teleMatrix.Available,
            RouteGuidanceVisible: hud.RouteGuidanceEnabled,
            TrafficVisible: hud.TrafficEnabled,
            PlayersVisible: hud.MultiplayerEnabled,
            TeleMatrixLine: teleMatrix.Line,
            TeleMatrixDestination: teleMatrix.Destination,
            TeleMatrixNextStop: teleMatrix.NextStop,
            TeleMatrixDelaySeconds: teleMatrix.DelaySeconds,
            TeleMatrixPunctualityState: teleMatrix.PunctualityState,
            CenterX: map.CenterX,
            CenterY: map.CenterY,
            RotationDegrees: map.RotationDegrees,
            RadiusMeters: map.RadiusMeters,
            OrientationMode: map.OrientationMode,
            PrimaryText: compactHud.PrimaryText,
            SecondaryText: compactHud.SecondaryText,
            ManeuverIcon: compactHud.ManeuverIcon,
            DistanceToManeuverMeters: compactHud.DistanceToManeuverMeters,
            RouteRemainingMeters: compactHud.RouteRemainingMeters,
            OffRoute: compactHud.OffRoute,
            TraveledRoute: visual.TraveledRoute,
            ForwardRoute: visual.ForwardRoute,
            RejoinRoute: visual.RejoinRoute,
            Markers: map.Markers);

        var primitives = OpenOmsiGroundArrowPrimitiveRuntime.Build(groundArrows)
            .Select(primitive => new OpenOmsiGroundArrowPrimitiveState(
                primitive.X,
                primitive.Y,
                primitive.Z,
                primitive.HeadingDegrees,
                primitive.WidthMeters,
                primitive.LengthMeters,
                primitive.HeightOffsetMeters,
                primitive.Opacity,
                primitive.DistanceAheadMeters,
                primitive.Kind))
            .ToArray();

        var world = new OpenOmsiWorldGuidanceFrameState(
            Visible: hud.RouteGuidanceEnabled && primitives.Length > 0,
            GroundArrows: groundArrows,
            GroundArrowPrimitives: primitives);

        return (full, overlay2D, world);
    }
}
