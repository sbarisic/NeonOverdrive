using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private sealed class ContactBody
    {
        public Player? Pilot;
        public Enemy? Npc;
        public Vector2 Start,
            Position,
            Velocity;
        public Vector2[] Hull = [];
        public float InverseMass,
            Radius;
        public int Index;
    }

    private readonly Dictionary<int, Vector2> contactStarts = [];
    private readonly List<ContactBody> contactBodies = [];
    private readonly SpatialGrid<ContactBody> contactGrid = new();

    private Vector2 ClearSpawn(
        Vector2 desired,
        float radius,
        int ignoreNpc = 0,
        int ignorePilot = 0
    )
    {
        bool Clear(Vector2 at) =>
            at.X >= radius
            && at.Y >= radius
            && at.X <= Width - radius
            && at.Y <= Height - radius
            && geometry?.TraceShips(at, at, radius) is null
            && !players.Values.Any(p =>
                p.Id != ignorePilot
                && p.Hull > 0
                && Vector2.Distance(at, p.Position)
                    < radius + ChassisCatalog.Get(p.Chassis).Radius + 2
            )
            && !enemies.Any(e =>
                e.Id != ignoreNpc
                && e.Hull > 0
                && Vector2.Distance(at, e.Position) < radius + EnemyRadius(e) + 2
            );
        if (Clear(desired))
            return desired;
        for (var ring = 1; ring <= 20; ring++)
        for (var i = 0; i < 24; i++)
        {
            var a = i * MathF.Tau / 24;
            var candidate =
                desired + new Vector2(MathF.Cos(a), MathF.Sin(a)) * ring * (radius + 10);
            if (Clear(candidate))
                return candidate;
        }
        // A crowded port must not create an overlapping body. The sector is much
        // larger than the bounded fleet; search other clear cells as a last resort.
        var stride = MathF.Max(64, radius * 2 + 4);
        for (var y = radius + 2; y < Height - radius; y += stride)
        for (var x = radius + 2; x < Width - radius; x += stride)
            if (Clear(new(x, y)))
                return new(x, y);
        throw new InvalidOperationException("No unoccupied ship launch position remains.");
    }

    private void AddEnemy(Enemy e)
    {
        if (e.Hull > 0 && !Stationary(e) && e.SurfacePlanet == 0)
        {
            e.Position = ClearSpawn(e.Position, EnemyRadius(e));
            e.Previous = e.Position;
        }
        enemies.Add(e);
    }

    private void BeginShipContacts()
    {
        contactStarts.Clear();
        foreach (var p in players.Values)
            contactStarts[p.Id] = p.Position;
        foreach (var e in enemies)
            contactStarts[-e.Id] = e.Position;
    }

    private static bool Stationary(Enemy e) =>
        e.Installation || e.Type is "turret" or "salvage-core" or "salvage-turret" or "repair-bot";

    private static Vector2[] ContactHull(Enemy e) =>
        Stationary(e) || e.SurfacePlanet != 0
            ? ShipContact.Disc(EnemyRadius(e))
            : ShipContact.Hull(e.Chassis, e.Heading, EnemyCatalog.Size(e.Level));

    private void StepShipContacts()
    {
        contactBodies.Clear();
        foreach (var p in players.Values.Where(p => p.Hull > 0))
        {
            var design = ChassisCatalog.Get(p.Chassis);
            contactBodies.Add(
                new()
                {
                    Pilot = p,
                    Start = contactStarts.GetValueOrDefault(p.Id, p.Position),
                    Position = p.Position,
                    Velocity = p.Velocity,
                    Hull = ShipContact.Hull(p.Chassis, p.Heading),
                    Radius = design.Radius,
                    InverseMass =
                        p.Menu || p.Editing || p.Docked != 0 || RaceCountdown(p)
                            ? 0
                            : 1 / (design.Length * design.Width),
                }
            );
        }
        var hailed = players
            .Values.Where(p => p.Connected && p.Hull > 0)
            .Select(p => ExplorationFor(p).Hailing)
            .ToHashSet();
        foreach (var e in enemies.Where(e => e.Hull > 0))
        {
            var start = contactStarts.GetValueOrDefault(-e.Id, e.Position);
            var design = ChassisCatalog.Get(e.Chassis);
            var size = EnemyCatalog.Size(e.Level);
            contactBodies.Add(
                new()
                {
                    Npc = e,
                    Start = start,
                    Position = e.Position,
                    Velocity = e.SurfacePlanet != 0 ? (e.Position - start) * TickRate : e.Velocity,
                    Hull = ContactHull(e),
                    Radius = EnemyRadius(e),
                    InverseMass =
                        Stationary(e)
                        || hailed.Contains(e.Id)
                        || e.State is "docking" or "contact" or "arrived"
                            ? 0
                            : 1 / (design.Length * design.Width * size * size),
                }
            );
        }
        for (var i = 0; i < contactBodies.Count; i++)
            contactBodies[i].Index = i;
        for (var pass = 0; pass < 4; pass++)
        {
            contactGrid.Rebuild(contactBodies, b => b.Position);
            var reach =
                contactBodies.Count == 0
                    ? 0
                    : contactBodies.Max(b => b.Radius + Vector2.Distance(b.Start, b.Position));
            var changed = false;
            foreach (var a in contactBodies)
            foreach (var b in contactGrid.Query(a.Start, a.Position, a.Radius + reach))
            {
                if (a.Index >= b.Index)
                    continue;
                if (
                    SectorCollision.DistanceToSegment(b.Position, a.Start, a.Position)
                    > a.Radius + b.Radius + Vector2.Distance(b.Start, b.Position)
                )
                    continue;
                var beforeA = a.Position;
                var beforeB = b.Position;
                if (
                    !ShipContact.Resolve(
                        a.Hull,
                        pass == 0 ? a.Start : a.Position,
                        ref a.Position,
                        ref a.Velocity,
                        a.InverseMass,
                        b.Hull,
                        pass == 0 ? b.Start : b.Position,
                        ref b.Position,
                        ref b.Velocity,
                        b.InverseMass
                    )
                )
                    continue;
                changed = true;
                if (a.InverseMass > 0 && geometry is not null)
                    a.Position = geometry.Move(beforeA, a.Position, a.Radius, ref a.Velocity);
                if (b.InverseMass > 0 && geometry is not null)
                    b.Position = geometry.Move(beforeB, b.Position, b.Radius, ref b.Velocity);
                ContactRam(a, b);
                ContactRam(b, a);
                foreach (var body in new[] { a, b })
                    if (body.Pilot is { } p)
                    {
                        p.CollisionAt = Tick;
                        CancelCruise(p);
                    }
            }
            if (!changed)
                break;
        }
        foreach (var b in contactBodies)
        {
            if (b.Pilot is { } p)
            {
                p.Position = b.Position;
                p.Velocity = b.Velocity;
            }
            if (b.Npc is { } e)
            {
                e.Position = b.Position;
                e.Velocity = b.Velocity;
            }
        }
    }

    private void ContactRam(ContactBody a, ContactBody b)
    {
        if (a.Npc is not { Type: "rammer" } ram || Tick < ram.FireAt)
            return;
        if (b.Pilot is { } p)
        {
            if (ram.Allied || p.Editing || Safe(p) || Tick < p.ProtectedUntil)
                return;
            Hit(
                new Shot { Owner = -ram.Id },
                p,
                null,
                EnemyCatalog.Damage(ram.Level) * (1 - Value(p, "impact"))
            );
        }
        else if (b.Npc is { } target)
        {
            if (ram.Allied == target.Allied)
                return;
            Hit(new Shot { Owner = -ram.Id }, null, target, EnemyCatalog.Damage(ram.Level));
        }
        else
            return;
        ram.FireAt = Tick + 30;
    }
}
