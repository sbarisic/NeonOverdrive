using System.Numerics;

namespace ArcadeDodger.Multiplayer;

/// <summary>One replaceable browser waypoint job, advanced once per frame rather than by map readers.</summary>
public sealed class WaypointNavigation(SectorNavigation navigation) : IDisposable
{
    private SectorNavigation.Search? search;
    private Vector2 start,
        target,
        requestedTarget,
        checkedStart;
    private float radius = -1,
        requestedRadius;
    private bool requested,
        known;
    public Vector2[] Path { get; private set; } = [];
    public bool Pending => requested && (!known || search is not null);
    public int Revision { get; private set; }
    public int SearchesStarted { get; private set; }

    public void Request(Vector2 from, Vector2 to, float clearance)
    {
        start = from;
        requestedTarget = to;
        requestedRadius = clearance;
        requested = true;
        var moved = Vector2.DistanceSquared(target, to);
        // Finish a nearby moving target's in-flight search. Restarting every 80 units
        // could starve long searches when tracking a relief ship or convoy.
        if (radius == clearance && search is not null && moved < 1600 * 1600)
            return;
        if (radius != clearance || moved > 80 * 80)
        {
            Dispose();
            known = false;
            if (radius != clearance || moved >= 1600 * 1600)
                SetPath([]);
            radius = clearance;
        }
    }

    public void Advance(int workItems = 256, double milliseconds = 2)
    {
        if (!requested)
            return;
        if (known && search is null && Vector2.DistanceSquared(start, checkedStart) > 40 * 40)
        {
            checkedStart = start;
            // Rejoin a still-valid path instead of recomputing A* every 160 units of flight.
            var join = Path.Length > 0 && navigation.CanTravel(start, Path[0], radius) ? 0 : -1;
            for (var i = 0; i < Math.Min(Path.Length - 1, 5); i++)
            {
                if (
                    Vector2.DistanceSquared(start, Path[i]) >= 80 * 80
                    || !navigation.CanTravel(start, Path[i + 1], radius)
                )
                    break;
                join = i + 1;
            }
            if (join > 0)
                SetPath(Path[join..]);
            if (join < 0)
            {
                known = false;
                SetPath([]);
            }
        }
        if (!known && search is null)
        {
            target = requestedTarget;
            radius = requestedRadius;
            checkedStart = start;
            search = navigation.Begin(start, target, radius);
            SearchesStarted++;
            known = true;
        }
        if (search is null)
            return;
        search.Advance(workItems, milliseconds);
        if (!search.Complete)
            return;
        var path = search.Path;
        Dispose();
        // The ship can move while planning. Never publish a first segment through cover.
        if (path.Length > 0 && !navigation.CanTravel(start, path[0], radius))
        {
            known = false;
            SetPath([]);
            return;
        }
        checkedStart = start;
        if (
            path.Length > 0
            && Vector2.DistanceSquared(target, requestedTarget) > 80 * 80
            && navigation.CanTravel(path[^1], requestedTarget, radius)
        )
        {
            path = [.. path, requestedTarget];
            target = requestedTarget;
        }
        SetPath(path);
    }

    private void SetPath(Vector2[] value)
    {
        if (Path.Length == 0 && value.Length == 0)
            return;
        Path = value;
        Revision++;
    }

    public void Dispose()
    {
        search?.Dispose();
        search = null;
    }
}
