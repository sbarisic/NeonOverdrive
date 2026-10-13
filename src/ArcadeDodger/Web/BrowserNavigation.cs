using System.Globalization;
using System.Numerics;
using ArcadeDodger.Multiplayer;

namespace ArcadeDodger.Web;

internal static partial class BrowserArenaHud
{
    private static WaypointNavigation? navigation;
    private static string routeJson = "[]";
    private static int routeRevision = -1;
    private static float routeRadius = ChassisCatalog.Get(0).Radius;

    public static bool NavigationPending => navigation?.Pending == true;

    private static Vector2[] Route(Vector2 start, Vector2 end)
    {
        navigation?.Request(start, end, routeRadius);
        return navigation?.Path ?? [];
    }

    public static void AdvanceNavigation(PlayerSnapshot? pilot)
    {
        if (pilot is not null)
        {
            routeRadius = ChassisCatalog.Get(pilot.Chassis).Radius;
            if (hasWaypoint)
                Route(new(pilot.X, pilot.Y), waypoint);
        }
        navigation?.Advance();
    }

    public static string Navigation(double x, double y, double tx, double ty)
    {
        Route(new((float)x, (float)y), new((float)tx, (float)ty));
        if (routeRevision != navigation?.Revision)
        {
            routeRevision = navigation?.Revision ?? -1;
            routeJson =
                "["
                + string.Join(
                    ",",
                    (navigation?.Path ?? []).Select(p =>
                        "{\"x\":"
                        + p.X.ToString(CultureInfo.InvariantCulture)
                        + ",\"y\":"
                        + p.Y.ToString(CultureInfo.InvariantCulture)
                        + "}"
                    )
                )
                + "]";
        }
        return routeJson;
    }
}
