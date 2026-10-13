using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Xunit;
using static ArcadeDodger.Multiplayer.Tests.WorkshopRig;

namespace ArcadeDodger.Multiplayer.Tests;

public class EquipmentTests
{
    private static object? Call(Room r, string method, params object?[] args) =>
        typeof(Room)
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(r, args);

    private static object Shot(int owner)
    {
        var shot = Activator.CreateInstance(
            typeof(Room).GetNestedType("Shot", BindingFlags.NonPublic)!,
            true
        )!;
        Set(shot, "Owner", owner);
        return shot;
    }

    private static EquipmentMessage Draft(EquipmentState e, WeaponEdit[]? edits = null) =>
        new(2, "apply", e.Revision, edits ?? [], e.Bays, e.Ship);

    [Fact]
    public void CompleteWeaponKeepsIdentityAndModsAcrossUnequipAndBayTransfer()
    {
        var store = new ProfileStore();
        store.Ensure("P");
        var weapon = EquipmentCatalog.Build(3, 4);
        Assert.Equal("ok", store.GrantEquipment("P", weapon, null));
        var e = store.Get("P").Equipment!;
        e.Bays[0] = new(weapon.Id, true);
        Assert.Equal("ok", store.ApplyEquipment("P", Draft(e), 1));
        e = store.Get("P").Equipment!;
        e.Bays[0] = new(null, false);
        e.Bays[1] = new(weapon.Id, true);
        Assert.Equal("level_required", store.ApplyEquipment("P", Draft(e), 7));
        Assert.Equal("ok", store.ApplyEquipment("P", Draft(e), 8));
        var saved = store.Get("P").Equipment!;
        Assert.Equal(
            weapon,
            saved.Weapons.Single(w => w.Id == weapon.Id) with
            {
                Attachments = weapon.Attachments,
            }
        );
        Assert.Equal(weapon.Attachments, saved.Weapons.Single(w => w.Id == weapon.Id).Attachments);
        Assert.Equal(weapon.Id, saved.Bays[1].Weapon);
        Assert.All(saved.Loose, n => Assert.Equal(0, n));
    }

    [Fact]
    public void EditsConserveOwnershipAndRejectDuplicatesAndStaleRevisions()
    {
        var s = new ProfileStore();
        s.Ensure("P");
        s.GrantModule("P", 5);
        var e = s.Get("P").Equipment!;
        var a = e.Weapons[0];
        var b = e.Weapons[1];
        Assert.Equal(
            "invalid_inventory",
            s.ApplyEquipment(
                "P",
                Draft(
                    e,
                    [new(a.Id, [5, 0, 0, 0, 0, 0, 0, 0]), new(b.Id, [5, 0, 0, 0, 0, 0, 0, 0])]
                ),
                1
            )
        );
        Assert.Equal(1, s.Get("P").Equipment!.Loose[5]);
        Assert.Equal(
            "ok",
            s.ApplyEquipment("P", Draft(e, [new(a.Id, [0, 0, 0, 0, 0, 0, 0, 5])]), 1)
        );
        Assert.Equal("stale_equipment", s.ApplyEquipment("P", Draft(e), 1));
        e = s.Get("P").Equipment!;
        Assert.Equal(
            "ok",
            s.ApplyEquipment(
                "P",
                Draft(e, [new(a.Id, new int[8]), new(b.Id, [5, 0, 0, 0, 0, 0, 0, 0])]),
                1
            )
        );
        Assert.Equal(0, s.Get("P").Equipment!.Loose[5]);
        Assert.Equal(1, s.Get("P").Owned[5]);
        e = s.Get("P").Equipment!;
        e.Bays[1] = e.Bays[0] with { Enabled = false };
        Assert.Equal("invalid_equipment", s.ApplyEquipment("P", Draft(e), 32));
    }

    [Fact]
    public void LegacyMigrationPreservesSpareEmittersAndEconomicStateAcrossRestarts()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var p = PilotProfile.Starter(3) with { Credits = 789, RunId = "life" };
            p.Owned[2] = 2;
            p.Owned[5] = 3;
            p.Weapons[0].Attachments[7] = 5;
            File.WriteAllText(
                path,
                JsonSerializer.Serialize(new Dictionary<string, PilotProfile> { { "P", p } })
            );
            var first = new ProfileStore(path).Get("P");
            var second = new ProfileStore(path).Get("P");
            Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
            Assert.Equal(6, first.Equipment!.Weapons.Length);
            Assert.Equal(2, first.Equipment.Loose[5]);
            Assert.Equal(5, first.Weapons[0].Attachments[7]);
            Assert.Equal(789, first.Credits);
            Assert.Equal(3, first.ReplayCount);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
        }
    }

    [Fact]
    public void DropGrantIsIdempotentAndWholeWeaponSaleIncludesEveryComponent()
    {
        var s = new ProfileStore();
        s.Ensure("P");
        var w = EquipmentCatalog.Build(4, 4);
        Assert.Equal("ok", s.GrantEquipment("P", w, null));
        Assert.Equal("ok", s.GrantEquipment("P", w, null));
        Assert.Equal(5, s.Get("P").Equipment!.Weapons.Length);
        var e = s.Get("P").Equipment!;
        var sale = new EquipmentMessage(1, "sell", e.Revision, Item: w.Id);
        Assert.Equal("ok", s.SellEquipment("P", sale));
        Assert.Equal("stale_equipment", s.SellEquipment("P", sale));
        var p = s.Get("P");
        Assert.Equal(100 + EquipmentCatalog.Resale(w), p.Credits);
        Assert.Equal(4, p.Equipment!.Weapons.Length);
        Assert.All(p.Equipment.Loose, n => Assert.Equal(0, n));
        Assert.Equal(
            "item_equipped",
            s.SellEquipment(
                "P",
                new(2, "sell", p.Equipment.Revision, Item: p.Equipment.Bays[0].Weapon)
            )
        );
    }

    [Fact]
    public void FailedSaveDoesNotPartiallyApplyOrSellEquipment()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var s = new ProfileStore(path);
            s.Ensure("P");
            var g = new ShipItem(Guid.NewGuid().ToString("N"), 8, 4);
            s.GrantEquipment("P", null, g);
            var before = JsonSerializer.Serialize(s.Get("P"));
            var e = s.Get("P").Equipment!;
            e.Ship[4] = g.Id;
            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.Equal("save_failed", s.ApplyEquipment("P", Draft(e), 1));
                Assert.Equal(
                    "save_failed",
                    s.SellEquipment("P", new(2, "sell", e.Revision, Item: g.Id))
                );
                Assert.Equal(before, JsonSerializer.Serialize(s.Get("P")));
            }
            Assert.Equal("ok", s.ApplyEquipment("P", Draft(e), 1));
            Assert.Equal(g.Id, new ProfileStore(path).Get("P").Equipment!.Ship[4]);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
        }
    }

    public static IEnumerable<object[]> GearCases =>
        Enumerable
            .Range(0, 12)
            .SelectMany(k => Enumerable.Range(1, 4).Select(t => new object[] { k, t }));

    [Theory, MemberData(nameof(GearCases))]
    public void EveryGearVariantEquipsAtLevelOneWithoutHealingOrResettingCooldowns(
        int kind,
        int tier
    )
    {
        var s = new ProfileStore();
        var r = Start(profiles: s);
        var p = Player(r);
        Set(p, "Hull", 2);
        Set(p, "HullFraction", .25f);
        Set(p, "Shield", 1);
        Set(p, "ShieldFraction", .5f);
        Set(p, "ShieldAt", 1000L);
        ((double[])Get(p, "WeaponReady"))[0] = 1000;
        var g = new ShipItem(Guid.NewGuid().ToString("N"), kind, tier);
        s.GrantEquipment("Modules", null, g);
        var e = s.Get("Modules").Equipment!;
        e.Ship[kind / 2] = g.Id;
        r.Input(1, new(1, 0, 0, 0, false, false, true));
        Assert.True(r.Equipment(1, Draft(e)));
        Assert.Equal(2, Get(p, "Hull"));
        Assert.Equal(.25f, Get(p, "HullFraction"));
        Assert.Equal(1, Get(p, "Shield"));
        Assert.Equal(.5f, Get(p, "ShieldFraction"));
        Assert.Equal(1000L, Get(p, "ShieldAt"));
        Assert.Equal(1000, ((double[])Get(p, "WeaponReady"))[0]);
        var view = r.Snapshot().Players[0];
        Assert.Equal(3 + (kind == 8 ? tier : 0), view.MaxHull);
        Assert.Equal(2 + (kind == 0 ? tier : 0), view.MaxShield);
        Assert.Equal(220 * (1 + (kind == 2 ? .05f * tier : 0)), view.MoveSpeed, 3);
        Assert.Equal(110 * (1 + (kind == 3 ? .1f * tier : 0)), view.Acceleration, 3);
        Assert.Equal(1200 + (kind == 4 ? 250 * tier : 0), view.SurveyRadius);
        Assert.Equal(1200 + (kind == 5 ? 200 * tier : 0), view.DetectionRadius);
        Assert.Equal(1 + (kind == 10 ? .05f * tier : 0), view.WeaponStats![0].Damage, 4);
    }

    [Fact]
    public void DormantCompleteBuildFiresLikeBareWeaponUntilCheckpointUnlocks()
    {
        var s = new ProfileStore();
        var r = Start(profiles: s);
        var w = new WeaponItem(Guid.NewGuid().ToString("N"), 1, [5, 5, 5, 5, 5, 5, 5, 5]);
        s.GrantEquipment("Modules", w, null);
        var e = s.Get("Modules").Equipment!;
        e.Bays[0] = new(w.Id, true);
        r.Input(1, new(1, 0, 0, 0, false, false, true));
        Assert.True(r.Equipment(1, Draft(e)));
        var enemy = Enemy(r, 1200, hull: 100);
        Fire(r, 10);
        Assert.Equal(99, Health(enemy));
        Assert.Equal(w.Attachments, s.Get("Modules").Weapons[0].Attachments);
    }

    [Fact]
    public void LargeInventoryIsPagedAndFreshRunClearsAllInstances()
    {
        var s = new ProfileStore();
        var r = Start(profiles: s);
        for (var i = 0; i < 130; i++)
            s.GrantEquipment("Modules", EquipmentCatalog.Build(1, 1), null);
        var first = r.Workshop(1).Inventory!;
        Assert.Equal(3, first.Pages);
        Assert.Equal(48, first.Weapons.Length);
        Assert.True(r.Equipment(1, new(1, "page", Page: 2)));
        Assert.Equal(38, r.Workshop(1).Inventory!.Weapons.Length);
        var old = s.Get("Modules");
        Assert.Equal("ok", s.ResetRun("Modules", old.RunId));
        Assert.Equal(105, s.Get("Modules").Credits);
        Assert.Equal(4, s.Get("Modules").Equipment!.Weapons.Length);
        Assert.Empty(s.Get("Modules").Equipment!.Gear);
    }

    [Fact]
    public void PreparedDeathRunKeepsItsWeaponIdentitiesWhenAdmitted()
    {
        var s = new ProfileStore();
        s.Ensure("P");
        s.ResetRun("P", s.Get("P").RunId);
        var prepared = s.Get("P");
        Assert.Equal("ok", s.StartRun("P", prepared.RunId));
        var admitted = s.Get("P");
        Assert.Equal(prepared.RunId, admitted.RunId);
        Assert.Equal(
            prepared.Equipment!.Weapons.Select(w => w.Id),
            admitted.Equipment!.Weapons.Select(w => w.Id)
        );
        Assert.Equal(105, admitted.Credits);
        Assert.False(admitted.RunPrepared);
    }

    [Fact]
    public void CombatDropsArePersonalForContributorsAndNeverRewardFriendlyDeaths()
    {
        var r = new Room(false, seed: 123);
        r.Join("A");
        r.Join("B");
        r.Join("C");
        ((IList)Get(r, "enemies")).Clear();
        Set(r, "creditSalt", 1u);
        foreach (var id in new[] { 1, 2, 3 })
        {
            Set(Player(r, id), "Position", new Vector2(8000, 7700));
            Set(Player(r, id), "Previous", new Vector2(8000, 7700));
        }
        for (var i = 1; i <= 100; i++)
        {
            var e = Enemy(r, 8000, 8000, i, 10);
            Call(r, "RecordContribution", e, Shot(1), 1f);
            Call(r, "RecordContribution", e, Shot(2), 1f);
            Call(r, "RecordContribution", e, Shot(3), .5f);
            Set(e, "Hull", 0);
        }
        r.Step();
        var drops = r.Snapshot().ModuleDrops!;
        Assert.InRange(drops.Length, 10, 50);
        Assert.Contains(drops, d => d.Kind == "weapon");
        Assert.Contains(drops, d => d.Kind == "gear");
        Assert.All(drops, d => Assert.Contains(d.Owner, new[] { 1, 2 }));
        Assert.All(r.ForViewer(r.Snapshot(), 1).ModuleDrops!, d => Assert.Equal(1, d.Owner));
        Assert.Empty(r.ForViewer(r.Snapshot(), 3).ModuleDrops!);
        var before = drops.Length;
        for (var i = 101; i <= 130; i++)
        {
            var e = Enemy(r, 8000, 8000, i, 10);
            Call(r, "RecordContribution", e, Shot(1), 20f);
            Set(e, "Allied", true);
            Set(e, "Hull", 0);
        }
        r.Step();
        Assert.Equal(before, r.Snapshot().ModuleDrops!.Length);
    }

    [Fact]
    public void RadarGearSeparatesExplorationFromEnemyDetection()
    {
        (Room Room, int Before, int After) Scan(int kind)
        {
            var s = new ProfileStore();
            var r = new Room(false, profiles: s, seed: 123);
            r.Join("P");
            Set(Player(r), "Position", new Vector2(8000, 8000));
            Set(Player(r), "Previous", new Vector2(8000, 8000));
            r.Step();
            var before = r.SectorSnapshot(1)!.Pilot!.Cells.Length;
            var g = new ShipItem(Guid.NewGuid().ToString("N"), kind, 4);
            s.GrantEquipment("P", null, g);
            var e = s.Get("P").Equipment!;
            e.Ship[2] = g.Id;
            r.Input(1, new(1, 0, 0, 0, false, false, true));
            Assert.True(r.Equipment(1, Draft(e)));
            r.Step();
            return (r, before, r.SectorSnapshot(1)!.Pilot!.Cells.Length);
        }
        var survey = Scan(4);
        Assert.True(survey.After > survey.Before * 2);
        Assert.Equal(1200, survey.Room.Snapshot().Players[0].DetectionRadius);
        var combat = Scan(5);
        Assert.Equal(combat.Before, combat.After);
        Assert.Equal(2000, combat.Room.Snapshot().Players[0].DetectionRadius);
    }

    [Fact]
    public void ReactiveArmorOnlyReducesHullAndRechargeAndCollectorBonusesUseExistingRules()
    {
        var s = new ProfileStore();
        var r = Start(profiles: s);
        var p = Player(r);
        foreach (var kind in new[] { 1, 6, 7, 9, 11 })
            s.GrantEquipment("Modules", null, new(Guid.NewGuid().ToString("N"), kind, 4));
        var e = s.Get("Modules").Equipment!;
        foreach (var g in e.Gear.Where(g => g.Kind != 7))
            e.Ship[g.Kind / 2] = g.Id;
        r.Input(1, new(1, 0, 0, 0, false, false, true));
        Assert.True(r.Equipment(1, Draft(e)));
        Set(p, "Editing", false);
        Set(p, "Pending", null!);
        Call(r, "Hit", Shot(-1), p, null, 3f);
        Assert.Equal(0, Get(p, "Shield"));
        Assert.Equal(.8f, (float)Get(p, "HullFraction"), 4);
        Assert.Equal(r.Tick + 90, (long)Get(p, "ShieldAt"));
        Set(p, "ShieldAt", 0L);
        r.Input(1, new(3, 0, 0, 0, false, false));
        r.Step();
        Assert.Equal(r.Tick + (int)(30 / 1.4f), (long)Get(p, "ShieldAt"));
        var enemy = Enemy(r, 1090, hull: 0);
        r.Step();
        Assert.Equal(105, r.Workshop(1).Credits);
        e = s.Get("Modules").Equipment!;
        e.Ship[3] = e.Gear.Single(g => g.Kind == 7).Id;
        r.Input(1, new(4, 0, 0, 0, false, false, true));
        Assert.True(r.Equipment(1, Draft(e) with { Sequence = 5 }));
        Enemy(r, 1000, id: 2, hull: 0);
        r.Input(1, new(6, 0, 0, 0, false, false));
        r.Step();
        Assert.Equal(112, r.Workshop(1).Credits);
    }
}
