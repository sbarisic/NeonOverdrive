using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    // Runs once per tick in the pilot priority order selected by Step.
    private void StepPlayer(Player p)
    {
        p.Previous = p.Position;
        if (p.Pending is { } pending)
        {
            if (!pending.Menu && Sector is not null && ExplorationFor(p).Hailing != 0)
                EndHail(p, ExplorationFor(p));
            p.Held = pending;
            p.Editing = pending.Editing && p.Hull > 0;
            p.Menu = pending.Menu;
            p.Ack = pending.Sequence;
            p.Pending = null;
        }
        if (Tick - p.LastInputTick > 7)
            p.Held = null;
        if (Tick - p.LastInputTick > 90)
            p.Editing = false;
        if (
            p.Hull > 0
            && (p.Shield < MaxShieldFor(p) || p.ShieldFraction > 0)
            && Tick >= p.ShieldAt
        )
        {
            p.Shield = Math.Min(MaxShieldFor(p), p.Shield + 1);
            if (p.Shield == MaxShieldFor(p))
                p.ShieldFraction = 0;
            p.ShieldAt = Tick + (int)(TickRate / (Value(p, "flux", 1) + .1f * Gear(p, 11)));
        }
        if (
            p.Hull > 0
            && (p.Hull < MaxHullFor(p) || p.HullFraction > 0)
            && Rank(p, "repair") > 0
            && Tick >= p.HullAt
        )
        {
            p.Hull = Math.Min(MaxHullFor(p), p.Hull + 1);
            if (p.Hull == MaxHullFor(p))
                p.HullFraction = 0;
            p.HullAt = Tick + (int)(Value(p, "repair", 10) * TickRate);
        }
        var input = p.Held;
        if (Sector is not null && ExplorationFor(p).Hailing != 0)
            p.Editing = true;
        if (input is not null)
            p.Aim = input.Aim;
        var frozen = p.Hull <= 0 || p.Editing || p.Menu || p.Docked != 0 || RaceCountdown(p);
        StepCruise(p, frozen);
        var collided = Movement.Steer(
            ref p.Position,
            ref p.Velocity,
            ref p.Heading,
            ref p.Throttle,
            input is null ? Vector2.Zero : new(input.X, input.Y),
            1f / TickRate,
            MoveSpeedFor(p) * CruiseMultiplier(p) * (Living(p).Tow == 0 ? 1 : .6f),
            AccelerationFor(p) * (p.Cruise ? 2 : 1),
            SteeringFor(p) * (p.Cruise ? 2 : 1),
            geometry,
            frozen,
            input is not null && p.Connected,
            TurnRateFor(p),
            ChassisCatalog.Get(p.Chassis).Radius,
            input?.Brake == true,
            input?.CursorSteering == true ? input.Aim : null,
            input?.FullThrottle == true
        );
        if (collided)
        {
            p.CollisionAt = Tick;
            CancelCruise(p);
        }
        if (frozen || input is null)
            return;
        if (input.Fire && !Safe(p) && !p.Cruise)
        {
            p.ProtectedUntil = 0;
            Fire(p);
        }
    }
}
