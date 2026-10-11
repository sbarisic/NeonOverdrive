using Xunit;

namespace ArcadeDodger.Multiplayer.Tests;

public class LifecycleTests
{
    private static (Room Room, int A, int B, int Seq) Pair()
    {
        var room = new Room(false);
        var a = room.Join("A")!.Value;
        var b = room.Join("B")!.Value;
        for (var i = 1; i <= 115; i++)
        {
            room.Input(a, new(i, -1, 0, MathF.PI, false, false));
            room.Input(b, new(i, 1, 0, 0, false, false));
            room.Step();
        }
        room.Input(a, new(116, 0, 0, MathF.PI, false, false));
        room.Input(b, new(116, 0, 0, 0, false, false));
        room.Step();
        return (room, a, b, 116);
    }

    [Fact]
    public void ShieldRecoversAfterFiveSecondsWithoutInputButHullDoesNot()
    {
        var (room, a, b, seq) = Pair();
        room.Input(a, new(++seq, 0, 0, MathF.PI, true, false));
        room.Step();
        room.Input(a, new(++seq, 0, 0, MathF.PI, false, false));
        for (var i = 0; i < 60 && room.Snapshot().Players[1].Shield == 2; i++) room.Step();
        Assert.Equal(1, room.Snapshot().Players[1].Shield);
        for (var i = 0; i < 149; i++) room.Step();
        Assert.Equal(1, room.Snapshot().Players[1].Shield);
        room.Step();
        Assert.Equal(2, room.Snapshot().Players[1].Shield);
        Assert.Equal(3, room.Snapshot().Players[1].Hull);
    }

    [Fact]
    public void RespawnRequiresDeadConnectedPlayerAndResetsCombatState()
    {
        var (room, a, b, seq) = Pair();
        Assert.False(room.Respawn(b, new(++seq)));
        for (var i = 0; i < 120 && room.Snapshot().Players[1].Hull > 0; i++)
        {
            room.Input(a, new(++seq, 0, 0, MathF.PI, true, false));
            room.Step();
        }
        Assert.Equal(0, room.Snapshot().Players[1].Hull);
        for (var i = 0; i < 180; i++) room.Step();
        Assert.Equal(0, room.Snapshot().Players[1].Shield);
        Assert.True(room.Respawn(b, new(++seq)));
        var fresh = room.Snapshot().Players[1];
        Assert.Equal(b, fresh.Id);
        Assert.Equal("B", fresh.Name);
        Assert.Equal(3, fresh.Hull);
        Assert.Equal(2, fresh.Shield);
        Assert.True(fresh.Protected);
        Assert.Equal(0, fresh.DashCooldown);
        Assert.Equal(seq, fresh.Ack);
        Assert.False(room.Respawn(b, new(seq)));
        Assert.False(room.Input(b, new(seq, 0, 0, 0, true, false)));
        room.Disconnect(b);
        Assert.False(room.Respawn(b, new(++seq)));
        Assert.False(room.Respawn(999, new(++seq)));
    }
}
