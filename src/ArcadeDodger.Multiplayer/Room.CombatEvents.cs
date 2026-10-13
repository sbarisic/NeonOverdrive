using System.Numerics;

namespace ArcadeDodger.Multiplayer;

// Bounded, short-lived presentation events. Kind: 0 endpoint, 1 impact, 2 destruction, 3 piercing contact.
public sealed record CombatEvent(
    int Id,
    int Shot,
    int Kind,
    float X,
    float Y,
    long Born,
    float Size,
    float Dx = 0,
    float Dy = 0
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
        Vector2 direction = default
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
                direction.Y
            )
        );
    }
}
