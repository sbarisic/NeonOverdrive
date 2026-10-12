using System.Globalization;
using System.Numerics;
using ArcadeDodger.Multiplayer;

namespace ArcadeDodger.Web;

internal static partial class BrowserArenaHud
{
    private static SectorNavigation? navigation;
    private static Vector2 routeStart,
        routeEnd;
    private static Vector2[] route = [];
    private static string routeJson = "[]";
    private static bool routeKnown;

    private static Vector2[] Route(Vector2 start, Vector2 end)
    {
        if (navigation is null)
            return [end];
        if (
            !routeKnown
            || Vector2.DistanceSquared(start, routeStart) > 160 * 160
            || Vector2.DistanceSquared(end, routeEnd) > 80 * 80
        )
        {
            routeStart = start;
            routeEnd = end;
            route = navigation.Route(start, end, Room.Radius);
            routeKnown = true;
            UpdateRouteJson();
        }
        var passed = 0;
        while (passed < route.Length - 1 && Vector2.DistanceSquared(start, route[passed]) < 80 * 80)
            passed++;
        if (passed > 0)
        {
            route = route[passed..];
            UpdateRouteJson();
        }
        return route;
    }

    private static void UpdateRouteJson() =>
        routeJson =
            "["
            + string.Join(
                ",",
                route.Select(p =>
                    "{\"x\":"
                    + p.X.ToString(CultureInfo.InvariantCulture)
                    + ",\"y\":"
                    + p.Y.ToString(CultureInfo.InvariantCulture)
                    + "}"
                )
            )
            + "]";

    public static string Navigation(double x, double y, double tx, double ty)
    {
        Route(new((float)x, (float)y), new((float)tx, (float)ty));
        return routeJson;
    }
}
