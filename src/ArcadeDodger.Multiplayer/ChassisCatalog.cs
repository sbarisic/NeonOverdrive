using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public enum NpcFaction
{
    Friendly,
    Neutral,
    Hostile,
}

public enum NpcRole
{
    Combat,
    Support,
    Trader,
    Miner,
    Scavenger,
    Patrol,
    Mercenary,
    Courier,
    Pirate,
    Police,
}

public sealed record HullPanel(Vector2[] Vertices, string Material);

public sealed record ChassisDefinition(
    int Id,
    string Name,
    float Length,
    float Width,
    int Hull,
    int Shield,
    float Armor,
    float Speed,
    float TurnRate,
    int Price,
    int Station,
    HullPanel[] Panels,
    Vector2[] Hardpoints
)
{
    public float Acceleration => 110 * Speed / 220;
    public float Steering => 165 * Speed / 220;
    public float Radius => MathF.Sqrt(Length * Length + Width * Width) / 2;
    public float TurnRadians => TurnRate * MathF.PI / 180;
}

/// <summary>Authored geometry and gameplay ratings shared by server, prediction and rendering.</summary>
public static class ChassisCatalog
{
    public static readonly ChassisDefinition[] Items =
    [
        Create(0, "Starter Skiff", 28, 14, 3, 2, 0, 220, 120, 0, 10),
        Create(1, "Needle Scout", 32, 16, 3, 2, 0, 260, 150, 150, 10),
        Create(2, "Kestrel Fighter", 40, 22, 5, 3, .05f, 230, 130, 300, 10),
        Create(3, "Mule Freighter", 52, 30, 8, 4, .10f, 170, 80, 400, 11),
        Create(4, "Warden Corvette", 56, 32, 9, 5, .10f, 195, 95, 550, 11),
        Create(5, "Aegis Frigate", 72, 40, 12, 7, .15f, 165, 70, 800, 12),
        Create(6, "Bastion Cruiser", 88, 48, 16, 10, .20f, 140, 55, 1200, 12),
    ];

    public static bool Valid(int id) => id >= 0 && id < Items.Length;

    public static ChassisDefinition Get(int id) => Items[Valid(id) ? id : 0];

    public static Vector2 Rotate(Vector2 local, float angle) =>
        new(
            local.X * MathF.Cos(angle) - local.Y * MathF.Sin(angle),
            local.X * MathF.Sin(angle) + local.Y * MathF.Cos(angle)
        );

    public static Vector2 Hardpoint(int chassis, int bay, Vector2 position, float heading) =>
        position + Rotate(Get(chassis).Hardpoints[Math.Clamp(bay, 0, 3)], heading);

    private static ChassisDefinition Create(
        int id,
        string name,
        float length,
        float width,
        int hull,
        int shield,
        float armor,
        float speed,
        float turn,
        int price,
        int station
    )
    {
        var x = length / 2;
        var y = width / 2;
        HullPanel Box(float left, float top, float right, float bottom, string material) =>
            new([new(left, top), new(right, top), new(right, bottom), new(left, bottom)], material);
        var panels = new List<HullPanel>
        {
            new(
                [
                    new(-x, -y * .48f),
                    new(x * .48f, -y * .48f),
                    new(x, 0),
                    new(x * .48f, y * .48f),
                    new(-x, y * .48f),
                ],
                "hull"
            ),
            Box(-x, -y * .34f, -x * .66f, y * .34f, "engine"),
            new([new(x * .1f, -y * .3f), new(x * .6f, 0), new(x * .1f, y * .3f)], "bridge"),
        };
        if (id is 1 or 2 or 4)
            foreach (var sign in new[] { -1, 1 })
                panels.Add(
                    new(
                        [
                            new(-x * .75f, sign * y * .3f),
                            new(-x * .7f, sign * y),
                            new(x * (id == 1 ? .7f : .2f), sign * y * .4f),
                        ],
                        "armor"
                    )
                );
        if (id >= 3)
            foreach (var sign in new[] { -1, 1 })
            {
                panels.Add(
                    Box(
                        -x * .8f,
                        sign * y * .72f - y * .28f,
                        x * (id == 3 ? .5f : .72f),
                        sign * y * .72f + y * .28f,
                        "armor"
                    )
                );
                panels.Add(
                    Box(
                        -x,
                        sign * y * .72f - y * .16f,
                        -x * .7f,
                        sign * y * .72f + y * .16f,
                        "engine"
                    )
                );
            }
        if (id >= 5)
            panels.Add(Box(-x * .4f, -y * .3f, 0, y * .3f, "bridge"));
        if (id == 5)
            foreach (var sign in new[] { -1, 1 })
                panels.Add(
                    new(
                        [
                            new(-x, sign * y),
                            new(x * .45f, sign * y * .62f),
                            new(x * .85f, sign * y * .24f),
                            new(-x * .45f, sign * y * .34f),
                        ],
                        "armor"
                    )
                );
        if (id == 6)
            foreach (var sign in new[] { -1, 1 })
            {
                panels.Add(
                    new(
                        [
                            new(x * .2f, sign * y * .42f),
                            new(x, sign * y * .48f),
                            new(x * .8f, sign * y),
                            new(x * .2f, sign * y),
                        ],
                        "armor"
                    )
                );
                panels.Add(
                    Box(
                        -x * .55f,
                        sign * y * .72f - y * .1f,
                        x * .2f,
                        sign * y * .72f + y * .1f,
                        "bridge"
                    )
                );
            }
        return new(
            id,
            name,
            length,
            width,
            hull,
            shield,
            armor,
            speed,
            turn,
            price,
            station,
            panels.ToArray(),
            [new(x * .6f, 0), new(x * .1f, -y * .65f), new(x * .1f, y * .65f), new(-x * .45f, 0)]
        );
    }
}
