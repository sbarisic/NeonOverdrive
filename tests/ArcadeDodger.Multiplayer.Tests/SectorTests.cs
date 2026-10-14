using System.Collections;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Xunit;
using static ArcadeDodger.Multiplayer.Tests.WorkshopRig;

namespace ArcadeDodger.Multiplayer.Tests;

public class SectorTests
{
    private static Room World(bool ai = false, int seed = 123, ProfileStore? store = null)
    {
        var r = new Room(ai, profiles: store, seed: seed);
        r.Join("SectorPilot");
        return r;
    }

    private static void At(Room r, Vector2 position, int id = 1)
    {
        Set(Player(r, id), "Position", position);
        Set(Player(r, id), "Previous", position);
        Set(Player(r, id), "ProtectedUntil", 0L);
    }

    private static Vector2 Position(Room r, int id) =>
        r.Sector!.Locations.Single(l => l.Id == id).Position;

    [Fact]
    public void SeededLayoutHasExactLandmarksAndClearRoutesAcrossManySeeds()
    {
        for (var seed = 0; seed < 30; seed++)
        {
            var a = SectorDefinition.Generate(seed);
            Assert.Equal(
                JsonSerializer.Serialize(a),
                JsonSerializer.Serialize(SectorDefinition.Generate(seed))
            );
            Assert.Equal(4, a.Locations.Count(l => l.Kind == "planet"));
            Assert.Equal(3, a.Locations.Count(l => l.Kind == "station"));
            Assert.Equal(4, a.Locations.Count(l => l.Kind == "base"));
            Assert.Equal(2, a.Locations.Count(l => l.Kind == "corridor"));
            Assert.Equal(8, a.Locations.Count(l => l.Kind == "cluster"));
            Assert.Equal(6, a.Locations.Count(l => l.Kind == "wreck"));
            Assert.NotEmpty(a.Obstacles);
            var geometry = new SectorCollision(a);
            var nodes = a
                .Locations.Where(l =>
                    l.Kind is "station" or "base" or "wreck" or "beacon" or "corridor"
                )
                .ToArray();
            var navigation = new SectorNavigation(a, geometry);
            var from = nodes.First(l => l.Kind == "station");
            foreach (var to in nodes.Where(l => l.Id != from.Id))
            {
                var approach = from.Position + new Vector2(140, 0);
                var route = navigation.Route(approach, to.Position, Room.Radius);
                Assert.NotEmpty(route);
                var previous = approach;
                foreach (var point in route)
                {
                    Assert.Null(geometry.TraceShips(previous, point, Room.Radius));
                    previous = point;
                }
            }
            foreach (var port in nodes.Where(l => l.Kind == "station"))
                Assert.Null(
                    geometry.Trace(port.Position, port.Position + new Vector2(180, 0), Room.Radius)
                );
        }
    }

    [Fact]
    public void SweptMovementSlidesAndCannotTunnelThroughCover()
    {
        var geometry = new SectorCollision(new(1, 12000, 8000, [], [new(1, 500, 500, 50)]));
        var hit = geometry.Move(new(100, 500), new(900, 500), 14);
        Assert.InRange(hit.X, 435, 437);
        var slide = geometry.Move(new(400, 460), new(600, 500), 14);
        Assert.True(slide.Y < 460);
        Assert.True(Vector2.Distance(slide, new(500, 500)) >= 64 - .1f);
    }

    [Fact]
    public void DockingGuardsPurchasesMovementCollectionAndReplay()
    {
        var r = World();
        r.Input(1, new(1, 0, 0, 0, false, false, true));
        r.Step();
        Assert.False(r.Buy(1, new(2, 6)));
        Assert.Equal("not_docked", r.Workshop(1).Result);
        Assert.False(r.SectorAction(1, new(3, "dock", 11)));
        Assert.True(r.SectorAction(1, new(4, "dock", 10)));
        Assert.False(r.SectorAction(1, new(4, "repair")));
        Assert.True(r.Buy(1, new(5, 6)));
        Assert.Equal(40, r.Workshop(1).Credits);
        var before = r.Snapshot().Players[0];
        r.Input(1, new(6, 1, 1, 0, true, true));
        r.Step();
        var after = r.Snapshot().Players[0];
        Assert.Equal(before.X, after.X);
        Assert.Equal(before.Y, after.Y);
        Assert.Empty(r.Snapshot().Shots);
        Assert.Equal(10, after.Docked);
        Assert.True(r.SectorAction(1, new(7, "undock")));
        Assert.Equal(0, r.Snapshot().Players[0].Docked);
    }

    [Fact]
    public void RepairIsPaidOnceAndFailedPersistenceCannotHeal()
    {
        var r = World();
        Set(Player(r), "Hull", 1);
        Assert.True(r.SectorAction(1, new(1, "dock", 10)));
        Assert.True(r.SectorAction(1, new(2, "repair")));
        Assert.Equal(80, r.Workshop(1).Credits);
        Assert.Equal(3, r.Snapshot().Players[0].Hull);
        Assert.False(r.SectorAction(1, new(2, "repair")));
        var folder = Path.Combine(Path.GetTempPath(), "neon-sector-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "profiles.json");
        var store = new ProfileStore(file);
        var failed = World(store: store);
        Set(Player(failed), "Hull", 1);
        failed.SectorAction(1, new(1, "dock", 10));
        Directory.CreateDirectory(file + ".tmp");
        try
        {
            Assert.False(failed.SectorAction(1, new(2, "repair")));
            Assert.Equal(1, failed.Snapshot().Players[0].Hull);
            Assert.Equal(100, store.Get("SectorPilot").Credits);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void ClearingBaseNeverRebuildsAndCorridorsContinueInEmptySector()
    {
        var r = World(true);
        var baseEnemy = ((IList)Get(r, "enemies"))
            .Cast<object>()
            .First(e => (bool)Get(e, "Installation"));
        var home = (int)Get(baseEnemy, "Home");
        Set(baseEnemy, "Hull", 0);
        r.Step();
        foreach (
            var e in ((IList)Get(r, "enemies"))
                .Cast<object>()
                .Where(e => (int)Get(e, "Home") == home)
        )
            Set(e, "Hull", 0);
        Step(r, 4000);
        Assert.Equal("destroyed", r.SectorSnapshot(1)!.Locations.Single(l => l.Id == home).State);
        Assert.DoesNotContain(r.Snapshot().Enemies!, e => e.Home == home);
        Assert.Contains(r.Snapshot().Enemies!, e => e.Home is 30 or 31);
        Assert.InRange(r.Snapshot().Enemies!.Length, 1, Room.EnemyCapacity);
    }

    [Fact]
    public void EnemyCreationDoesNotDependOnPilotPositionOrPresence()
    {
        var empty = new Room(seed: 44);
        var occupied = new Room(seed: 44);
        occupied.Join("Pilot");
        Step(empty, 1800);
        Step(occupied, 1800);
        Assert.Equal(empty.Snapshot().Enemies, occupied.Snapshot().Enemies);
        Assert.All(
            empty.Snapshot().Enemies!,
            e =>
                Assert.Contains(
                    e.Home,
                    new[] { 10, 11, 12, 20, 21, 22, 23, 30, 31, 40, 41, 42, 43, 44, 45, 80, 81, 83 }
                )
        );
    }

    [Fact]
    public void SurveyRecoveryAndSuppressionRewardsArePersonalAndCannotReplay()
    {
        var r = World();
        r.Join("Other");
        r.SectorAction(1, new(1, "dock", 10));
        Assert.True(r.SectorAction(1, new(2, "accept", 1)));
        r.SectorAction(1, new(3, "undock"));
        var seq = 4;
        foreach (var target in new[] { 50, 51, 52 })
        {
            At(r, Position(r, target));
            Assert.True(r.SectorAction(1, new(seq++, "interact", target)));
            Step(r, 90);
        }
        Assert.True(r.SectorSnapshot(1)!.Pilot!.Ready);
        Assert.Empty(r.SectorSnapshot(2)!.Pilot!.Progress);
        At(r, Position(r, 10));
        r.SectorAction(1, new(seq++, "dock", 10));
        Assert.True(r.SectorAction(1, new(seq++, "claim")));
        Assert.Equal(140, r.Workshop(1).Credits);
        Assert.Equal(30, r.Snapshot().Players[0].Experience);
        Assert.False(r.SectorAction(1, new(seq++, "claim")));
        Assert.False(r.SectorAction(1, new(seq++, "accept", 1)));
        Assert.True(r.SectorAction(1, new(seq++, "accept", 2)));
        r.SectorAction(1, new(seq++, "undock"));
        At(r, Position(r, 40));
        Assert.True(r.SectorAction(1, new(seq++, "interact", 40)));
        Step(r, 90);
        Assert.True(r.SectorSnapshot(1)!.Pilot!.Carrying);
        Set(Player(r), "Hull", 0);
        r.Step();
        Assert.False(r.SectorSnapshot(1)!.Pilot!.Carrying);
        Assert.Equal(2, r.SectorSnapshot(1)!.Pilot!.Contract);
    }

    [Fact]
    public void SuppressionNeedsContributionAndDestroyedContractsCannotBeAccepted()
    {
        var r = World();
        r.Join("Other");
        r.SectorAction(1, new(1, "dock", 10));
        r.SectorAction(1, new(2, "accept", 3));
        r.SectorAction(1, new(3, "undock"));
        var target = Position(r, 20);
        At(r, target + new Vector2(250, 0));
        for (var i = 0; i < 20; i++)
        {
            r.Input(1, new(4 + i, 0, 0, MathF.PI, true, false));
            r.Step();
        }
        var e = ((IList)Get(r, "enemies")).Cast<object>().First(e => (int)Get(e, "Home") == 20);
        Assert.True(Health(e) < 120);
        Set(e, "Hull", 0);
        r.Step();
        Assert.True(r.SectorSnapshot(1)!.Pilot!.Ready);
        Assert.True(r.SectorAction(2, new(1, "dock", 10)));
        Assert.False(r.SectorAction(2, new(2, "accept", 3)));
        At(r, Position(r, 10));
        r.SectorAction(1, new(30, "dock", 10));
        Assert.True(r.SectorAction(1, new(31, "claim")));
        Assert.Equal(200, r.Workshop(1).Credits);
    }

    [Fact]
    public void DiscoverySurvivesDeathAndRejoinButIsPrivate()
    {
        var r = World();
        r.Join("Other");
        At(r, Position(r, 12));
        r.Step();
        r.SectorAction(1, new(1, "dock", 12));
        Set(Player(r), "Hull", 0);
        r.Step();
        Assert.True(r.Respawn(1, new(2)));
        Assert.InRange(
            Vector2.Distance(
                new(r.Snapshot().Players[0].X, r.Snapshot().Players[0].Y),
                Position(r, 12)
            ),
            0,
            180
        );
        Assert.Contains(12, r.SectorSnapshot(1)!.Pilot!.Discovered);
        Assert.DoesNotContain(12, r.SectorSnapshot(2)!.Pilot!.Discovered);
        r.Disconnect(1);
        var id = r.Join("sectorpilot")!.Value;
        Assert.Contains(12, r.SectorSnapshot(id)!.Pilot!.Discovered);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void SanctuaryBlocksEveryEmitterAndIncomingOrdnance(int emitter)
    {
        var r = World();
        Equip(r, Weapon(emitter));
        Fire(r, 30);
        Assert.Empty(r.Snapshot().Shots);
        Assert.Empty(r.Snapshot().Beams!);
        var shooter = r.Join("Attacker")!.Value;
        At(r, Position(r, 10) + new Vector2(600, 0), shooter);
        for (var i = 0; i < 120; i++)
        {
            r.Input(shooter, new(i + 1, 0, 0, MathF.PI, true, false));
            r.Step();
        }
        Assert.Equal(2, r.Snapshot().Players[0].Shield);
        Assert.Equal(3, r.Snapshot().Players[0].Hull);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void AsteroidsBlockAllEmitters(int emitter)
    {
        var r = World();
        var rock = r.Sector!.Obstacles.First();
        At(r, rock.Position - new Vector2(rock.Radius + 150, 0));
        Equip(r, Weapon(emitter));
        var enemy = Enemy(r, rock.X + rock.Radius + 70, rock.Y, 999, 100);
        Fire(r, 60);
        Assert.Equal(100, Health(enemy));
    }

    [Fact]
    public void InteractionMovementInterruptsAndThermalWarningPrecedesDamage()
    {
        var r = World();
        r.SectorAction(1, new(1, "dock", 10));
        r.SectorAction(1, new(2, "accept", 1));
        r.SectorAction(1, new(3, "undock"));
        At(r, Position(r, 50));
        r.SectorAction(1, new(4, "interact", 50));
        Step(r, 30);
        Assert.Equal(50, r.SectorSnapshot(1)!.Pilot!.InteractionTarget);
        Assert.InRange(r.SectorSnapshot(1)!.Pilot!.Interaction, .32f, .34f);
        r.Input(1, new(5, 1, 0, 0, false, false));
        r.Step();
        Step(r, 90);
        Assert.Empty(r.SectorSnapshot(1)!.Pilot!.Progress);
        Assert.Equal(0, r.SectorSnapshot(1)!.Pilot!.InteractionTarget);
        var heat = World();
        At(heat, Position(heat, 60));
        Step(heat, 59);
        Assert.Equal(2, heat.Snapshot().Players[0].Shield);
        heat.Step();
        Assert.Equal(1, heat.Snapshot().Players[0].Shield);
    }

    [Fact]
    public void FailedContractSaveCanRetryWithoutLosingRewardOrDoublingXp()
    {
        var folder = Path.Combine(Path.GetTempPath(), "neon-contract-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "profiles.json");
        var store = new ProfileStore(file);
        var r = World(store: store);
        try
        {
            r.SectorAction(1, new(1, "dock", 10));
            r.SectorAction(1, new(2, "accept", 2));
            r.SectorAction(1, new(3, "undock"));
            At(r, Position(r, 40));
            r.SectorAction(1, new(4, "interact", 40));
            Step(r, 90);
            At(r, Position(r, 10));
            r.SectorAction(1, new(5, "dock", 10));
            Directory.CreateDirectory(file + ".tmp");
            Assert.False(r.SectorAction(1, new(6, "claim")));
            Assert.True(r.SectorSnapshot(1)!.Pilot!.Ready);
            Assert.Equal(100, r.Workshop(1).Credits);
            Assert.Equal(0, r.Snapshot().Players[0].Experience);
            Directory.Delete(file + ".tmp");
            Assert.True(r.SectorAction(1, new(7, "claim")));
            Assert.False(r.SectorAction(1, new(7, "claim")));
            Assert.Equal(160, new ProfileStore(file).Get("SectorPilot").Credits);
            Assert.Equal(45, r.Snapshot().Players[0].Experience);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void PredictionUsesTheSameSweptObstacleMotionAsAuthority()
    {
        var r = World();
        var rock = r.Sector!.Obstacles[0];
        At(r, rock.Position - new Vector2(rock.Radius + 35, 0));
        r.Step();
        var initial = r.Snapshot().Players[0];
        var prediction = new LocalPrediction();
        prediction.SetSector(r.Sector);
        prediction.Receive(initial, r.Tick, 0);
        prediction.Input(1, 0, 0, true, 0);
        r.Input(1, new(1, 1, 0, 0, false, true));
        r.Step();
        var predicted = prediction.Sample(1d / Room.TickRate)!;
        var actual = r.Snapshot().Players[0];
        Assert.Equal(actual.X, predicted.X, 1);
        Assert.Equal(actual.Y, predicted.Y, 1);
    }

    [Fact]
    public void InstallationsAcceptDamageStatusesButIgnoreControlAndGravity()
    {
        var r = World();
        var at = Position(r, 20);
        At(r, at + new Vector2(650, 0));
        Equip(r, Weapon(2, 17, 18, 19, 20, 21, 22, 5));
        var e = ((IList)Get(r, "enemies")).Cast<object>().First(e => (int)Get(e, "Home") == 20);
        for (var i = 0; i < 30; i++)
        {
            r.Input(1, new(3 + i, 0, 0, MathF.PI, true, false));
            r.Step();
        }
        Assert.True(Health(e) < 116);
        Assert.Equal(at, (Vector2)Get(e, "Position"));
        Assert.Equal(0L, Get(e, "SlowUntil"));
        Assert.Equal(0L, Get(e, "StunUntil"));
        Assert.True((long)Get(e, "BurnUntil") > r.Tick);
        Assert.True((long)Get(e, "CorrosionUntil") > r.Tick);
    }

    [Fact]
    public void ScanDamageInterruptsAndDockedPilotsCannotCollectDrops()
    {
        var r = World();
        r.SectorAction(1, new(1, "dock", 10));
        r.SectorAction(1, new(2, "accept", 1));
        r.SectorAction(1, new(3, "undock"));
        At(r, Position(r, 50));
        r.SectorAction(1, new(4, "interact", 50));
        Step(r, 30);
        Set(Player(r), "LastDamage", r.Tick);
        r.Step();
        Assert.Equal(0, r.SectorSnapshot(1)!.Pilot!.Interaction);
        At(r, Position(r, 10));
        r.SectorAction(1, new(5, "dock", 10));
        var enemy = Enemy(r, Position(r, 10).X, Position(r, 10).Y, 999, 0);
        r.Step();
        Step(r, 5);
        Assert.NotEmpty(r.Snapshot().Credits!);
        Assert.NotEmpty(r.Snapshot().Energy!);
        Assert.Equal(0, r.Snapshot().Players[0].Experience);
    }

    [Fact]
    public void SectorCommandsRejectUnknownActionsAndClientAuthority()
    {
        Assert.True(
            ClientProtocol.TryParse(
                Encoding.UTF8.GetBytes(
                    """{"v":15,"type":"sector","seq":1,"action":"dock","target":10}"""
                ),
                out _
            )
        );
        Assert.False(
            ClientProtocol.TryParse(
                Encoding.UTF8.GetBytes(
                    """{"v":15,"type":"sector","seq":1,"action":"teleport","target":10}"""
                ),
                out _
            )
        );
        Assert.False(
            ClientProtocol.TryParse(
                Encoding.UTF8.GetBytes(
                    """{"v":15,"type":"sector","seq":1,"action":"claim","target":10,"credits":100}"""
                ),
                out _
            )
        );
    }
}
