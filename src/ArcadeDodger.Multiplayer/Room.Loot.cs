using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed record ModuleDropSnapshot(
    int Id,
    float X,
    float Y,
    int Module,
    int Owner,
    string Kind = "module",
    int Tier = 0,
    int GearKind = -1
);

public sealed partial class Room
{
    private sealed record ModuleDrop(
        int Id,
        Vector2 Position,
        int Module,
        long Expires,
        int Owner,
        string RunId,
        WeaponItem? Weapon = null,
        ShipItem? Gear = null
    )
    {
        public Vector2 Previous = Position,
            Velocity;
        public bool Pending;
    }

    private readonly List<ModuleDrop> moduleDrops = [];
    private int nextModuleDrop;
    private readonly List<Energy> pendingEnergy = [];
    private readonly List<Credit> pendingCredits = [];
    private readonly List<ModuleDrop> pendingModules = [];

    private void AdmitPersonalDrops()
    {
        pendingEnergy.RemoveAll(d => d.Expires <= Tick);
        pendingCredits.RemoveAll(d => d.Expires <= Tick);
        pendingModules.RemoveAll(d => d.Expires <= Tick);
        while (energy.Count < EnergyCapacity && pendingEnergy.Count > 0)
        {
            energy.Add(pendingEnergy[0]);
            pendingEnergy.RemoveAt(0);
        }
        while (credits.Count < 128 && pendingCredits.Count > 0)
        {
            credits.Add(pendingCredits[0]);
            pendingCredits.RemoveAt(0);
        }
        while (moduleDrops.Count < 128 && pendingModules.Count > 0)
        {
            moduleDrops.Add(pendingModules[0]);
            pendingModules.RemoveAt(0);
        }
    }

    private (Vector2 Position, Vector2 Velocity) Eject(Enemy enemy, int seed)
    {
        var surface = Location(
            enemy.SurfacePlanet != 0 ? enemy.SurfacePlanet : Location(enemy.Home)?.Parent ?? 0
        );
        var outward = surface is { Solid: true }
            ? MathF.Atan2(enemy.Position.Y - surface.Y, enemy.Position.X - surface.X)
            : (float?)null;
        for (var ring = 0; ring < 20; ring++)
        for (var direction = 0; direction < 32; direction++)
        {
            var angle = (seed * .618034f + direction / 32f) * MathF.Tau;
            if (outward is { } radial)
                angle = radial + ((seed * .618034f + direction / 32f) % 1 - .5f) * MathF.PI;
            var normal = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var point =
                enemy.Position
                + normal * (EnemyRadius(enemy) + (enemy.Installation ? 24 : 2) + ring * 24);
            if (point.X < 16 || point.Y < 16 || point.X > Width - 16 || point.Y > Height - 16)
                continue;
            if (geometry?.TraceShips(point, point, 12) is not null)
                continue;
            return (point, normal * (enemy.Installation ? 180 : 60));
        }
        // Guaranteed clear arrival exit if dense geometry exhausts local placement.
        return (SelectSpawn(), Vector2.Zero);
    }

    private Vector2 MovePickup(Vector2 position, ref Vector2 velocity)
    {
        var next = MoveShip(position, position + velocity / TickRate, 12);
        velocity *= .78f;
        if (velocity.LengthSquared() < 1)
            velocity = Vector2.Zero;
        return next;
    }

    private void StepPickupMotion()
    {
        for (var i = 0; i < energy.Count; i++)
        {
            var d = energy[i];
            var v = d.Velocity;
            energy[i] = d with
            {
                Previous = d.Position,
                Position = MovePickup(d.Position, ref v),
                Velocity = v,
            };
        }
        for (var i = 0; i < credits.Count; i++)
        {
            var d = credits[i];
            if (d.Pending)
                continue;
            var v = d.Velocity;
            credits[i] = d with
            {
                Previous = d.Position,
                Position = MovePickup(d.Position, ref v),
                Velocity = v,
            };
        }
        for (var i = 0; i < moduleDrops.Count; i++)
        {
            var d = moduleDrops[i];
            if (d.Pending)
                continue;
            var v = d.Velocity;
            moduleDrops[i] = d with
            {
                Previous = d.Position,
                Position = MovePickup(d.Position, ref v),
                Velocity = v,
            };
        }
    }

    private bool Eligible(Player p, int owner) =>
        p.Hull > 0
        && p.Connected
        && !p.Editing
        && !Racing(p)
        && p.Docked == 0
        && (owner == 0 || p.Id == owner);

    private void StepModuleDrops()
    {
        moduleDrops.RemoveAll(d => Tick >= d.Expires && !d.Pending);
        foreach (var d in moduleDrops.ToArray())
        {
            if (
                d.Pending
                || !players.TryGetValue(d.Owner, out var p)
                || !Eligible(p, d.Owner)
                || p.RunId != d.RunId
            )
                continue;
            if (
                Intersection(
                    p.Previous - d.Previous,
                    p.Position - d.Position,
                    Radius + 12 + Value(p, "collector")
                )
                is null
            )
                continue;
            d.Pending = true;
            PersistForRun(
                p,
                () =>
                    d.Weapon is not null || d.Gear is not null
                        ? profiles.GrantEquipment(p.Name, d.Weapon, d.Gear)
                        : profiles.GrantModule(p.Name, d.Module),
                result =>
                {
                    if (result == "ok")
                    {
                        moduleDrops.RemoveAll(m => m.Id == d.Id);
                        p.LootNotice = d.Gear is { } gear
                            ? $"Recovered {EquipmentCatalog.Items[gear.Kind].Name} {gear.Tier}"
                            : $"Recovered {ModuleCatalog.Get(d.Module).Name}{(d.Weapon is not null ? " build" : "")}";
                        p.LootNoticeUntil = Tick + 150;
                    }
                    else
                        d.Pending = false;
                }
            );
        }
    }

    private void RewardEncounter(Enemy enemy)
    {
        foreach (var (id, contribution) in enemy.Contributors)
        {
            var p =
                players.GetValueOrDefault(id) ?? suspended.Values.FirstOrDefault(p => p.Id == id);
            if (p is null || p.Hull <= 0 || p.RunId != contribution.Run || contribution.Damage < 1)
                continue;
            var state = ExplorationFor(p);
            if (state.Job == "bounty" && !enemy.Installation && enemy.SurfacePlanet == 0)
                state.JobProgress.Add(enemy.Id);
            if (
                Contracts.FirstOrDefault(c => c.Id == state.Contract)
                    is { Kind: "interdict" } contract
                && contract.Target == enemy.Home
                && !enemy.Installation
            )
            {
                state.Progress.Add(enemy.Id);
                state.Ready = state.Progress.Count >= 3;
            }
            if (
                !enemy.Allied
                && CreditRoll(enemy.Id ^ 0x517c3, id) < (enemy.Installation ? .35f : .15f)
            )
            {
                var region = Location(
                    enemy.SurfacePlanet != 0
                        ? enemy.SurfacePlanet
                        : Location(enemy.Home)?.Parent ?? 0
                );
                region ??= Sector
                    ?.Locations.Where(l => l.Kind == "planet")
                    .OrderBy(l => Vector2.DistanceSquared(l.Position, enemy.Position))
                    .FirstOrDefault();
                var tier = Math.Clamp(region?.Id ?? 1, 1, 4);
                var roll = CreditRoll(enemy.Id ^ 0x725b1, id);
                var componentLevel = ModuleLevels.Roll(
                    enemy.Level,
                    CreditRoll(enemy.Id ^ 0x21a11, id)
                );
                var weapon =
                    roll < .5f
                        ? EquipmentCatalog.Build(1 + (int)(roll * 8), tier, componentLevel)
                        : null;
                var gear = weapon is null
                    ? new ShipItem(
                        Guid.NewGuid().ToString("N"),
                        EquipmentCatalog.GearForRoll(
                            (roll - .5f) * 2,
                            CreditRoll(enemy.Id ^ 0x59a2, id)
                        ),
                        tier
                    )
                    : null;
                var point = Eject(enemy, id + 3);
                pendingModules.Add(
                    new(
                        ++nextModuleDrop,
                        point.Position,
                        weapon?.Emitter ?? 0,
                        Tick + EnergyLifetimeTicks,
                        p.Id,
                        p.RunId,
                        weapon,
                        gear
                    )
                    {
                        Velocity = point.Velocity,
                    }
                );
            }
            if (!enemy.Installation)
                continue;
            p.LootNotice = "Installation cleared - personal rewards nearby";
            p.LootNoticeUntil = Tick + 150;
            var reward = Eject(enemy, id);
            var xp = Eject(enemy, id + 1);
            pendingEnergy.Add(
                new(++nextEnergy, xp.Position, 30 * enemy.Level, Tick + EnergyLifetimeTicks, p.Id)
                {
                    Velocity = xp.Velocity,
                }
            );
            pendingCredits.Add(
                new(
                    ++nextCredit,
                    reward.Position,
                    (int)
                        MathF.Ceiling(
                            40 * enemy.Level * (Tick < enemy.MarkUntil ? enemy.MarkValue : 1)
                        ),
                    Tick + EnergyLifetimeTicks,
                    p.Id
                )
                {
                    Velocity = reward.Velocity,
                }
            );
            if (CreditRoll(enemy.Id, id) < .5f)
            {
                var module = 1 + (int)(CreditRoll(enemy.Id ^ 0x13579, id) * 28);
                module = ModuleLevels.Key(
                    module,
                    ModuleLevels.Roll(enemy.Level, CreditRoll(enemy.Id ^ 0x42931, id))
                );
                var point = Eject(enemy, id + 2);
                pendingModules.Add(
                    new(
                        ++nextModuleDrop,
                        point.Position,
                        module,
                        Tick + EnergyLifetimeTicks,
                        p.Id,
                        p.RunId
                    )
                    {
                        Velocity = point.Velocity,
                    }
                );
            }
        }
    }
}
