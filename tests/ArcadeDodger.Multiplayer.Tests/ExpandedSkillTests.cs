using System.Collections;
using System.Numerics;
using System.Reflection;
using Xunit;
using static ArcadeDodger.Multiplayer.Tests.WorkshopRig;
namespace ArcadeDodger.Multiplayer.Tests;
public class ExpandedSkillTests
{
    static HashSet<string> All(params string[] families)=>SkillCatalog.Items.Where(s=>families.Contains(s.Family)).Select(s=>s.Id).ToHashSet();
    [Theory][InlineData(1,1)][InlineData(2,4)][InlineData(3,2)][InlineData(4,2)]
    public void StrengthAndRateHaveCorrectEmitterSemantics(int emitter,float damage)
    {
        var stats=Room.CalculateWeaponStats(All("strength","rate"),emitter);
        Assert.Equal(damage*1.75f*(emitter==2?1.5f:1),stats.Damage,4);Assert.Equal(1.75f,stats.StatusPower);
        Assert.Equal((emitter==3?18:emitter==4?12:7)/1.5f,stats.Interval,4);
    }
    [Theory][InlineData(1,1.4f,1f/1.4f,1.25f,1f,1f,1)][InlineData(2,1.4f,1f,1f,1.2f,1.25f,0)][InlineData(3,1.15f,1.4f/1.15f,1.25f,1f,1f,0)][InlineData(4,1.4f,1f,1.2f,1f,1f,0)]
    public void EmitterMasteryEffectsAreAdditiveAndDoNotLeak(int emitter,float damage,float splash,float speed,float range,float shield,int pierce)
    {
        var family=new[]{"kinetic","laser","rocket","plasma"}[emitter-1];var stats=Room.CalculateWeaponStats(All(family),emitter);var basic=Room.CalculateWeaponStats([],emitter);
        Assert.Equal(basic.Damage*damage,stats.Damage,4);Assert.Equal(splash,stats.SplashMultiplier,4);Assert.Equal(basic.Speed*speed,stats.Speed,3);Assert.Equal(1300*range,stats.Range,3);Assert.Equal(shield,stats.ShieldMultiplier);Assert.Equal(pierce,stats.Pierce);
        Assert.Equal(emitter>=3?1.2f:1,stats.RadiusMultiplier);Assert.Equal(Room.CalculateWeaponStats([],emitter%4+1),Room.CalculateWeaponStats(All(family),emitter%4+1));
    }
    [Fact] public void RangeAndDynamicsAreAppliedBeforeCheckpoints()
    {
        var s=All("range","dynamics");Assert.Equal(1690,Room.CalculateWeaponStats(s,1).Range,2);Assert.Equal(2197,Room.CalculateWeaponStats(s,2).Range,2);
        var r=Start();Equip(r,Weapon(2,5));Set(Player(r),"Skills",s);Fire(r);Assert.Equal(1000+2197*.05f,r.Snapshot().Beams![0].EndX,2);
    }
    [Fact] public void FractionalCadenceDoesNotLoseRateToPerShotRounding()
    {
        var r=Start();Set(Player(r),"Skills",All("rate"));var shots=(IList)Get(r,"shots");int fired=0;
        for(var i=0;i<300;i++){Fire(r,1,i+1);fired+=shots.Count;shots.Clear();}Assert.InRange(fired,64,65);
    }
    [Fact] public void StrengthScalesDotOnceAndLaserDamageIncludesFireRate()
    {
        var r=Start();Equip(r,Weapon(2,17));Set(Player(r),"Skills",All("strength","rate"));var e=Enemy(r,1200);Fire(r);r.Input(1,new(4,0,0,0,false,false));Step(r,90);
        Assert.Equal(100-10.5f/30-3*1.75f,Health(e),2);
    }
    [Theory][InlineData(0,150,30)][InlineData(1,120,30)][InlineData(2,90,30)][InlineData(3,90,20)][InlineData(4,90,15)]
    public void ShieldRechargeAndFluxRespectExactTimings(int tier,int delay,int interval)
    {
        var r=Start();var learned=All();if(tier>=1)learned.Add("recharge.1");if(tier>=2)learned.Add("recharge.2");if(tier>=3)learned.Add("flux.1");if(tier>=4)learned.Add("flux.2");Set(Player(r),"Skills",learned);Set(Player(r),"Shield",1);
        typeof(Room).GetMethod("Damage",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(r,[Player(r)]);Step(r,delay-1);Assert.Equal(0,r.Snapshot().Players[0].Shield);r.Step();Assert.Equal(1,r.Snapshot().Players[0].Shield);Step(r,interval);Assert.Equal(2,r.Snapshot().Players[0].Shield);
    }
    [Theory][InlineData(1,300)][InlineData(2,150)]
    public void HullRepairStartsAfterTenSecondsThenUsesTierInterval(int tier,int interval)
    {
        var r=Start();Set(Player(r),"Skills",new HashSet<string>{$"repair.{tier}"});Set(Player(r),"Shield",0);
        typeof(Room).GetMethod("Damage",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(r,[Player(r)]);Step(r,299);Assert.Equal(2,r.Snapshot().Players[0].Hull);r.Step();Assert.Equal(3,r.Snapshot().Players[0].Hull);
        Set(Player(r),"Hull",2);Step(r,interval-1);Assert.Equal(2,r.Snapshot().Players[0].Hull);r.Step();Assert.Equal(3,r.Snapshot().Players[0].Hull);Set(Player(r),"Hull",0);Step(r,300);Assert.Equal(0,r.Snapshot().Players[0].Hull);
    }
    [Fact] public void MobilitySnapshotsAndNovaMatchPredictionAndStrength()
    {
        var r=Start();Set(Player(r),"Skills",All("phase","longphase","blink","slipstream","comet","nova","strength"));var p=r.Snapshot().Players[0];Assert.Equal(360,p.MoveSpeed);Assert.Equal(4,p.DashMultiplier);Assert.Equal(60,p.DashCooldownMax);Assert.Equal("long-phase",p.Ability);
        r.Input(1,new(1,1,0,0,false,true));r.Step();Assert.Equal(1048,r.Snapshot().Players[0].X);Assert.Equal(8,r.Snapshot().Shots.Length);Assert.All(((IList)Get(r,"shots")).Cast<object>(),s=>Assert.Equal(1.75f,Get(s,"Damage")));
        var prediction=new LocalPrediction();prediction.Receive(p,0,0);prediction.Input(1,0,0,true,0);Assert.Equal(1048,prediction.Sample(1d/30)!.X,2);
    }
    [Fact] public void IncomeSkillsMultiplyCollectorPayoutAndNeverXp()
    {
        var r=Start();Set(Player(r),"Skills",All("fortune"));Enemy(r,1000,hull:0);r.Step();Assert.Equal(110,r.Workshop(1).Credits);Assert.Equal(10,r.Snapshot().Players[0].Experience);Assert.Equal(10,r.LastCreditReward(1)!.Amount);
        Assert.False(r.LastCreditReward(1)!.Lucky);
    }
    [Fact] public void LuckRollIsStableAndBothOutcomesUseFortuneBeforeRounding()
    {
        var r=Start();Set(r,"creditSalt",123456u);Set(Player(r),"Skills",All("fortune","luck","luckyfind"));var method=typeof(Room).GetMethod("CreditRoll",BindingFlags.NonPublic|BindingFlags.Instance)!;
        var outcomes=new HashSet<bool>();for(var i=1;i<=50;i++){var roll=(float)method.Invoke(r,[i,1])!;Assert.Equal(roll,method.Invoke(r,[i,1]));Enemy(r,1000,id:i,hull:0);r.Step();var reward=r.LastCreditReward(1)!;Assert.Equal(roll<.35f,reward.Lucky);Assert.Equal(reward.Lucky?20:10,reward.Amount);outcomes.Add(reward.Lucky);}Assert.Equal(2,outcomes.Count);
    }
    [Fact] public void CollectionHarvestSiphonAndSalvageApplyOnPickup()
    {
        var r=Start();Set(Player(r),"Skills",All("collector","harvest","siphon","salvage"));Set(Player(r),"Shield",0);Set(Player(r),"ShieldAt",10000L);Set(Player(r),"DashReadyAt",100L);
        Enemy(r,1080,hull:0);r.Step();Assert.Equal(15,r.Snapshot().Players[0].Experience);Assert.Equal(1,r.Snapshot().Players[0].Shield);Assert.Equal(84,r.Snapshot().Players[0].DashCooldown);Assert.Equal(105,r.Workshop(1).Credits);
    }
    [Fact] public void LuckyPayoutRetriesDiskFailureWithoutRerollOrEarlyReceipt()
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+"-luck.json");
        try
        {
            var store=new ProfileStore(path);var r=Start(profiles:store);Set(r,"creditSalt",123456u);Set(Player(r),"Skills",All("fortune","luck","luckyfind"));
            var roll=(float)typeof(Room).GetMethod("CreditRoll",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(r,[1,1])!;
            using(var locked=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {Enemy(r,1000,hull:0);Step(r,4);Assert.Single(r.Snapshot().Credits!);Assert.Equal(100,r.Workshop(1).Credits);Assert.Null(r.LastCreditReward(1));}
            r.Step();Assert.Empty(r.Snapshot().Credits!);Assert.Equal(roll<.35f?120:110,r.Workshop(1).Credits);Assert.Equal(roll<.35f,r.LastCreditReward(1)!.Lucky);
            Assert.Equal(r.Workshop(1).Credits,new ProfileStore(path).Get("Modules").Credits);r.Step();Assert.Equal(1,r.LastCreditReward(1)!.Sequence);
        }finally{File.Delete(path);File.Delete(path+".tmp");}
    }
    [Theory][InlineData("fortune.1",6)][InlineData("fortune.2",7)][InlineData("fortune.3",10)]
    public void FortuneRoundsOnceAtEveryTier(string skill,int credits)
    {var r=Start();Set(Player(r),"Skills",new HashSet<string>{skill});Enemy(r,1000,hull:0);r.Step();Assert.Equal(100+credits,r.Workshop(1).Credits);}
    [Fact] public void LaserMasteryRateAndAmplifierSplitConserveDpsAndConversionKeepsOriginBonuses()
    {
        var r=Start();Equip(r,Weapon(2,5,11,25));Set(Player(r),"Skills",All("laser","strength","rate","dynamics"));Fire(r);
        Assert.Equal(2,r.Snapshot().Shots.Length);var shots=((IList)Get(r,"shots")).Cast<object>().ToArray();
        Assert.Equal(4*1.4f*1.75f*1.5f,shots.Sum(s=>(float)Get(s,"Damage")),3); // double strength at amplifier, divide by two volleys/sec
        Assert.All(shots,s=>{Assert.Equal(1300*1.2f*1.3f,(float)Get(s,"InitialRange"),2);Assert.Equal(1.25f,Get(s,"ShieldMultiplier"));Assert.Equal(520f/30,((Vector2)Get(s,"Velocity")).Length(),3);});
    }
    [Fact] public void KineticPierceAndRocketSplashMasteriesAffectRealContacts()
    {
        var r=Start();Set(Player(r),"Skills",All("kinetic"));var a=Enemy(r,1060);var b=Enemy(r,1100,id:2);Fire(r);r.Input(1,new(4,0,0,0,false,false));Step(r,5);Assert.Equal(98.6f,Health(a),3);Assert.Equal(98.6f,Health(b),3);
        var q=Start();Equip(q,Weapon(3));Set(Player(q),"Skills",All("rocket"));var direct=Enemy(q,1100);var splash=Enemy(q,1100,860,2);Fire(q);q.Input(1,new(4,0,0,0,false,false));Step(q,8);Assert.Equal(97.7f,Health(direct),3);Assert.Equal(97.2f,Health(splash),3);
    }

}
