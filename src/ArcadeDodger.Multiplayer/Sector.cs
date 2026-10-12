using System.Numerics;
using System.Text.Json.Serialization;
namespace ArcadeDodger.Multiplayer;

public sealed record SectorLocation(int Id,string Kind,string Name,float X,float Y,float Radius,string Biome="",int Parent=0)
{ [JsonIgnore] public Vector2 Position=>new(X,Y); }
public sealed record SectorObstacle(int Id,float X,float Y,float Radius)
{ [JsonIgnore] public Vector2 Position=>new(X,Y); }
public sealed record SectorDefinition(int Seed,float Width,float Height,SectorLocation[] Locations,SectorObstacle[] Obstacles)
{
    public const float DefaultWidth=12000,DefaultHeight=8000;
    public static SectorDefinition Generate(int seed)
    {
        var rng=new Random(seed);var locations=new List<SectorLocation>();var obstacles=new List<SectorObstacle>();
        // A jittered route skeleton guarantees reachable destinations and reproducible geography.
        Vector2 J(float x,float y)=>new(x+rng.Next(-160,161),y+rng.Next(-160,161));
        void Add(int id,string kind,string name,Vector2 p,float radius,string biome="",int parent=0)=>locations.Add(new(id,kind,name,p.X,p.Y,radius,biome,parent));
        var planets=new[]{J(2400,2100),J(5600,1900),J(9000,3300),J(6900,6100)};
        var names=new[]{"Haven","Cinder","Zephyr","The Sundering"};var biomes=new[]{"inhabited","volcanic","gas","shattered"};
        for(var i=0;i<4;i++)Add(1+i,"planet",names[i],planets[i],i==2?760:580,biomes[i]);
        Add(10,"station","Arrival Port",new(1200,1800),90);Add(11,"station","Engineering Station",new(4200,4200),90);Add(12,"station","Frontier Outpost",new(9000,6000),90);
        for(var i=0;i<4;i++)Add(20+i,"base",names[i]+" Hostile Array",planets[i]+new Vector2(650,350),85,"",i+1);
        Add(30,"corridor","North Arrival Corridor",new(7500,180),110);Add(31,"corridor","East Arrival Corridor",new(11820,5000),110);
        for(var i=0;i<6;i++)Add(40+i,"wreck","Salvage Wreck "+(i+1),Vector2.Lerp(planets[i%4],planets[(i+1)%4],.4f)+new Vector2(0,400+i*50),45);
        for(var i=0;i<3;i++)Add(50+i,"beacon","Orbital Survey "+(i+1),planets[i+1]+new Vector2(-500,-400),24);
        Add(60,"thermal","Cinder Thermal Vent",planets[1]+new Vector2(-250,200),170);
        var clusters=new[]{new Vector2(3000,3200),new(4300,900),new(6300,3000),new(10200,2200),new(10200,4700),new(7400,7000),new(5600,6600),new(5800,5300)};
        // Keep all routes between interactable destinations clear, not only the nearest route.
        var nodes=locations.Where(l=>l.Kind is "station" or "base" or "wreck" or "beacon" or "corridor").ToArray();
        for(var c=0;c<clusters.Length;c++)
        {
            Add(70+c,"cluster","Asteroid Field "+(c+1),clusters[c],330);
            var accepted=0;
            for(var n=0;n<240&&accepted<(c>=5?12:7);n++)
            {
                var angle=(float)rng.NextDouble()*MathF.Tau;var p=clusters[c]+new Vector2(MathF.Cos(angle),MathF.Sin(angle))*rng.Next(70,370);var radius=rng.Next(28,64);
                if(locations.Any(l=>l.Kind!="planet"&&l.Kind!="cluster"&&Vector2.Distance(p,l.Position)<l.Radius+radius+130))continue;
                if(nodes.Any(a=>nodes.Any(b=>a.Id<b.Id&&SectorCollision.DistanceToSegment(p,a.Position,b.Position)<radius+55)))continue;
                if(obstacles.Any(o=>Vector2.Distance(o.Position,p)<o.Radius+radius+15))continue;
                obstacles.Add(new(obstacles.Count+1,p.X,p.Y,radius));accepted++;
            }
        }
        return new(seed,DefaultWidth,DefaultHeight,locations.ToArray(),obstacles.ToArray());
    }
}

// Static geometry uses a spatial grid shared by simulation and client prediction.
public sealed class SectorCollision
{
    private readonly SectorDefinition sector;
    private readonly Dictionary<(int,int),List<SectorObstacle>> cells=[];
    public SectorCollision(SectorDefinition sector)
    {
        this.sector=sector;
        foreach(var o in sector.Obstacles)for(var x=(int)((o.X-o.Radius)/256);x<=(int)((o.X+o.Radius)/256);x++)for(var y=(int)((o.Y-o.Radius)/256);y<=(int)((o.Y+o.Radius)/256);y++)
        {if(!cells.TryGetValue((x,y),out var cell))cells[(x,y)]=cell=[];cell.Add(o);}
    }
    public IEnumerable<SectorObstacle> Query(Vector2 a,Vector2 b,float padding=0)
    {
        var seen=System.Buffers.ArrayPool<int>.Shared.Rent(sector.Obstacles.Length+1);
        Array.Clear(seen,0,sector.Obstacles.Length+1);
        try {
        for(var x=(int)MathF.Floor((MathF.Min(a.X,b.X)-padding)/256);x<=(int)MathF.Floor((MathF.Max(a.X,b.X)+padding)/256);x++)
        for(var y=(int)MathF.Floor((MathF.Min(a.Y,b.Y)-padding)/256);y<=(int)MathF.Floor((MathF.Max(a.Y,b.Y)+padding)/256);y++)
            if(cells.TryGetValue((x,y),out var cell))foreach(var o in cell)if(seen[o.Id]==0){seen[o.Id]=1;yield return o;}
        } finally {System.Buffers.ArrayPool<int>.Shared.Return(seen);}
    }
    public (float T,Vector2 Normal)? Trace(Vector2 a,Vector2 b,float radius=0)
    {
        (float T,Vector2 Normal)? hit=null;
        foreach(var o in Query(a,b,radius))if(Circle(a-o.Position,b-o.Position,o.Radius+radius) is {} t && (hit is null||t<hit.Value.T))
        {var normal=Vector2.Lerp(a,b,t)-o.Position;hit=(t,normal.LengthSquared()>.0001f?Vector2.Normalize(normal):Vector2.UnitX);}
        return hit;
    }
    public (float T,Vector2 Normal)? TraceShips(Vector2 from,Vector2 to,float radius)
    {
        var hit=Trace(from,to,radius);
        foreach(var l in sector.Locations.Where(l=>l.Kind=="base"))if(Circle(from-l.Position,to-l.Position,l.Radius+radius) is {} t&&(hit is null||t<hit.Value.T))
        {var normal=Vector2.Lerp(from,to,t)-l.Position;hit=(t,normal.LengthSquared()>.001f?Vector2.Normalize(normal):Vector2.UnitX);}
        return hit;
    }
    public Vector2 Move(Vector2 from,Vector2 to,float radius)
    {var velocity=Vector2.Zero;return Move(from,to,radius,ref velocity);}
    public Vector2 Move(Vector2 from,Vector2 to,float radius,ref Vector2 velocity)
    {
        for(var i=0;i<4;i++)
        {
            var hit=TraceShips(from,to,radius);
            if(hit is null)break;
            velocity-=hit.Value.Normal*MathF.Min(0,Vector2.Dot(velocity,hit.Value.Normal));
            var at=Vector2.Lerp(from,to,MathF.Max(0,hit.Value.T-.0001f))+hit.Value.Normal*.02f;
            var left=to-at;left-=hit.Value.Normal*MathF.Min(0,Vector2.Dot(left,hit.Value.Normal));from=at;to=at+left;
        }
        var bounded=Vector2.Clamp(to,new(radius),new(sector.Width-radius,sector.Height-radius));
        if(bounded.X!=to.X)velocity.X=0;if(bounded.Y!=to.Y)velocity.Y=0;return bounded;
    }
    public static float DistanceToSegment(Vector2 p,Vector2 a,Vector2 b)
    {var d=b-a;return Vector2.Distance(p,a+d*(d.LengthSquared()<.001f?0:Math.Clamp(Vector2.Dot(p-a,d)/d.LengthSquared(),0,1)));}
    public static float? Circle(Vector2 start,Vector2 end,float radius)
    {
        var c=start.LengthSquared()-radius*radius;if(c<=0)return 0;var d=end-start;var a=d.LengthSquared();if(a<.000001f)return null;
        var b=2*Vector2.Dot(start,d);var disc=b*b-4*a*c;if(disc<0)return null;var t=(-b-MathF.Sqrt(disc))/(2*a);return t is >=0 and <=1?t:null;
    }
}
