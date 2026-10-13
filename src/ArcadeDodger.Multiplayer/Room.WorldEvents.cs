using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private string worldEvent = "";
    private int worldEventTarget;
    private long worldEventAt,
        worldEventEnds;
    private readonly HashSet<int> invasion = [];
    private readonly HashSet<int> eventCargo = [];

    private void StepWorldEvents()
    {
        if (Sector is null || !enemiesEnabled)
            return;
        if (worldEventAt == 0)
            worldEventAt = Tick + worldRandom!.Next(360, 601) * TickRate;
        if (worldEvent == "" && Tick >= worldEventAt)
        {
            var kind = worldRandom!.Next(3);
            if (kind == 0)
            {
                var target = territory
                    .Values.Where(t => t.Friendly)
                    .OrderByDescending(t => t.Id)
                    .FirstOrDefault();
                if (target is null)
                {
                    worldEventAt = Tick + 60 * TickRate;
                    return;
                }
                worldEvent = "invasion-warning";
                worldEventTarget = target.Id;
                worldEventEnds = Tick + 120 * TickRate;
                Broadcast(
                    "sector",
                    "Long-range sensors",
                    $"Invasion detected at East Arrival Corridor. {Location(target.Id)!.Name} threatened in two minutes."
                );
            }
            else
            {
                var before = worldCargo.Count;
                var location = Sector
                    .Locations.Where(l => l.Kind == "planet")
                    .OrderBy(_ => worldRandom.Next())
                    .First();
                var at = Exterior(location.Position + new Vector2(location.Radius + 700, -600));
                AddCargo(
                    kind == 1 ? "anomaly" : "container",
                    at,
                    kind == 1 ? 200 : 100,
                    300 * TickRate
                );
                if (worldCargo.Count == before)
                {
                    worldEventAt = Tick + 60 * TickRate;
                    return;
                }
                worldEvent = kind == 1 ? "ancient-signal" : "resource-rush";
                worldEventTarget = worldCargo[^1].Id;
                eventCargo.Clear();
                eventCargo.Add(worldEventTarget);
                worldEventEnds = Tick + 300 * TickRate;
                if (kind == 2)
                    for (var i = 0; i < 4; i++)
                    {
                        var count = worldCargo.Count;
                        AddCargo("container", at + new Vector2(i * 90, 80), 100, 300 * TickRate);
                        if (worldCargo.Count > count)
                            eventCargo.Add(worldCargo[^1].Id);
                    }
                Broadcast(
                    "sector",
                    "Long-range sensors",
                    kind == 1
                        ? $"Ancient signal in {location.Name}'s region. Ask a contact for an exploration assignment."
                        : $"Resource rush near {location.Name}. Limited salvage; scavengers are already moving."
                );
            }
        }
        if (worldEvent == "invasion-warning" && Tick >= worldEventEnds)
        {
            var count = 4 + (int)MathF.Round(8 * FriendlyControl);
            if (enemies.Count + count > EnemyLimit)
                return;
            if (!sources.TryGetValue(31, out var corridor))
                return;
            for (var i = 0; i < count; i++)
            {
                Launch(
                    corridor,
                    true,
                    i,
                    Math.Min(
                        8,
                        EnemyCatalog.Region(corridor.Location.Id) + (int)(4 * FriendlyControl)
                    )
                );
                invasion.Add(enemies[^1].Id);
                enemies[^1].Role = NpcRole.Pirate;
            }
            worldEvent = "invasion";
            worldEventEnds = Tick + 900 * TickRate;
            Broadcast(
                "sector",
                "Defense command",
                "Invasion fleet has entered the corridor. Intercept before it reaches the objective."
            );
        }
        if (worldEvent is "resource-rush" or "ancient-signal")
        {
            eventCargo.RemoveWhere(id => !worldCargo.Any(c => c.Id == id));
            if (!eventCargo.Contains(worldEventTarget) && eventCargo.Count > 0)
                worldEventTarget = eventCargo.Min();
        }
        if (
            worldEvent != ""
            && worldEvent != "invasion-warning"
            && (
                Tick >= worldEventEnds
                || worldEvent is "resource-rush" or "ancient-signal" && eventCargo.Count == 0
                || worldEvent == "invasion"
                    && (
                        !territory[worldEventTarget].Friendly
                        || !enemies.Any(e => e.Hull > 0 && invasion.Contains(e.Id))
                    )
            )
        )
        {
            worldEvent = "";
            invasion.Clear();
            eventCargo.Clear();
            worldEventTarget = 0;
            worldEventAt = Tick + worldRandom!.Next(360, 601) * TickRate;
        }
    }

    private bool MoveWorldMissionNpc(Enemy npc)
    {
        Vector2? goal =
            invasion.Contains(npc.Id) && territory.TryGetValue(worldEventTarget, out var site)
                ? site.Position
                : null;
        if (raidSite != 0 && raiders.Contains(npc.Id) && RaidTarget(npc) is null)
            goal = Location(raidSite)!.Position;
        if (npc.Role == NpcRole.Patrol && !civilians.Any(c => c.Ship == npc))
            goal = territory
                .Values.OrderBy(t => t.Friendly && !t.Contested ? 1 : 0)
                .ThenBy(t => Vector2.DistanceSquared(t.Position, npc.Position))
                .FirstOrDefault()
                ?.Position;
        if (npc.Role == NpcRole.Courier)
        {
            goal = Location(npc.CourierDestination)!.Position;
            if (Vector2.Distance(npc.Position, goal.Value) < 220)
            {
                npc.State = "arrived";
                if (npc.CourierDepartAt == 0)
                    npc.CourierDepartAt = Tick + 60 * TickRate;
                else if (Tick >= npc.CourierDepartAt)
                {
                    npc.CourierDestination = npc.CourierDestination == 12 ? 11 : 12;
                    npc.CourierDepartAt = 0;
                    npc.State = "traveling";
                }
                return true;
            }
        }
        if (goal is null)
            return false;
        if (
            enemyGrid
                .Query(npc.Position, npc.Position, 700)
                .Any(e =>
                    e.Allied != npc.Allied && e.Hull > 0 && VisibleLine(npc.Position, e.Position)
                )
        )
            return false;
        if (
            !npc.Allied
            && players.Values.Any(p =>
                p.Hull > 0
                && !Safe(p)
                && !p.Editing
                && Vector2.Distance(p.Position, npc.Position) < 500
            )
        )
            return false;
        var delta = Navigate(npc, goal.Value) - npc.Position;
        npc.State = "traveling";
        if (delta.LengthSquared() > 1)
        {
            npc.Aim = MathF.Atan2(delta.Y, delta.X);
            npc.Position = MoveShip(
                npc.Position,
                npc.Position + Vector2.Normalize(delta) * 100 / TickRate,
                EnemyRadius(npc)
            );
        }
        return true;
    }
}
