using System.Numerics;
using System.Text;
using Xunit;
using static ArcadeDodger.Multiplayer.Tests.WorkshopRig;

namespace ArcadeDodger.Multiplayer.Tests;

public class ThrottleOverrideTests
{
    [Theory]
    [InlineData(0f)]
    [InlineData(.35f)]
    [InlineData(1f)]
    public void FullThrottleIsImmediateAndReleaseRestoresSetpoint(float setpoint)
    {
        Vector2 position = new(5000),
            velocity = default;
        float heading = 0,
            throttle = setpoint;
        Movement.Steer(
            ref position,
            ref velocity,
            ref heading,
            ref throttle,
            new(0, -1),
            .1f,
            fullThrottle: true
        );
        Assert.Equal(setpoint, throttle);
        Assert.Equal(11f, velocity.X, 3);
        Movement.Steer(ref position, ref velocity, ref heading, ref throttle, Vector2.Zero, .1f);
        Assert.Equal(setpoint, throttle);
        Assert.Equal(11f + 11f * setpoint, velocity.X, 3);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void BrakeMenuAndMissingInputSuppressOverride(
        bool brake,
        bool immobilized,
        bool available
    )
    {
        Vector2 position = new(5000),
            velocity = new(100, 0);
        float heading = 0,
            throttle = .4f;
        Movement.Steer(
            ref position,
            ref velocity,
            ref heading,
            ref throttle,
            new(0, -1),
            .1f,
            immobilized: immobilized,
            inputAvailable: available,
            brake: brake,
            fullThrottle: true
        );
        Assert.Equal(0, throttle);
        Assert.Equal(
            immobilized ? 0
                : brake ? 70
                : 100,
            velocity.X,
            3
        );
    }

    [Fact]
    public void PredictionReplaysOverrideAndReleaseWithoutChangingSavedThrottle()
    {
        var room = Start();
        Set(Player(room), "Throttle", .35f);
        var prediction = new LocalPrediction();
        prediction.Receive(room.Snapshot().Players[0], room.Tick, 0);
        prediction.Input(0, 0, 0, false, 0, fullThrottle: true);
        room.Input(1, new(1, 0, 0, 0, false, false, FullThrottle: true));
        room.Step();
        var boosted = room.Snapshot().Players[0];
        var predicted = prediction.Sample(1d / Room.TickRate)!;
        Assert.Equal(.35f, boosted.Throttle);
        Assert.True(boosted.FullThrottle);
        Assert.True(predicted.FullThrottle);
        Assert.Equal(boosted.VelocityX, predicted.VelocityX, 3);
        prediction.Input(0, 0, 0, false, 1d / Room.TickRate);
        room.Input(1, new(2, 0, 0, 0, false, false));
        room.Step();
        var released = room.Snapshot().Players[0];
        predicted = prediction.Sample(2d / Room.TickRate)!;
        Assert.Equal(.35f, released.Throttle);
        Assert.False(released.FullThrottle);
        Assert.False(predicted.FullThrottle);
        Assert.Equal(released.X, predicted.X, 3);
        Assert.Equal(released.VelocityX, predicted.VelocityX, 3);
        Assert.True(released.VelocityX - boosted.VelocityX < boosted.VelocityX);
    }

    [Fact]
    public void ExpiredOverrideCannotKeepApplyingThrust()
    {
        var room = Start();
        Set(Player(room), "Throttle", .5f);
        room.Input(1, new(1, 0, 0, 0, false, false, FullThrottle: true));
        Step(room, 8);
        var expired = room.Snapshot().Players[0];
        Step(room, 5);
        Assert.Equal(0, expired.Throttle);
        Assert.False(expired.FullThrottle);
        Assert.Equal(expired.VelocityX, room.Snapshot().Players[0].VelocityX);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", true)]
    [InlineData("1", false)]
    [InlineData("null", false)]
    public void FullThrottleIntentMustBeBoolean(string value, bool valid)
    {
        var json =
            $$"""{"v":{{ClientProtocol.Version}},"type":"input","seq":1,"x":0,"y":0,"aim":0,"fire":false,"menu":false,"fullThrottle":{{value}}}""";
        Assert.Equal(valid, ClientProtocol.TryParse(Encoding.UTF8.GetBytes(json), out var message));
        if (valid)
            Assert.Equal(value == "true", Assert.IsType<InputMessage>(message).FullThrottle);
    }
}
