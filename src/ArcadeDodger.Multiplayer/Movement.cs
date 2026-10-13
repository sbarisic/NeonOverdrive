using System.Numerics;

namespace ArcadeDodger.Multiplayer;

/// <summary>Shared thrust, coasting and swept sliding for simulation and prediction.</summary>
public static class Movement
{
    public static bool Steer(
        ref Vector2 position,
        ref Vector2 velocity,
        ref float heading,
        ref float throttle,
        Vector2 controls,
        float dt,
        float speed = 300,
        float acceleration = 1200,
        float steering = 1800,
        SectorCollision? geometry = null,
        bool immobilized = false,
        bool inputAvailable = true,
        float turnRate = MathF.Tau / 3,
        float radius = Room.Radius
    )
    {
        if (immobilized || !inputAvailable)
            throttle = 0;
        if (immobilized)
        {
            velocity = Vector2.Zero;
            return false;
        }
        // Stationary pilots still turn, but need no repeated obstacle sweeps.
        if (throttle == 0 && velocity == Vector2.Zero && (!inputAvailable || controls.Y >= 0))
        {
            if (inputAvailable)
                heading = MathF.IEEERemainder(heading + controls.X * turnRate * dt, MathF.Tau);
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
                heading = MathF.IEEERemainder(heading + controls.X * turnRate * slice, MathF.Tau);
                throttle = Math.Clamp(throttle - controls.Y * .5f * slice, 0, 1);
            }
            collided |= Step(
                ref position,
                ref velocity,
                new Vector2(MathF.Cos(heading), MathF.Sin(heading)) * throttle,
                slice,
                speed,
                acceleration,
                steering,
                geometry,
                immobilized,
                radius
            );
            remaining -= slice;
        }
        return collided;
    }

    public static bool Step(
        ref Vector2 position,
        ref Vector2 velocity,
        Vector2 input,
        float dt,
        float speed = 300,
        float acceleration = 1200,
        float steering = 1800,
        SectorCollision? geometry = null,
        bool immobilized = false,
        float radius = Room.Radius
    )
    {
        if (immobilized)
        {
            velocity = Vector2.Zero;
            return false;
        }
        if (input.LengthSquared() > 1)
            input = Vector2.Normalize(input);
        var target = input * speed;
        var rate =
            input == Vector2.Zero ? 300
            : velocity.LengthSquared() > 1
            && Vector2.Dot(Vector2.Normalize(velocity), Vector2.Normalize(input)) < .7071068f
                ? steering
            : acceleration;
        var delta = target - velocity;
        var distance = delta.Length();
        var previous = velocity;
        velocity = distance <= rate * dt ? target : velocity + delta / distance * (rate * dt);
        if (velocity.LengthSquared() > speed * speed)
            velocity = Vector2.Normalize(velocity) * speed;
        // Average velocity keeps acceleration/coasting stable across replay slice sizes.
        var accelerating = MathF.Min(dt, distance / rate);
        var end =
            position
            + (previous + velocity) * (.5f * accelerating)
            + velocity * (dt - accelerating);
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
