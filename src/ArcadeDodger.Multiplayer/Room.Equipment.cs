using System.Collections.Immutable;

namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    private static int Gear(Player p, int kind) => p.GearTiers[kind];

    private static float Detection(Player p) => 1200 + 200 * Gear(p, 5);

    private static float Survey(Player p) => 1200 + 250 * Gear(p, 4);

    private static void LoadEquipment(Player p, PilotProfile profile)
    {
        p.Chassis = profile.Chassis;
        p.Weapons = profile.Weapons.ToImmutableArray();
        Array.Clear(p.GearTiers);
        if (profile.Equipment is { } e)
            foreach (var g in e.Gear.Where(g => e.Ship.Contains(g.Id)))
                p.GearTiers[g.Kind] = g.Tier;
        p.StatsCount = -1;
        if (p.Hull - p.HullFraction > MaxHullFor(p))
        {
            p.Hull = MaxHullFor(p);
            p.HullFraction = 0;
        }
        if (p.Shield - p.ShieldFraction > MaxShieldFor(p))
        {
            p.Shield = MaxShieldFor(p);
            p.ShieldFraction = 0;
        }
    }

    internal static InventoryPage Page(EquipmentState e, int page)
    {
        const int size = 48;
        var count = e.Weapons.Length + e.Gear.Length;
        var pages = Math.Max(1, (count + size - 1) / size);
        page = Math.Clamp(page, 0, pages - 1);
        var start = page * size;
        return new(
            e.Revision,
            page,
            pages,
            e.Weapons.Skip(start).Take(size).Select(w => w.Copy()).ToArray(),
            e.Gear.Skip(Math.Max(0, start - e.Weapons.Length))
                .Take(Math.Max(0, size - Math.Max(0, Math.Min(size, e.Weapons.Length - start))))
                .ToArray(),
            [.. e.Loose],
            [.. e.Bays],
            [.. e.Ship]
        );
    }

    public bool Equipment(int id, EquipmentMessage request)
    {
        if (
            !players.TryGetValue(id, out var p)
            || !p.Connected
            || request.Sequence <= p.LastReceived
        )
            return false;
        p.LastReceived = request.Sequence;
        if (request.Action == "page")
        {
            p.InventoryPage = request.Page;
            return true;
        }
        p.WorkshopSequence = request.Sequence;
        p.WorkshopResult =
            p.EconomyPending ? "busy"
            : p.Hull <= 0 || !(p.Pending?.Editing ?? p.Editing) ? "open_workshop"
            : request.Action == "sell" && p.Docked == 0 ? "not_docked"
            : "pending";
        if (p.WorkshopResult != "pending")
            return false;
        p.EconomyPending = true;
        var level = LevelForExperience(p.Experience);
        var checkpoint = Checkpoint(p);
        PersistForRun(
            p,
            () =>
                request.Action == "apply"
                    ? profiles.ApplyEquipment(p.Name, request, level, checkpoint)
                    : profiles.SellEquipment(p.Name, request),
            result =>
            {
                p.EconomyPending = false;
                if (p.WorkshopSequence == request.Sequence)
                    p.WorkshopResult = result;
                if (result == "ok" && p.Hull > 0)
                    LoadEquipment(p, profiles.Get(p.Name));
            }
        );
        return p.WorkshopResult is "pending" or "ok";
    }
}
