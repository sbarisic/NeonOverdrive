using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public enum OrbitOutcome
{
    Pending,
    Orbit,
    Collision,
    Escape,
    Horizon,
}

/// <summary>A bounded, incremental presentation simulation; never changes the pilot.</summary>
public sealed class OrbitForecast
{
    private const int Budget = 3600;
    private readonly SectorDefinition sector;
    private readonly SectorCollision geometry;
    private readonly PlayerSnapshot pilot;
    private readonly SectorLocation? body;
    private readonly List<Vector2> points = [];
    private readonly float radius;
    private readonly Vector2 start;
    private readonly Vector2 initialVelocity;
    private readonly float initialDistance;
    private Vector2 position;
    private Vector2 velocity;
    private float heading;
    private float throttle;
    private float minimumDistance;
    private float maximumDistance;
    private double travel;
    private int direction;
    private bool consistent = true;
    private int elapsed;

    public OrbitForecast(
        SectorDefinition sector,
        SectorCollision geometry,
        PlayerSnapshot pilot,
        float throttle
    )
    {
        this.sector = sector;
        this.geometry = geometry;
        this.pilot = pilot;
        position = start = new(pilot.X, pilot.Y);
        velocity = initialVelocity = new(pilot.VelocityX, pilot.VelocityY);
        heading = pilot.Heading;
        this.throttle = throttle;
        radius = ChassisCatalog.Get(pilot.Chassis).Radius;
        Points = points.AsReadOnly();
        if (
            !Finite(position)
            || !Finite(velocity)
            || !float.IsFinite(heading)
            || !float.IsFinite(throttle)
        )
        {
            Outcome = OrbitOutcome.Horizon;
            return;
        }
        points.Add(position);
        body = sector
            .Locations.Where(l =>
                l.Solid
                && l.Kind == "planet"
                && Vector2.Distance(position, l.Position) - l.Radius < sector.GravityRange
            )
            .MinBy(l => Vector2.DistanceSquared(position, l.Position));
        if (body is null)
        {
            Outcome = OrbitOutcome.Escape;
            return;
        }
        initialDistance =
            minimumDistance =
            maximumDistance =
                Vector2.Distance(position, body.Position);
        if (geometry.TraceShips(position, position, radius) is not null || Outside(position))
            Finish(OrbitOutcome.Collision);
    }

    public IReadOnlyList<Vector2> Points { get; }
    public OrbitOutcome Outcome { get; private set; }
    public Vector2? Impact { get; private set; }

    public void Advance(int steps = 120)
    {
        for (var i = 0; i < steps && Outcome == OrbitOutcome.Pending && elapsed < Budget; i++)
        {
            var previous = position;
            var collided = Movement.Steer(
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
                radius: radius,
                fullThrottle: false
            );
            elapsed++;
            if (!Finite(position) || !Finite(velocity))
            {
                position = previous;
                Finish(OrbitOutcome.Horizon);
                break;
            }
            if (collided || Outside(position))
            {
                Finish(OrbitOutcome.Collision);
                break;
            }
            var offset = position - body!.Position;
            var distance = offset.Length();
            if (distance - body.Radius >= sector.GravityRange)
            {
                Finish(OrbitOutcome.Escape);
                break;
            }
            minimumDistance = MathF.Min(minimumDistance, distance);
            maximumDistance = MathF.Max(maximumDistance, distance);
            var before = previous - body.Position;
            var angle = Math.Atan2(
                (double)before.X * offset.Y - (double)before.Y * offset.X,
                (double)before.X * offset.X + (double)before.Y * offset.Y
            );
            if (Math.Abs(angle) > 1e-7)
            {
                var sign = Math.Sign(angle);
                if (direction == 0)
                    direction = sign;
                else if (sign != direction)
                    consistent = false;
            }
            travel += angle;
            // Require a completed circuit, closure and radial stability, not a speed heuristic.
            if (
                consistent
                && Math.Abs(travel) >= Math.Tau
                && maximumDistance - minimumDistance <= initialDistance * .25f
                && Vector2.Distance(position, start) <= initialDistance * .1f
                && Vector2.Distance(velocity, initialVelocity)
                    <= MathF.Max(1, initialVelocity.Length() * .15f)
            )
                Finish(OrbitOutcome.Orbit);
            else if (elapsed == Budget)
                Finish(OrbitOutcome.Horizon);
            else if (elapsed % 10 == 0)
                points.Add(position);
        }
    }

    private bool Outside(Vector2 at) =>
        at.X < radius
        || at.Y < radius
        || at.X > sector.Width - radius
        || at.Y > sector.Height - radius;

    private void Finish(OrbitOutcome outcome)
    {
        Outcome = outcome;
        if (outcome == OrbitOutcome.Collision)
            Impact = position;
        if (points.Count == 0 || points[^1] != position)
            points.Add(position);
    }

    private static bool Finite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
}

/// <summary>Constant-velocity interception for finite-speed projectiles.</summary>
public static class ProjectileIntercept
{
    public static bool TrySolve(
        Vector2 muzzle,
        Vector2 target,
        Vector2 velocity,
        float speed,
        float range,
        out Vector2 point,
        out float seconds
    )
    {
        point = default;
        seconds = 0;
        if (
            !float.IsFinite(muzzle.X)
            || !float.IsFinite(muzzle.Y)
            || !float.IsFinite(target.X)
            || !float.IsFinite(target.Y)
            || !float.IsFinite(velocity.X)
            || !float.IsFinite(velocity.Y)
            || !float.IsFinite(speed)
            || !float.IsFinite(range)
            || speed <= 0
            || range <= 0
        )
            return false;
        double x = (double)target.X - muzzle.X,
            y = (double)target.Y - muzzle.Y;
        double a =
            (double)velocity.X * velocity.X
            + (double)velocity.Y * velocity.Y
            - (double)speed * speed;
        double b = 2 * (x * velocity.X + y * velocity.Y);
        double c = x * x + y * y;
        double time;
        if (a == 0)
            time = b == 0 ? double.NaN : -c / b;
        else
        {
            var discriminant = b * b - 4 * a * c;
            if (discriminant < 0)
                return false;
            var q = -.5 * (b + Math.CopySign(Math.Sqrt(discriminant), b));
            var first = q / a;
            var second = q == 0 ? double.NaN : c / q;
            time =
                first > 0 && second > 0 ? Math.Min(first, second)
                : first > 0 ? first
                : second;
        }
        if (!double.IsFinite(time) || time <= 0 || time > float.MaxValue || speed * time > range)
            return false;
        var aim = new Vector2(
            (float)(target.X + velocity.X * time),
            (float)(target.Y + velocity.Y * time)
        );
        if (!float.IsFinite(aim.X) || !float.IsFinite(aim.Y))
            return false;
        point = aim;
        seconds = (float)time;
        return true;
    }
}
