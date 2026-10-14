using System.Numerics;

namespace ArcadeDodger.Multiplayer;

// Bounded, short-lived presentation events. Kind: 0 endpoint, 1 impact, 2 destruction, 3 piercing contact, 5 completed service.
public sealed record CombatEvent(
    int Id,
    int Shot,
    int Kind,
    float X,
    float Y,
    long Born,
    float Size,
    float Dx = 0,
    float Dy = 0,
    int Chassis = -1,
    float Heading = 0,
    float Scale = 1,
    string Faction = "",
    float VelocityX = 0,
    float VelocityY = 0,
    int WeaponKind = 0,
    string Material = ""
);

public sealed partial class Room
{
    private readonly Queue<CombatEvent> combatEvents = new();
    private int nextCombatEvent;

    private void CombatFx(
        int shot,
        int kind,
        Vector2 position,
        float size,
        Vector2 direction = default,
        int chassis = -1,
        float heading = 0,
        float scale = 1,
        string faction = "",
        Vector2 velocity = default,
        int weaponKind = 0,
        string material = ""
    )
    {
        while (
            combatEvents.TryPeek(out var first)
            && (Tick - first.Born > 8 || combatEvents.Count >= 512)
        )
            combatEvents.Dequeue();
        combatEvents.Enqueue(
            new(
                ++nextCombatEvent,
                shot,
                kind,
                position.X,
                position.Y,
                Tick,
                size,
                direction.X,
                direction.Y,
                chassis,
                heading,
                scale,
                faction,
                velocity.X,
                velocity.Y,
                weaponKind,
                material
            )
        );
    }
}
