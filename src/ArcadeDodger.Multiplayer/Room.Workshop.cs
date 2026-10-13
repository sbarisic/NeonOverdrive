using System.Collections.Immutable;
using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public sealed record CreditSnapshot(int Id, float X, float Y, int Value, int Owner = 0);

public sealed partial class Room
{
    private readonly ProfileStore profiles;

    private sealed record Credit(int Id, Vector2 Position, int Value, long Expires, int Owner = 0)
    {
        public Vector2 Previous = Position,
            Velocity;
        public bool Pending;
        public readonly Dictionary<int, (int Amount, bool Lucky)> Claims = [];
    }

    private readonly List<Credit> credits = [];
    private int nextCredit;

    public WorkshopSnapshot Workshop(int id)
    {
        var p = players[id];
        return profiles.Workshop(p.Name, p.InventoryPage, p.WorkshopSequence, p.WorkshopResult);
    }

    public bool Buy(int id, BuyMessage request)
    {
        if (
            !players.TryGetValue(id, out var p)
            || !p.Connected
            || request.Sequence <= p.LastReceived
        )
            return false;
        p.LastReceived = request.Sequence;
        if (p.EconomyPending)
        {
            p.WorkshopSequence = request.Sequence;
            p.WorkshopResult = "busy";
            return false;
        }
        p.WorkshopSequence = request.Sequence;
        p.WorkshopResult =
            p.Hull <= 0 || !(p.Pending?.Editing ?? p.Editing) ? "open_workshop"
            : Sector is not null && p.Docked == 0 && !TraderAvailable(p) ? "not_docked"
            : "pending";
        if (p.WorkshopResult != "pending")
            return false;
        p.EconomyPending = true;
        PersistForRun(
            p,
            () => profiles.Buy(p.Name, request.Module),
            result =>
            {
                if (p.WorkshopSequence == request.Sequence)
                    p.WorkshopResult = result;
                p.EconomyPending = false;
            }
        );
        return p.WorkshopResult is "ok" or "pending";
    }

    public bool Equip(int id, EquipMessage request)
    {
        if (
            !players.TryGetValue(id, out var p)
            || !p.Connected
            || request.Sequence <= p.LastReceived
        )
            return false;
        p.LastReceived = request.Sequence;
        if (p.EconomyPending)
        {
            p.WorkshopSequence = request.Sequence;
            p.WorkshopResult = "busy";
            return false;
        }
        p.WorkshopSequence = request.Sequence;
        p.EquipSequence = request.Sequence;
        if (p.Hull <= 0 || !(p.Pending?.Editing ?? p.Editing))
        {
            p.WorkshopResult = "open_workshop";
            return false;
        }
        if (
            !ProfileStore.ValidLoadout(request.Weapons, profiles.Get(p.Name).Owned)
            || !ModuleCatalog.LevelAllows(request.Weapons, LevelForExperience(p.Experience))
            || request
                .Weapons.Where((w, i) => !Unlocked(p, i) && w.Emitter != p.Weapons[i].Emitter)
                .Any()
        )
        {
            p.WorkshopResult = "level_required";
            p.EquipAccepted = false;
            return false;
        }
        p.WorkshopResult = "pending";
        p.EconomyPending = true;
        PersistForRun(
            p,
            () => profiles.Equip(p.Name, request.Weapons, LevelForExperience(p.Experience)),
            result =>
            {
                if (p.WorkshopSequence == request.Sequence)
                {
                    p.WorkshopResult = result;
                    p.EquipAccepted = result == "ok";
                }
                p.EconomyPending = false;
                if (result == "ok" && p.Hull > 0)
                    p.Weapons = request.Weapons.Select(w => w.Copy()).ToImmutableArray();
            }
        );
        return p.WorkshopResult is "ok" or "pending";
    }

    private void DropCredits(Enemy e)
    {
        if (credits.Count >= 128)
            return;
        var drop = Eject(e, nextCredit + 1);
        credits.Add(
            new(
                ++nextCredit,
                drop.Position,
                (int)
                    MathF.Ceiling(
                        (e.Gunner ? 12 : 5)
                            * e.Level
                            * (Tick < e.MarkUntil ? Definition(28).Value : 1)
                    ),
                Tick + 30 * TickRate
            )
            {
                Velocity = drop.Velocity,
            }
        );
    }

    // Room salt is private. Pickup/player rolls remain identical across failed save retries.
    private readonly uint creditSalt = (uint)Random.Shared.NextInt64(1, uint.MaxValue);

    private float CreditRoll(int pickup, int player)
    {
        var x = creditSalt ^ (uint)pickup * 0x9e3779b9u ^ (uint)player * 0x85ebca6bu;
        x ^= x >> 16;
        x *= 0x7feb352du;
        x ^= x >> 15;
        x *= 0x846ca68bu;
        x ^= x >> 16;
        return (x >> 8) / 16777216f;
    }

    private void StepCredits()
    {
        credits.RemoveAll(c => Tick >= c.Expires && !c.Pending);
        for (var i = credits.Count - 1; i >= 0; i--)
        {
            var c = credits[i];
            if (c.Pending)
                continue;
            var collector = players
                .Values.Where(p => Eligible(p, c.Owner))
                .Select(p =>
                    (
                        Player: p,
                        Contact: Intersection(
                            p.Previous - c.Previous,
                            p.Position - c.Position,
                            Radius + 12 + Value(p, "collector")
                        )
                    )
                )
                .Where(p => p.Contact.HasValue)
                .OrderBy(p => p.Contact)
                .ThenBy(p => p.Player.Id)
                .FirstOrDefault()
                .Player;
            if (collector is not null)
            {
                if (!c.Claims.TryGetValue(collector.Id, out var claim))
                {
                    var chance = Value(collector, "luckyfind", Value(collector, "luck"));
                    var doubled = CreditRoll(c.Id, collector.Id) < chance;
                    claim = (
                        (int)
                            MathF.Floor(
                                c.Value * (1 + Value(collector, "fortune")) * (doubled ? 2 : 1)
                            ),
                        doubled
                    );
                    c.Claims[collector.Id] = claim;
                }
                var (amount, lucky) = claim;
                c.Pending = true;
                PersistForRun(
                    collector,
                    () => profiles.Credit(collector.Name, amount),
                    result =>
                    {
                        if (result == "ok")
                        {
                            collector.CreditReward = new(++collector.CreditSequence, amount, lucky);
                            credits.RemoveAll(d => d.Id == c.Id);
                        }
                        else
                            c.Pending = false;
                    }
                );
            }
        }
    }
}
