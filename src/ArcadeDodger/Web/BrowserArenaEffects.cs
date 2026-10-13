using System.Numerics;
using ArcadeDodger.Multiplayer;
using Raylib_cs;

namespace ArcadeDodger.Web;

internal sealed partial class BrowserArenaWorld
{
    private void ObserveCombat(RoomSnapshot frame, Vector2 origin, float time)
    {
        foreach (var e in frame.CombatEvents ?? [])
        {
            if (e.Id <= lastCombatEvent)
                continue;
            lastCombatEvent = e.Id;
            if (!shotTransitions || e.Kind == 0)
                continue;
            Vector2 position = new(e.X, e.Y);
            if (!Visible(position, origin, 160))
                continue;
            // Reserve room for destruction; cosmetic trails/muzzles must not crowd it out.
            if (effects.Count >= EffectCapacity)
            {
                var disposable = effects.FindIndex(f =>
                    f.Kind is EffectKind.Trail or EffectKind.Muzzle
                );
                if (disposable < 0)
                    continue;
                effects.RemoveAt(disposable);
            }
            var death = e.Kind == 2;
            effects.Add(
                new(
                    position,
                    new(e.Dx, e.Dy),
                    death ? new Color(255, 145, 60, 255) : Gold,
                    time,
                    death ? .85f : .24f,
                    death ? EffectKind.Death : EffectKind.Hit,
                    e.Id,
                    death ? Math.Clamp(e.Size / 20, .7f, 3) : Math.Clamp(MathF.Sqrt(e.Size), .7f, 2)
                )
            );
        }
    }

    // Small world-only impulses. HUD stays still; aim uses the same camera offset.
    public Vector2 CameraKick(float time, bool reduced)
    {
        if (reduced)
            return Vector2.Zero;
        float strength = 0;
        foreach (var e in effects)
        {
            var life = Math.Clamp(1 - (time - e.Born) / e.Duration, 0, 1);
            strength = Math.Max(
                strength,
                (
                    e.Kind == EffectKind.Death ? 3
                    : e.Kind == EffectKind.Muzzle ? .3f
                    : 0
                )
                    * life
                    * life
            );
        }
        return new(MathF.Sin(time * 83) * strength, MathF.Cos(time * 107) * strength * .7f);
    }

    private void DrawEffects(Vector2 origin, float time, bool low)
    {
        foreach (var e in effects)
        {
            var age = Math.Max(0, time - e.Born);
            var progress = Math.Clamp(age / e.Duration, 0, 1);
            var alpha = 1 - progress;
            var color = Raylib.Fade(e.Color, alpha);
            var size = e.Scale;
            if (!Visible(e.Position, origin, 180))
                continue;
            if (e.Kind == EffectKind.Trail)
            {
                if (!low)
                    Raylib.DrawRectangleLinesEx(
                        new(e.Position.X - 14, e.Position.Y - 14, 28, 28),
                        1,
                        Raylib.Fade(color, .25f)
                    );
                continue;
            }
            if (e.Kind == EffectKind.Muzzle)
            {
                if (low)
                    continue;
                var side = new Vector2(-e.Direction.Y, e.Direction.X);
                Raylib.DrawCircleV(e.Position, 18 * alpha, Raylib.Fade(color, .12f));
                Raylib.DrawTriangle(
                    e.Position + side * 5 * alpha,
                    e.Position - side * 5 * alpha,
                    e.Position + e.Direction * 24 * alpha,
                    color
                );
                Raylib.DrawLineEx(
                    e.Position - side * 8 * alpha,
                    e.Position + side * 8 * alpha,
                    2,
                    Raylib.Fade(NeonRendering.Ink, alpha)
                );
                Raylib.DrawCircleV(e.Position, 4 * alpha, Raylib.Fade(NeonRendering.Ink, alpha));
                continue;
            }
            var death = e.Kind == EffectKind.Death;
            if (e.Kind == EffectKind.Hit && e.Direction.LengthSquared() > .1f)
            {
                Raylib.DrawLineEx(
                    e.Position,
                    e.Position + e.Direction * (12 + 40 * progress),
                    low ? 1 : 2,
                    color
                );
                if (!low)
                {
                    var side = new Vector2(-e.Direction.Y, e.Direction.X);
                    for (var i = -1; i <= 1; i += 2)
                        Raylib.DrawLineEx(
                            e.Position + e.Direction * 4,
                            e.Position + e.Direction * (14 + age * 80) + side * i * 14 * progress,
                            1,
                            color
                        );
                }
            }
            var radius = (death ? 8 + 70 * progress : 3 + 24 * progress) * size;
            Raylib.DrawCircleLines((int)e.Position.X, (int)e.Position.Y, radius, color);
            if (!low)
            {
                // Layered fireball, short white flash, and a fading shockwave.
                Raylib.DrawCircleGradient(
                    e.Position,
                    (death ? 48 : 12) * size * alpha,
                    Raylib.Fade(e.Color, alpha * .7f),
                    Raylib.Fade(e.Color, 0)
                );
                if (death)
                    for (int i = 0; i < 5; i++)
                    {
                        var angle = e.Seed + i * 2.399963f;
                        var puff =
                            e.Position
                            + new Vector2(MathF.Cos(angle), MathF.Sin(angle))
                                * progress
                                * 28
                                * size;
                        Raylib.DrawCircleGradient(
                            puff,
                            (12 + i % 3 * 4) * size * alpha,
                            Raylib.Fade(Gold, alpha * .8f),
                            Raylib.Fade(e.Color, 0)
                        );
                    }
                Raylib.DrawCircleGradient(
                    e.Position,
                    (death ? 20 : 5) * size * alpha,
                    Raylib.Fade(NeonRendering.Ink, alpha),
                    Raylib.Fade(Gold, 0)
                );
                Raylib.DrawCircleV(
                    e.Position,
                    (death ? 13 : 4) * size * MathF.Max(0, 1 - progress * 4),
                    NeonRendering.Ink
                );
                if (death)
                    Raylib.DrawCircleLines(
                        (int)e.Position.X,
                        (int)e.Position.Y,
                        radius * 1.35f,
                        Raylib.Fade(Gold, alpha * .3f)
                    );
            }
            int count = low ? (death ? 5 : 2) : (death ? 22 : 7);
            for (int i = 0; i < count; i++)
            {
                var angle = i * 2.399963f + e.Seed * .37f;
                Vector2 direction = new(MathF.Cos(angle), MathF.Sin(angle));
                var travel =
                    (1 - MathF.Exp(-age * 3)) * (death ? 50 + i % 5 * 13 : 18 + i % 3 * 12) * size;
                var point = e.Position + direction * travel;
                Raylib.DrawLineEx(
                    point,
                    point - direction * (death ? 8 : 4) * alpha,
                    death ? 2.5f : 1.5f,
                    color
                );
                if (death && !low && i % 4 == 0)
                {
                    Raylib.DrawPoly(
                        point,
                        3,
                        3 * alpha * size,
                        e.Seed + age * 240,
                        new Color(115, 100, 105, (int)(alpha * 255))
                    );
                    Raylib.DrawCircleV(
                        point - direction * 5,
                        5 * size * progress,
                        Raylib.Fade(e.Color, alpha * .08f)
                    );
                }
            }
        }
    }
}
