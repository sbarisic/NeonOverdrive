using System.Numerics;

namespace ArcadeDodger.Multiplayer;

/// <summary>Shared thrust, coasting and swept sliding for simulation and prediction.</summary>
public static class Movement
{
    public static void Step(
        ref Vector2 position,
        ref Vector2 velocity,
        Vector2 input,
        float dt,
        float speed = 300,
        float acceleration = 1200,
        float steering = 1800,
        SectorCollision? geometry = null,
        bool immobilized = false
    )
    {
        if (immobilized)
        {
            velocity = Vector2.Zero;
            return;
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
            position = geometry.Move(position, end, Room.Radius, ref velocity);
        else
        {
            position = Vector2.Clamp(
                end,
                new(Room.Radius),
                new(Room.Width - Room.Radius, Room.Height - Room.Radius)
            );
            if (position.X != end.X)
                velocity.X = 0;
            if (position.Y != end.Y)
                velocity.Y = 0;
        }
    }
}
