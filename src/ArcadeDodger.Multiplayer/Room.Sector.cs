using System.Numerics;
namespace ArcadeDodger.Multiplayer;
public sealed record SectorActionMessage(int Sequence,string Action,int Target=0):SequencedMessage(Sequence);
public sealed record SectorReceipt(int Sequence,string Result);
public sealed record LocationState(int Id,int Hull,string State,int LaunchTicks=0);
public sealed record ContractDefinition(int Id,int Station,string Kind,int Target,int Credits,int Experience);
public sealed record SectorSessionSnapshot(int Docked,int LastStation,int[] Discovered,int[] Cells,int Contract,int[] Progress,bool Carrying,bool Ready,int[] Completed,float Interaction,SectorReceipt? Receipt,int RepairCost=0,MapPing[]? Pings=null,string LootNotice="",int[]? CoolingContracts=null,int[]? Contributions=null);
public sealed record MapPing(int Id,int Sender,string Name,int Location,int Pilot,float X,float Y,long Expires,bool Broadcast,int Patrol=0);
public sealed record SectorState(LocationState[] Locations,SectorSessionSnapshot? Pilot=null);
public sealed partial class Room
{
    public SectorDefinition? Sector {get;}
    private readonly SectorCollision? geometry;
    private readonly Dictionary<int,Source> sources=[];
    private readonly Dictionary<int,System.Numerics.Vector2[]> patrolRoutes=[];
    private Vector2[] PatrolRoute(int homeId)
    {
        if(patrolRoutes.TryGetValue(homeId,out var cached))return cached;
        var home=Location(homeId)?.Position??Vector2.Zero;
        var route=Location(homeId)?.Kind=="corridor"?Location(homeId==30?22:23)!.Position:home;
        return patrolRoutes[homeId]=new[]{route+new Vector2(300,-200),route+new Vector2(550,200),route+new Vector2(-250,500),home+new Vector2(-150,-250)}.Select(p=>Vector2.Clamp(p,new(70),new(Width-70,Height-70))).ToArray();
    }
    private readonly Dictionary<string,Exploration> explorers=[];
    private readonly List<MapPing> pings=[];private int nextPing;
    private sealed class Source {public required SectorLocation Location;public int LaunchCount;public long NextLaunch;public long Warning;public bool Destroyed;}
    private sealed class Exploration
    {
        public int LastStation=10,Contract,InteractionTarget,SessionPlayer;public long InteractionStart,LastPing=-150;
        public readonly Dictionary<int,long> ContractCooldown=[];
        public bool Carrying,Ready;public readonly HashSet<int> Discovered=[10],Cells=[],Progress=[],Completed=[];
        public SectorReceipt? Receipt;
    }
    public ContractDefinition[] Contracts=>Sector is null?[]:Enumerable.Range(0,3).SelectMany(i=>new[]{
        new ContractDefinition(i*3+1,10+i,"survey",50,40,30),new(i*3+2,10+i,"recover",40+i*2,60,45),new(i*3+3,10+i,"suppress",20+i,100,75),new(100+i,10+i,"interdict",i==0?30:31,60,45)}).ToArray();
    private Exploration ExplorationFor(Player p)
    {var key=FriendBook.Key(p.Name);if(!explorers.TryGetValue(key,out var state))explorers[key]=state=new();return state;}
    private void PilotDied(Player p)
    {
        if(!p.ResetPending&&!p.ResetComplete)CombatFx(0,2,p.Position,20);
        ResetAfterDeath(p);
        if(Sector is null)return;var s=ExplorationFor(p);if(s.SessionPlayer!=p.Id)return;
        s.Carrying=false;s.InteractionTarget=0;if(Contracts.FirstOrDefault(c=>c.Id==s.Contract)?.Kind=="recover")s.Ready=false;
    }
    private SectorLocation? Location(int id)=>Sector?.Locations.FirstOrDefault(l=>l.Id==id);
    private bool Sanctuary(Vector2 p)=>Sector?.Locations.Any(l=>l.Kind=="station"&&Vector2.DistanceSquared(p,l.Position)<=300*300)==true;
    private bool Safe(Player p)=>p.Docked!=0||Sanctuary(p.Position);
    private bool VisibleLine(Vector2 from,Vector2 to)=>geometry?.Trace(from,to) is null && !(Sector?.Locations.Any(l=>l.Kind=="station"&&SectorCollision.Circle(from-l.Position,to-l.Position,300).HasValue)??false);
    private Vector2 MoveShip(Vector2 from,Vector2 to,float radius)=>geometry?.Move(from,to,radius)??Vector2.Clamp(to,new(radius),new(Width-radius,Height-radius));
    private void InitializeSector()
    {
        if(Sector is null)return;
        foreach(var l in Sector.Locations.Where(l=>l.Kind is "base" or "corridor"))
        {
            sources[l.Id]=new(){Location=l,NextLaunch=l.Kind=="base"?600:1800};
            if(l.Kind!="base")continue;
            enemies.Add(new(){Id=++nextEnemy,Installation=true,Home=l.Id,Position=l.Position,Previous=l.Position,Hull=120,MaxHull=120,Level=EnemyCatalog.Region(l.Id)});
            if(enemiesEnabled)for(var i=0;i<4;i++)Launch(sources[l.Id],i%2==1,i);
        }
    }
    private void Launch(Source source,bool gunner,int offset=0)
    {
        var p=source.Location.Position+new Vector2(120+offset*35,offset*35);
        p=Vector2.Clamp(p,new(25),new(Width-25,Height-25));
        var level=EnemyCatalog.Region(source.Location.Id);
        var available=EnemyCatalog.Items.Where(t=>t.MinimumLevel<=level).ToArray();
        var type=available[source.LaunchCount++%available.Length];
        var hull=(int)MathF.Ceiling(type.Hull*EnemyCatalog.Health(level));
        enemies.Add(new(){Id=++nextEnemy,Type=type.Id,Level=level,MaxHull=hull,Gunner=type.Id!="rammer",Home=source.Location.Id,Position=p,Previous=p,FireAt=Tick+60,State="launch",LaunchUntil=Tick+60,Patrol=offset%4,Hull=hull});
    }
    private void StepSources()
    {
        foreach(var source in sources.Values)
        {
            if(source.Destroyed)continue;
            var corridor=source.Location.Kind=="corridor";var count=enemies.Count(e=>!e.Installation&&e.Hull>0&&e.Home==source.Location.Id);var quota=corridor?9:8;var volley=corridor?3:1;
            if(count+volley>quota)continue;
            if(Tick>=source.NextLaunch-(corridor?150:90)&&source.Warning==0)source.Warning=Tick;
            if(Tick<source.NextLaunch||enemies.Count+volley>EnemyLimit)continue;
            for(var i=0;i<volley;i++)Launch(source,corridor?i==1:nextEnemy%2==0,i);
            source.Warning=0;source.NextLaunch=Tick+(corridor?1800:600);
        }
    }
    private void RecordContribution(Enemy enemy,Shot? shot,float amount)
    {
        if(amount<=0||shot is null||!players.TryGetValue(shot.Owner,out var attacker)||attacker.Hull<=0)return;
        var old=enemy.Contributors.GetValueOrDefault(attacker.Id);
        enemy.Contributors[attacker.Id]=(attacker.RunId,(old.Run==attacker.RunId?old.Damage:0)+amount);
        if(!enemy.Installation||enemy.Contributors[attacker.Id].Damage<1)return;
        var state=ExplorationFor(attacker);if(Contracts.FirstOrDefault(c=>c.Id==state.Contract) is {Kind:"suppress"} c&&c.Target==enemy.Home)state.Progress.Add(enemy.Home);
    }
    private void DestroyInstallation(Enemy enemy)
    {
        if(!enemy.Installation)return;
        sources[enemy.Home].Destroyed=true;sources[enemy.Home].Warning=0;
        foreach(var state in explorers.Values)
            if(Contracts.FirstOrDefault(c=>c.Id==state.Contract) is {Kind:"suppress"} c&&c.Target==enemy.Home)
            {if(state.Progress.Contains(enemy.Home))state.Ready=true;else {state.Contract=0;state.Progress.Clear();state.Ready=false;}}
    }
    public RoomSnapshot ForViewer(RoomSnapshot frame,int id)
    {
        if(Sector is null||!players.TryGetValue(id,out var p))return frame;
        bool Near(float x,float y)=>Vector2.DistanceSquared(p.Position,new(x,y))<=1400*1400;
        return frame with {Players=frame.Players.Select(other=>other.Id==id?other:other with {Weapons=System.Collections.Immutable.ImmutableArray<WeaponConfig>.Empty,WeaponStats=null,UnlockedWeapons=null,Skills=System.Collections.Immutable.ImmutableArray<string>.Empty}).ToArray(),Enemies=frame.Enemies?.Where(e=>Near(e.X,e.Y)).ToArray(),Shots=frame.Shots.Where(e=>e.Owner==id||Near(e.X,e.Y)).ToArray(),
            Beams=frame.Beams?.Where(e=>e.Owner==id||SectorCollision.DistanceToSegment(p.Position,new(e.X,e.Y),new(e.EndX,e.EndY))<1400).ToArray(),
            ModuleDrops=frame.ModuleDrops?.Where(e=>e.Owner==id&&Near(e.X,e.Y)).ToArray(),Energy=frame.Energy?.Where(e=>(e.Owner==0||e.Owner==id)&&Near(e.X,e.Y)).ToArray(),Credits=frame.Credits?.Where(e=>(e.Owner==0||e.Owner==id)&&Near(e.X,e.Y)).ToArray(),Mines=frame.Mines?.Where(e=>Near(e.X,e.Y)).ToArray(),CombatEvents=frame.CombatEvents?.Where(e=>Near(e.X,e.Y)).ToArray(),Explosions=frame.Explosions?.Where(e=>Near(e.X,e.Y)).ToArray()};
    }
    public SectorState? SectorSnapshot(int player)
    {
        if(Sector is null)return null;
        SectorSessionSnapshot? pilot=null;
        if(players.TryGetValue(player,out var p))
        {
            var s=ExplorationFor(p);pilot=new(p.Docked,s.LastStation,s.Discovered.Order().ToArray(),s.Cells.Order().ToArray(),s.Contract,s.Progress.Order().ToArray(),s.Carrying,s.Ready,s.Completed.Order().ToArray(),s.InteractionTarget==0?0:Math.Clamp((Tick-s.InteractionStart)/90f,0,1),s.Receipt,(MaxHullFor(p)-p.Hull+(p.HullFraction>0?1:0))*10,pings.Where(g=>g.Expires>Tick&&(g.Sender==p.Id||g.Broadcast||friends.Mutual(p.Name,g.Name))).ToArray(),Tick<p.LootNoticeUntil?p.LootNotice:"",s.ContractCooldown.Where(c=>c.Value>Tick).Select(c=>c.Key).ToArray(),enemies.Where(e=>e.Installation&&e.Contributors.TryGetValue(p.Id,out var contribution)&&contribution.Run==p.RunId&&contribution.Damage>=1).Select(e=>e.Home).ToArray());
        }
        return new(sources.Values.Select(s=>new LocationState(s.Location.Id,s.Location.Kind=="corridor"?0:enemies.FirstOrDefault(e=>e.Installation&&e.Home==s.Location.Id)?.Hull??0,s.Destroyed?"destroyed":s.Warning>0?"launching":"active",s.Warning>0?(int)Math.Max(0,s.NextLaunch-Tick):0)).ToArray(),pilot);
    }
    public bool SectorAction(int id,SectorActionMessage request)
    {
        if(!players.TryGetValue(id,out var p)||!p.Connected||request.Sequence<=p.LastReceived||Sector is null)return false;
        p.LastReceived=request.Sequence;var state=ExplorationFor(p);var result="invalid_action";
        if(p.Hull<=0)result="ship_destroyed";
        else switch(request.Action)
        {
            case "ping":case "broadcast":case "defend":
                if(Tick-state.LastPing<150){result="ping_cooldown";break;}
                var destination=Location(request.Target);
                Player? ally=null;
                if(request.Action=="defend")
                {
                    ally=players.GetValueOrDefault(request.Target);
                    if(ally is null||ally.Hull<=0||!ally.Connected||!friends.Mutual(p.Name,ally.Name)||!enemies.Any(e=>e.Target==ally.Id&&e.Hull>0)){result="no_threatened_ally";break;}
                }
                else if(destination is null||!state.Discovered.Contains(destination.Id)){result="undiscovered";break;}
                var point=ally?.Position??destination!.Position;
                pings.Add(new(++nextPing,p.Id,p.Name,ally is null?destination!.Id:0,ally?.Id??0,point.X,point.Y,Tick+900,request.Action=="broadcast",ally is null?0:enemies.First(e=>e.Target==ally.Id&&e.Hull>0).Home));
                if(ally is null)foreach(var recipient in players.Values.Where(other=>other.Connected&&(other.Id==p.Id||request.Action=="broadcast"||friends.Mutual(p.Name,other.Name))))ExplorationFor(recipient).Discovered.Add(destination!.Id);
                state.LastPing=Tick;result="ok";break;
            case "dock":
                var station=Location(request.Target);
                if(station?.Kind!="station"||Vector2.Distance(p.Position,station.Position)>180)result="out_of_range";
                else {p.Docked=station.Id;state.LastStation=station.Id;p.Held=null;p.Velocity=Vector2.Zero;state.InteractionTarget=0;result="ok";}break;
            case "undock":
                if(p.Docked==0){result="not_docked";break;}
                p.Position=Location(p.Docked)!.Position+new Vector2(150,0);p.Previous=p.Position;p.Docked=0;p.Held=null;result="ok";break;
            case "repair":
                if(p.Docked==0){result="not_docked";break;}
                var cost=(MaxHullFor(p)-p.Hull+(p.HullFraction>0?1:0))*10;
                if(cost==0||p.EconomyPending){result=cost==0?"already_repaired":"busy";break;}
                p.EconomyPending=true;result="pending";state.Receipt=new(request.Sequence,"pending");
                Persist(()=>profiles.Debit(p.Name,cost),r=>{if(r=="ok"&&p.Hull>0){p.Hull=MaxHullFor(p);p.HullFraction=0;}p.EconomyPending=false;if(state.Receipt?.Sequence==request.Sequence)state.Receipt=new(request.Sequence,r);});
                if(!profiles.BackgroundWrites)result=state.Receipt!.Result;break;
            case "accept":
                var contract=Contracts.FirstOrDefault(c=>c.Id==request.Target);
                if(contract is null||contract.Station!=p.Docked){result="not_docked";break;}
                if(state.Contract!=0||state.Completed.Contains(contract.Id)||state.ContractCooldown.GetValueOrDefault(contract.Id)>Tick){result="unavailable";break;}
                if(contract.Kind=="suppress"&&sources[contract.Target].Destroyed){result="unavailable";break;}
                state.Contract=contract.Id;state.Progress.Clear();state.Carrying=false;state.Ready=false;result="ok";break;
            case "claim":
                var reward=Contracts.FirstOrDefault(c=>c.Id==state.Contract);
                if(reward is null||p.Docked!=reward.Station){result="not_docked";break;}
                if(!state.Ready){result="incomplete";break;}
                if(p.EconomyPending){result="busy";break;}
                p.EconomyPending=true;result="pending";state.Receipt=new(request.Sequence,"pending");
                Persist(()=>profiles.Credit(p.Name,reward.Credits),r=>
                {if(r=="ok"){if(p.Hull>0)p.Experience=Math.Min(MaxExperience,p.Experience+reward.Experience);if(reward.Kind=="interdict")state.ContractCooldown[reward.Id]=Tick+5*60*TickRate;else state.Completed.Add(reward.Id);state.Contract=0;state.Carrying=false;state.Ready=false;state.Progress.Clear();}p.EconomyPending=false;if(state.Receipt?.Sequence==request.Sequence)state.Receipt=new(request.Sequence,r);});
                if(!profiles.BackgroundWrites)result=state.Receipt!.Result;break;

            case "interact":
                var target=Location(request.Target);var active=Contracts.FirstOrDefault(c=>c.Id==state.Contract);
                if(p.Docked!=0||p.Editing||target is null||Vector2.Distance(p.Position,target.Position)>100||!VisibleLine(p.Position,target.Position)){result="out_of_range";break;}
                if(active is null||!(active.Kind=="survey"&&target.Kind=="beacon"&&!state.Progress.Contains(target.Id)||active.Kind=="recover"&&target.Id==active.Target&&!state.Carrying)){result="unavailable";break;}
                state.InteractionTarget=target.Id;state.InteractionStart=Tick;result="ok";break;
            case "cancel":state.InteractionTarget=0;result="ok";break;
        }
        state.Receipt=new(request.Sequence,result);return result=="ok";
    }
    private void StepExploration()
    {
        if(Sector is null)return;
        pings.RemoveAll(p=>p.Expires<=Tick);
        foreach(var p in players.Values.Where(p=>p.Connected))
        {
            var s=ExplorationFor(p);
            if(p.Hull<=0){s.Carrying=false;if(Contracts.FirstOrDefault(c=>c.Id==s.Contract)?.Kind=="recover")s.Ready=false;s.InteractionTarget=0;continue;}
            foreach(var l in Sector.Locations)if(Vector2.DistanceSquared(p.Position,l.Position)<=1200*1200)s.Discovered.Add(l.Id);
            var cx=(int)p.Position.X/200;var cy=(int)p.Position.Y/200;
            for(var x=Math.Max(0,cx-6);x<=Math.Min(59,cx+6);x++)for(var y=Math.Max(0,cy-6);y<=Math.Min(39,cy+6);y++)if(Vector2.DistanceSquared(p.Position,new(x*200+100,y*200+100))<=1200*1200)s.Cells.Add(y*60+x);
            if(s.InteractionTarget!=0)
            {
                var target=Location(s.InteractionTarget)!;
                if(p.Editing||p.Docked!=0||p.Position!=p.Previous||p.Held is {X:not 0} or {Y:not 0}||p.Held?.Fire==true||Vector2.Distance(p.Position,target.Position)>100||p.LastDamage>=s.InteractionStart)s.InteractionTarget=0;
                else if(Tick-s.InteractionStart>=90)
                {if(target.Kind=="beacon"){s.Progress.Add(target.Id);s.Ready=s.Progress.Count>=3;}else {s.Carrying=true;s.Ready=true;}s.InteractionTarget=0;}
            }
            if(Tick%30==0&&!Safe(p)&&!p.Editing)
                foreach(var thermal in Sector.Locations.Where(l=>l.Kind=="thermal"))if(Tick%300>=60&&Tick%300<180&&Vector2.Distance(p.Position,thermal.Position)<thermal.Radius)Damage(p);
        }
    }
}
