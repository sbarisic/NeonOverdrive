using System.Numerics;
using Xunit;

namespace ArcadeDodger.Multiplayer.Tests;

public class FlightForecastTests
{
    private static SectorDefinition Sector(float acceleration = 45) =>
        new(
            1,
            4000,
            4000,
            [new(1, "planet", "Small", 2000, 2000, 50, Solid: true)],
            [],
            600,
            acceleration
        );

    private static PlayerSnapshot Pilot(Vector2 velocity) =>
        new(
            1,
            "Pilot",
            2150,
            2000,
            0,
            3,
            2,
            0,
            false,
            true,
            VelocityX: velocity.X,
            VelocityY: velocity.Y,
            Heading: .7f,
            Throttle: .8f,
            FullThrottle: true
        );

    private static void Complete(OrbitForecast forecast)
    {
        for (var i = 0; i < 30 && forecast.Outcome == OrbitOutcome.Pending; i++)
            forecast.Advance();
        Assert.NotEqual(OrbitOutcome.Pending, forecast.Outcome);
        Assert.InRange(forecast.Points.Count, 1, 361);
        Assert.All(
            forecast.Points,
            p =>
            {
                Assert.True(float.IsFinite(p.X));
                Assert.True(float.IsFinite(p.Y));
            }
        );
    }

    [Fact]
    public void CoastRequiresAFullCircuitAndIgnoresFullThrottle()
    {
        var sector = Sector();
        var geometry = new SectorCollision(sector);
        var speed = MathF.Sqrt(geometry.GravityAt(new(2150, 2000)).Length() * 150);
        var pilot = Pilot(new(0, speed));
        var saved = pilot with { };
        var forecast = new OrbitForecast(sector, geometry, pilot, 0);
        forecast.Advance(120);
        Assert.Equal(OrbitOutcome.Pending, forecast.Outcome);
        Complete(forecast);
        Assert.Equal(OrbitOutcome.Orbit, forecast.Outcome);
        Assert.Null(forecast.Impact);
        Assert.Equal(saved, pilot);
        Assert.Equal(new Vector2(pilot.X, pilot.Y), forecast.Points[0]);
    }

    [Fact]
    public void ProductionPlanetCircularSpeedAloneDoesNotProveAStableCoastOrbit()
    {
        var sector = new SectorDefinition(
            1,
            24000,
            16000,
            [new(1, "planet", "Rocky", 5000, 5000, 1160, Solid: true)],
            []
        );
        var geometry = new SectorCollision(sector);
        var position = new Vector2(6260, 5000);
        var speed = MathF.Sqrt(geometry.GravityAt(position).Length() * 1260);
        var pilot = Pilot(new(0, speed)) with { X = position.X, Y = position.Y };
        var forecast = new OrbitForecast(sector, geometry, pilot, 0);
        Complete(forecast);
        // Short-range quadratic game gravity is not an inverse-square orbital model.
        Assert.NotEqual(OrbitOutcome.Orbit, forecast.Outcome);
    }

    [Fact]
    public void RadialFallTerminatesAtSweptCollision()
    {
        var sector = Sector();
        var forecast = new OrbitForecast(sector, new(sector), Pilot(new(-100, 0)), 0);
        Complete(forecast);
        Assert.Equal(OrbitOutcome.Collision, forecast.Outcome);
        Assert.NotNull(forecast.Impact);
    }

    [Fact]
    public void OutwardFlightEscapesGravity()
    {
        var sector = Sector();
        var forecast = new OrbitForecast(sector, new(sector), Pilot(new(220, 0)), 0);
        Complete(forecast);
        Assert.Equal(OrbitOutcome.Escape, forecast.Outcome);
    }

    [Fact]
    public void NoNearbySolidPlanetEscapesImmediately()
    {
        var sector = Sector() with { Locations = [] };
        var forecast = new OrbitForecast(sector, new(sector), Pilot(Vector2.Zero), 0);
        Assert.Equal(OrbitOutcome.Escape, forecast.Outcome);
        forecast.Advance(int.MaxValue);
        Assert.Single(forecast.Points);
    }

    [Fact]
    public void HorizonIsBoundedAndIncremental()
    {
        var sector = Sector(.00001f);
        var forecast = new OrbitForecast(sector, new(sector), Pilot(Vector2.Zero), 0);
        forecast.Advance(0);
        Assert.Single(forecast.Points);
        forecast.Advance(3599);
        Assert.Equal(OrbitOutcome.Pending, forecast.Outcome);
        forecast.Advance(1);
        Assert.Equal(OrbitOutcome.Horizon, forecast.Outcome);
        Assert.InRange(forecast.Points.Count, 1, 361);
        var points = forecast.Points.ToArray();
        forecast.Advance(int.MaxValue);
        Assert.Equal(points, forecast.Points.ToArray());
    }

    [Fact]
    public void SuppliedThrottleUsesSharedMovementWithoutMutatingSnapshot()
    {
        var sector = Sector();
        var geometry = new SectorCollision(sector);
        var pilot = Pilot(new(0, 50));
        var position = new Vector2(pilot.X, pilot.Y);
        var velocity = new Vector2(pilot.VelocityX, pilot.VelocityY);
        var heading = pilot.Heading;
        var throttle = pilot.Throttle;
        for (var i = 0; i < 10; i++)
            Movement.Steer(
                ref position,
                ref velocity,
                ref heading,
                ref throttle,
                Vector2.Zero,
                1f / 30,
                pilot.MoveSpeed,
                pilot.Acceleration,
                pilot.Steering,
                geometry,
                inputAvailable: true,
                turnRate: pilot.TurnRate,
                radius: ChassisCatalog.Get(pilot.Chassis).Radius,
                fullThrottle: false
            );
        var forecast = new OrbitForecast(sector, geometry, pilot, pilot.Throttle);
        forecast.Advance(10);
        Assert.Equal(position, forecast.Points[^1]);
        Assert.True(pilot.FullThrottle);
        Assert.Equal(.8f, pilot.Throttle);
    }

    [Fact]
    public void StationaryTargetHasDirectIntercept()
    {
        Assert.True(
            ProjectileIntercept.TrySolve(
                Vector2.Zero,
                new(100, 0),
                Vector2.Zero,
                50,
                100,
                out var point,
                out var seconds
            )
        );
        Assert.Equal(new Vector2(100, 0), point);
        Assert.Equal(2, seconds);
    }

    [Fact]
    public void LateralTargetIsLedToActualMeetingPoint()
    {
        var target = new Vector2(100, 0);
        var velocity = new Vector2(0, 30);
        Assert.True(
            ProjectileIntercept.TrySolve(
                Vector2.Zero,
                target,
                velocity,
                50,
                200,
                out var point,
                out var seconds
            )
        );
        Assert.Equal(2.5f, seconds, 4);
        Assert.Equal(target + velocity * seconds, point);
        Assert.Equal(50 * seconds, point.Length(), 4);
    }

    [Fact]
    public void EqualSpeedApproachingTargetUsesLinearSolution()
    {
        Assert.True(
            ProjectileIntercept.TrySolve(
                Vector2.Zero,
                new(100, 0),
                new(-50, 0),
                50,
                100,
                out var point,
                out var seconds
            )
        );
        Assert.Equal(1, seconds);
        Assert.Equal(new Vector2(50, 0), point);
    }

    [Theory]
    [InlineData(60, 50, 1000)]
    [InlineData(50, 50, 1000)]
    [InlineData(0, 50, 99)]
    [InlineData(0, 0, 1000)]
    [InlineData(0, -50, 1000)]
    [InlineData(0, 50, -1)]
    [InlineData(float.NaN, 50, 1000)]
    [InlineData(0, float.PositiveInfinity, 1000)]
    [InlineData(0, 50, float.NaN)]
    public void ImpossibleOutOfRangeAndInvalidInputsAreRejected(
        float velocity,
        float speed,
        float range
    )
    {
        Assert.False(
            ProjectileIntercept.TrySolve(
                Vector2.Zero,
                new(100, 0),
                new(velocity, 0),
                speed,
                range,
                out var point,
                out var seconds
            )
        );
        Assert.Equal(Vector2.Zero, point);
        Assert.Equal(0, seconds);
    }

    [Fact]
    public void NonfinitePositionsAreRejected()
    {
        Assert.False(
            ProjectileIntercept.TrySolve(
                new(float.NaN, 0),
                new(100, 0),
                Vector2.Zero,
                50,
                1000,
                out _,
                out _
            )
        );
        Assert.False(
            ProjectileIntercept.TrySolve(
                Vector2.Zero,
                new(100, float.PositiveInfinity),
                Vector2.Zero,
                50,
                1000,
                out _,
                out _
            )
        );
    }
}
