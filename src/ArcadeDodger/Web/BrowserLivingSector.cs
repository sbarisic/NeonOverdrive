using System.Numerics;
using ArcadeDodger.Multiplayer;
using Raylib_cs;

namespace ArcadeDodger.Web;

internal sealed partial class BrowserArenaWorld
{
    private void DrawLivingSector(RoomSnapshot snapshot, Vector2 origin, int ownId)
    {
        if (snapshot.Sector?.World is not { } world)
            return;
        foreach (var cargo in world.Cargo)
        {
            var point = new Vector2(cargo.X, cargo.Y);
            if (!Visible(point, origin, 100))
                continue;
            if (cargo.Kind is "derelict" or "disabled-ship")
                DrawChassis(
                    cargo.Kind == "derelict" ? 6 : 3,
                    point,
                    .4f,
                    .4f,
                    0,
                    0,
                    new Color(125, 140, 155, 255),
                    true
                );
            else
            {
                Raylib.DrawPolyLinesEx(point, cargo.Kind == "anomaly" ? 6 : 4, 20, 45, 2, Gold);
                Raylib.DrawCircleV(point, 4, Cyan);
            }
            BrowserText.DrawText(
                cargo.Kind.ToUpperInvariant(),
                (int)point.X - 35,
                (int)point.Y - 40,
                10,
                Gold
            );
            if (snapshot.Players.FirstOrDefault(p => p.Id == cargo.TowOwner) is { } owner)
                Raylib.DrawLineEx(new(owner.X, owner.Y), point, 2, Cyan);
        }
        if (world.Race is not { } race || race.State is not ("racing" or "countdown"))
            return;
        for (var i = Math.Max(0, race.Checkpoint - 1); i < race.Course.Length / 2; i++)
        {
            var point = new Vector2(race.Course[i * 2], race.Course[i * 2 + 1]);
            if (!Visible(point, origin, 120))
                continue;
            Raylib.DrawCircleLines(
                (int)point.X,
                (int)point.Y,
                100,
                i == race.Checkpoint ? Gold : Raylib.Fade(Cyan, .3f)
            );
            BrowserText.DrawText((i + 1).ToString(), (int)point.X, (int)point.Y, 16, Gold);
        }
        for (var i = 0; i + 2 < race.Ghost.Length; i += 3)
            if (MathF.Abs(race.Ghost[i] - race.Seconds) < .25f)
            {
                var angle =
                    i + 5 < race.Ghost.Length
                        ? MathF.Atan2(
                            race.Ghost[i + 5] - race.Ghost[i + 2],
                            race.Ghost[i + 4] - race.Ghost[i + 1]
                        )
                        : 0;
                DrawChassis(
                    snapshot.Players.FirstOrDefault(p => p.Id == ownId)?.Chassis ?? 0,
                    new(race.Ghost[i + 1], race.Ghost[i + 2]),
                    angle,
                    angle,
                    0,
                    0,
                    Raylib.Fade(Cyan, .3f),
                    true
                );
                break;
            }
    }
}
