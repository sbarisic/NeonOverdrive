using System.Numerics;
using System.Text;
using Xunit;
using static ArcadeDodger.Multiplayer.Tests.WorkshopRig;

namespace ArcadeDodger.Multiplayer.Tests;

public class InertialFlightTests
{
    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(144)]
    public void RenderCadenceAndPacketBurstsDoNotChangeFlight(int frames)
    {
        Vector2 Simulate(int hz)
        {
            Vector2 position = new(5000),
                velocity = new(90, 60);
            float heading = 0,
                throttle = .5f;
            for (var i = 0; i < hz; i++)
                Movement.Steer(
                    ref position,
                    ref velocity,
                    ref heading,
                    ref throttle,
                    new(.5f, -.5f),
                    1f / hz
                );
            return velocity;
        }
        Assert.InRange(Vector2.Distance(Simulate(30), Simulate(frames)), 0, .2f);

        var one = Start();
        var many = Start();
        for (var tick = 0; tick < 120; tick++)
        {
            var brake = tick >= 90;
            one.Input(1, new(tick + 1, .5f, -1, .7f, false, false, Brake: brake));
            for (var packet = 1; packet <= frames; packet++)
                many.Input(
                    1,
                    new(tick * frames + packet, .5f, -1, .7f, false, false, Brake: brake)
                );
            one.Step();
            many.Step();
            var a = one.Snapshot().Players[0];
            var b = many.Snapshot().Players[0];
            Assert.Equal(a.X, b.X);
            Assert.Equal(a.Y, b.Y);
            Assert.Equal(a.VelocityX, b.VelocityX);
            Assert.Equal(a.Heading, b.Heading);
            Assert.Equal(a.Throttle, b.Throttle);
            Assert.Equal(a.Braking, b.Braking);
        }
    }

    [Fact]
    public void TimedOutBrakeAndDisconnectResumeCoastingWithoutReplenishment()
    {
        var room = Start();
        Set(Player(room), "Velocity", new Vector2(200, 0));
        room.Input(1, new(1, 0, 0, 0, false, false, Brake: true));
        Step(room, 8);
        var expired = room.Snapshot().Players[0];
        Assert.False(expired.Braking);
        Assert.Equal(130, expired.VelocityX, 3);
        Step(room, 30);
        Assert.Equal(expired.VelocityX, room.Snapshot().Players[0].VelocityX);
        room.Disconnect(1);
        Step(room, 10);
        var exposed = room.Snapshot().Players[0];
        Assert.Equal(130, exposed.VelocityX, 3);
        Assert.Equal(1, room.Join("Modules"));
        var restored = room.Snapshot().Players[0];
        Assert.Equal(exposed.X, restored.X);
        Assert.Equal(exposed.VelocityX, restored.VelocityX);
        Assert.Equal(exposed.Hull, restored.Hull);
        Assert.False(restored.Braking);
    }

    [Theory]
    [InlineData(.75f, MathF.PI / 2)]
    [InlineData(1.5f, MathF.PI)]
    public void HullTurnsWithoutTurningMomentum(float seconds, float expected)
    {
        Vector2 position = new(5000),
            velocity = new(100, 50);
        float heading = 0,
            throttle = 0;
        Movement.Steer(ref position, ref velocity, ref heading, ref throttle, new(1, 0), seconds);
        Assert.Equal(new Vector2(100, 50), velocity);
        Assert.InRange(MathF.Abs(MathF.Abs(heading) - expected), 0, .0001f);
        Assert.InRange(Vector2.Distance(position, new Vector2(5000) + velocity * seconds), 0, .1f);
        var stoppedHeading = heading;
        Movement.Steer(ref position, ref velocity, ref heading, ref throttle, Vector2.Zero, 1);
        Assert.Equal(stoppedHeading, heading);
        Assert.Equal(new Vector2(100, 50), velocity);
    }

    [Fact]
    public void ThrustKeepsSidewaysMomentumAndThrottleScalesAcceleration()
    {
        Vector2 position = new(5000),
            velocity = new(60, 0);
        Movement.Step(ref position, ref velocity, Vector2.UnitY * .5f, 1);
        Assert.Equal(new Vector2(60, 55), velocity);
        Movement.Step(ref position, ref velocity, -Vector2.UnitX, .1f);
        Assert.Equal(43.5f, velocity.X);
        Assert.Equal(55, velocity.Y);
    }

    [Fact]
    public void PerpendicularHullThrustUsesNormalAcceleration()
    {
        Vector2 position = new(5000),
            velocity = new(60, 0);
        float heading = MathF.PI / 2,
            throttle = .5f;
        Movement.Steer(ref position, ref velocity, ref heading, ref throttle, Vector2.Zero, .1f);
        Assert.Equal(60, velocity.X, 3);
        Assert.Equal(5.5f, velocity.Y, 3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void EachChassisReachesItsSpeedInTwoSecondsOfFullThrust(int id)
    {
        var chassis = ChassisCatalog.Get(id);
        Vector2 position = new(5000),
            velocity = Vector2.Zero;
        float heading = 0,
            throttle = 1;
        Movement.Steer(
            ref position,
            ref velocity,
            ref heading,
            ref throttle,
            Vector2.Zero,
            2,
            chassis.Speed,
            chassis.Acceleration,
            chassis.Steering
        );
        Assert.InRange(velocity.X, chassis.Speed - .01f, chassis.Speed);
        Assert.Equal(0, velocity.Y);
    }

    [Fact]
    public void SpeedCapKeepsResultantDirectionAndLostAllowanceBleedsOff()
    {
        Vector2 position = new(5000),
            velocity = new(220, 0);
        Movement.Step(ref position, ref velocity, Vector2.UnitY, .1f);
        Assert.Equal(220, velocity.Length(), 3);
        Assert.Equal(11f / 220, velocity.Y / velocity.X, 5);
        velocity = new(400, 300);
        Movement.Step(ref position, ref velocity, Vector2.Zero, .1f);
        Assert.Equal(470, velocity.Length(), 3);
        Assert.Equal(.75f, velocity.Y / velocity.X, 5);
    }

    [Fact]
    public void BrakeStopsExactlyWithoutReverseAndLeavesThrottleOff()
    {
        Vector2 position = new(5000),
            velocity = new(180, 0);
        float heading = 0,
            throttle = 1;
        Movement.Steer(
            ref position,
            ref velocity,
            ref heading,
            ref throttle,
            new(1, -1),
            1,
            brake: true
        );
        Assert.Equal(Vector2.Zero, velocity);
        Assert.Equal(0, throttle);
        Assert.InRange(position.X, 5053.99f, 5054.01f);
        Assert.InRange(heading, 2.09f, 2.10f);
        Movement.Steer(ref position, ref velocity, ref heading, ref throttle, Vector2.Zero, 1);
        Assert.Equal(Vector2.Zero, velocity);
    }

    [Fact]
    public void MissingInputReleasesBrakeAndThrustButPreservesMomentum()
    {
        Vector2 position = new(5000),
            velocity = new(100, 50);
        float heading = .4f,
            throttle = 1;
        Movement.Steer(
            ref position,
            ref velocity,
            ref heading,
            ref throttle,
            new(1, -1),
            1,
            inputAvailable: false,
            brake: true
        );
        Assert.Equal(new Vector2(100, 50), velocity);
        Assert.Equal(.4f, heading);
        Assert.Equal(0, throttle);
    }

    [Fact]
    public void BrakeCancelsCruiseAndPredictionMatchesTheSameTick()
    {
        var room = Start();
        var player = Player(room);
        Set(player, "GearTiers", new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 });
        Set(player, "Cruise", true);
        Set(player, "Throttle", 1f);
        Set(player, "Velocity", new Vector2(400, 0));
        var before = room.Snapshot().Players[0];
        var prediction = new LocalPrediction();
        prediction.Receive(before, room.Tick, 0);
        prediction.Input(1, -1, .7f, false, 0, brake: true);
        room.Input(1, new(3, 1, -1, .7f, false, false, Brake: true));
        room.Step();
        var actual = room.Snapshot().Players[0];
        var predicted = prediction.Sample(1d / 30)!;
        Assert.Equal("cooldown", actual.CruiseState);
        Assert.True(actual.Braking);
        Assert.Equal(0, actual.Throttle);
        Assert.Equal(390, actual.VelocityX, 3);
        Assert.Equal(actual.X, predicted.X, 3);
        Assert.Equal(actual.Heading, predicted.Heading, 4);
        Assert.Equal(actual.VelocityX, predicted.VelocityX, 3);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", true)]
    [InlineData("1", false)]
    [InlineData("null", false)]
    public void BrakeIntentIsStrictlyBoolean(string value, bool valid)
    {
        var json =
            $$"""{"v":15,"type":"input","seq":1,"x":0,"y":0,"aim":0,"fire":false,"menu":false,"brake":{{value}}}""";
        Assert.Equal(valid, ClientProtocol.TryParse(Encoding.UTF8.GetBytes(json), out var parsed));
        if (valid)
            Assert.Equal(value == "true", Assert.IsType<InputMessage>(parsed).Brake);
        Assert.False(
            ClientProtocol.TryParse(
                Encoding.UTF8.GetBytes(json.Replace("\"v\":15", "\"v\":11")),
                out _
            )
        );
    }
}
