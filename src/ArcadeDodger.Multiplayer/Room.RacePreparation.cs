using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private sealed class RacePreparation
    {
        public required string Key;
        public required Vector2[] Goals;
        public readonly List<Vector2> Points = [];
        public Vector2[] Path = [];
        public float Radius;
        public int Leg;
        public bool Failed;
        public SectorNavigation.Search? Search;
    }

    private readonly Dictionary<string, RacePreparation> raceCourses = [];

    private RacePreparation PrepareRace(int category, Vector2[] goals, Vector2 start, float radius)
    {
        var key = $"{category}:{radius:R}";
        if (raceCourses.TryGetValue(key, out var cached))
            return cached;
        var value = new RacePreparation
        {
            Key = key,
            Goals = goals,
            Radius = radius,
        };
        value.Points.Add(start);
        raceCourses[key] = value;
        return value;
    }

    private void AdvanceRaceCourses()
    {
        var budget = 256;
        foreach (var course in raceCourses.Values.Where(c => !c.Failed && c.Path.Length == 0))
        {
            if (budget <= 0)
                break;
            course.Search ??= navigation!.Begin(
                course.Points[^1],
                course.Goals[course.Leg],
                course.Radius
            );
            var before = course.Search.WorkItems;
            course.Search.Advance(Math.Min(64, budget));
            budget -= course.Search.WorkItems - before;
            if (!course.Search.Complete)
                continue;
            if (course.Search.Path.Length == 0)
                course.Failed = true;
            foreach (var point in course.Search.Path)
                if (Vector2.Distance(course.Points[^1], point) > 100)
                    course.Points.Add(point);
            course.Search.Dispose();
            course.Search = null;
            if (++course.Leg == course.Goals.Length && !course.Failed)
                course.Path = course.Points.ToArray();
        }
    }

    private bool ReadyAtGrid(Player pilot, RaceRun race) =>
        pilot.Connected
        && pilot.Hull > 0
        && (pilot.LastDamage < 0 || Tick - pilot.LastDamage >= 5 * TickRate)
        && Vector2.Distance(pilot.Position, race.Stage) <= 20
        && pilot.Velocity.Length() < 5
        && MathF.Abs(MathF.IEEERemainder(pilot.Heading - race.StageHeading, MathF.Tau))
            <= MathF.PI / 18;

    private void StageRace(Player pilot, RaceRun race)
    {
        if (race.State == "preparing")
        {
            var preparation = raceCourses[race.Preparation];
            if (preparation.Failed)
            {
                race.State = "course_unavailable";
                return;
            }
            if (preparation.Path.Length < 2)
                return;
            race.Course = preparation.Path;
            var direction = Vector2.Normalize(race.Course[1] - race.Course[0]);
            var side = new Vector2(-direction.Y, direction.X);
            var radius = ChassisCatalog.Get(pilot.Chassis).Radius;
            var found = false;
            for (var i = 0; i < 48; i++)
            {
                var offset = i == 0 ? 0 : (i + 1) / 2 * 120 * (i % 2 == 0 ? -1 : 1);
                var at = race.Course[0] + side * offset;
                if (
                    at.X < radius
                    || at.Y < radius
                    || at.X > Width - radius
                    || at.Y > Height - radius
                    || geometry!.TraceShips(at, at, radius) is not null
                    || geometry.TraceShips(at, race.Course[1], radius) is not null
                    || races.Values.Any(r =>
                        r != race
                        && r.State is "staging" or "countdown"
                        && Vector2.Distance(r.Stage, at) < 115
                    )
                    || players.Values.Any(other =>
                        other != pilot
                        && other.Hull > 0
                        && Vector2.Distance(other.Position, at)
                            < ChassisCatalog.Get(other.Chassis).Radius + radius + 5
                    )
                    || enemies.Any(e =>
                        e.Hull > 0 && Vector2.Distance(e.Position, at) < EnemyRadius(e) + radius + 5
                    )
                )
                    continue;
                race.Stage = at;
                found = true;
                break;
            }
            if (!found)
            {
                race.State = "staging_unavailable";
                return;
            }
            race.StageHeading = MathF.Atan2(direction.Y, direction.X);
            race.StageDeadline = Tick + 60 * TickRate;
            race.State = "staging";
        }
        race.Staged = ReadyAtGrid(pilot, race);
        if (!race.Staged && Tick >= race.StageDeadline)
        {
            race.State = "staging_timeout";
            return;
        }
        var group = races
            .Values.Where(r => r.Category == race.Category && r.State is "preparing" or "staging")
            .ToArray();
        foreach (var entrant in races.Where(pair => group.Contains(pair.Value)))
            entrant.Value.Staged =
                players.TryGetValue(entrant.Key, out var ship) && ReadyAtGrid(ship, entrant.Value);
        if (group.Any(r => r.State == "preparing" || !r.Staged))
            return;
        foreach (var ready in group)
        {
            ready.State = "countdown";
            ready.Start = ready.ProgressAt = Tick + 10 * TickRate;
        }
    }
}
