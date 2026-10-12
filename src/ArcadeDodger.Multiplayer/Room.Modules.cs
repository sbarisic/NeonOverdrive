using System.Numerics;
namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    public const int ModuleCount=29, WeaponSlots=8;
    private Shot CreateModularShot(Player p,float aim,int weapon=0)
    {
        var w=p.Weapons[weapon];var kind=w.Emitter;
        var stats=Stats(p,kind);var range=stats.Range;var speed=stats.Speed;
        return new Shot {Id=++nextShot,Owner=p.Id,OwnerName=p.Name,Weapon=weapon,Position=p.Position,BirthPosition=p.Position,
            Born=Tick,Expires=Tick+Math.Max(180,(int)MathF.Ceiling(range*MathF.Pow(1.3f,w.Attachments.Count(id=>id==7))/MathF.Min(speed,520)*TickRate)+30),Kind=kind,Program=(int[])w.Attachments.Clone(),Range=range,InitialRange=range,
            Velocity=new Vector2(MathF.Cos(aim),MathF.Sin(aim))*speed/TickRate,
            SpeedMultiplier=speed/(kind==3?430:kind==4?520:650), Damage=stats.Damage, VisualPower=stats.Damage/(kind==2?4:kind>=3?2:1),
            Blast=(kind==3?85:kind==4?45:0)*stats.RadiusMultiplier,RadiusMultiplier=stats.RadiusMultiplier,
            SplashMultiplier=stats.SplashMultiplier,ShieldMultiplier=stats.ShieldMultiplier,Pierce=stats.Pierce,StatusPower=stats.StatusPower};
    }

    private static float Gate(Shot s)=>s.Program is not null&&s.Cursor<8?s.InitialRange*(s.Cursor+1)*.05f:float.PositiveInfinity;
    private static int Effects(Shot s)=>(s.Burns>0?1:0)|(s.Rads>0?2:0)|(s.Freezes>0?4:0)|(s.Corrodes>0?8:0)|(s.Teslas>0?16:0);
    private void StepProjectiles()
    {
        var count=shots.Count;
        for(var index=0;index<count&&index<shots.Count;index++)
        {
            var s=shots[index];
            if(s.Expires<=Tick)continue;
            Guide(s); Advance(s,1,0,int.MaxValue);
        }
    }
    // Budget is the remaining fraction of this tick for moving projectiles; beams trace immediately.
    private void Advance(Shot s,float budget,int firstSegment,int segmentLimit)
    {
        for(var guard=0;guard<96&&budget>.00001f&&s.Expires>Tick;guard++)
        {
            if(s.IsBeam&&beams.Count-firstSegment>=segmentLimit)return;
            while(s.Program is not null&&s.Cursor<8&&s.Program[s.Cursor]==0)s.Cursor++;
            if(s.Program is not null&&s.Distance>=s.Range-.001f){if(!s.IsBeam)Explode(s,s.Position,null,null);s.Expires=Tick;return;}
            if(s.Distance+.001f>=Gate(s))
            {
                var module=s.Program![s.Cursor++];
                if(ApplyStage(s,module,budget,firstSegment,segmentLimit))return;
                continue;
            }
            var speed=s.Velocity.Length();if(speed<.00001f)
            {
                foreach(var c in Contacts(s,s.Position,s.Position)) {Contact(s,c.Pilot,c.Alien,s.Position);if(s.Pierce--<=0){s.Expires=Tick;break;}}
                return;
            }
            var direction=s.Velocity/speed;
            var wallX=direction.X>.00001f?(Width-s.Position.X)/direction.X:direction.X<-.00001f?-s.Position.X/direction.X:float.PositiveInfinity;
            var wallY=direction.Y>.00001f?(Height-s.Position.Y)/direction.Y:direction.Y<-.00001f?-s.Position.Y/direction.Y:float.PositiveInfinity;
            var wall=MathF.Max(0,MathF.Min(wallX,wallY));
            var length=MathF.Min(s.IsBeam?float.PositiveInfinity:budget*speed,MathF.Min(wall,MathF.Min(Gate(s)-s.Distance,s.Program is null?float.PositiveInfinity:s.Range-s.Distance)));
            length=MathF.Max(0,length);
            var start=s.Position;var end=start+direction*length;
            var cover=geometry?.Trace(start,end);float? sanctuary=null;
            if(Sector is not null)foreach(var station in Sector.Locations.Where(l=>l.Kind=="station"))
                if(SectorCollision.Circle(start-station.Position,end-station.Position,300) is {} t&&(sanctuary is null||t<sanctuary))sanctuary=t;
            var boundary=cover?.T??2;var absorbed=sanctuary.HasValue&&sanctuary<=boundary;if(absorbed)boundary=sanctuary!.Value;
            if(boundary<=1){end=Vector2.Lerp(start,end,boundary);length*=boundary;}
            var contacts=Contacts(s,start,end,s.IsBeam?1:1-budget,s.IsBeam?1:MathF.Min(1,1-budget+length/speed));var stopped=false;
            foreach(var c in contacts)
            {
                if(c.Pilot is {Hull:<=0}||c.Alien is {Hull:<=0})continue;
                var at=Vector2.Lerp(start,end,c.Time);
                Contact(s,c.Pilot,c.Alien,at);
                if(c.Pilot is not null)s.HitPlayers.Add(c.Pilot.Id);
                if(c.Alien is not null)s.HitEnemies.Add(c.Alien.Id);
                if(s.Proximity||s.Pierce--<=0){end=at;stopped=true;break;}
            }
            var travelled=Vector2.Distance(start,end);
            if(s.IsBeam)
                beams.Add(new(s.Owner,beams.Count-firstSegment,start.X,start.Y,end.X,end.Y,Effects(s),s.Weapon,s.Cursor,s.VisualPower));
            else Pull(s,end,travelled/speed/TickRate);
            s.Position=end;s.Distance+=travelled;
            if(!s.IsBeam)budget-=travelled/speed;
            if(stopped){if(s.IsBeam)Pull(s,end,1f/TickRate);s.Expires=Tick;return;}
            if(boundary<=1)
            {
                if(absorbed){s.Expires=Tick;return;}
                if(s.Bounces--<=0){if(!s.IsBeam)Explode(s,end,null,null);s.Expires=Tick;return;}
                s.Velocity=Vector2.Reflect(s.Velocity,cover!.Value.Normal);s.Position+=cover.Value.Normal*.03f;
                continue;
            }
            if(length>=wall-.001f)
            {
                if(s.Bounces--<=0){if(!s.IsBeam)Explode(s,end,null,null);s.Expires=Tick;return;}
                if(wallX<=wall+.001f)s.Velocity.X=-s.Velocity.X;
                if(wallY<=wall+.001f)s.Velocity.Y=-s.Velocity.Y;
            }
            if(s.IsBeam&&s.Distance>=s.Range-.001f)return;
            if(length<.00001f&&wall>.001f)return;
        }
    }
    // True means the parent ended; descendants continue with the unused tick budget.
    private bool ApplyStage(Shot s,int id,float budget,int firstSegment,int limit)
    {
        if(id==0)return false;
        var d=ModuleCatalog.Get(id);
        switch(id)
        {
            case 5:s.Damage*=d.Value;s.VisualPower*=d.Value;break;
            case 6:if(s.IsBeam)s.Range*=d.Value;else {s.Velocity*=d.Value;s.SpeedMultiplier*=d.Value;};break;
            case 7:s.Range*=d.Value;break;
            case 8:s.Pierce+=(int)d.Value;break;
            case 9:s.Spread*=d.Value;break;
            case 10:s.ShieldMultiplier*=d.Value;break;
            case 11:case 12:case 13:
                var count=(int)d.Value;
                if(s.IsBeam?beams.Count-firstSegment+count>limit:shots.Count+count>ProjectileLimit)break;
                for(var i=0;i<count;i++)
                {
                    var angle=(-d.Radius+2*d.Radius*i/(count-1))*s.Spread;
                    var child=s.Copy();child.Id=++nextShot;child.Parent=s.Id;child.Ancestors=[..s.Ancestors,s.Id];child.Born=Tick;child.BirthPosition=s.Position;
                    child.Damage/=count;child.VisualPower/=count;
                    child.Velocity=Vector2.Transform(s.Velocity,Matrix3x2.CreateRotation(angle));
                    if(id==13)child.Range=child.Distance+(child.Range-child.Distance)*.5f;
                    if(!s.IsBeam)shots.Add(child);
                    Advance(child,budget,firstSegment,limit);
                }
                s.Expires=Tick;return true;
            case 14:s.Homing=true;Guide(s);break;
            case 15:s.Bounces+=(int)d.Value;break;
            case 16:s.Relays++;break;
            case 17:s.Burns++;s.Burning=true;break;
            case 18:s.Freezes++;s.Cryo=true;break;
            case 19:s.Rads++;s.Radiation=true;break;
            case 20:s.Corrodes++;break;
            case 21:s.Teslas++;break;
            case 22:s.Gravity++;break;
            case 23:s.Blasts++;break;
            case 24:s.Proximity=true;break;
            case 25:
                if(s.Kind==4)break;
                if(!s.IsBeam){s.Kind=4;s.Blast=MathF.Max(s.Blast,45*s.RadiusMultiplier);s.Velocity=Vector2.Normalize(s.Velocity)*520*s.SpeedMultiplier/TickRate;break;}
                if(s.EmitPlasma&&shots.Count<ProjectileLimit)
                {
                    var child=s.Copy();child.Id=++nextShot;child.Parent=s.Id;child.Ancestors=[..s.Ancestors,s.Id];child.BirthPosition=s.Position;child.Born=Tick;
                    child.IsBeam=false;child.Kind=4;child.Damage/=2;child.Blast=45*child.RadiusMultiplier;child.Velocity=Vector2.Normalize(s.Velocity)*520*s.SpeedMultiplier/TickRate;child.Expires=Tick+180;
                    shots.Add(child);if(players.TryGetValue(s.Owner,out var p))p.PlasmaReady[s.Weapon]=Tick+15;
                }
                return true;
            case 26:
                if(!s.IsBeam||s.EmitMine)
                {
                    if(AddMine(s))
                    {
                        if(s.IsBeam&&players.TryGetValue(s.Owner,out var p))p.MineReady[s.Weapon]=Tick+30;
                    }
                    else if(!s.IsBeam)
                    {
                        // Keep the payload at its checkpoint and retry when deployment capacity returns.
                        s.Cursor--;s.Expires++;return true;
                    }
                }
                s.Expires=Tick;return true;
            case 27:s.Leeches++;break;
            case 28:s.SalvageMark=true;break;
        }
        return false;
    }
    private readonly List<(float Time,Player? Pilot,Enemy? Alien)> contactBuffer=[];
    private List<(float Time,Player? Pilot,Enemy? Alien)> Contacts(Shot s,Vector2 start,Vector2 end,float startTime=0,float endTime=1)
    {
        var result=contactBuffer;result.Clear();
        foreach(var p in playerGrid.Query(start,end,140))
        {
            if(p.Id==s.Owner||Friendly(s,p)||p.Hull<=0||p.Editing||Safe(p)||Tick<p.ProtectedUntil||s.HitPlayers.Contains(p.Id)||(s.IsBeam&&HasSkill(p,PhaseDash)&&Tick<p.DashUntil))continue;
            var t=Intersection(start-Vector2.Lerp(p.Previous,p.Position,startTime),end-Vector2.Lerp(p.Previous,p.Position,endTime),s.Proximity?Definition(24).Radius:Radius+3);
            if(t.HasValue)result.Add((t.Value,p,null));
        }
        if(s.Owner>0)foreach(var e in enemyGrid.Query(start,end,140))
        {
            if(e.Hull<=0||Sanctuary(e.Position)||s.HitEnemies.Contains(e.Id))continue;
            var t=Intersection(start-Vector2.Lerp(e.Previous,e.Position,startTime),end-Vector2.Lerp(e.Previous,e.Position,endTime),s.Proximity?(e.Installation?85:0)+Definition(24).Radius:EnemyRadius(e)+3);
            if(t.HasValue)result.Add((t.Value,null,e));
        }
        result.Sort(static(a,b)=>{var time=a.Time.CompareTo(b.Time);return time!=0?time:(a.Pilot?.Id??a.Alien!.Id).CompareTo(b.Pilot?.Id??b.Alien!.Id);});return result;
    }
    private void Guide(Shot s)
    {
        if(!s.Homing)return;
        var target=enemyGrid.Query(s.Position,s.Position,600).Where(e=>e.Hull>0&&!Sanctuary(e.Position)&&VisibleLine(s.Position,e.Position)&&!s.HitEnemies.Contains(e.Id)).OrderBy(e=>Vector2.DistanceSquared(e.Position,s.Position)).FirstOrDefault();
        if(target is null)return;var delta=target.Position-s.Position;
        if(delta.LengthSquared()<1||delta.LengthSquared()>Definition(14).Radius*Definition(14).Radius)return;
        var speed=s.Velocity.Length();var direction=s.IsBeam?Vector2.Normalize(delta):Vector2.Lerp(Vector2.Normalize(s.Velocity),Vector2.Normalize(delta),Definition(14).Value);
        if(direction.LengthSquared()>.00001f)s.Velocity=Vector2.Normalize(direction)*speed;
    }
    private void Pull(Shot s,Vector2 at,float seconds)
    {
        if(s.Gravity==0)return;
        foreach(var e in enemyGrid.Query(at,at,100).Where(e=>e.Hull>0&&!e.Installation&&!Sanctuary(e.Position)&&VisibleLine(at,e.Position)).ToArray())
        {var delta=at-e.Position;var distance=delta.Length();if(distance>.001f&&distance<Definition(22).Radius){e.Position=MoveShip(e.Position,e.Position+delta/distance*MathF.Min(distance,Definition(22).Value*s.Gravity*seconds),20);enemyGrid.Move(e,e.Position);}}
    }
}
public sealed record ExplosionSnapshot(int Id,float X,float Y,float Radius,long Born);
