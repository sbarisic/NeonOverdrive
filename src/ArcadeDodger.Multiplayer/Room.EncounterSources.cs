using System.Collections.Immutable;
using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private void InitializeSector()
    {
        if (Sector is null)
            return;
        foreach (var l in Sector.Locations.Where(l => l.Kind is "base" or "corridor"))
        {
            sources[l.Id] = new() { Location = l, NextLaunch = l.Kind == "base" ? 600 : 1800 };
            if (l.Kind != "base")
                continue;
            enemies.Add(
                new()
                {
                    Id = ++nextEnemy,
                    Installation = true,
                    Home = l.Id,
                    Position = l.Position,
                    Previous = l.Position,
                    Hull = 120,
                    MaxHull = 120,
                    Level = EnemyCatalog.Region(l.Id),
                }
            );
            if (enemiesEnabled)
                for (var i = 0; i < 4; i++)
                    Launch(sources[l.Id], i % 2 == 1, i);
        }
    }

    private void Launch(Source source, bool gunner, int offset = 0)
    {
        var p = Exterior(
            source.Location.Position + new Vector2(200 + offset * 35, offset * 35),
            200
        );
        p = Vector2.Clamp(p, new(25), new(Width - 25, Height - 25));
        var level = EnemyCatalog.Region(source.Location.Id);
        var available = EnemyCatalog
            .Items.Where(t =>
                t.MinimumLevel <= level && t.Id is not ("turret" or "rover" or "relief")
            )
            .ToArray();
        var type = available[source.LaunchCount++ % available.Length];
        var hull = (int)MathF.Ceiling(type.Hull * EnemyCatalog.Health(level));
        enemies.Add(
            new()
            {
                Id = ++nextEnemy,
                Type = type.Id,
                Level = level,
                MaxHull = hull,
                Gunner = type.Id != "rammer",
                Home = source.Location.Id,
                Position = p,
                Previous = p,
                FireAt = Tick + 60,
                State = "launch",
                LaunchUntil = Tick + 60,
                Patrol = offset % 4,
                Hull = hull,
            }
        );
    }

    private void StepSources()
    {
        foreach (var source in sources.Values)
        {
            if (source.Destroyed)
                continue;
            if (source.Location.Surface && Tick % 600 == 0)
                for (
                    var i = enemies.Count(e =>
                        e.Home == source.Location.Id && e.Type == "rover" && e.Hull > 0
                    );
                    i < 2;
                    i++
                )
                    LaunchSurface(source.Location, false, i + 2);
            if (source.Location.Faction == "friendly")
                continue;
            var corridor = source.Location.Kind == "corridor";
            var count = enemies.Count(e =>
                !e.Installation
                && e.SurfacePlanet == 0
                && e.Hull > 0
                && e.Home == source.Location.Id
            );
            var quota = corridor ? 9 : 8;
            var volley = corridor ? 3 : 1;
            if (count + volley > quota)
                continue;
            if (Tick >= source.NextLaunch - (corridor ? 150 : 90) && source.Warning == 0)
                source.Warning = Tick;
            if (Tick < source.NextLaunch || enemies.Count + volley > EnemyLimit)
                continue;
            for (var i = 0; i < volley; i++)
                Launch(source, corridor ? i == 1 : nextEnemy % 2 == 0, i);
            source.Warning = 0;
            source.NextLaunch = Tick + (corridor ? 1800 : 600);
        }
    }
}
