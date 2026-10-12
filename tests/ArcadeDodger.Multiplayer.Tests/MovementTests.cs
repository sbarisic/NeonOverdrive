using System.Numerics;
using Xunit;
using static ArcadeDodger.Multiplayer.Tests.WorkshopRig;

namespace ArcadeDodger.Multiplayer.Tests;

public class MovementTests
{
    [Fact]
    public void AcceleratesThenCoastsOneSecondAndStops()
    {
        var r = Start();
        for (int i = 1; i <= 30; i++)
        {
            r.Input(1, new(i, 1, 0, 0, false, false));
            r.Step();
        }
        var a = r.Snapshot().Players[0];
        Assert.Equal(300, a.VelocityX);
        Assert.InRange(a.X, 1262, 1263);
        r.Input(1, new(31, 0, 0, 0, false, false));
        Step(r, 30);
        var b = r.Snapshot().Players[0];
        Assert.Equal(0, b.VelocityX);
        Assert.Equal(150, b.X - a.X, 2);
        Step(r, 30);
        Assert.Equal(b.X, r.Snapshot().Players[0].X);
    }

    [Fact]
    public void PacketBurstDoesNotMultiplyThrustAndMenuStopsWithoutProtection()
    {
        var r = Start();
        for (int i = 1; i <= 100; i++)
            r.Input(1, new(i, 1, 0, 0, false, false));
        r.Step();
        Assert.Equal(40, r.Snapshot().Players[0].VelocityX, 3);
        r.Input(1, new(101, 1, 0, 0, true, true));
        r.Step();
        Assert.Equal(0, r.Snapshot().Players[0].VelocityX);
        Assert.Empty(r.Snapshot().Shots);
        Assert.False(r.Snapshot().Players[0].Editing);
    }

    [Fact]
    public void CounterSteeringIsStrongerAndDiagonalsAreBounded()
    {
        Vector2 p = new(1000),
            v = new(300, 0);
        Movement.Step(ref p, ref v, -Vector2.UnitX, 1f / 30);
        Assert.Equal(240, v.X);
        v = Vector2.Zero;
        for (int i = 0; i < 60; i++)
            Movement.Step(ref p, ref v, Vector2.One, 1f / 30);
        Assert.Equal(300, v.Length(), 3);
    }

    [Fact]
    public void DisconnectCoastsAndReconnectDoesNotReplaceBody()
    {
        var r = Start();
        Set(Player(r), "Velocity", new Vector2(300, 0));
        r.SaveRuns();
        Assert.Equal(300, ((ProfileStore)Get(r, "profiles")).Get("Modules").Run!.VelocityX);
        r.Disconnect(1);
        Step(r, 3);
        var p = r.Snapshot().Players[0];
        Assert.Equal(270, p.VelocityX);
        Assert.Equal(1, r.Join("Modules"));
        Assert.Equal(p.X, r.Snapshot().Players[0].X);
        Assert.Equal(270, r.Snapshot().Players[0].VelocityX);
    }

    [Fact]
    public void BoundariesRemoveNormalVelocityAndPreserveSlide()
    {
        Vector2 p = new(Room.Width - 15, 1000),
            v = new(200, 200);
        Movement.Step(ref p, ref v, Vector2.Zero, 1f / 30);
        Assert.Equal(Room.Width - Room.Radius, p.X);
        Assert.Equal(0, v.X);
        Assert.True(v.Y > 0);
    }

    [Fact]
    public void KineticDefaultsAndConversionUseIndependentBaseSpeeds()
    {
        var stats = Room.CalculateWeaponStats([], 1);
        Assert.Equal(1300, stats.Speed);
        Assert.Equal(5, stats.Interval);
        var r = Start();
        Equip(r, Weapon(1, 25));
        Fire(r);
        r.Input(1, new(4, 0, 0, 0, false, false));
        Step(r, 2);
        var shot = Assert.Single(r.Snapshot().Shots);
        Assert.Equal(4, shot.Kind);
        Assert.Equal(520f / 30, new Vector2(shot.Dx, shot.Dy).Length(), 3);
    }

    [Fact]
    public void LegacyCheckpointSkillsMigrateWithoutChangingResourcesOrHealing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "-legacy.json");
        try
        {
            var store = new ProfileStore(path);
            store.Ensure("Legacy");
            store.Credit("Legacy", 42);
            store.Buy("Legacy", 6);
            var profile = store.Get("Legacy");
            var old = new[]
            {
                "phase.1",
                "longphase.1",
                "blink.1",
                "blink.2",
                "comet.1",
                "nova.1",
                "salvage.1",
            };
            var run = new RunCheckpoint(
                500,
                old,
                2,
                1,
                .25f,
                .5f,
                new double[4],
                new long[4],
                new long[4],
                99,
                150,
                200,
                40
            );
            File.WriteAllText(
                path,
                System
                    .Text.Json.JsonSerializer.Serialize(
                        new Dictionary<string, PilotProfile>
                        {
                            { "LEGACY", profile with { Run = run } },
                        }
                    )
                    .Replace("\"VelocityX\":200", "\"DashCooldown\":100")
            );
            var loaded = new ProfileStore(path);
            var after = loaded.Get("Legacy");
            Assert.Equal(profile.Credits, after.Credits);
            Assert.Equal(profile.Owned, after.Owned);
            Assert.Equal(2, after.Run!.Hull);
            Assert.Equal(.25f, after.Run.HullFraction);
            Assert.Equal(old.Select(SkillCatalog.Migrate), after.Run.Skills);
            Assert.Equal(after.Run.Skills, after.Run.Skills.Select(SkillCatalog.Migrate));
            var room = new Room(false, profiles: loaded);
            room.Join("Legacy");
            Assert.Equal(0, room.Snapshot().Players[0].VelocityX);
            Assert.Equal(0, room.Snapshot().Players[0].VelocityY);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
        }
    }

    [Fact]
    public void SixShotsPerSecondAndMenuStopsContinuousBeams()
    {
        var r = Start();
        var shots = (System.Collections.IList)Get(r, "shots");
        int count = 0;
        for (int i = 1; i <= 60; i++)
        {
            r.Input(1, new(i, 0, 0, 0, true, false));
            r.Step();
            count += shots.Count;
            shots.Clear();
        }
        Assert.Equal(12, count);
        var laser = Start();
        Equip(laser, Weapon(2));
        laser.Input(1, new(3, 0, 0, 0, true, true));
        laser.Step();
        Assert.Empty(laser.Snapshot().Beams!);
    }

    [Fact]
    public void ImpactDampenersHalveRammingDamage()
    {
        float Run(bool armor)
        {
            var r = Start(ai: true);
            if (armor)
                Set(Player(r), "Skills", new HashSet<string> { "impact.1" });
            var enemy = Enemy(r, 1000);
            Set(enemy, "Target", 1);
            r.Step();
            return (float)Get(Player(r), "ShieldFraction");
        }
        Assert.Equal(.25f, Run(false), 3);
        Assert.Equal(.125f, Run(true), 3);
    }

    [Fact]
    public void SweptMovementSlidesAgainstCoverAndRemovesNormalVelocity()
    {
        var sector = SectorDefinition.Generate(42);
        var geometry = new SectorCollision(sector);
        var obstacle = sector.Obstacles[0];
        Vector2 p = obstacle.Position - new Vector2(obstacle.Radius + Room.Radius + 1, 0),
            v = new(300, 80);
        for (int i = 0; i < 20; i++)
            Movement.Step(ref p, ref v, Vector2.UnitX, 1f / 30, geometry: geometry);
        Assert.True(Vector2.Distance(p, obstacle.Position) >= obstacle.Radius + Room.Radius - .1f);
        Assert.True(float.IsFinite(v.X));
    }
}
