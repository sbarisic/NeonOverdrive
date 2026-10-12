using System.Collections.Immutable;
using System.Diagnostics;
using System.Numerics;
using Xunit;
using Xunit.Abstractions;
using static ArcadeDodger.Multiplayer.Tests.WorkshopRig;
namespace ArcadeDodger.Multiplayer.Tests;
public class SectorLoadTests(ITestOutputHelper output)
{
    [Fact] public void SpreadPilotsWithFourExtremeWeaponsStayBounded()
    {
        var r=new Room(seed:234);Step(r,4000);var all=SkillCatalog.Items.Where(s=>s.Id.StartsWith("strength.")||s.Id.StartsWith("rate.")||s.Id.StartsWith("bay.")).Select(s=>s.Id).ToHashSet();
        for(var i=0;i<24;i++)
        {
            var id=r.Join("Load"+i)!.Value;var p=Player(r,id);var pos=new Vector2(2000+(i%6)*1400,900+(i/6)*1800);
            Set(p,"Position",pos);Set(p,"Previous",pos);Set(p,"Hull",10000);Set(p,"Experience",Room.ExperienceForLevel(15));Set(p,"Skills",new HashSet<string>(all));
            Set(p,"Weapons",new[]{Weapon(2,13,13,13,13),Weapon(2,25,11,11,5),Weapon(2,26),Weapon(3,11,11,5)}.ToImmutableArray());
        }
        var samples=new List<double>();
        for(var tick=1;tick<=180;tick++)
        {
            for(var id=1;id<=24;id++)r.Input(id,new(tick,0,0,(id%8)*.4f,true,false));
            var timer=Stopwatch.StartNew();r.Step();samples.Add(timer.Elapsed.TotalMilliseconds);
            var frame=r.Snapshot();Assert.InRange(frame.Enemies!.Length,0,Room.EnemyCapacity);Assert.InRange(frame.Shots.Length,0,Room.ProjectileCapacity);Assert.InRange(frame.Beams!.Length,0,1536);Assert.InRange(frame.Mines!.Length,0,128);
            foreach(var beams in frame.Beams.GroupBy(b=>b.Owner))Assert.InRange(beams.Count(),0,64);
        }
        samples.Sort();output.WriteLine($"24 pilots, four chains, 180 ticks: mean {samples.Average():0.00} ms, p95 {samples[(int)(samples.Count*.95)]:0.00} ms, max {samples[^1]:0.00} ms");
        Assert.True(samples.Average()<33.33,$"Mean simulation tick exceeds 30 Hz: {samples.Average():0.00} ms");
    }
}
