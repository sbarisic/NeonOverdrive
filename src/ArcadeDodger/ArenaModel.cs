using System.Numerics;
namespace ArcadeDodger;
public enum EnemyKind { Rammer, Gunner, Spinner }
public sealed class Enemy
{
    public EnemyKind Kind { get; set; }
    public Vector2 Position { get; set; }
    public float Radius { get; set; } = 20;
    public int Health { get; set; } = 2;
    public float HitFlash { get; set; }
    public float Telegraph { get; set; }
    public Vector2 WarningAim { get; set; }
    public float AttackCooldown { get; set; } = 1.5f;
    internal int Burst;
    internal float Phase;
    public Box Bounds => new(Position.X-Radius,Position.Y-Radius,Radius*2,Radius*2);
}
public sealed class HostileProjectile
{
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public float Radius { get; set; } = 5;
    public float Life { get; set; } = 6;
}
public sealed class ArenaModel(Random random)
{
    public const int EnemyCap = 24, ProjectileCap = 240;
    public List<Enemy> Enemies { get; } = new();
    public List<HostileProjectile> HostileProjectiles { get; } = new();
    public List<HostileProjectile> HostileShots => HostileProjectiles;
    public int HostileShotsFired { get; private set; }
    private float spawn;
    public void Reset() { Enemies.Clear(); HostileProjectiles.Clear(); HostileShotsFired=0; spawn=0; }
    public bool Update(float dt, Box player, Box previous, float score)
    {
        if(dt<=0) return false;
        Vector2 center=new(player.X+player.Width/2,player.Y+player.Height/2);
        spawn+=dt;
        if(spawn>=Math.Max(.7f,2.3f-score*.012f))
        {
            spawn=0;
            if(Enemies.Count<EnemyCap) for(int i=0;i<30;i++)
            {
                Vector2 p=new(30+random.NextSingle()*(GameModel.ArenaWidth-60),30+random.NextSingle()*(GameModel.ArenaHeight-60));
                float distance=Vector2.Distance(p,center);
                if(distance<420 || distance>1100 || Enemies.Count(e=>Vector2.Distance(e.Position,center)<700)>=6) continue;
                var kind=(EnemyKind)random.Next(score<12?1:score<28?2:3);
                Enemies.Add(new Enemy { Kind=kind, Position=p, Health=kind==EnemyKind.Rammer?2:kind==EnemyKind.Gunner?3:4 }); break;
            }
        }
        bool lethal=false;
        int active=Enemies.Count(e=>e.Telegraph>0 || e.Burst>0);
        foreach(var e in Enemies)
        {
            e.HitFlash=Math.Max(0,e.HitFlash-dt); e.Phase+=dt;
            Vector2 old=e.Position, delta=center-old;
            float distance=delta.Length(); Vector2 direction=distance>.001f?delta/distance:Vector2.UnitY;
            Vector2 velocity=e.Kind==EnemyKind.Rammer?direction*110:
                e.Kind==EnemyKind.Gunner?direction*(distance>420?80:distance<320?-70:0):
                direction*(distance>470?65:distance<350?-65:0)+new Vector2(-direction.Y,direction.X)*65;
            e.Position=Vector2.Clamp(old+velocity*dt,new(e.Radius),new(GameModel.ArenaWidth-e.Radius,GameModel.ArenaHeight-e.Radius));
            lethal |= Sweep(old,e.Position,previous,player,e.Radius);
            if(e.Kind==EnemyKind.Rammer) continue;
            if(distance>700) { if(e.Telegraph>0 || e.Burst>0) active--; e.Telegraph=0; e.Burst=0; e.AttackCooldown=Math.Max(e.AttackCooldown,1); continue; }
            if(e.Telegraph>0)
            {
                e.Telegraph=Math.Max(0,e.Telegraph-dt);
                if(e.Telegraph==0) { e.Burst=e.Kind==EnemyKind.Gunner?3:2; e.AttackCooldown=0; }
            }
            else if(e.Burst>0)
            {
                e.AttackCooldown-=dt;
                if(e.AttackCooldown<=0)
                {
                    if(e.Kind==EnemyKind.Gunner) Fire(e.Position,e.WarningAim,220);
                    else for(int i=0;i<12;i++) { if(i%4==0) continue; float a=i*MathF.Tau/12+e.Phase*.3f; Fire(e.Position,new(MathF.Cos(a),MathF.Sin(a)),145); }
                    e.Burst--; e.AttackCooldown=e.Burst>0?.24f:3.2f;
                    if(e.Burst==0) active--;
                }
            }
            else
            {
                e.AttackCooldown-=dt;
                if(e.AttackCooldown<=0 && active<3) { e.Telegraph=.9f; e.WarningAim=direction; active++; }
            }
        }
        for(int i=HostileProjectiles.Count-1;i>=0;i--)
        {
            var p=HostileProjectiles[i]; Vector2 old=p.Position; p.Position+=p.Velocity*dt; p.Life-=dt;
            lethal |= Sweep(old,p.Position,previous,player,p.Radius);
            if(p.Life<=0 || p.Position.X<0 || p.Position.Y<0 || p.Position.X>GameModel.ArenaWidth || p.Position.Y>GameModel.ArenaHeight) HostileProjectiles.RemoveAt(i);
        }
        return lethal;
    }
    private void Fire(Vector2 position, Vector2 direction,float speed)
    {
        if(HostileProjectiles.Count>=ProjectileCap) return;
        HostileProjectiles.Add(new() { Position=position, Velocity=direction*speed }); HostileShotsFired++;
    }
    private static bool Sweep(Vector2 a,Vector2 b,Box previous,Box next,float radius)
    {
        b-=new Vector2(next.X-previous.X,next.Y-previous.Y);
        float enter=0,exit=1; Vector2 d=b-a;
        for(int axis=0;axis<2;axis++)
        {
            float start=axis==0?a.X:a.Y,delta=axis==0?d.X:d.Y;
            float min=(axis==0?previous.X:previous.Y)-radius,max=(axis==0?previous.X+previous.Width:previous.Y+previous.Height)+radius;
            if(MathF.Abs(delta)<.00001f) { if(start<min || start>max) return false; }
            else { float t1=(min-start)/delta,t2=(max-start)/delta; enter=Math.Max(enter,Math.Min(t1,t2));exit=Math.Min(exit,Math.Max(t1,t2));if(enter>exit)return false; }
        }
        return true;
    }
}
