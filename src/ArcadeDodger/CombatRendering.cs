using System.Numerics;
using Raylib_cs;

namespace ArcadeDodger;

public sealed partial class GameApp
{
    private void AdvanceCombatEffects(float dt, GameState previous)
    {
        muzzle = Math.Max(0, muzzle - dt);
        shakeLife = Math.Max(0, shakeLife - dt);
        if (previous == GameState.Playing)
            foreach (Vector2 position in model.Combat.Explosions)
            {
                if (rings.Count < 80) rings.Add(new Spark(position, Vector2.Zero, 0.6f));
                shakeLife = 0.15f;
                for (int i = 0; i < (reducedEffects ? 5 : 24) && sparks.Count < 800; i++)
                {
                    float a = effectsRandom.NextSingle() * MathF.Tau;
                    sparks.Add(new Spark(position, new Vector2(MathF.Cos(a), MathF.Sin(a)) *
                        (60 + effectsRandom.NextSingle() * 170), 0.7f));
                }
            }
        model.Combat.Explosions.Clear();
        shake = !reducedEffects && shakeLife > 0 ? new Vector2(MathF.Sin(clock * 113), MathF.Cos(clock * 97)) * 3 : Vector2.Zero;
        for (int i = rings.Count - 1; i >= 0; i--)
            if (rings[i].Life <= dt) rings.RemoveAt(i);
            else rings[i] = rings[i] with { Life = rings[i].Life - dt };
    }

    private void DrawCombat()
    {
        Color gold = new(255, 210, 80, 255);
        foreach (var shot in model.Combat.Projectiles)
        {
            if (!ArenaView.Visible(shot.Position, cameraOrigin)) continue;
            NeonRendering.DrawProjectile(shot.Position, shot.Velocity, gold);
        }
        foreach (var pickup in model.Combat.Pickups)
        {
            if (!ArenaView.Visible(pickup.Position, cameraOrigin)) continue;
            bool weapon = pickup.Kind is PickupKind.Blaster or PickupKind.Spread or PickupKind.Rapid;
            Color color = weapon ? gold : PlayerColor;
            Vector2 p = pickup.Position;
            float radius = 17 + 3 * MathF.Sin(clock * 5);
            Raylib.DrawCircleV(p, radius + 5, Raylib.Fade(color, 0.1f));
            Raylib.DrawCircleLines((int)p.X, (int)p.Y, radius, color);
            Raylib.DrawRectangleLinesEx(new Rectangle(p.X - 10, p.Y - 10, 20, 20), 2, color);
            string label = pickup.Kind.ToString().ToUpperInvariant();
            Raylib.DrawText(label, (int)p.X - Raylib.MeasureText(label, 12) / 2, (int)p.Y - 32, 12, color);
            Raylib.DrawText(weapon ? "W" : "+", (int)p.X - 5, (int)p.Y - 7, 16, Ink);
        }
        foreach (Spark ring in rings)
            Raylib.DrawCircleLines((int)ring.Position.X, (int)ring.Position.Y,
                12 + (0.6f - ring.Life) * 140, Raylib.Fade(gold, ring.Life / 0.6f));
        Vector2 center = new(model.Player.X + 14, model.Player.Y + 14);
        Vector2 direction = aim - center;
        direction = direction.LengthSquared() < 0.001f ? -Vector2.UnitY : Vector2.Normalize(direction);
        Raylib.DrawLineEx(center, center + direction * 23, 5, Ink);
        if (muzzle > 0 && !reducedEffects)
        {
            Vector2 tip = center + direction * 25;
            Raylib.DrawCircleV(tip, 14, Raylib.Fade(gold, 0.2f));
            Raylib.DrawCircleV(tip, 6, Ink);
        }
        if (model.State == GameState.Playing)
        {
            NeonRendering.DrawReticle(aim, PlayerColor);
        }
    }

    private void DrawLoadout()
    {
        Raylib.DrawText("CURRENT WEAPON", 26, 493, 10, ConsoleMuted);
        Raylib.DrawText(model.Combat.Weapon.ToString().ToUpperInvariant(), 26, 506, 16, ConsoleAmber);
        Raylib.DrawLine(141, 490, 141, 518, ConsoleBorder);
        DrawModifierRow("MULTI", model.Combat.Multishot, 490);
        DrawModifierRow("PIERCE", model.Combat.Pierce, 504);
        DrawModifierRow("OVERDRIVE", model.Combat.Overdrive, 518);
        Raylib.DrawLine(349, 490, 349, 518, ConsoleBorder);
        // Collection totals are distinct from active levels; compact weapon totals
        // sit under the weapon, modifier totals beside their respective meters.
        var collected = model.Combat.CollectedPickups;
        Raylib.DrawText($"B:{collected[PickupKind.Blaster]} S:{collected[PickupKind.Spread]} R:{collected[PickupKind.Rapid]}", 26, 521, 10, ConsoleMuted);
        Raylib.DrawText($"x{collected[PickupKind.Multishot]}", 312, 491, 8, ConsoleMuted);
        Raylib.DrawText($"x{collected[PickupKind.Pierce]}", 312, 505, 8, ConsoleMuted);
        Raylib.DrawText($"x{collected[PickupKind.Overdrive]}", 312, 519, 8, ConsoleMuted);
        Raylib.DrawText("P pause   ESC exit", 742, 504, 10, ConsoleMuted);
        string effects = $"F FX: {(reducedEffects ? "LOW" : "FULL")}";
        Raylib.DrawText(effects, 932 - Raylib.MeasureText(effects, 10), 504, 10, ConsoleCyan);
        Raylib.DrawText("WASD move  MOUSE aim  LMB fire", 730, 519, 10, ConsoleMuted);
    }

    private static void DrawModifierRow(string label, int level, int y)
    {
        Raylib.DrawText(label, 163, y, 10, ConsoleCyan);
        for (int i = 0; i < 3; i++)
        {
            Raylib.DrawRectangle(236 + i * 9, y + 2, 6, 6, i < level ? ConsoleCyan : ConsoleBorder);
        }
        string value = $"{level}/3";
        Raylib.DrawText(value, 280, y, 10, Ink);
    }
}
