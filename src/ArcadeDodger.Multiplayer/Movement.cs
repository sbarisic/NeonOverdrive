using System.Numerics;

namespace ArcadeDodger.Multiplayer;

/// <summary>Shared thrust, coasting and swept sliding for simulation and prediction.</summary>
public static class Movement
{
    public const float BrakeDeceleration = 300;
    public const int ModelVersion = 6;

    public static float AssistedAcceleration(float acceleration, Vector2 gravity) =>
        acceleration + 2 * gravity.Length();

    public static bool Steer(
        ref Vector2 position,
        ref Vector2 velocity,
        ref float heading,
        ref float throttle,
        Vector2 controls,
        float dt,
        float speed = 220,
        float acceleration = 110,
        float steering = 165,
        SectorCollision? geometry = null,
        bool immobilized = false,
        bool inputAvailable = true,
        float turnRate = MathF.Tau / 3,
        float radius = Room.Radius,
        bool brake = false,
        float? cursorHeading = null,
        bool fullThrottle = false
    )
    {
        brake &= inputAvailable && !immobilized;
        fullThrottle &= inputAvailable && !immobilized && !brake;
        if (immobilized || !inputAvailable || brake)
            throttle = 0;
        if (immobilized)
        {
            velocity = Vector2.Zero;
            return false;
        }
        // Stationary pilots still turn, but need no repeated obstacle sweeps.
        if (
            !fullThrottle
            && throttle == 0
            && velocity == Vector2.Zero
            && (!inputAvailable || controls.Y >= 0)
            && (geometry?.GravityAt(position) ?? Vector2.Zero) == Vector2.Zero
        )
        {
            if (inputAvailable)
                heading = Turn(heading, controls.X, cursorHeading, turnRate * dt);
            return false;
        }
        // Fixed substeps bound turn integration error independently of packet/render cadence.
        var remaining = dt;
        var collided = false;
        while (remaining > .000001f)
        {
            var slice = MathF.Min(remaining, 1f / 120);
            if (!immobilized && inputAvailable)
            {
                heading = Turn(heading, controls.X, cursorHeading, turnRate * slice);
                if (!brake && !fullThrottle)
                    throttle = Math.Clamp(throttle - controls.Y * .5f * slice, 0, 1);
            }
            collided |= Step(
                ref position,
                ref velocity,
                new Vector2(MathF.Cos(heading), MathF.Sin(heading)) * (fullThrottle ? 1 : throttle),
                slice,
                speed,
                acceleration,
                steering,
                geometry,
                immobilized,
                radius,
                brake
            );
            remaining -= slice;
        }
        return collided;
    }

    public static float Turn(float heading, float manual, float? target, float step)
    {
        var change =
            manual != 0 ? manual * step
            : target is { } aim
                ? Math.Clamp(MathF.IEEERemainder(aim - heading, MathF.Tau), -step, step)
            : 0;
        return MathF.IEEERemainder(heading + change, MathF.Tau);
    }

    public static bool Step(
        ref Vector2 position,
        ref Vector2 velocity,
        Vector2 input,
        float dt,
        float speed = 220,
        float acceleration = 110,
        float steering = 165,
        SectorCollision? geometry = null,
        bool immobilized = false,
        float radius = Room.Radius,
        bool brake = false
    )
    {
        if (immobilized)
        {
            velocity = Vector2.Zero;
            return false;
        }
        if (input.LengthSquared() > 1)
            input = Vector2.Normalize(input);
        // Engines add momentum along the hull. They never cancel lateral drift.
        var previous = velocity;
        var previousSpeed = previous.Length();
        // Avoid treating roundoff at a perpendicular heading as opposing thrust.
        var rate =
            Vector2.Dot(previous, input) < -previousSpeed * .00001f ? steering : acceleration;
        var gravity = geometry?.GravityAt(position) ?? Vector2.Zero;
        rate = AssistedAcceleration(rate, gravity);
        Vector2 end;
        if (brake)
        {
            var brakingTime = MathF.Min(dt, previousSpeed / BrakeDeceleration);
            velocity =
                previousSpeed > 0
                    ? previous
                        * (MathF.Max(0, previousSpeed - BrakeDeceleration * dt) / previousSpeed)
                    : Vector2.Zero;
            end = position + (previous + velocity) * (.5f * brakingTime);
        }
        else
        {
            velocity += input * (rate * dt);
            // A lost cruise/tow allowance bleeds off gradually, including at zero throttle.
            // The magnitude limit never turns velocity toward the nose.
            var limit = MathF.Max(speed, previousSpeed - BrakeDeceleration * dt);
            if (velocity.LengthSquared() > limit * limit)
                velocity = Vector2.Normalize(velocity) * limit;
            end = position + (previous + velocity) * (.5f * dt);
        }
        // External acceleration applies even with idle engines or brakes.
        velocity += gravity * dt;
        end += gravity * (.5f * dt * dt);
        if (geometry is not null)
            position = geometry.Move(position, end, radius, ref velocity);
        else
        {
            position = Vector2.Clamp(
                end,
                new(radius),
                new(Room.Width - radius, Room.Height - radius)
            );
            if (position.X != end.X)
                velocity.X = 0;
            if (position.Y != end.Y)
                velocity.Y = 0;
        }
        return Vector2.DistanceSquared(position, end) > .000001f;
    }
}
