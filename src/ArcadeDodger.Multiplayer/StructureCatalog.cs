using System.Numerics;

namespace ArcadeDodger.Multiplayer;

/// <summary>Authored station and ground machinery using the same panels as ship hulls.</summary>
public static class StructureCatalog
{
    private static HullPanel Box(float x, float y, float width, float height, string material) =>
        new(
            [new(x, y), new(x + width, y), new(x + width, y + height), new(x, y + height)],
            material
        );

    public static readonly HullPanel[] Station =
    [
        Box(-100, -18, 200, 36, "hull"),
        Box(-18, -100, 36, 200, "hull"),
        new(
            [new(-45, -30), new(0, -52), new(45, -30), new(45, 30), new(0, 52), new(-45, 30)],
            "armor"
        ),
        Box(-110, -32, 42, 12, "bridge"),
        Box(-110, 20, 42, 12, "bridge"),
        Box(68, -32, 42, 12, "bridge"),
        Box(68, 20, 42, 12, "bridge"),
        Box(-32, -110, 12, 42, "engine"),
        Box(20, -110, 12, 42, "engine"),
        Box(-32, 68, 12, 42, "engine"),
        Box(20, 68, 12, 42, "engine"),
        Box(-22, -22, 44, 44, "bridge"),
    ];
    public static readonly HullPanel[] Turret =
    [
        Box(-15, -18, 22, 36, "armor"),
        Box(-20, -22, 10, 44, "hull"),
        Box(-7, -10, 18, 20, "bridge"),
        Box(4, -5, 25, 10, "engine"),
    ];
    public static readonly HullPanel[] Rover =
    [
        Box(-15, -21, 20, 42, "hull"),
        Box(-18, -24, 10, 12, "engine"),
        Box(-18, 12, 10, 12, "engine"),
        new([new(-5, -12), new(14, -8), new(19, 0), new(14, 8), new(-5, 12)], "armor"),
        Box(2, -4, 26, 8, "bridge"),
    ];
}
