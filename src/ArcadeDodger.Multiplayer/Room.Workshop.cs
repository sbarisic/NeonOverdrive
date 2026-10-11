using System.Numerics;
using System.Collections.Immutable;
namespace ArcadeDodger.Multiplayer;
public sealed record CreditSnapshot(int Id,float X,float Y,int Value);
public sealed partial class Room
{
    private readonly ProfileStore profiles;
    private sealed record Credit(int Id,Vector2 Position,int Value,long Expires);
    private readonly List<Credit> credits=[];
    private int nextCredit;
    public WorkshopSnapshot Workshop(int id)
    {var p=players[id];var profile=profiles.Get(p.Name);return new(profile.Credits,profile.Owned,profile.Weapons,p.WorkshopSequence,p.WorkshopResult);}
    public bool Buy(int id,BuyMessage request)
    {
        if(!players.TryGetValue(id,out var p)||!p.Connected||request.Sequence<=p.LastReceived)return false;
        p.LastReceived=request.Sequence;p.WorkshopSequence=request.Sequence;
        p.WorkshopResult=p.Hull<=0||!(p.Pending?.Editing??p.Editing)?"open_workshop":profiles.Buy(p.Name,request.Module);
        return p.WorkshopResult=="ok";
    }
    public bool Equip(int id,EquipMessage request)
    {
        if(!players.TryGetValue(id,out var p)||!p.Connected||request.Sequence<=p.LastReceived)return false;
        p.LastReceived=request.Sequence;p.WorkshopSequence=request.Sequence;p.EquipSequence=request.Sequence;
        p.WorkshopResult=p.Hull<=0||!(p.Pending?.Editing??p.Editing)?"open_workshop":profiles.Equip(p.Name,request.Weapons);
        p.EquipAccepted=p.WorkshopResult=="ok";
        if(p.EquipAccepted)p.Weapons=profiles.Get(p.Name).Weapons.ToImmutableArray();
        return p.EquipAccepted;
    }
    private void DropCredits(Enemy e)
    {if(credits.Count<128)credits.Add(new(++nextCredit,e.Position,(int)MathF.Ceiling((e.Gunner?12:5)*(Tick<e.MarkUntil?Definition(28).Value:1)),Tick+30*TickRate));}
    // Room salt is private. Pickup/player rolls remain identical across failed save retries.
    private readonly uint creditSalt=(uint)Random.Shared.NextInt64(1,uint.MaxValue);
    private float CreditRoll(int pickup,int player)
    {var x=creditSalt ^ (uint)pickup*0x9e3779b9u ^ (uint)player*0x85ebca6bu;x^=x>>16;x*=0x7feb352du;x^=x>>15;x*=0x846ca68bu;x^=x>>16;return (x>>8)/16777216f;}
    private void StepCredits()
    {
        credits.RemoveAll(c=>Tick>=c.Expires);
        for(var i=credits.Count-1;i>=0;i--)
        {
            var c=credits[i];var collector=players.Values.Where(p=>p.Hull>0&&p.Connected&&!p.Editing)
                .Select(p=>(Player:p,Contact:Intersection(p.Previous-c.Position,p.Position-c.Position,Radius+12+Value(p,"collector"))))
                .Where(p=>p.Contact.HasValue).OrderBy(p=>p.Contact).ThenBy(p=>p.Player.Id).FirstOrDefault().Player;
            if(collector is not null)
            {
                var chance=Value(collector,"luckyfind",Value(collector,"luck"));
                var lucky=CreditRoll(c.Id,collector.Id)<chance;
                var amount=(int)MathF.Floor(c.Value*(1+Value(collector,"fortune"))*(lucky?2:1));
                if(profiles.Credit(collector.Name,amount)=="ok")
                {collector.CreditReward=new(++collector.CreditSequence,amount,lucky);credits.RemoveAt(i);}
            }
        }
    }
}
