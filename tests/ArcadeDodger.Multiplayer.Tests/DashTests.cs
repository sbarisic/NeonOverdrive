using Xunit;

namespace ArcadeDodger.Multiplayer.Tests;

public class DashTests
{
    [Fact]
    public void FixedDurationCooldownAndPacketRate()
    {
        var room = new Room();
        var id = room.Join("Dash")!.Value;
        var start = room.Snapshot().Players.Single();
        for (var i = 1; i <= Room.DashDuration; i++)
        {
            for (var j = 1; j <= 10; j++) room.Input(id, new(i * 10 + j, -1, 0, 0, false, true));
            room.Step();
        }
        Assert.Equal(180, start.X - room.Snapshot().Players.Single().X, 3);
        var before = room.Snapshot().Players.Single();
        room.Input(id, new(100, -1, 0, 0, false, true));
        room.Step();
        var after = room.Snapshot().Players.Single();
        Assert.Equal(10, before.X - after.X, 3);
        Assert.Equal(84, after.DashCooldown);
        for (var tick = 8; tick <= 90; tick++)
        {
            room.Input(id, new(100 + tick, 0, 0, 0, false, false));
            room.Step();
        }
        Assert.Equal(1, room.Snapshot().Players.Single().DashCooldown);
        before = room.Snapshot().Players.Single();
        room.Input(id, new(200, -1, 0, 0, false, true));
        room.Step();
        after = room.Snapshot().Players.Single();
        Assert.Equal(30, before.X - after.X, 3);
        Assert.Equal(Room.DashCooldownTicks, after.DashCooldown);
    }

    [Fact]
    public void StationaryDashUsesAimAndDisconnectStopsIt()
    {
        var room = new Room();
        var id = room.Join("Dash")!.Value;
        var start = room.Snapshot().Players.Single();
        room.Input(id, new(1, 0, 0, MathF.PI, false, true));
        room.Step();
        var dashed = room.Snapshot().Players.Single();
        Assert.Equal(30, start.X - dashed.X, 3);
        Assert.Equal(start.Y, dashed.Y, 3);
        room.Disconnect(id);
        for (var i = 0; i < 8; i++) room.Step();
        Assert.Equal(dashed.X, room.Snapshot().Players.Single().X);
        Assert.Equal(dashed.Y, room.Snapshot().Players.Single().Y);
    }

    [Fact]
    public void DashClampsAtArenaBoundary()
    {
        var room = new Room();
        var id = room.Join("Dash")!.Value;
        room.Input(id, new(1, 1, 0, 0, false, true));
        for (var i = 0; i < Room.DashDuration; i++) room.Step();
        Assert.Equal(Room.Width - Room.Radius, room.Snapshot().Players.Single().X);
    }
}
