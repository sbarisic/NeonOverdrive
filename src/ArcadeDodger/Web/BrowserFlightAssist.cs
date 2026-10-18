using System.Numerics;
using System.Text.Json;
using ArcadeDodger.Multiplayer;
using Raylib_cs;

namespace ArcadeDodger.Web;

internal sealed record FlightAssistInfo(
    int Target,
    int Candidate,
    string Orbit,
    string Lead,
    float? X,
    float? Y,
    string? Release,
    string? Coast,
    int Points
);

internal sealed partial class BrowserArena
{
    private SectorDefinition? assistSector;
    private SectorCollision? assistGeometry;
    private OrbitForecast? releaseOrbit,
        coastOrbit;
    private PlayerSnapshot? orbitSeed;
    private double forecastAt;
    private string assistRun = "";
    private Vector2 aimCursor;
    private bool cursorKnown;
    private float displayWidth = 1280;
    private int leadTarget,
        leadCandidate;
    private double candidateAt;
    private Vector2 previousTargetVelocity;
    private double targetVelocityAt;
    private float leadConfidence = 1;
    private string orbitNotice = "",
        leadNotice = "";
    private Vector2? leadPoint;

    public void DisplayWidth(double width) => displayWidth = (float)Math.Max(1, width);

    // Also used by deterministic browser regressions; contains no hidden-world data.
    public string FlightAssistState() =>
        JsonSerializer.Serialize(
            new FlightAssistInfo(
                leadTarget,
                leadCandidate,
                orbitNotice,
                leadNotice,
                leadPoint?.X,
                leadPoint?.Y,
                releaseOrbit?.Outcome.ToString(),
                coastOrbit?.Outcome.ToString(),
                releaseOrbit?.Points.Count ?? 0
            ),
            SnapshotJson.Default.FlightAssistInfo
        );

    private void ResetFlightAssist()
    {
        releaseOrbit = coastOrbit = null;
        orbitSeed = null;
        leadTarget = leadCandidate = 0;
        leadPoint = null;
        orbitNotice = leadNotice = "";
    }

    private void DrawFlightAssist(PlayerSnapshot? pilot, double now)
    {
        if (
            pilot is null
            || pilot.Hull <= 0
            || !pilot.Connected
            || menuOpen
            || pilot.Menu
            || pilot.Editing
            || pilot.Docked != 0
            || snapshot is null
        )
        {
            ResetFlightAssist();
            return;
        }
        if (assistRun != pilot.RunId)
        {
            ResetFlightAssist();
            assistRun = pilot.RunId;
        }
        DrawOrbitForecast(pilot, now);
        DrawLead(pilot, now);
    }

    private void DrawOrbitForecast(PlayerSnapshot pilot, double now)
    {
        orbitNotice = "";
        if (
            assistSector is null
            || assistGeometry is null
            || pilot.Braking
            || pilot.CruiseState is "active" or "spooling"
        )
            return;
        var at = new Vector2(pilot.X, pilot.Y);
        if (
            !assistSector.Locations.Any(l =>
                l.Solid && Vector2.Distance(at, l.Position) < l.Radius + assistSector.GravityRange
            )
        )
        {
            releaseOrbit = coastOrbit = null;
            return;
        }
        var changed =
            orbitSeed is null
            || MathF.Abs(pilot.Throttle - orbitSeed.Throttle) > .02f
            || pilot.Throttle > 0
                && MathF.Abs(MathF.IEEERemainder(pilot.Heading - orbitSeed.Heading, MathF.Tau))
                    > .1f
            || Vector2.Distance(at, new(orbitSeed.X, orbitSeed.Y)) > 100
            || Vector2.Distance(
                new(pilot.VelocityX, pilot.VelocityY),
                new(orbitSeed.VelocityX, orbitSeed.VelocityY)
            ) > 20;
        if (releaseOrbit is null || changed || now - forecastAt > .8)
        {
            orbitSeed = pilot;
            releaseOrbit = new(assistSector, assistGeometry, pilot, pilot.Throttle);
            coastOrbit =
                pilot.Throttle == 0 ? releaseOrbit : new(assistSector, assistGeometry, pilot, 0);
            forecastAt = now;
        }
        releaseOrbit.Advance(120);
        if (coastOrbit != releaseOrbit)
            coastOrbit!.Advance(120);
        var stable = coastOrbit!.Outcome == OrbitOutcome.Orbit;
        var color =
            releaseOrbit.Outcome == OrbitOutcome.Collision ? new Color(255, 95, 95, 210)
            : stable && pilot.Throttle == 0 ? new Color(80, 245, 130, 220)
            : new Color(65, 210, 235, 180);
        DrawForecastPath(releaseOrbit, color, false);
        if (stable && pilot.Throttle > 0)
            DrawForecastPath(coastOrbit, new Color(80, 245, 130, 200), true);
        orbitNotice = stable
            ? pilot.Throttle == 0
                ? "COAST ORBIT PREDICTED - RELEASE W"
                : "ORBIT READY - SET THROTTLE TO 0"
            : releaseOrbit.Outcome == OrbitOutcome.Collision
                ? "RELEASE PATH: COLLISION"
                : releaseOrbit.Outcome == OrbitOutcome.Pending
                    ? "RELEASE PATH: CALCULATING"
                    : "RELEASE PATH: NO COAST ORBIT";
        BrowserText.DrawText(orbitNotice, (int)at.X - 110, (int)at.Y + 65, 10, color);
    }

    private static void DrawForecastPath(OrbitForecast forecast, Color color, bool dashed)
    {
        for (var i = 1; i < forecast.Points.Count; i++)
            if (!dashed || i % 2 == 0)
                Raylib.DrawLineEx(forecast.Points[i - 1], forecast.Points[i], 1.5f, color);
        if (forecast.Impact is { } hit)
        {
            Raylib.DrawLineEx(hit - new Vector2(6), hit + new Vector2(6), 2, color);
            Raylib.DrawLineEx(hit + new Vector2(-6, 6), hit + new Vector2(6, -6), 2, color);
        }
    }

    private void DrawLead(PlayerSnapshot pilot, double now)
    {
        var previousLead = leadPoint;
        leadNotice = "";
        leadPoint = null;
        if (!cursorKnown || snapshot is null)
            return;
        var at = new Vector2(pilot.X, pilot.Y);
        var candidates = (snapshot.Enemies ?? [])
            .Where(e =>
                e.Hull > 0
                && e.Faction == "hostile"
                && Vector2.DistanceSquared(at, new(e.X, e.Y))
                    <= pilot.DetectionRadius * pilot.DetectionRadius
            )
            .ToArray();
        var target = candidates.FirstOrDefault(e => e.Id == leadTarget);
        if (target is null)
            leadTarget = 0;
        var cursor = origin - cameraKick + aimCursor * view;
        var margin = 18 * view.X / displayWidth;
        var near = candidates
            .Where(e =>
                Vector2.Distance(cursor, new(e.X, e.Y)) <= e.Radius + margin
                && e.X >= origin.X
                && e.X <= origin.X + view.X
                && e.Y >= origin.Y
                && e.Y <= origin.Y + view.Y
            )
            .MinBy(e => Vector2.DistanceSquared(cursor, new(e.X, e.Y)));
        if (
            target is not null
            && previousLead is { } reticle
            && Vector2.Distance(cursor, reticle) < 12 * view.X / displayWidth
        )
            near = target;
        if (near is not null && near.Id != leadTarget)
        {
            if (leadCandidate != near.Id)
            {
                leadCandidate = near.Id;
                candidateAt = now;
            }
            if (now - candidateAt >= .12)
            {
                leadTarget = near.Id;
                target = near;
                previousTargetVelocity = new(near.VelocityX, near.VelocityY);
                targetVelocityAt = now;
                leadConfidence = 1;
                leadCandidate = 0;
            }
        }
        else
            leadCandidate = 0;
        if (target is null)
            return;
        var velocity = new Vector2(target.VelocityX, target.VelocityY);
        if (now - targetVelocityAt >= .12)
        {
            leadConfidence =
                Vector2.Distance(velocity, previousTargetVelocity) > 8
                    ? .35f
                    : MathF.Min(1, leadConfidence + .15f);
            previousTargetVelocity = velocity;
            targetVelocityAt = now;
        }
        var targetAt = new Vector2(target.X, target.Y);
        var marker = Raylib.Fade(new Color(255, 210, 80, 255), leadConfidence);
        Raylib.DrawCircleLines((int)target.X, (int)target.Y, target.Radius + 7, marker);
        if (pilot.Weapons.IsDefaultOrEmpty)
            return;
        var bay = Enumerable
            .Range(0, Math.Min(4, pilot.Weapons.Length))
            .FirstOrDefault(
                i => pilot.Weapons[i].Enabled && (pilot.ActiveBays & (1 << i)) != 0,
                -1
            );
        if (bay < 0)
            return;
        var muzzle = ChassisCatalog.Hardpoint(pilot.Chassis, bay, at, pilot.Heading);
        var weapon = pilot.Weapons[bay];
        Vector2 point;
        if (weapon.Emitter == 2)
        {
            point = targetAt;
            var range = WeaponLead.BeamRange(pilot, bay);
            if (Vector2.Distance(muzzle, point) > range)
                return;
            leadNotice = $"BAY {bay + 1} - BEAM";
        }
        else
        {
            if (
                !WeaponLead.TrySolve(
                    pilot,
                    bay,
                    targetAt,
                    velocity,
                    out point,
                    out _,
                    out var limited
                )
            )
            {
                leadNotice = limited
                    ? $"BAY {bay + 1} - COMPLEX PATH"
                    : $"BAY {bay + 1} - OUT OF RANGE";
                BrowserText.DrawText(
                    leadNotice,
                    (int)target.X + 15,
                    (int)target.Y - 25,
                    10,
                    marker
                );
                return;
            }
            leadNotice = $"BAY {bay + 1} - LEAD";
        }
        if (assistGeometry?.Trace(muzzle, point, weapon.Emitter == 1 ? 1.5f : 3) is not null)
        {
            leadNotice = $"BAY {bay + 1} - BLOCKED";
            return;
        }
        // Mouse aim is measured from the hull centre, but shots start at a hardpoint.
        // Shift the cursor reticle so aiming at it produces the solved muzzle direction.
        var aimPoint = at + point - muzzle;
        leadPoint = aimPoint;
        Raylib.DrawLineEx(targetAt, point, 1, Raylib.Fade(marker, .3f));
        Raylib.DrawLineEx(point, aimPoint, 1, Raylib.Fade(marker, .3f));
        Raylib.DrawCircleLines((int)aimPoint.X, (int)aimPoint.Y, 8, marker);
        BrowserText.DrawText(leadNotice, (int)aimPoint.X + 12, (int)aimPoint.Y - 10, 10, marker);
    }
}
