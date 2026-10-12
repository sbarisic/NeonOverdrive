using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed record MineSnapshot(int Id, int Owner, float X, float Y, bool Armed, float Power = 1);

public sealed partial class Room
{
    private readonly List<ExplosionSnapshot> explosions = [];
    private int nextExplosion,
        nextMine;

    private sealed record Mine(int Id, Shot Payload, long ArmedAt, long Expires);

    private readonly List<Mine> mines = [];

    private static ModuleDefinition Definition(int id) => ModuleCatalog.Get(id);

    private static int Duration(int id) => (int)MathF.Ceiling(Definition(id).Seconds * TickRate);

    private float Hit(Shot s, Player? p, Enemy? e, float amount)
    {
        if (amount <= 0)
            return 0;
        float actual = 0;
        if (p is not null)
        {
            if (p.Hull <= 0 || p.Editing || Safe(p) || Tick < p.ProtectedUntil || Friendly(s, p))
                return 0;
            p.LastDamage = Tick;
            var shield = MathF.Min(
                MathF.Max(0, p.Shield - p.ShieldFraction),
                amount * s.ShieldMultiplier
            );
            amount -= shield / s.ShieldMultiplier;
            actual += shield;
            p.ShieldFraction += shield;
            while (p.Shield > 0 && p.ShieldFraction >= 1 - .00001f)
            {
                p.Shield--;
                p.ShieldFraction = MathF.Max(0, p.ShieldFraction - 1);
            }
            var hull = MathF.Min(
                MathF.Max(0, p.Hull - p.HullFraction),
                amount * (1 - .05f * Gear(p, 9))
            );
            actual += hull;
            p.HullFraction += hull;
            while (p.Hull > 0 && p.HullFraction >= 1 - .00001f)
            {
                p.Hull--;
                p.HullFraction = MathF.Max(0, p.HullFraction - 1);
            }
            if (p.Hull <= 0)
                PilotDied(p);
            p.ShieldAt = Tick + ShieldDelay(p);
            p.HullAt = Tick + 10 * TickRate;
        }
        if (e is not null && e.Hull > 0 && !Sanctuary(e.Position) && CanHit(s, e))
        {
            if (players.TryGetValue(s.Owner, out var attacker))
            {
                if (!e.Installation && !attacker.Editing && !Safe(attacker))
                {
                    e.Investigate = attacker.Position;
                    e.InvestigateUntil = Tick + 150;
                }
            }
            e.LastHit = Tick;
            var absorbed = MathF.Min(
                MathF.Max(0, e.Shield - e.ShieldFraction),
                amount * s.ShieldMultiplier
            );
            e.ShieldFraction += absorbed;
            while (e.Shield > 0 && e.ShieldFraction >= 1 - .00001f)
            {
                e.Shield--;
                e.ShieldFraction = MathF.Max(0, e.ShieldFraction - 1);
            }
            amount -= absorbed / s.ShieldMultiplier;
            amount *= 1 + (Tick < e.CorrosionUntil ? e.Corrosion : 0);
            actual = MathF.Min(MathF.Max(0, e.Hull - e.DamageFraction), amount);
            e.DamageFraction += actual;
            RecordContribution(e, s, actual + absorbed);
            while (e.Hull > 0 && e.DamageFraction >= 1 - .00001f)
            {
                e.Hull--;
                e.DamageFraction = MathF.Max(0, e.DamageFraction - 1);
            }
            if (s.Burns > 0)
            {
                if (
                    Tick >= e.BurnUntil
                    || Definition(17).Value * s.Burns * s.StatusPower >= e.BurnPower
                )
                    e.BurnSource = s.Copy();
                e.BurnPower = MathF.Max(
                    Tick < e.BurnUntil ? e.BurnPower : 0,
                    Definition(17).Value * s.Burns * s.StatusPower
                );
                e.BurnUntil = Tick + Duration(17);
            }
            if (s.Rads > 0)
            {
                if (
                    Tick >= e.RadiationUntil
                    || Definition(19).Value * s.Rads * s.StatusPower >= e.RadPower
                )
                    e.RadiationSource = s.Copy();
                e.RadPower = MathF.Max(
                    Tick < e.RadiationUntil ? e.RadPower : 0,
                    Definition(19).Value * s.Rads * s.StatusPower
                );
                e.RadiationUntil = Tick + Duration(19);
            }
            if (s.Freezes > 0 && !e.Installation)
            {
                e.SlowFactor = MathF.Min(
                    Tick < e.SlowUntil ? e.SlowFactor : 1,
                    MathF.Pow(Definition(18).Value, s.Freezes)
                );
                e.SlowUntil = Tick + Duration(18);
            }
            if (s.Corrodes > 0)
            {
                e.Corrosion = MathF.Max(
                    Tick < e.CorrosionUntil ? e.Corrosion : 0,
                    Definition(20).Value * s.Corrodes
                );
                e.CorrosionUntil = Tick + Duration(20);
            }
            if (s.SalvageMark)
                e.MarkUntil = Tick + Duration(28);
            if (s.Teslas > 0 && !e.Installation)
            {
                e.Charge += actual * s.Teslas;
                if (e.Charge >= Definition(21).Value && Tick >= e.StunReady)
                {
                    e.Charge -= Definition(21).Value;
                    e.StunUntil = Tick + Duration(21);
                    e.StunReady = Tick + 30;
                }
            }
        }
        Recover(s, actual);
        return actual;
    }

    private void Recover(Shot? s, float actual)
    {
        if (
            s is null
            || s.Leeches <= 0
            || !players.TryGetValue(s.Owner, out var owner)
            || owner.Hull <= 0
        )
            return;
        owner.LeechFraction += actual * Definition(27).Value * s.Leeches;
        while (
            owner.LeechFraction >= 1
            && (owner.Shield < MaxShieldFor(owner) || owner.ShieldFraction > 0)
        )
        {
            owner.Shield = Math.Min(MaxShieldFor(owner), owner.Shield + 1);
            owner.LeechFraction--;
            if (owner.Shield == MaxShieldFor(owner))
                owner.ShieldFraction = 0;
        }
        if (owner.Shield >= MaxShieldFor(owner) && owner.ShieldFraction == 0)
            owner.LeechFraction = 0;
    }

    private void Contact(Shot s, Player? p, Enemy? e, Vector2 at)
    {
        var amount = s.IsBeam ? s.Damage / TickRate : s.Damage;
        Hit(s, p, e, amount);
        if (s.Secondary)
            return;
        if (!s.IsBeam || Tick % 15 == s.Weapon % 15)
            Explode(s, at, e, p);
        if (s.Relays > 0)
        {
            var visitedP = new HashSet<int>(s.HitPlayers);
            var visitedE = new HashSet<int>(s.HitEnemies);
            if (p is not null)
                visitedP.Add(p.Id);
            if (e is not null)
                visitedE.Add(e.Id);
            var point = at;
            var power = amount;
            for (var jump = 0; jump < s.Relays; jump++)
            {
                var candidates = new List<(Vector2 Pos, Player? P, Enemy? E)>();
                candidates.AddRange(
                    players
                        .Values.Where(t =>
                            t.Id != s.Owner
                            && t.Hull > 0
                            && !t.Editing
                            && Tick >= t.ProtectedUntil
                            && !Friendly(s, t)
                            && !visitedP.Contains(t.Id)
                        )
                        .Select(t => (t.Position, (Player?)t, (Enemy?)null))
                );
                candidates.AddRange(
                    enemies
                        .Where(t => t.Hull > 0 && CanHit(s, t) && !visitedE.Contains(t.Id))
                        .Select(t => (t.Position, (Player?)null, (Enemy?)t))
                );
                var next = candidates
                    .Where(t =>
                        VisibleLine(point, t.Pos)
                        && Vector2.DistanceSquared(t.Pos, point)
                            <= Definition(16).Radius * Definition(16).Radius
                    )
                    .OrderBy(t => Vector2.DistanceSquared(t.Pos, point))
                    .FirstOrDefault();
                if (next.P is null && next.E is null)
                    break;
                power *= Definition(16).Value;
                Hit(s, next.P, next.E, power);
                AddExplosion(next.Pos, 12);
                if (next.P is not null)
                    visitedP.Add(next.P.Id);
                if (next.E is not null)
                    visitedE.Add(next.E.Id);
                point = next.Pos;
            }
        }
    }

    private void AddExplosion(Vector2 at, float radius)
    {
        explosions.Add(new(++nextExplosion, at.X, at.Y, radius, Tick));
        if (explosions.Count > 32)
            explosions.RemoveAt(0);
    }

    private void Explode(Shot s, Vector2 position, Enemy? directEnemy, Player? directPlayer)
    {
        var radius = MathF.Max(
            s.Blast,
            s.Blasts > 0 ? Definition(23).Radius * s.RadiusMultiplier : 0
        );
        if (radius <= 0)
            return;
        var amount = (s.Blast > 0 ? s.Damage : 0) + s.Damage * Definition(23).Value * s.Blasts;
        amount *= s.SplashMultiplier;
        if (s.IsBeam)
            amount *= .5f;
        foreach (var e in enemyGrid.Query(position, position, radius))
            if (
                e != directEnemy
                && e.Hull > 0
                && VisibleLine(position, e.Position)
                && Vector2.DistanceSquared(e.Position, position) <= radius * radius
            )
                Hit(s, null, e, amount);
        foreach (var p in playerGrid.Query(position, position, radius))
            if (
                p != directPlayer
                && p.Id != s.Owner
                && VisibleLine(position, p.Position)
                && Vector2.DistanceSquared(p.Position, position) <= radius * radius
            )
                Hit(s, p, null, amount);
        AddExplosion(position, radius);
    }

    private bool AddMine(Shot s)
    {
        if (mines.Count >= 128 || mines.Count(m => m.Payload.Owner == s.Owner) >= 32)
            return false;
        var payload = s.Copy();
        payload.IsBeam = false;
        payload.Blast = MathF.Max(Definition(26).Radius * payload.RadiusMultiplier, payload.Blast);
        payload.Secondary = true;
        mines.Add(new(++nextMine, payload, Tick + 15, Tick + Duration(26)));
        return true;
    }

    private void StepMines()
    {
        for (var i = mines.Count - 1; i >= 0; i--)
        {
            var m = mines[i];
            if (Tick >= m.Expires || m.Payload.Cancelled)
            {
                mines.RemoveAt(i);
                continue;
            }
            if (Tick < m.ArmedAt)
                continue;
            var s = m.Payload;
            if (
                enemyGrid
                    .Query(s.Position, s.Position, 140)
                    .Any(e =>
                        e.Hull > 0
                        && CanHit(s, e)
                        && !Sanctuary(e.Position)
                        && VisibleLine(s.Position, e.Position)
                        && Vector2.DistanceSquared(e.Position, s.Position)
                            <= MathF.Pow(e.Installation ? 140 : 55, 2)
                    )
                || playerGrid
                    .Query(s.Position, s.Position, 55)
                    .Any(p =>
                        p.Id != s.Owner
                        && p.Hull > 0
                        && !p.Editing
                        && !Safe(p)
                        && VisibleLine(s.Position, p.Position)
                        && Tick >= p.ProtectedUntil
                        && !Friendly(s, p)
                        && Vector2.DistanceSquared(p.Position, s.Position) <= 55 * 55
                    )
            )
            {
                Explode(s, s.Position, null, null);
                mines.RemoveAt(i);
            }
        }
    }

    private void StepStatuses()
    {
        explosions.RemoveAll(e => Tick - e.Born > 10);
        // Snapshot sources before applying pulses; death during a pulse cannot invalidate enumeration.
        foreach (var e in enemies.Where(e => e.Hull > 0).ToArray())
        {
            if (Tick <= e.BurnUntil)
                StatusDamage(e, e.BurnPower / TickRate, e.BurnSource);
            if (e.Hull > 0 && Tick <= e.RadiationUntil)
                foreach (
                    var target in enemyGrid
                        .Query(e.Position, e.Position, 100)
                        .Where(n =>
                            n.Hull > 0
                            && VisibleLine(e.Position, n.Position)
                            && Vector2.DistanceSquared(n.Position, e.Position)
                                <= Definition(19).Radius * Definition(19).Radius
                        )
                        .ToArray()
                )
                    StatusDamage(target, e.RadPower / TickRate, e.RadiationSource);
        }
    }

    private void StatusDamage(Enemy e, float amount, Shot? source)
    {
        if (Sanctuary(e.Position) || source is not null && !CanHit(source, e))
            return;
        amount *= 1 + (Tick < e.CorrosionUntil ? e.Corrosion : 0);
        var actual = MathF.Min(MathF.Max(0, e.Hull - e.DamageFraction), amount);
        e.DamageFraction += actual;
        Recover(source, actual);
        if (actual > 0)
            RecordContribution(e, source, actual);
        while (e.Hull > 0 && e.DamageFraction >= 1 - .00001f)
        {
            e.Hull--;
            e.DamageFraction = MathF.Max(0, e.DamageFraction - 1);
        }
    }
}
