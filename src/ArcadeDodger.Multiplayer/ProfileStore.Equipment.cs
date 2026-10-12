namespace ArcadeDodger.Multiplayer;

public sealed partial class ProfileStore
{
    public WorkshopSnapshot Workshop(string name, int page, int sequence, string result)
    {
        var p = profiles.GetValueOrDefault(FriendBook.Key(name)) ?? Get(name);
        return new(
            p.Credits,
            [.. p.Owned],
            p.Weapons.Select(w => w.Copy()).ToArray(),
            sequence,
            result,
            p.ReplayCount,
            p.StartingCredits,
            Room.Page(p.Equipment!, page)
        );
    }

    public static bool ValidEquipment(EquipmentState e)
    {
        if (
            e.Revision < 0
            || e.Weapons is null
            || e.Gear is null
            || e.Loose is not { Length: 29 }
            || e.Bays is not { Length: 4 }
            || e.Ship is not { Length: 6 }
            || e.Loose.Any(i => i < 0)
            || e.Loose.Take(5).Any(i => i != 0)
        )
            return false;
        var ids = new HashSet<string>();
        foreach (var w in e.Weapons)
            if (
                w is null
                || !Guid.TryParseExact(w.Id, "N", out _)
                || !ids.Add(w.Id)
                || w.Emitter is < 1 or > 4
                || w.Tier is < 0 or > 4
                || w.Attachments is not { Length: 8 }
                || w.Attachments.Any(i => i != 0 && i is < 5 or > 28)
            )
                return false;
        foreach (var g in e.Gear)
            if (
                g is null
                || !Guid.TryParseExact(g.Id, "N", out _)
                || !ids.Add(g.Id)
                || g.Kind is < 0 or > 11
                || g.Tier is < 1 or > 4
            )
                return false;
        var equipped = new HashSet<string>();
        foreach (var b in e.Bays)
            if (
                b is null
                || b.Weapon is { } id && (!equipped.Add(id) || !e.Weapons.Any(w => w.Id == id))
            )
                return false;
        for (var i = 0; i < 6; i++)
            if (
                e.Ship[i] is { } id
                && (!equipped.Add(id) || !e.Gear.Any(g => g.Id == id && g.Kind / 2 == i))
            )
                return false;
        return true;
    }

    public string ApplyEquipment(
        string name,
        EquipmentMessage request,
        int level,
        RunCheckpoint? checkpoint = null
    )
    {
        var p = Get(name);
        var e = p.Equipment!;
        if (e.Revision != request.Revision)
            return "stale_equipment";
        if (
            request.Edits is null
            || request.Edits.Length > 32
            || request.Bays is not { Length: 4 }
            || request.Ship is not { Length: 6 }
        )
            return "invalid_equipment";
        var seen = new HashSet<string>();
        var map = e.Weapons.ToDictionary(w => w.Id);
        foreach (var edit in request.Edits)
        {
            if (
                edit is null
                || edit.Id is null
                || !seen.Add(edit.Id)
                || !map.TryGetValue(edit.Id, out var w)
                || edit.Attachments is not { Length: 8 }
                || edit.Attachments.Any(i => i != 0 && i is < 5 or > 28)
            )
                return "invalid_equipment";
            foreach (var part in w.Attachments.Where(i => i > 0))
                e.Loose[part]++;
            map[edit.Id] = w with { Attachments = [.. edit.Attachments] };
        }
        foreach (var edit in request.Edits)
        foreach (var part in edit.Attachments.Where(i => i > 0))
            if (--e.Loose[part] < 0)
                return "invalid_inventory";
        for (var i = 0; i < 4; i++)
            if (
                request.Bays[i] is null
                || level < ModuleCatalog.UnlockLevels[i]
                    && (request.Bays[i].Weapon != e.Bays[i].Weapon || request.Bays[i].Enabled)
            )
                return "level_required";
        e = e with
        {
            Revision = checked(e.Revision + 1),
            Weapons = map.Values.ToArray(),
            Bays = [.. request.Bays],
            Ship = [.. request.Ship],
        };
        if (!ValidEquipment(e))
            return "invalid_equipment";
        if (checkpoint is not null)
        {
            int Tier(int kind) =>
                e.Gear.Where(g => g.Kind == kind && e.Ship.Contains(g.Id)).Sum(g => g.Tier);
            var hull = 3 + SkillCatalog.Rank(checkpoint.Skills, "hull") + Tier(8);
            var shield = 2 + SkillCatalog.Rank(checkpoint.Skills, "shield") + Tier(0);
            checkpoint = checkpoint with
            {
                Hull = Math.Min(checkpoint.Hull, hull),
                Shield = Math.Min(checkpoint.Shield, shield),
                HullFraction =
                    checkpoint.Hull - checkpoint.HullFraction > hull ? 0 : checkpoint.HullFraction,
                ShieldFraction =
                    checkpoint.Shield - checkpoint.ShieldFraction > shield
                        ? 0
                        : checkpoint.ShieldFraction,
            };
            p = p with { Run = checkpoint };
        }
        return Commit(name, e.Project(p));
    }

    public string GrantEquipment(string name, WeaponItem? weapon, ShipItem? gear)
    {
        var p = Get(name);
        var e = p.Equipment!;
        // A frozen drop ID is also the idempotency key across persistence retries.
        if (
            weapon is not null && e.Weapons.Any(w => w.Id == weapon.Id)
            || gear is not null && e.Gear.Any(g => g.Id == gear.Id)
        )
            return "ok";
        if ((weapon is null) == (gear is null))
            return "invalid_item";
        e = e with
        {
            Revision = checked(e.Revision + 1),
            Weapons = weapon is null ? e.Weapons : [.. e.Weapons, weapon.Copy()],
            Gear = gear is null ? e.Gear : [.. e.Gear, gear],
        };
        return ValidEquipment(e) ? Commit(name, e.Project(p)) : "invalid_item";
    }

    public string SellEquipment(string name, EquipmentMessage request)
    {
        var p = Get(name);
        var e = p.Equipment!;
        if (e.Revision != request.Revision)
            return "stale_equipment";
        long value;
        if (request.Item is { } id)
        {
            if (e.Bays.Any(b => b.Weapon == id) || e.Ship.Contains(id))
                return "item_equipped";
            if (e.Weapons.FirstOrDefault(w => w.Id == id) is { } w)
            {
                value = EquipmentCatalog.Resale(w);
                e = e with { Weapons = e.Weapons.Where(w => w.Id != id).ToArray() };
            }
            else if (e.Gear.FirstOrDefault(g => g.Id == id) is { } g)
            {
                value = 25 * g.Tier;
                e = e with { Gear = e.Gear.Where(g => g.Id != id).ToArray() };
            }
            else
                return "invalid_item";
        }
        else
        {
            if (
                request.Module is < 5 or > 28
                || request.Quantity < 1
                || request.Quantity > e.Loose[request.Module]
            )
                return "invalid_inventory";
            value = (long)EquipmentCatalog.Resale(request.Module) * request.Quantity;
            e.Loose[request.Module] -= request.Quantity;
        }
        if (p.Credits > long.MaxValue - value)
            return "credit_limit";
        e = e with { Revision = checked(e.Revision + 1) };
        return Commit(name, e.Project(p with { Credits = p.Credits + value }));
    }

    private static PilotProfile AddModule(PilotProfile p, int module)
    {
        var e = p.Equipment!;
        if (module <= 4)
            e = e with
            {
                Weapons = [.. e.Weapons, new(Guid.NewGuid().ToString("N"), module, new int[8])],
            };
        else
            e.Loose[module]++;
        return (e with { Revision = checked(e.Revision + 1) }).Project(p);
    }
}
