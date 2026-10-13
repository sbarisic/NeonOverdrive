using System.Collections;
using System.Numerics;
using System.Reflection;
using Xunit;

namespace ArcadeDodger.Multiplayer.Tests;

internal static class WorkshopRig
{
    public static float MuzzleX => 1000 + ChassisCatalog.Get(0).Hardpoints[0].X;

    public static object Get(object o, string key) =>
        o.GetType()
            .GetField(key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(o)!;

    public static void Set(object o, string key, object value) =>
        o.GetType()
            .GetField(key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(o, value);

    public static object Player(Room r, int id = 1) => ((IDictionary)Get(r, "players"))[id]!;

    public static Room Start(int level = 1, ProfileStore? profiles = null, bool ai = false)
    {
        var r = new Room(ai, profiles: profiles, sectorEnabled: false);
        r.Join("Modules");
        Set(Player(r), "Experience", Room.ExperienceForLevel(level));
        Set(Player(r), "Position", new Vector2(1000, 800));
        Set(Player(r), "Previous", new Vector2(1000, 800));
        Set(Player(r), "ProtectedUntil", 0L);
        return r;
    }

    public static WeaponConfig Weapon(int emitter, params int[] parts)
    {
        var stages = new int[8];
        parts.CopyTo(stages, 0);
        return new(emitter, stages);
    }

    public static void Fund(Room r, params WeaponConfig[] configs)
    {
        var store = (ProfileStore)Get(r, "profiles");
        var name = r.Snapshot().Players[0].Name;
        store.Credit(name, 100000);
        var ids = configs.SelectMany(w => w.Attachments.Append(w.Emitter)).Where(id => id > 1);
        foreach (var id in ids)
            Assert.Equal("ok", store.Buy(name, id));
    }

    public static WeaponConfig[] Loadout(params WeaponConfig[] weapons)
    {
        var all = PilotProfile.Starter().Weapons;
        for (var i = 0; i < weapons.Length; i++)
            all[i] = weapons[i];
        return all;
    }

    public static void Equip(Room r, params WeaponConfig[] weapons)
    {
        // Weapon-mechanics fixtures explicitly earn the levels required by their loadout.
        var required = weapons
            .Select(
                (w, i) =>
                    ModuleCatalog.UnlockLevels[i]
                    + Array.FindLastIndex(w.Attachments, id => id != 0)
                    + 1
            )
            .DefaultIfEmpty(1)
            .Max();
        var player = Player(r);
        Set(
            player,
            "Experience",
            Math.Max((int)Get(player, "Experience"), Room.ExperienceForLevel(required))
        );
        Fund(r, weapons);
        r.Input(1, new(1, 0, 0, 0, false, false, true));
        r.Step();
        Assert.True(r.Equip(1, new(2, Loadout(weapons))));
    }

    public static object Enemy(Room r, float x, float y = 800, int id = 1, int hull = 100)
    {
        var e = Activator.CreateInstance(
            typeof(Room).GetNestedType("Enemy", BindingFlags.NonPublic)!,
            true
        )!;
        Set(e, "Id", id);
        Set(e, "Hull", hull);
        Set(e, "Position", new Vector2(x, y));
        Set(e, "Previous", new Vector2(x, y));
        ((IList)Get(r, "enemies")).Add(e);
        return e;
    }

    public static void Fire(Room r, int ticks = 1, int sequence = 3)
    {
        for (var i = 0; i < ticks; i++)
        {
            r.Input(1, new(sequence + i, 0, 0, 0, true, false));
            r.Step();
        }
    }

    public static void Step(Room r, int ticks)
    {
        for (var i = 0; i < ticks; i++)
            r.Step();
    }

    public static float Health(object e) => (int)Get(e, "Hull") - (float)Get(e, "DamageFraction");
}
