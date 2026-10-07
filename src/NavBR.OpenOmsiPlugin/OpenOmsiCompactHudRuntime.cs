using NavBR.Shared.PluginBridge;

namespace NavBR.OpenOmsiPlugin;

internal sealed record OpenOmsiCompactHudState(
    bool Available,
    string? PrimaryText,
    string? SecondaryText,
    string? Maneuver,
    string? ManeuverIcon,
    double? DistanceToManeuverMeters,
    double? RouteRemainingMeters,
    int? DelaySeconds,
    string? PunctualityState,
    bool OffRoute);

internal static class OpenOmsiCompactHudRuntime
{
    public static OpenOmsiCompactHudState Build(
        OpenOmsiGuidanceState guidance,
        OpenOmsiMiniMapRuntimeState miniMap,
        OpenOmsiTeleMatrixState teleMatrix,
        OpenOmsiRouteRuntimeState route)
    {
        if (!guidance.Available &&
            !miniMap.Available &&
            !teleMatrix.Available)
        {
            return new(
                false,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                false);
        }

        var primary = route.OffRoute
            ? "Retorne à rota"
            : guidance.Maneuver switch
            {
                "left" => "Vire à esquerda",
                "right" => "Vire à direita",
                "slight-left" => "Mantenha-se à esquerda",
                "slight-right" => "Mantenha-se à direita",
                "uturn" => "Faça o retorno",
                "continue" => "Siga em frente",
                _ => teleMatrix.NextStop
            };

        var secondary = !string.IsNullOrWhiteSpace(teleMatrix.NextStop)
            ? $"Próxima: {teleMatrix.NextStop}"
            : teleMatrix.Destination;

        return new(
            Available: true,
            PrimaryText: primary,
            SecondaryText: secondary,
            Maneuver: guidance.Maneuver,
            ManeuverIcon: IconFor(guidance.Maneuver, route.OffRoute),
            DistanceToManeuverMeters:
                route.OffRoute
                    ? route.DistanceFromRouteMeters
                    : guidance.DistanceToManeuverMeters,
            RouteRemainingMeters:
                miniMap.Available
                    ? miniMap.RemainingMeters
                    : null,
            DelaySeconds: teleMatrix.DelaySeconds,
            PunctualityState: teleMatrix.PunctualityState,
            OffRoute: route.OffRoute);
    }

    private static string IconFor(string? maneuver, bool offRoute)
    {
        if (offRoute)
        {
            return "rejoin";
        }

        return maneuver switch
        {
            "left" => "turn-left",
            "right" => "turn-right",
            "slight-left" => "slight-left",
            "slight-right" => "slight-right",
            "uturn" => "uturn",
            _ => "straight"
        };
    }
}
