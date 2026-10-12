using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using ArcadeDodger.Server;
using Xunit;
using static ArcadeDodger.Multiplayer.Tests.WorkshopRig;
namespace ArcadeDodger.Multiplayer.Tests;

public class CooperativeRunTests
{
    private static object? Invoke(Room r,string method,params object?[] args)=>typeof(Room).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(r,args);
    private static void At(Room r,Vector2 point,int id=1){Set(Player(r,id),"Position",point);Set(Player(r,id),"Previous",point);}
    private static object Shot(int owner)=>Activator.CreateInstance(typeof(Room).GetNestedType("Shot",BindingFlags.NonPublic)!,true) is {} shot?Init(shot,owner):throw new Exception();
    private static object Init(object shot,int owner){Set(shot,"Owner",owner);return shot;}

    [Fact] public void DeathDurablyClearsEconomyAndNeverGrantsStarterTwice()
    {
        var store=new ProfileStore();var r=Start(10,store);Equip(r,Weapon(2,5,11));var original=store.Get("Modules").RunId;
        Set(Player(r),"Hull",0);Assert.True(r.Respawn(1,new(10)));var profile=store.Get("Modules");
        Assert.Equal(100,profile.Credits);Assert.Equal(4,profile.Owned.Sum());Assert.Equal(4,profile.Owned[1]);Assert.NotEqual(original,profile.RunId);
        Assert.All(profile.Weapons,w=>{Assert.Equal(1,w.Emitter);Assert.All(w.Attachments,id=>Assert.Equal(0,id));});
        store.Credit("Modules",7);Assert.Equal("ok",store.ResetRun("Modules",original));Assert.Equal(107,store.Get("Modules").Credits);
        Assert.Equal(0,r.Snapshot().Players[0].Experience);
    }
    [Fact] public async Task BackgroundDeathWaitsForDurabilityAndReconnectRetainsTheDeadBody()
    {
        var store=new ProfileStore();var r=Start(profiles:store);store.BackgroundWrites=true;
        var hold=new TaskCompletionSource();var blocker=store.Schedule(()=>{hold.Task.GetAwaiter().GetResult();return "ok";});
        Set(Player(r),"Hull",0);Assert.False(r.Respawn(1,new(1)));r.Disconnect(1);Assert.Equal(1,r.Join("modules"));Assert.Equal(0,r.Snapshot().Players[0].Hull);
        hold.SetResult();await store.FlushAsync();r.Step();Assert.True(r.Respawn(1,new(2)));Assert.Equal(100,store.Get("modules").Credits);
    }
    [Fact] public void ReconnectKeepsPilotHealthSkillsAndCooldownsBeforeAndAfterBodyRemoval()
    {
        var r=Start(5);Set(Player(r),"Hull",2);Set(Player(r),"Shield",0);Set(Player(r),"ShieldAt",10000L);
        Set(Player(r),"Skills",new HashSet<string>{"strength.1"});var before=r.Snapshot().Players[0];
        r.Disconnect(1);Assert.Equal(1,r.Join("MODULES"));Assert.Equal(before.RunId,r.Snapshot().Players[0].RunId);Assert.Equal(2,r.Snapshot().Players[0].Hull);
        r.Disconnect(1);Step(r,151);Assert.Empty(r.Snapshot().Players);Assert.Equal(1,r.Join("Modules"));var after=r.Snapshot().Players[0];
        Assert.Equal(2,after.Hull);Assert.Equal(0,after.Shield);Assert.Equal(before.Experience,after.Experience);Assert.Contains("strength.1",after.Skills);Assert.Equal(0,after.VelocityX);
    }
    [Fact] public void RestartRestoresRunCheckpointAtNewArrivalPort()
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+"-run.json");
        try {var store=new ProfileStore(path);var r=Start(7,store);Set(Player(r),"Hull",1);Set(Player(r),"Shield",0);Set(Player(r),"ShieldFraction",.25f);r.SaveRuns();
            var resumed=new Room(false,profiles:new ProfileStore(path),seed:3);resumed.Join("Modules");var p=resumed.Snapshot().Players[0];
            Assert.Equal(7,p.Level);Assert.Equal(1,p.Hull);Assert.Equal(0,p.Shield);Assert.InRange(Vector2.Distance(new(p.X,p.Y),resumed.Sector!.Locations.Single(l=>l.Id==10).Position),100,180);
        } finally {File.Delete(path);File.Delete(path+".tmp");}
    }
    [Fact] public void DestroyedInstallationRewardsEachContributorOutsideSolidFootprint()
    {
        var r=new Room(false,seed:123);r.Join("A");r.Join("B");r.Join("Observer");
        var enemy=((IList)Get(r,"enemies")).Cast<object>().First(e=>(int)Get(e,"Home")==20);
        At(r,(Vector2)Get(enemy,"Position")+new Vector2(200,0),1);At(r,(Vector2)Get(enemy,"Position")+new Vector2(-200,0),2);
        Invoke(r,"Hit",Shot(1),null,enemy,1f);Invoke(r,"Hit",Shot(2),null,enemy,119f);r.Step();
        var geometry=new SectorCollision(r.Sector!);Assert.Equal(2,r.Snapshot().Credits!.Length);Assert.Equal(2,r.Snapshot().Energy!.Length);
        foreach(var drop in r.Snapshot().Credits!)Assert.Null(geometry.TraceShips(new(drop.X,drop.Y),new(drop.X,drop.Y),12));
        Assert.Single(r.ForViewer(r.Snapshot(),1).Credits!);Assert.Empty(r.ForViewer(r.Snapshot(),3).Credits!);
        var credit=r.Snapshot().Credits!.Single(c=>c.Owner==1);At(r,new(credit.X,credit.Y));r.Step();Assert.Equal(140,r.Workshop(1).Credits);
        Assert.Equal("destroyed",r.SectorSnapshot(1)!.Locations.Single(l=>l.Id==20).State);
    }
    [Theory] [InlineData(1,.25f)] [InlineData(2,.5f)] [InlineData(3,.75f)] [InlineData(4,1f)]
    public void EnemyDamageAccumulatesFractionally(int level,float damage)
    {
        var r=Start();var p=Player(r);Set(p,"ShieldAt",10000L);var shot=Shot(-1);
        Invoke(r,"Hit",shot,p,null,EnemyCatalog.Damage(level));Assert.Equal(2-damage,(int)Get(p,"Shield")-(float)Get(p,"ShieldFraction"),4);
    }
    [Fact] public void RegionCatalogProducesSmallEasyShipsAndAllThreeNewTypes()
    {
        var r=new Room(seed:123);Step(r,1300);var ships=r.Snapshot().Enemies!.Where(e=>e.Kind!="installation").ToArray();
        Assert.All(ships.Where(e=>e.Home==20),e=>{Assert.Equal(1,e.Level);Assert.InRange(e.Radius,10,14);});
        foreach(var type in new[]{"interceptor","sniper","tender"})Assert.Contains(ships,e=>e.Kind==type);
        Assert.All(ships,e=>Assert.True(e.Level>=EnemyCatalog.Get(e.Kind).MinimumLevel));
    }
    [Fact] public void MapPingsRespectFriendshipAndDoNotRevealTerrain()
    {
        var friends=new FriendBook();var r=new Room(false,friends,seed:123);r.Join("A");r.Join("B");
        Assert.True(r.SectorAction(1,new(1,"ping",10)));Assert.Empty(r.SectorSnapshot(2)!.Pilot!.Pings!);
        Assert.False(r.SectorAction(1,new(2,"broadcast",10)));Step(r,150);Assert.True(r.SectorAction(1,new(3,"broadcast",10)));
        var state=r.SectorSnapshot(2)!.Pilot!;Assert.Single(state.Pings!);Assert.DoesNotContain(23,state.Discovered);
        Assert.False(r.SectorAction(2,new(1,"ping",23)));Step(r,901);Assert.Empty(r.SectorSnapshot(2)!.Pilot!.Pings!);
    }
    [Fact] public void InterdictionIsPersonalRepeatableAndNeverCreatesEnemies()
    {
        var r=new Room(false,seed:123);r.Join("A");Assert.True(r.SectorAction(1,new(1,"dock",10)));Assert.True(r.SectorAction(1,new(2,"accept",100)));
        var before=r.Snapshot().Enemies!.Length;
        for(var i=0;i<3;i++){var e=Enemy(r,4000,3000,200+i,1);Set(e,"Home",30);Invoke(r,"Hit",Shot(1),null,e,1f);r.Step();}
        Assert.Equal(before,r.Snapshot().Enemies!.Length);Assert.True(r.SectorSnapshot(1)!.Pilot!.Ready);
        Assert.True(r.SectorAction(1,new(3,"claim")));Assert.Equal(160,r.Workshop(1).Credits);Assert.False(r.SectorAction(1,new(4,"accept",100)));
        Step(r,9000);Assert.True(r.SectorAction(1,new(5,"accept",100)));
    }
    [Fact] public void SnapshotDeltaEncoderCanSkipQueuedFramesWithoutLosingPrivateState()
    {
        var r=Start();var encoder=new SnapshotEncoder();RoomSnapshot Frame()=>r.Snapshot() with {Workshop=r.Workshop(1)};
        using var first=JsonDocument.Parse(encoder.Encode(Frame()));Assert.Equal(JsonValueKind.Object,first.RootElement.GetProperty("workshop").ValueKind);
        using var same=JsonDocument.Parse(encoder.Encode(Frame()));Assert.Equal(JsonValueKind.Null,same.RootElement.GetProperty("workshop").ValueKind);
        ((ProfileStore)Get(r,"profiles")).Credit("Modules",50);
        using var changed=JsonDocument.Parse(encoder.Encode(Frame()));Assert.Equal(150,changed.RootElement.GetProperty("workshop").GetProperty("credits").GetInt64());
    }
    [Fact] public void FailedDeathSaveBlocksRespawnAndPreservesOldProfileUntilRetry()
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+"-death.json");
        try
        {
            var store=new ProfileStore(path);var r=Start(profiles:store);Equip(r,Weapon(1,6));var before=store.Get("Modules");
            using(var locked=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {Set(Player(r),"Hull",0);Assert.False(r.Respawn(1,new(20)));Assert.Equal(before.Credits,store.Get("Modules").Credits);r.Disconnect(1);Assert.Equal(1,r.Join("Modules"));Assert.Equal(0,r.Snapshot().Players[0].Hull);}
            Assert.True(r.Respawn(1,new(21)));Assert.Equal(100,store.Get("Modules").Credits);Assert.Equal(0,store.Get("Modules").Owned[6]);
        }finally{File.Delete(path);File.Delete(path+".tmp");}
    }
    [Fact] public void CreditPersistenceRetryKeepsPayoutWhenIncomeSkillsChange()
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+"-claim.json");
        try
        {
            var store=new ProfileStore(path);var r=Start(profiles:store);
            using(var locked=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {Enemy(r,1000,hull:0);r.Step();Assert.Single(r.Snapshot().Credits!);Set(Player(r),"Skills",new HashSet<string>{"fortune.3","luckyfind.1"});}
            r.Step();Assert.Equal(105,store.Get("Modules").Credits);Assert.False(r.LastCreditReward(1)!.Lucky);
        }finally{File.Delete(path);File.Delete(path+".tmp");}
    }
    [Fact] public void TenderRepairsOnlyOtherLivingShipsAndSniperTelegraphs()
    {
        var r=Start(ai:true);At(r,new(500,500));var tender=Enemy(r,300,500,id:80,hull:5);tender.GetType().GetProperty("Type")!.SetValue(tender,"tender");Set(tender,"Gunner",true);Set(tender,"MaxHull",5);
        var ally=Enemy(r,320,500,id:81,hull:2);Set(ally,"MaxHull",3);Step(r,15);Assert.InRange(Health(ally),2.2f,2.3f);Assert.Equal(5,Health(tender));
        var sniper=Enemy(r,250,500,id:82,hull:6);sniper.GetType().GetProperty("Type")!.SetValue(sniper,"sniper");Set(sniper,"Gunner",true);Set(sniper,"Level",3);r.Step();
        Assert.True(r.Snapshot().Enemies!.Single(e=>e.Id==82).Telegraph);Assert.DoesNotContain(r.Snapshot().Shots,s=>s.Owner==-82);
        Step(r,30);Assert.Contains(r.Snapshot().Shots,s=>s.Owner==-82);
    }
    [Fact] public void ModuleDropIsPersonalAndGrantsOneOwnedCopy()
    {
        var r=new Room(false,seed:123);r.Join("A");r.Join("B");
        uint salt=1;for(;salt<1000;salt++){Set(r,"creditSalt",salt);if((float)Invoke(r,"CreditRoll",1,1)!<.5f)break;}
        var enemy=((IList)Get(r,"enemies")).Cast<object>().First(e=>(int)Get(e,"Home")==20);var point=(Vector2)Get(enemy,"Position");At(r,point+new Vector2(200,0));
        Invoke(r,"Hit",Shot(1),null,enemy,120f);r.Step();var drop=Assert.Single(r.ForViewer(r.Snapshot(),1).ModuleDrops!);
        Assert.Empty(r.ForViewer(r.Snapshot(),2).ModuleDrops!);var before=r.Workshop(1).Owned[drop.Module];At(r,new(drop.X,drop.Y));r.Step();
        Assert.Empty(r.Snapshot().ModuleDrops!);Assert.Equal(before+1,r.Workshop(1).Owned[drop.Module]);r.Step();Assert.Equal(before+1,r.Workshop(1).Owned[drop.Module]);
    }
    [Fact] public async Task ReconnectingDuringAnEquipDoesNotReplayTheOldSessionReceipt()
    {
        var store=new ProfileStore();var r=Start(profiles:store);store.BackgroundWrites=true;
        var hold=new TaskCompletionSource();_ = store.Schedule(()=>{hold.Task.GetAwaiter().GetResult();return "ok";});
        r.Input(1,new(1,0,0,0,false,false,true));r.Step();Assert.True(r.Equip(1,new(100,Loadout(Weapon(1)))));
        r.Disconnect(1);Assert.Equal(1,r.Join("Modules"));Assert.Equal(0,r.Workshop(1).Sequence);
        hold.SetResult();await store.FlushAsync();r.Step();Assert.Equal(0,r.Workshop(1).Sequence);Assert.Equal("",r.Workshop(1).Result);
    }
}
