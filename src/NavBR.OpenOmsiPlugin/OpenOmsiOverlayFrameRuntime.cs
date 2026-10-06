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
            TeleMatrixVisible: hud.TeleMatrixEnabled,
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

        var world = new OpenOmsiWorldGuidanceFrameState(
            Visible: hud.RouteGuidanceEnabled && groundArrows.Length > 0,
            GroundArrows: groundArrows);

        return (full, overlay2D, world);
    }
}
