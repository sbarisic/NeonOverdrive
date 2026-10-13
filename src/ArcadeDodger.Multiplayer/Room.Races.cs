using System.Globalization;
using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed record RaceRecord(string Key, string Category, float Seconds, float[] Ghost);

public sealed record RaceSnapshot(
    string Category,
    string State,
    int Checkpoint,
    float Seconds,
    int Countdown,
    float[] Course,
    float[] Ghost
);

public sealed partial class Room
{
    private sealed class RaceRun
    {
        public required string Run,
            Category,
            Key;
        public required Vector2[] Course;
        public long Start,
            ProgressAt,
            CollisionAt;
        public int Checkpoint = 1;
        public float Penalty;
        public string State = "countdown";
        public readonly List<float> Ghost = [];
        public float[] PreviousGhost = [];
        public float Result;
        public bool Touching;
    }

    private readonly Dictionary<int, RaceRun> races = [];

    private bool Racing(Player p) =>
        races.TryGetValue(p.Id, out var race)
        && race.Run == p.RunId
        && race.State is "countdown" or "racing";

    private bool RaceCountdown(Player p) => Racing(p) && races[p.Id].Start > Tick;

    private string JoinRace(Player p, int category)
    {
        if (category is < 0 or > 2 || Sector is null || navigation is null)
            return "invalid_race";
        if (Racing(p))
            return "already_racing";
        if (
            p.Menu
            || p.Editing
            || p.Docked != 0
            || p.EconomyPending
            || Living(p).Tow != 0
            || Living(p).Police.Length > 0
            || Living(p).IllegalCargo > 0
            || p.LastDamage >= 0 && Tick - p.LastDamage < 5 * TickRate
        )
            return "race_not_ready";
        var start = Location(10)!.Position + new Vector2(400, 0);
        if (Vector2.Distance(p.Position, start) > 500)
            return "return_to_race_start";
        var kind = new[] { "sprint", "obstacle", "rally" }[category];
        Vector2[] goals = category switch
        {
            0 =>
            [
                start + new Vector2(1100, 0),
                start + new Vector2(1100, 900),
                start + new Vector2(0, 900),
                start,
            ],
            1 =>
            [
                Exterior(Location(1)!.Position + new Vector2(-1500, 0)),
                start + new Vector2(0, -1600),
                start,
            ],
            _ =>
            [
                Location(11)!.Position + new Vector2(450, 0),
                Location(12)!.Position + new Vector2(450, 0),
                start,
            ],
        };
        var points = new List<Vector2> { start };
        foreach (var goal in goals)
        {
            var path = navigation.Route(points[^1], goal, ChassisCatalog.Get(p.Chassis).Radius);
            if (path.Length == 0)
                return "course_unavailable";
            foreach (var point in path)
                if (Vector2.Distance(points[^1], point) > 100)
                    points.Add(point);
        }
        var existing = races.Values.FirstOrDefault(r =>
            r.Category == kind && r.State == "countdown" && r.Start > Tick
        );
        var startAt = existing?.Start ?? Tick + 10 * TickRate;
        var key = string.Create(
            CultureInfo.InvariantCulture,
            $"flight{Movement.ModelVersion}:{Sector.Seed}:{kind}:{p.Chassis}:{Gear(p, 12)}:{MoveSpeedFor(p):F2}:{TurnRateFor(p):F3}:{AccelerationFor(p):F2}:{SteeringFor(p):F2}"
        );
        races[p.Id] = new()
        {
            Run = p.RunId,
            Category = kind,
            Key = key,
            Course = points.ToArray(),
            Start = startAt,
            ProgressAt = startAt,
            PreviousGhost =
                profiles.Get(p.Name).RaceRecords?.FirstOrDefault(r => r.Key == key)?.Ghost ?? [],
        };
        foreach (var shot in shots.Where(s => s.Owner == p.Id))
        {
            shot.Cancelled = true;
            shot.Expires = Tick;
        }
        foreach (var mine in mines.Where(m => m.Payload.Owner == p.Id))
            mine.Payload.Cancelled = true;
        foreach (var enemy in enemies)
        {
            if (enemy.BurnSource?.Owner == p.Id)
            {
                enemy.BurnSource = null;
                enemy.BurnUntil = 0;
            }
            if (enemy.RadiationSource?.Owner == p.Id)
            {
                enemy.RadiationSource = null;
                enemy.RadiationUntil = 0;
            }
        }
        p.Throttle = 0;
        p.Velocity = Vector2.Zero;
        Broadcast(
            "race",
            "Race control",
            $"{kind} starts in {(startAt - Tick) / TickRate}s. Combat disabled for entrants."
        );
        return "ok";
    }

    private string StopRace(Player p)
    {
        if (races.TryGetValue(p.Id, out var r))
            r.State = "abandoned";
        return "ok";
    }

    private void StepRaces()
    {
        foreach (var (id, race) in races)
        {
            if (race.State is not ("countdown" or "racing"))
                continue;
            if (
                !players.TryGetValue(id, out var p)
                || p.RunId != race.Run
                || !p.Connected
                || p.Hull <= 0
                || p.Menu
                || p.Editing
            )
            {
                race.State = "abandoned";
                continue;
            }
            if (Tick < race.Start)
                continue;
            race.State = "racing";
            if (
                Tick - race.Start > 900 * TickRate
                || Tick - race.ProgressAt > 90 * TickRate
                || !Enumerable
                    .Range(1, race.Course.Length - 1)
                    .Any(i =>
                        SectorCollision.DistanceToSegment(
                            p.Position,
                            race.Course[i - 1],
                            race.Course[i]
                        ) < 700
                    )
            )
            {
                race.State = "disqualified";
                continue;
            }
            if (Tick % 15 == 0 && race.Ghost.Count < 5400)
                race.Ghost.AddRange([
                    (Tick - race.Start) / (float)TickRate,
                    p.Position.X,
                    p.Position.Y,
                ]);
            var touching = p.CollisionAt == Tick;
            if (
                race.Category == "obstacle"
                && touching
                && !race.Touching
                && Tick - race.CollisionAt > TickRate
                && Vector2.DistanceSquared(p.Position, p.Previous) > .01f
            )
            {
                race.CollisionAt = Tick;
                race.Penalty += 2;
            }
            race.Touching = touching;
            if (
                SectorCollision.DistanceToSegment(
                    race.Course[race.Checkpoint],
                    p.Previous,
                    p.Position
                ) > 100
            )
                continue;
            race.ProgressAt = Tick;
            if (++race.Checkpoint < race.Course.Length)
                continue;
            race.Result = (Tick - race.Start) / (float)TickRate + race.Penalty;
            race.State = "saving";
            SaveRaceResult(p, race);
        }
    }

    private void SaveRaceResult(Player p, RaceRun race)
    {
        race.State = "saving";
        var record = new RaceRecord(race.Key, race.Category, race.Result, race.Ghost.ToArray());
        Persist(
            () => profiles.SaveRace(p.Name, record),
            result => race.State = result == "ok" ? "finished" : "save_failed"
        );
    }

    private string RetryRaceSave(Player p)
    {
        if (
            !races.TryGetValue(p.Id, out var race)
            || race.Run != p.RunId
            || race.State != "save_failed"
        )
            return "no_failed_record";
        SaveRaceResult(p, race);
        return race.State == "finished" ? "ok" : "pending";
    }

    private RaceSnapshot? RaceFor(Player p) =>
        races.TryGetValue(p.Id, out var r) && r.Run == p.RunId
            ? new(
                r.Category,
                r.State,
                r.Checkpoint,
                r.Result > 0
                    ? r.Result
                    : MathF.Max(0, (Tick - r.Start) / (float)TickRate) + r.Penalty,
                (int)Math.Max(0, (r.Start - Tick) / TickRate),
                r.Course.SelectMany(v => new[] { v.X, v.Y }).ToArray(),
                r.PreviousGhost
            )
            : null;
}

public sealed partial class ProfileStore
{
    public string SaveRace(string name, RaceRecord record)
    {
        var p = Get(name);
        var records = p.RaceRecords ?? [];
        if (
            !float.IsFinite(record.Seconds)
            || record.Seconds <= 0
            || record.Ghost.Length > 5400
            || record.Ghost.Length % 3 != 0
            || record.Ghost.Any(v => !float.IsFinite(v))
        )
            return "invalid_race";
        if (records.Any(r => r.Key == record.Key && r.Seconds <= record.Seconds))
            return "ok";
        return Commit(
            name,
            p with
            {
                RaceRecords = [record, .. records.Where(r => r.Key != record.Key).Take(31)],
            }
        );
    }
}
