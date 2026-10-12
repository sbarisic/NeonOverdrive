using Xunit;

namespace ArcadeDodger.Multiplayer.Tests;

public sealed class EnemyTests
{
    [Fact]
    public void SectorHasGarrisonsBeforeAnyPilotArrives()
    {
        var room = new Room(seed: 42);
        Assert.Equal(47, room.Snapshot().Enemies!.Length);
        for (var i = 0; i < 600; i++)
            room.Step();
        Assert.InRange(room.Snapshot().Enemies!.Length, 47, 51);
        Assert.All(
            room.Snapshot().Enemies!,
            e => Assert.Contains(e.Home, new[] { 20, 21, 22, 23, 80, 81, 83 })
        );
    }

    [Fact]
    public void PatrolsIgnoreSafePortAndEngageNearbyExposedPilots()
    {
        var room = new Room(seed: 42);
        room.Join("pilot");
        WorkshopRig.Step(room, 100);
        Assert.DoesNotContain(room.Snapshot().Enemies!, e => e.State == "engage");
        var enemy = room.Snapshot().Enemies!.First(e => e.Kind == "rammer");
        WorkshopRig.Set(
            WorkshopRig.Player(room),
            "Position",
            new System.Numerics.Vector2(enemy.X + 100, enemy.Y)
        );
        room.Step();
        Assert.Contains(room.Snapshot().Enemies!, e => e.State == "engage");
    }

    [Fact]
    public void PlayerShotsCanKillPatrolShips()
    {
        var room = WorkshopRig.Start();
        var enemy = WorkshopRig.Enemy(room, 1100, hull: 3);
        WorkshopRig.Fire(room, 45);
        Assert.DoesNotContain(room.Snapshot().Enemies!, e => e.Id == 1);
        Assert.NotEmpty(room.Snapshot().Energy!);
    }

    [Fact]
    public void NewShotWaitsForBirthThenMovesEveryRenderFrame()
    {
        var playback = new SnapshotPlayback();
        playback.Receive(new RoomSnapshot(1, "snapshot", 100, [], []), 0);
        playback.Receive(
            new RoomSnapshot(1, "snapshot", 102, [], [new(1, 1, 40, 0, 20, 0, 101)]),
            2d / 30
        );
        Assert.Empty(playback.Sample(2d / 30)!.Shots);
        var first = Assert.Single(playback.Sample(4d / 30)!.Shots);
        var second = Assert.Single(playback.Sample(4.5d / 30)!.Shots);
        Assert.Equal(20, first.X, 3);
        Assert.Equal(30, second.X, 3);
        Assert.Equal(40, Assert.Single(playback.Sample(10)!.Shots).X);
    }
}
