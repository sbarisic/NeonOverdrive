using System.Numerics;
using Xunit;
namespace ArcadeDodger.Tests;
public class DirectorTests
{
    private static readonly Box Player = new(1426,796,28,28);
    [Fact] public void TelegraphAndLocalAttackBudgetPrecedeFiring()
    {
        var a=new ArenaModel(new Random(5));
        for(int i=0;i<5;i++) a.Enemies.Add(new Enemy { Kind=EnemyKind.Gunner, Position=new(1850,700+i*50), AttackCooldown=0 });
        a.Update(.05f,Player,Player,0);
        Assert.Equal(3,a.Enemies.Count(e=>e.Telegraph>0));
        for(int i=0;i<16;i++) a.Update(.05f,Player,Player,0);
        Assert.Equal(0,a.HostileShotsFired);
        for(int i=0;i<10;i++) a.Update(.05f,Player,Player,0);
        Assert.True(a.HostileShotsFired>0);
        Assert.True(a.Enemies.Count(e=>e.Telegraph>0)<=3);
    }
    [Fact] public void RemoteEnemiesCannotAttackAndProjectilesExpire()
    {
        var a=new ArenaModel(new Random(8));
        a.Enemies.Add(new Enemy { Kind=EnemyKind.Spinner,Position=new(2800,1500),AttackCooldown=0 });
        a.HostileProjectiles.Add(new HostileProjectile { Position=new(0,0),Life=.04f });
        a.Update(.05f,Player,Player,0); Assert.Empty(a.HostileProjectiles);
        for(int i=0;i<20;i++) a.Update(.05f,Player,Player,0);
        Assert.Equal(0,a.HostileShotsFired);
    }
    [Theory] [InlineData(0,EnemyKind.Rammer)] [InlineData(20,EnemyKind.Gunner)] [InlineData(40,EnemyKind.Spinner)]
    public void DirectorProgressesAndSpawnsSafely(float score,EnemyKind expected)
    {
        var a=new ArenaModel(new Random(42)); var kinds=new HashSet<EnemyKind>();
        for(int i=0;i<2500;i++)
        {
            a.Update(.05f,Player,Player,score);
            foreach(var e in a.Enemies) { Assert.True(Vector2.Distance(e.Position,new(1440,810))>=414); kinds.Add(e.Kind); }
            a.Enemies.Clear(); a.HostileProjectiles.Clear();
        }
        Assert.Contains(expected,kinds);
        if(score==0) Assert.Single(kinds);
        if(score==20) Assert.DoesNotContain(EnemyKind.Spinner,kinds);
    }
    [Fact] public void EnemyAndBulletCountsRemainCappedUnderLongSimulation()
    {
        var a=new ArenaModel(new Random(10));
        for(int i=0;i<10000;i++) { a.Update(.05f,Player,Player,100); Assert.InRange(a.Enemies.Count,0,24); Assert.InRange(a.HostileProjectiles.Count,0,240); }
        Assert.True(a.HostileShotsFired>0);
        a.Reset(); Assert.Empty(a.Enemies); Assert.Empty(a.HostileProjectiles); Assert.Equal(0,a.HostileShotsFired);
    }
}
