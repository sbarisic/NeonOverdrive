using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private void FlyNpc(Enemy npc, Vector2 goal, float speed, float stop = 0)
    {
        if (
            Sector?.Locations.FirstOrDefault(l =>
                l.Kind == "station" && Vector2.DistanceSquared(l.Position, goal) < 4
            ) is
            { } station
        )
        {
            var angle = (npc.Id * 2.399963f + npc.BlockedRoutes * .7f) % MathF.Tau;
            goal =
                station.Position
                + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (120 + EnemyRadius(npc));
            stop = 8;
        }
        npc.FlightGoal = goal;
        npc.FlightSpeed = speed;
        npc.FlightStop = stop;
    }

    private void StepNpcFlight()
    {
        var hailed = players
            .Values.Where(p => p.Connected && p.Hull > 0)
            .Select(p => ExplorationFor(p).Hailing)
            .ToHashSet();
        // Rotate budget priority so late-spawned convoys cannot starve behind patrol searches.
        foreach (
            var npc in enemies
                .Skip((int)(Tick % Math.Max(1, enemies.Count)))
                .Concat(enemies.Take((int)(Tick % Math.Max(1, enemies.Count))))
        )
        {
            if (npc.Hull <= 0 || Stationary(npc) || npc.SurfacePlanet != 0)
                continue;
            var design = ChassisCatalog.Get(npc.Chassis);
            var fixedBody =
                hailed.Contains(npc.Id) || npc.State is "docking" or "contact" or "arrived";
            var stunned = Tick < npc.StunUntil;
            var speed =
                MathF.Max(
                    1,
                    npc.FlightSpeed > 0 ? npc.FlightSpeed : EnemyCatalog.Get(npc.Type).Speed
                ) * (Tick < npc.SlowUntil ? npc.SlowFactor : 1);
            var goal = npc.FlightGoal ?? npc.Position;
            if (
                !fixedBody
                && npc.FlightGoal is not null
                && Vector2.Distance(npc.Position, goal) > MathF.Max(40, npc.FlightStop + 10)
            )
            {
                if (Vector2.DistanceSquared(npc.Position, npc.ProgressPosition) > 30 * 30)
                {
                    npc.ProgressPosition = npc.Position;
                    npc.ProgressAt = Tick;
                    npc.BlockedRoutes = 0;
                }
                else if (Tick - npc.ProgressAt >= 5 * TickRate && npc.RouteSearch is null)
                {
                    npc.ProgressAt = Tick;
                    npc.BlockedRoutes++;
                    npc.RouteSearch?.Dispose();
                    npc.RouteSearch = null;
                    npc.Route = [];
                    npc.RepathAt = 0;
                    if (npc.BlockedRoutes == 3)
                        Broadcast(
                            "traffic",
                            "Traffic control",
                            "Approach blocked. Ships are seeking another clear route."
                        );
                }
            }
            else
                npc.ProgressAt = Tick;
            var next = fixedBody || npc.FlightGoal is null ? npc.Position : Navigate(npc, goal);
            var offset = next - npc.Position;
            var distance = offset.Length();
            var final = Vector2.DistanceSquared(next, Exterior(goal, EnemyRadius(npc) + 100)) < 4;
            var remaining = MathF.Max(0, distance - (final ? npc.FlightStop : 12));
            // Slow before corners as well as endpoints. Compensate for lateral momentum
            // through engine thrust, never by replacing the ship's velocity.
            var gravity = geometry?.GravityAt(npc.Position) ?? Vector2.Zero;
            var thrust = Movement.AssistedAcceleration(2 * design.Acceleration, gravity);
            var desiredSpeed = MathF.Min(speed, MathF.Sqrt(2 * thrust * remaining));
            var desired = distance > .01f ? offset / distance * desiredSpeed : Vector2.Zero;
            var correction = (desired - npc.Velocity) * 2 - gravity;
            var heading =
                correction.LengthSquared() > .01f
                    ? MathF.Atan2(correction.Y, correction.X)
                    : npc.Heading;
            var angle = MathF.Abs(MathF.IEEERemainder(heading - npc.Heading, MathF.Tau));
            var braking = remaining < 2 || npc.FlightGoal is null;
            var targetThrottle = angle < .7f ? Math.Clamp(correction.Length() / thrust, 0, 1) : 0;
            var controls = new Vector2(
                0,
                Math.Clamp((npc.Throttle - targetThrottle) * TickRate * 2, -1, 1)
            );
            var before = npc.Position;
            Movement.Steer(
                ref npc.Position,
                ref npc.Velocity,
                ref npc.Heading,
                ref npc.Throttle,
                controls,
                1f / TickRate,
                speed,
                design.Acceleration * 2,
                design.Steering * 2,
                geometry,
                fixedBody,
                !stunned,
                design.TurnRadians * 1.5f,
                EnemyRadius(npc),
                braking,
                heading
            );
            if (!npc.Allied && Sanctuary(npc.Position))
            {
                npc.Position = before;
                npc.Velocity = Vector2.Zero;
                npc.Throttle = 0;
                npc.Target = 0;
                npc.Patrol = (npc.Patrol + 1) % 4;
            }
        }
    }
}
