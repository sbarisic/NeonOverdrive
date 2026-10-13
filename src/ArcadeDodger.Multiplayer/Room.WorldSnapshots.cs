using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private LivingSnapshot LivingState(Player p)
    {
        var state = Living(p);
        var agent = civilians.FirstOrDefault(c =>
            c.Ship.Id == state.Conversation && c.Ship.Hull > 0
        );
        return new(
            radio.ToArray(),
            agent?.Stock.Where(s => !agent.Reserved.Contains(s.Id)).Take(48).ToArray() ?? [],
            worldCargo
                .Where(c => Vector2.Distance(c.Position, p.Position) <= Survey(p))
                .Select(c => new CargoSnapshot(
                    c.Id,
                    c.Kind,
                    c.Position.X,
                    c.Position.Y,
                    c.Credits,
                    c.Owner
                ))
                .ToArray(),
            territory
                .Values.Select(t => new TerritorySnapshot(
                    t.Id,
                    t.Friendly ? "friendly" : "hostile",
                    t.Progress / (30f * TickRate),
                    t.Contested
                ))
                .ToArray(),
            state.Conversation,
            state.Job,
            state.JobState,
            state.Targets,
            state.Police,
            state.ScanAt == 0 ? 0 : Math.Clamp((Tick - state.ScanAt) / (3f * TickRate), 0, 1),
            state.Tow,
            worldEvent,
            worldEventTarget,
            (int)Math.Max(0, (worldEventEnds - Tick) / TickRate),
            RaceFor(p),
            WorldGoal(p),
            enemies
                .Where(e =>
                    e.Allied
                    && e.Hull > 0
                    && !e.Installation
                    && e.SurfacePlanet == 0
                    && !e.Type.StartsWith("salvage-")
                    && e.Type != "repair-bot"
                    && Vector2.Distance(e.Position, p.Position) <= Detection(p)
                )
                .OrderBy(e => Vector2.DistanceSquared(e.Position, p.Position))
                .Take(32)
                .Select(e =>
                {
                    var civilian = civilians.FirstOrDefault(c => c.Ship == e);
                    var contact = contacts.FirstOrDefault(c => c.Ship == e);
                    return new RadioContact(
                        e.Id,
                        civilian?.Name ?? contact?.Name ?? $"{e.Role} {e.Id}",
                        e.Role.ToString().ToLowerInvariant(),
                        e.State,
                        civilian?.Destination ?? contact?.Destination ?? e.Home,
                        civilian?.CargoRevision ?? 0,
                        civilian?.Stock.Count > 0 || contact?.Role == "trader",
                        e.Role != NpcRole.Police
                    );
                })
                .ToArray(),
            EventGoal()
        );
    }

    private WorldObjective? EventGoal()
    {
        Vector2? position = worldEvent.StartsWith("invasion")
            ? Location(worldEventTarget)?.Position
            : worldCargo.FirstOrDefault(c => c.Id == worldEventTarget)?.Position;
        if (worldEvent.Length == 0 || position is null)
            return null;
        var uncertainty = worldEvent == "ancient-signal" ? 1500f : 0;
        var point = position.Value;
        if (uncertainty > 0)
            point = new(
                MathF.Round(point.X / uncertainty) * uncertainty,
                MathF.Round(point.Y / uncertainty) * uncertainty
            );
        return new(worldEventTarget, "event", worldEvent, point.X, point.Y, uncertainty);
    }

    private WorldObjective? WorldGoal(Player p)
    {
        var state = Living(p);
        if (state.JobState != "active" || state.Targets.Length == 0)
            return null;
        var id = state.Targets[0];
        Vector2? at =
            state.Job is "exploration" or "recovery"
                ? worldCargo.FirstOrDefault(c => c.Id == id)?.Position
            : state.Job == "smuggling" ? Location(id)?.Position
            : enemies.FirstOrDefault(e => e.Id == id)?.Position;
        if (at is null)
            return null;
        var uncertainty =
            state.Job == "exploration"
                ? new[] { 1500f, 650f, 0f }[Math.Min(2, state.Progress.Count)]
                : 0;
        var point = at.Value;
        if (uncertainty > 0)
            point = new(
                MathF.Round(point.X / uncertainty) * uncertainty,
                MathF.Round(point.Y / uncertainty) * uncertainty
            );
        return new(
            id,
            "assignment",
            state.Job + (uncertainty > 0 ? " · approximate signal" : " · objective"),
            point.X,
            point.Y,
            uncertainty
        );
    }
}
