using Xunit;

namespace ArcadeDodger.Multiplayer.Tests;

public sealed class EnemyTests
{
    [Fact]
    public void EmptyRoomDoesNotSpawn()
    {
        var room = new Room();
        for (int i = 0; i < 1000; i++) room.Step();
        Assert.Empty(room.Snapshot().Enemies!);
    }

    [Fact]
    public void EnemiesSpawnSafelyPursueAndDamage()
    {
        var room = new Room(); room.Join("pilot");
        for (int i = 0; i < 90; i++) room.Step();
        var first = Assert.Single(room.Snapshot().Enemies!);
        var player = Assert.Single(room.Snapshot().Players);
        Assert.True(MathF.Sqrt(MathF.Pow(first.X - player.X, 2) + MathF.Pow(first.Y - player.Y, 2)) > 290);
        Assert.Equal("rammer", first.Kind);
        bool gunner = false, hostileShot = false;
        for (int i = 0; i < 900; i++)
        {
            room.Step();
            var state = room.Snapshot();
            gunner |= state.Enemies!.Any(e => e.Kind == "gunner");
            hostileShot |= state.Shots.Any(s => s.Owner < 0);
            Assert.InRange(state.Enemies!.Length, 0, Room.EnemyCapacity);
            Assert.InRange(state.Players[0].Hull, 0, 3);
        }
        Assert.True(gunner); Assert.True(hostileShot);
        Assert.Equal(0, room.Snapshot().Players[0].Hull);
    }

    [Fact]
    public void PlayerShotsCanKillEnemies()
    {
        var room = new Room(); var id = room.Join("pilot")!.Value;
        for (int i = 0; i < 90; i++) room.Step();
        var enemyId = Assert.Single(room.Snapshot().Enemies!).Id;
        for (int i = 1; i <= 150; i++)
        {
            var state = room.Snapshot();
            var enemy = state.Enemies!.FirstOrDefault(e => e.Id == enemyId);
            if (enemy is null) return;
            var player = state.Players[0];
            room.Input(id, new InputMessage(i, 0, 0, MathF.Atan2(enemy.Y - player.Y, enemy.X - player.X), true, false));
            room.Step();
        }
        Assert.DoesNotContain(room.Snapshot().Enemies!, e => e.Id == enemyId);
    }

    [Fact]
    public void NewShotWaitsForBirthThenMovesEveryRenderFrame()
    {
        var playback = new SnapshotPlayback();
        playback.Receive(new RoomSnapshot(1, "snapshot", 100, [], []), 0);
        playback.Receive(new RoomSnapshot(1, "snapshot", 102, [], [new(1, 1, 40, 0, 20, 0, 101)]), 2d / 30);
        Assert.Empty(playback.Sample(2d / 30)!.Shots);
        var first = Assert.Single(playback.Sample(4d / 30)!.Shots);
        var second = Assert.Single(playback.Sample(4.5d / 30)!.Shots);
        Assert.Equal(20, first.X, 3);
        Assert.Equal(30, second.X, 3);
        Assert.Equal(40, Assert.Single(playback.Sample(10)!.Shots).X);
    }
}
