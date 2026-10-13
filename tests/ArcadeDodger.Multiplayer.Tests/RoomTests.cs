using Xunit;

namespace ArcadeDodger.Multiplayer.Tests;

public class RoomTests
{
    [Fact]
    public void InputsAreAppliedOnlyOnTickAndPacketRateCannotAccelerateMovement()
    {
        var room = new Room(sectorEnabled: false);
        var id = room.Join("A")!.Value;
        var start = room.Snapshot().Players.Single();
        for (var seq = 1; seq <= 100; seq++)
            Assert.True(room.Input(id, new(seq, -1, -1, 0, false, false)));
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(start),
            System.Text.Json.JsonSerializer.Serialize(room.Snapshot().Players.Single())
        );
        room.Step();
        var end = room.Snapshot().Players.Single();
        Assert.Equal(100, end.Ack);
        Assert.Equal(
            .1f,
            MathF.Sqrt(MathF.Pow(end.X - start.X, 2) + MathF.Pow(end.Y - start.Y, 2)),
            2
        );
        Assert.False(room.Input(id, new(100, 0, 0, 0, false, false)));
        Assert.False(room.Input(id, new(101, float.NaN, 0, 0, false, false)));
    }

    [Fact]
    public void MovementStopsWhenStaleAndStaysInsideArena()
    {
        var room = new Room(sectorEnabled: false);
        var id = room.Join("A")!.Value;
        room.Input(id, new(1, 0, -1, 0, false, false));
        for (var i = 0; i < 40; i++)
            room.Step();
        var stopped = room.Snapshot().Players.Single();
        for (var i = 0; i < 100; i++)
            room.Step();
        Assert.Equal(stopped.X, room.Snapshot().Players.Single().X);
        for (var i = 2; i < 2400; i++)
        {
            room.Input(id, new(i, 0, -1, 0, false, false));
            room.Step();
        }
        var end = room.Snapshot().Players.Single();
        Assert.Equal(Room.Width - Room.Radius, end.X);
        Assert.InRange(end.Y, Room.Radius, Room.Height - Room.Radius);
    }

    [Fact]
    public void AttackEndsProtectionAndShotsCannotHitOwner()
    {
        var room = new Room(sectorEnabled: false);
        var id = room.Join("A")!.Value;
        Assert.True(room.Snapshot().Players.Single().Protected);
        for (var i = 1; i <= 30; i++)
        {
            room.Input(id, new(i, -1, 0, MathF.PI, true, false));
            room.Step();
        }
        var end = room.Snapshot().Players.Single();
        Assert.False(end.Protected);
        Assert.Equal(3, end.Hull);
        Assert.Equal(2, end.Shield);
        Assert.InRange(room.Snapshot().Shots.Length, 1, 6);
    }

    [Fact]
    public void TwoPlayersCanDealAuthoritativeDamage()
    {
        // Isolate PvP: ambient rammers can otherwise damage A after B is dead.
        var room = new Room(enemiesEnabled: false, sectorEnabled: false);
        var a = room.Join("A")!.Value;
        var b = room.Join("B")!.Value;
        WorkshopRig.Set(
            WorkshopRig.Player(room, a),
            "Position",
            new System.Numerics.Vector2(1700, 800)
        );
        WorkshopRig.Set(
            WorkshopRig.Player(room, b),
            "Position",
            new System.Numerics.Vector2(1000, 800)
        );
        for (var i = 1; i <= 400; i++)
        {
            var players = room.Snapshot().Players;
            var direction = players[1].X - players[0].X;
            room.Input(
                a,
                new(
                    i,
                    MathF.Abs(direction) > 400 ? MathF.Sign(direction) : 0,
                    0,
                    MathF.PI,
                    true,
                    false
                )
            );
            room.Input(
                b,
                new(i, MathF.Abs(direction) > 400 ? -MathF.Sign(direction) : 0, 0, 0, false, false)
            );
            room.Step();
        }
        Assert.Equal(0, room.Snapshot().Players.Single(p => p.Id == b).Hull);
        Assert.Equal(3, room.Snapshot().Players.Single(p => p.Id == a).Hull);
    }

    [Fact]
    public void DisconnectRetainsShipForGraceAndRoomCapacityIsBounded()
    {
        var room = new Room(sectorEnabled: false);
        for (var i = 0; i < Room.Capacity; i++)
            Assert.NotNull(room.Join("Pilot" + i));
        Assert.Null(room.Join("Overflow"));
        room.Disconnect(1);
        Assert.False(room.Input(1, new(1, 1, 0, 0, true, false)));
        for (var i = 0; i < 149; i++)
            room.Step();
        Assert.Equal(Room.Capacity, room.PlayerCount);
        Assert.False(room.Snapshot().Players[0].Connected);
        room.Step();
        Assert.Equal(Room.Capacity - 1, room.PlayerCount);
        Assert.NotNull(room.Join("Next"));
    }
}
