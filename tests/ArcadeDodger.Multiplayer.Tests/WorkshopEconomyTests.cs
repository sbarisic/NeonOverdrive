using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Xunit;
using static ArcadeDodger.Multiplayer.Tests.WorkshopRig;
namespace ArcadeDodger.Multiplayer.Tests;
public class WorkshopEconomyTests
{
    [Fact] public void CatalogHasFourEmittersAndExactlyTwentyFourPricedAttachments()
    {Assert.Equal(28,ModuleCatalog.Items.Length);Assert.Equal(24,ModuleCatalog.Items.Count(m=>m.Id>4));Assert.Equal(28,ModuleCatalog.Items.Select(m=>m.Id).Distinct().Count());Assert.All(ModuleCatalog.Items.Where(m=>m.Id>1),m=>Assert.True(m.Price>0));}
    [Fact] public void WalletOwnedCopiesAndLockedConfigurationsSurviveRestartAndNamesNormalize()
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+"-profiles.json");
        try
        {
            var store=new ProfileStore(path);Assert.Equal("ok",store.Ensure(" Player1 "));Assert.Equal(100,store.Get("player1").Credits);
            Assert.Equal("ok",store.Buy("PLAYER1",6));var loadout=Loadout(Weapon(1),Weapon(1,6));loadout[1]=loadout[1] with {Enabled=false};Assert.Equal("ok",store.Equip("Player1",loadout));
            var restored=new ProfileStore(path).Get(" player1 ");Assert.Equal(40,restored.Credits);Assert.Equal(1,restored.Owned[6]);Assert.Equal(6,restored.Weapons[1].Attachments[0]);Assert.False(restored.Weapons[1].Enabled);
            restored.Owned[6]=99;Assert.Equal(1,store.Get("Player1").Owned[6]);
        }finally{File.Delete(path);File.Delete(path+".tmp");}
    }
    [Fact] public void FailedDiskWritesCannotAcknowledgePurchasesOrConsumeCreditDrops()
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+"-profiles.json");
        try
        {
            var store=new ProfileStore(path);var r=Start(profiles:store);
            using(var locked=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                Assert.Equal("save_failed",store.Buy("Modules",6));Assert.Equal(100,store.Get("Modules").Credits);Assert.Equal(0,store.Get("Modules").Owned[6]);
                Enemy(r,1000,hull:0);r.Step();Assert.Single(r.Snapshot().Credits!);Assert.Equal(100,r.Workshop(1).Credits);
                Assert.Equal("save_failed",store.Equip("Modules",Loadout(Weapon(0))));Assert.Equal(1,store.Get("Modules").Weapons[0].Emitter);
            }
            r.Step();Assert.Empty(r.Snapshot().Credits!);Assert.Equal(105,r.Workshop(1).Credits);Assert.Equal(105,new ProfileStore(path).Get("Modules").Credits);
        }finally{File.Delete(path);File.Delete(path+".tmp");}
    }
    [Fact] public void PickupsAreSeparateFromXpContestedOnceAndBlockedInMenus()
    {
        var r=Start();var id=r.Join("Second")!.Value;Set(Player(r,id),"Position",new Vector2(1000,800));Enemy(r,1000,hull:0);
        r.Input(1,new(1,0,0,0,false,false,true));r.Input(id,new(1,0,0,0,false,false,true));r.Step();Assert.Single(r.Snapshot().Credits!);Assert.Equal(100,r.Workshop(1).Credits);
        r.Input(1,new(2,0,0,0,false,false));r.Input(id,new(2,0,0,0,false,false));r.Step();Assert.Empty(r.Snapshot().Credits!);Assert.Equal(105,r.Workshop(1).Credits);Assert.Equal(100,r.Workshop(id).Credits);
        r.Step();Assert.Equal(105,r.Workshop(1).Credits);
    }
    [Theory][InlineData(false,5)][InlineData(true,12)]
    public void CreditValuesAndSalvageBonusAreDeterministic(bool gunner,int value)
    {
        var r=Start();var enemy=Enemy(r,1400,hull:0);Set(enemy,"Gunner",gunner);Set(enemy,"MarkUntil",100L);r.Step();Assert.Equal((int)Math.Ceiling(value*1.25),Assert.Single(r.Snapshot().Credits!).Value);
        Step(r,900);Assert.Empty(r.Snapshot().Credits!);
    }
    [Fact] public void ProfilesCannotEquipUnownedCopiesEvenInDisabledOrLockedRows()
    {
        var store=new ProfileStore();store.Buy("P",6);var w=Loadout(Weapon(1,6),Weapon(1,6) with {Enabled=false});Assert.Equal("invalid_inventory",store.Equip("P",w));
        w[1]=Weapon(1);Assert.Equal("ok",store.Equip("P",w));Assert.Equal("invalid_item",store.Buy("P",1));
    }
    [Fact] public void EquipWireRejectsMalformedNestedFieldsAndOldProtocol()
    {
        var json=JsonSerializer.Serialize(new{v = 5,type="equip",seq=1,weapons=Loadout(Weapon(1,6))},new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.True(ClientProtocol.TryParse(Encoding.UTF8.GetBytes(json),out var message));Assert.IsType<EquipMessage>(message);
        foreach(var invalid in new[]{json.Replace("\"emitter\":1","\"emitter\":1,\"emitter\":1"),json.Replace("\"enabled\":true","\"enabled\":1"),json.Replace("[6,0,0,0,0,0,0,0]","[6]"),json.Replace("\"v\":5","\"v\":1")})
            Assert.False(ClientProtocol.TryParse(Encoding.UTF8.GetBytes(invalid),out _));
    }
    [Fact] public void SameNormalizedNameCannotConnectTwiceButDisconnectAllowsRejoin()
    {var r=Start();Assert.Null(r.Join(" modules "));r.Disconnect(1);Assert.NotNull(r.Join("MODULES"));}
    [Fact] public void PlasmaAndMinesIgnoreFriendsAndSecondaryDamageCannotLeechOverkill()
    {
        var friends=new FriendBook();friends.Change("Modules","Ally","request");friends.Change("Ally","Modules","accept");var r=new Room(false,friends,sectorEnabled:false);r.Join("Modules");Set(Player(r),"Position",new Vector2(1000,800));
        var ally=r.Join("Ally")!.Value;Set(Player(r,ally),"Position",new Vector2(1080,800));Set(Player(r,ally),"ProtectedUntil",0L);
        Equip(r,Weapon(2,26));Fire(r);r.Input(1,new(4,0,0,0,false,false));Step(r,20);Assert.Single(r.Snapshot().Mines!);Assert.Equal(2,r.Snapshot().Players[1].Shield);
        Enemy(r,1080);r.Step();Assert.Empty(r.Snapshot().Mines!);Assert.Equal(2,r.Snapshot().Players[1].Shield);
    }
}
