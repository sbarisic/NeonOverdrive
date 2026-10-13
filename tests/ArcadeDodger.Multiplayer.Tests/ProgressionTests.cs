using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace ArcadeDodger.Multiplayer.Tests;

public class ProgressionTests
{
    // Controlled private fixtures avoid adding a client-accessible reward/spawn API.
    private static object Field(object instance, string name) =>
        instance
            .GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(instance)!;

    private static void Set(object instance, string name, object value) =>
        instance
            .GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(instance, value);

    private static object Player(Room room, int id) => ((IDictionary)Field(room, "players"))[id]!;

    private static void Enemy(Room room, Vector2 position, bool gunner = false, int hull = 0)
    {
        var type = typeof(Room).GetNestedType("Enemy", BindingFlags.NonPublic)!;
        var enemy = Activator.CreateInstance(type, true)!;
        Set(enemy, "Position", position);
        Set(enemy, "Previous", position);
        Set(enemy, "Gunner", gunner);
        Set(enemy, "Hull", hull);
        ((IList)Field(room, "enemies")).Add(enemy);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(19, 1)]
    [InlineData(20, 2)]
    [InlineData(59, 2)]
    [InlineData(60, 3)]
    [InlineData(1000000, 100)]
    public void LevelsUseTotalRunEnergy(int xp, int level) =>
        Assert.Equal(level, Room.LevelForExperience(xp));

    [Fact]
    public void RealProjectileKillDropsOnceAndCollectionConsumesValue()
    {
        var room = new Room(false, sectorEnabled: false);
        var id = room.Join("pilot")!.Value;
        var p = room.Snapshot().Players.Single();
        Enemy(room, new(p.X - 100, p.Y), hull: 1);
        room.Input(id, new(1, 0, 0, MathF.PI, true, false));
        room.Step();
        room.Input(id, new(2, 0, 0, MathF.PI, false, false));
        for (var i = 0; i < 5; i++)
            room.Step();
        var drop = Assert.Single(room.Snapshot().Energy!);
        Assert.Equal(10, drop.Value);
        Assert.Empty(room.Snapshot().Enemies!);
        for (var i = 3; i < 180 && room.Snapshot().Energy!.Length > 0; i++)
        {
            var target = room.Snapshot().Energy![0];
            var pilot = room.Snapshot().Players[0];
            var delta = Vector2.Normalize(new Vector2(target.X - pilot.X, target.Y - pilot.Y));
            var heading = MathF.Atan2(delta.Y, delta.X);
            var turn = MathF.IEEERemainder(heading - pilot.Heading, MathF.Tau);
            room.Input(
                id,
                new(i, Math.Clamp(turn * 5, -1, 1), pilot.Throttle < .3f ? -1 : 0, 0, false, false)
            );
            room.Step();
        }
        Assert.Empty(room.Snapshot().Energy!);
        Assert.Equal(10, room.Snapshot().Players.Single().Experience);
        room.Step();
        Assert.Equal(10, room.Snapshot().Players.Single().Experience);
    }

    [Fact]
    public void SimultaneousCollectorsNeverDuplicateRewardAndRespawnResetsRun()
    {
        var room = new Room(false, sectorEnabled: false);
        var a = room.Join("a")!.Value;
        var b = room.Join("b")!.Value;
        var point = new Vector2(600, 600);
        Set(Player(room, a), "Position", point);
        Set(Player(room, b), "Position", point);
        Enemy(room, point, true);
        Enemy(room, point);
        room.Step();
        Assert.Equal(25, room.Snapshot().Players.Sum(p => p.Experience));
        Assert.Equal(25, room.Snapshot().Players[0].Experience);
        Assert.Equal(2, room.Snapshot().Players[0].Level);
        Assert.Empty(room.Snapshot().Energy!);
        Set(Player(room, a), "Hull", 0);
        Assert.True(room.Respawn(a, new(1)));
        Assert.Equal(0, room.Snapshot().Players[0].Experience);
        Assert.Equal(1, room.Snapshot().Players[0].Level);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DeadOrDisconnectedPlayersCannotCollect(bool dead)
    {
        var room = new Room(false, sectorEnabled: false);
        var id = room.Join("a")!.Value;
        var p = room.Snapshot().Players.Single();
        if (dead)
            Set(Player(room, id), "Hull", 0);
        else
            room.Disconnect(id);
        Enemy(room, new(p.X, p.Y));
        room.Step();
        Assert.Single(room.Snapshot().Energy!);
        Assert.Equal(0, room.Snapshot().Players.Single().Experience);
    }

    [Fact]
    public void DropsAreBoundedExpireAndRoundTripThroughSnapshots()
    {
        var room = new Room(false, sectorEnabled: false);
        for (var i = 0; i < Room.EnergyCapacity + 5; i++)
            Enemy(room, new(500, 500));
        room.Step();
        var snapshot = room.Snapshot();
        Assert.Equal(Room.EnergyCapacity, snapshot.Energy!.Length);
        Assert.Equal(Room.EnergyCapacity, snapshot.Energy.Select(e => e.Id).Distinct().Count());
        var copy = JsonSerializer.Deserialize<RoomSnapshot>(JsonSerializer.Serialize(snapshot))!;
        Assert.Equal(snapshot.Energy, copy.Energy);
        for (var i = 0; i < Room.EnergyLifetimeTicks - 1; i++)
            room.Step();
        Assert.Equal(Room.EnergyCapacity, room.Snapshot().Energy!.Length);
        room.Step();
        Assert.Empty(room.Snapshot().Energy!);
    }
}
