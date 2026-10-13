using System.Collections;
using System.Numerics;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;
using static ArcadeDodger.Multiplayer.Tests.WorkshopRig;

namespace ArcadeDodger.Multiplayer.Tests;

public class LivingEventTests(ITestOutputHelper output)
{
    private static object? Call(Room room, string name, params object[] args) =>
        typeof(Room)
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(room, args);

    [Fact]
    public void CorridorInvasionWaitsForCapacityAndSurvivesFactoryDestructionWithoutRevealingEnemies()
    {
        var room = new Room(true, seed: 123);
        room.Join("Observer");
        var source = ((IDictionary)Get(room, "sources"))[23]!;
        Set(source, "Destroyed", true);
        Set(room, "worldEvent", "invasion-warning");
        Set(room, "worldEventTarget", 12);
        Set(room, "worldEventEnds", 2L);
        Set(room, "<Tick>k__BackingField", 3L);
        var enemies = (IList)Get(room, "enemies");
        var original = enemies.Count;
        while (enemies.Count < Room.EnemyCapacity)
            Enemy(room, 23000, 15000, 1000 + enemies.Count);
        Call(room, "StepWorldEvents");
        Assert.Equal("invasion-warning", Get(room, "worldEvent"));
        Assert.Equal(Room.EnemyCapacity, enemies.Count);
        while (enemies.Count > original)
            enemies.RemoveAt(enemies.Count - 1);
        var discovery = room.SectorSnapshot(1)!.Pilot!.Cells.ToArray();
        Call(room, "StepWorldEvents");
        Assert.Equal("invasion", Get(room, "worldEvent"));
        var invaders = (HashSet<int>)Get(room, "invasion");
        Assert.Equal(8, invaders.Count);
        var corridor = room.Sector!.Locations.Single(l => l.Id == 31).Position;
        foreach (
            var enemy in enemies.Cast<object>().Where(e => invaders.Contains((int)Get(e, "Id")))
        )
            Assert.InRange(Vector2.Distance(corridor, (Vector2)Get(enemy, "Position")), 0, 1000);
        var snapshot = room.SectorSnapshot(1)!;
        Assert.Equal(discovery, snapshot.Pilot!.Cells);
        Assert.Equal(12, snapshot.World!.EventObjective!.Id);
    }

    [Fact]
    public void TenMinuteLivingWorldSoakKeepsSharedBudgetsAndFiniteState()
    {
        var room = new Room(true, seed: 234);
        room.Join("SoakA");
        room.Join("SoakB");
        var events = new HashSet<string>();
        var peakNpcs = 0;
        var peakShots = 0;
        for (var tick = 0; tick < 600 * Room.TickRate; tick++)
        {
            for (var id = 1; id <= 2; id++)
                room.Input(
                    id,
                    new(
                        tick + 1,
                        tick % 450 < 50 ? 1 : 0,
                        tick % 900 < 120 ? -1 : 0,
                        tick * .013f,
                        true,
                        false
                    )
                );
            room.Step();
            if (tick % 30 != 0)
                continue;
            var snapshot = room.Snapshot();
            peakNpcs = Math.Max(peakNpcs, snapshot.Enemies!.Length);
            peakShots = Math.Max(peakShots, snapshot.Shots.Length);
            Assert.InRange(snapshot.Enemies.Length, 0, Room.EnemyCapacity);
            Assert.InRange(snapshot.Shots.Length, 0, Room.ProjectileCapacity);
            foreach (var enemy in snapshot.Enemies)
                Assert.True(float.IsFinite(enemy.X) && float.IsFinite(enemy.Y));
            var world = room.SectorSnapshot(1)!.World!;
            Assert.InRange(world.Cargo.Length, 0, 64);
            Assert.InRange(world.Radio.Length, 0, 32);
            if (world.Event.Length > 0)
                events.Add(world.Event);
        }
        Assert.NotEmpty(events);
        output.WriteLine(
            $"Ten simulated minutes: peak NPCs {peakNpcs}, projectiles {peakShots}; events {string.Join(", ", events)}."
        );
    }
}
