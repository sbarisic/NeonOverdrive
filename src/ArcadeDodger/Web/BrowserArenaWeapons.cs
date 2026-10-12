using System.Numerics;
using ArcadeDodger.Multiplayer;
using Raylib_cs;

namespace ArcadeDodger.Web;

internal sealed partial class BrowserArenaWorld
{
    private void DrawWeapons(RoomSnapshot snapshot, Vector2 origin, float time, bool reducedEffects)
    {
        foreach (var shot in snapshot.Shots)
        {
            Vector2 p = new(shot.X, shot.Y);
            if (!Visible(p, origin, 35))
                continue;
            var size = Math.Clamp(MathF.Sqrt(shot.Power), .65f, 4);
            if (size > 1 && shot.Kind != 1)
                Raylib.DrawCircleV(p, 5 * size, Raylib.Fade(ProjectileColor(shot), .5f));
            Color color = shot.Owner < 0 ? Hostile : ProjectileColor(shot);
            if (shot.Owner < 0)
            {
                // Alien rounds have a solid circular silhouette, unlike pilot tracer rounds.
                Raylib.DrawCircleV(p, 7, new Color(30, 0, 28, 255));
                Raylib.DrawCircleV(p, 5, Hostile);
                Raylib.DrawCircleV(p, 2, NeonRendering.Ink);
            }
            else if (shot.Kind == 2)
            {
                var direction = Direction(new(shot.Dx, shot.Dy));
                Raylib.DrawLineEx(
                    p - direction * 42,
                    p,
                    reducedEffects ? 3 : 7,
                    Raylib.Fade(color, .35f)
                );
                Raylib.DrawLineEx(p - direction * 38, p, 2, color);
            }
            else if (shot.Kind == 3)
            {
                var direction = Direction(new(shot.Dx, shot.Dy));
                Raylib.DrawLineEx(p - direction * 22, p, 5, Gold);
                if (!reducedEffects)
                    for (int i = 1; i <= 4; i++)
                    {
                        var puff = p - direction * (9 + i * 8);
                        Raylib.DrawCircleV(puff, 2 + i, Raylib.Fade(color, (5 - i) * .055f));
                    }
                Raylib.DrawPoly(p, 3, 7, MathF.Atan2(shot.Dy, shot.Dx) * 180 / MathF.PI, color);
            }
            else if (shot.Kind == 4)
            {
                Raylib.DrawCircleV(p, 11, Raylib.Fade(color, .25f));
                Raylib.DrawCircleV(p, 6, color);
                Raylib.DrawCircleV(p, 2, NeonRendering.Ink);
                if (!reducedEffects)
                {
                    var orbit =
                        new Vector2(MathF.Cos(time * 18 + shot.Id), MathF.Sin(time * 18 + shot.Id))
                        * 9;
                    Raylib.DrawCircleV(p + orbit, 2, color);
                    Raylib.DrawCircleV(p - orbit, 2, color);
                }
            }
            else
            {
                Vector2 d = Direction(new(shot.Dx, shot.Dy));
                if (!reducedEffects)
                    Raylib.DrawCircleV(p, 3 * size, Raylib.Fade(color, .18f));
                Raylib.DrawLineEx(p - d * 6, p, 1.5f * size, color);
                Raylib.DrawCircleV(p, 1.5f * size, NeonRendering.Ink);
            }
        }
        foreach (var beam in snapshot.Beams ?? [])
        {
            var owner = players.GetValueOrDefault(beam.Owner);
            if (owner is null || owner.Hull <= 0 || owner.Editing)
                continue;
            Vector2 start = new(beam.X, beam.Y),
                end = new(beam.EndX, beam.EndY);
            if (!SegmentVisible(start, end, origin))
                continue;
            var color = ProjectileColor(
                new(0, beam.Owner, beam.X, beam.Y, Kind: 2, Effects: beam.Effects)
            );
            var thickness = Math.Clamp(MathF.Sqrt(beam.Power), .5f, 5);
            if (!reducedEffects)
                Raylib.DrawLineEx(start, end, 11 * thickness, Raylib.Fade(color, .15f));
            Raylib.DrawLineEx(
                start,
                end,
                (reducedEffects ? 3 : 5) * thickness,
                Raylib.Fade(color, .8f)
            );
            Raylib.DrawLineEx(start, end, 1.5f * thickness, NeonRendering.Ink);
            Raylib.DrawCircleV(end, reducedEffects ? 3 : 4 + MathF.Sin(time * 31) * 1.2f, color);
        }
    }
}
