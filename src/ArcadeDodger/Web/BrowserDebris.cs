using System.Numerics;
using ArcadeDodger.Multiplayer;
using Raylib_cs;

namespace ArcadeDodger.Web;

internal sealed partial class BrowserArenaWorld
{
    private sealed record Fragment(
        HullPanel Panel,
        Vector2 Position,
        Vector2 Velocity,
        float Heading,
        float Spin,
        float Scale,
        Color Color,
        float Born,
        float Lifetime
    );

    private readonly List<Fragment> fragments = new(192);

    private void Breakup(CombatEvent e, float time, bool low)
    {
        if (!ChassisCatalog.Valid(e.Chassis))
            return;
        var panels = ChassisCatalog.Get(e.Chassis).Panels;
        var random = new Random(e.Id);
        var limit = low ? 64 : 192;
        foreach (var panel in panels.Take(low ? 8 : 24))
        {
            while (fragments.Count >= limit)
                fragments.RemoveAt(0);
            var center =
                panel.Vertices.Aggregate(Vector2.Zero, (v, p) => v + p) / panel.Vertices.Length;
            var offset = ChassisCatalog.Rotate(center * e.Scale, e.Heading);
            var angle = MathF.Atan2(offset.Y, offset.X) + (random.NextSingle() - .5f) * .8f;
            var velocity =
                new Vector2(e.VelocityX, e.VelocityY) * .35f
                + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (18 + random.NextSingle() * 35);
            fragments.Add(
                new(
                    new(panel.Vertices.Select(p => p - center).ToArray(), panel.Material),
                    new Vector2(e.X, e.Y) + offset,
                    velocity,
                    e.Heading,
                    (random.NextSingle() - .5f) * 5,
                    e.Scale,
                    e.Faction == "hostile" ? Rammer
                        : e.Faction == "neutral" ? Gold
                        : Cyan,
                    time,
                    .6f + random.NextSingle() * .6f
                )
            );
        }
    }

    private void DrawDebris(float time, bool low)
    {
        fragments.RemoveAll(f => time - f.Born >= f.Lifetime);
        var limit = low ? 64 : 192;
        if (fragments.Count > limit)
            fragments.RemoveRange(0, fragments.Count - limit);
        foreach (var f in fragments)
        {
            var age = time - f.Born;
            DrawPanels(
                [f.Panel],
                f.Position + f.Velocity * age,
                f.Heading + f.Spin * age,
                Raylib.Fade(f.Color, 1 - age / f.Lifetime),
                f.Scale
            );
        }
    }
}
