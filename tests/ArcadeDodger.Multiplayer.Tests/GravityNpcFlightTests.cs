using System.Numerics;
using Xunit;
using static ArcadeDodger.Multiplayer.Tests.WorkshopRig;

namespace ArcadeDodger.Multiplayer.Tests;

public class GravityNpcFlightTests
{
    private static object? Call(object target, string name, params object[] args) =>
        target
            .GetType()
            .GetMethod(
                name,
                System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Static
            )!
            .Invoke(target, args);

    private static SectorCollision Geometry(bool solid = true) =>
        new(new(1, 24000, 16000, [new(1, "planet", "Test", 5000, 5000, 1160, Solid: solid)], []));

    [Theory]
    [InlineData(0, 45)]
    [InlineData(200, 11.25f)]
    [InlineData(400, 0)]
    [InlineData(500, 0)]
    public void GravityUsesSurfaceDistanceAndNeverAffectsGas(float altitude, float expected)
    {
        var at = new Vector2(6160 + altitude, 5000);
        Assert.Equal(-expected, Geometry().GravityAt(at).X, 4);
        Assert.Equal(Vector2.Zero, Geometry(false).GravityAt(at));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(144)]
    public void IdleShipsFallWithFrameIndependentGravity(int hz)
    {
        (Vector2 Position, Vector2 Velocity) Run(int frequency)
        {
            Vector2 at = new(6360, 5000),
                velocity = default;
            float heading = 0,
                throttle = 0;
            var geometry = Geometry();
            for (var i = 0; i < frequency * 2; i++)
                Movement.Steer(
                    ref at,
                    ref velocity,
                    ref heading,
                    ref throttle,
                    Vector2.Zero,
                    1f / frequency,
                    geometry: geometry,
                    inputAvailable: false
                );
            return (at, velocity);
        }
        var actual = Run(hz);
        var reference = Run(120);
        Assert.True(actual.Position.X < 6350);
        Assert.True(actual.Velocity.X < -20);
        Assert.InRange(Vector2.Distance(reference.Position, actual.Position), 0, .15f);
        Assert.InRange(Vector2.Distance(reference.Velocity, actual.Velocity), 0, .1f);
    }

    [Fact]
    public void SurfaceContactSlidesAndImmobilizationSuppressesGravity()
    {
        Vector2 at = new(6180, 5000),
            velocity = new(-200, 70);
        float heading = 0,
            throttle = 0;
        var geometry = Geometry();
        for (var i = 0; i < 120; i++)
            Movement.Steer(
                ref at,
                ref velocity,
                ref heading,
                ref throttle,
                Vector2.Zero,
                1f / 120,
                geometry: geometry,
                radius: 20
            );
        Assert.True(Vector2.Distance(at, new(5000, 5000)) >= 1179.99f);
        Assert.True(at.Y > 5040);
        var previous = at;
        Movement.Steer(
            ref at,
            ref velocity,
            ref heading,
            ref throttle,
            Vector2.Zero,
            1,
            geometry: geometry,
            immobilized: true
        );
        Assert.Equal(previous, at);
        Assert.Equal(Vector2.Zero, velocity);
    }

    [Fact]
    public void BrakeCanHoldScanPositionAndThrustCanEscape()
    {
        Vector2 at = new(6180, 5000),
            velocity = default;
        float heading = 0,
            throttle = 0;
        var geometry = Geometry();
        for (var i = 0; i < 90; i++)
        {
            var previous = at;
            Movement.Steer(
                ref at,
                ref velocity,
                ref heading,
                ref throttle,
                Vector2.Zero,
                1f / 30,
                geometry: geometry,
                radius: 20,
                brake: true
            );
            Assert.InRange(Vector2.Distance(at, previous), 0, .05f);
        }
        for (var i = 0; i < 240; i++)
            Movement.Steer(
                ref at,
                ref velocity,
                ref heading,
                ref throttle,
                new(0, -1),
                1f / 30,
                geometry: geometry,
                radius: 20
            );
        Assert.True(at.X > 6560);
    }

    [Fact]
    public void NpcGoalsAccelerateWithoutTeleportingAndStunsCoast()
    {
        var room = Start(ai: true);
        var npc = Enemy(room, 3000, 3000, 99, 100);
        var start = (Vector2)Get(npc, "Position");
        Call(room, "FlyNpc", npc, start + new Vector2(800, 0), 120f, 20f);
        Assert.Equal(start, Get(npc, "Position"));
        Call(room, "StepNpcFlight");
        var first = ((Vector2)Get(npc, "Velocity")).Length();
        Assert.InRange(first, .001f, 10);
        for (var i = 0; i < 30; i++)
            Call(room, "StepNpcFlight");
        Assert.True(((Vector2)Get(npc, "Velocity")).Length() > first);
        var before = (Vector2)Get(npc, "Velocity");
        Set(npc, "StunUntil", 1000L);
        Call(room, "StepNpcFlight");
        Assert.Equal(before, Get(npc, "Velocity"));
        Assert.Equal(0f, Get(npc, "Throttle"));
    }

    [Theory]
    [InlineData(44)]
    [InlineData(234)]
    public void RoamingContactCompletesAnAccelerationBasedStationRoute(int seed)
    {
        var room = new Room(true, seed: seed);
        var units = (System.Collections.IList)Get(room, "enemies");
        var npc = units.Cast<object>().First(e => (string)Get(e, "type") == "contact");
        var original = (int)Get(npc, "Home");
        for (var i = 0; i < 10000 && (int)Get(npc, "Home") == original; i++)
        {
            Set(room, "<Tick>k__BackingField", 1801L + i);
            Set(room, "npcRouteWork", 2048);
            Call(room, "MoveContact", npc);
            Call(room, "StepNpcFlight");
        }
        Assert.NotEqual(original, (int)Get(npc, "Home"));
        Assert.InRange(((Vector2)Get(npc, "Velocity")).Length(), 0, 10);
    }
}
