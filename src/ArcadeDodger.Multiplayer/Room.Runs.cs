using System.Collections.Immutable;
namespace ArcadeDodger.Multiplayer;

public sealed partial class Room
{
    public bool ContainsRun(string name)=>players.Values.Any(p=>FriendBook.Key(p.Name)==FriendBook.Key(name))||suspended.ContainsKey(FriendBook.Key(name));
    public void SaveRuns(){foreach(var p in players.Values){if(p.Hull<=0)ResetAfterDeath(p);else SaveCheckpoint(p);}}
    private readonly List<(Task<string> Task, Action<string> Complete)> transactions=[];
    private readonly Dictionary<string,Player> suspended=[];
    private void Persist(Func<string> operation,Action<string> complete)
    {
        if(!profiles.BackgroundWrites){complete(operation());return;}
        transactions.Add((profiles.Schedule(operation),complete));
    }
    private void CompleteTransactions()
    {
        // Completion order is the same as durable write order.
        while(transactions.Count>0&&transactions[0].Task.IsCompleted)
        {
            var item=transactions[0];transactions.RemoveAt(0);
            item.Complete(item.Task.IsCompletedSuccessfully?item.Task.Result:"save_failed");
        }
    }
    private RunCheckpoint Checkpoint(Player p)=>new(p.Experience,p.Skills.ToArray(),p.Hull,p.Shield,p.HullFraction,p.ShieldFraction,
        p.WeaponReady.Select(t=>Math.Max(0,t-Tick)).ToArray(),p.PlasmaReady.Select(t=>Math.Max(0,t-Tick)).ToArray(),
        p.MineReady.Select(t=>Math.Max(0,t-Tick)).ToArray(),Math.Max(0,p.ShieldAt-Tick),Math.Max(0,p.HullAt-Tick),p.Velocity.X,p.Velocity.Y);
    private void SaveCheckpoint(Player p)
    {
        if(p.Hull<=0||p.CheckpointPending||p.ResetPending)return;
        var checkpoint=Checkpoint(p);var run=p.RunId;p.CheckpointPending=true;
        Persist(()=>profiles.SaveRun(p.Name,run,checkpoint),_=>p.CheckpointPending=false);
    }
    private void RestoreCheckpoint(Player p,RunCheckpoint? r)
    {
        if(r is null)return;
        p.Experience=r.Experience;p.Skills=r.Skills.Where(s=>SkillCatalog.Get(s)!=null).ToHashSet();
        p.Hull=r.Hull;p.Shield=r.Shield;p.HullFraction=r.HullFraction;p.ShieldFraction=r.ShieldFraction;
        p.WeaponReady=r.WeaponCooldown.Select(t=>Tick+t).ToArray();
        p.PlasmaReady=r.PlasmaCooldown.Select(t=>Tick+t).ToArray();p.MineReady=r.MineCooldown.Select(t=>Tick+t).ToArray();
        p.ShieldAt=Tick+r.ShieldDelay;p.HullAt=Tick+r.HullDelay;
    }
    private void ResetAfterDeath(Player p)
    {
        if(p.ResetComplete||p.ResetPending)return;
        p.ResetPending=true;p.Velocity=System.Numerics.Vector2.Zero;
        foreach(var shot in shots)if(shot.Owner==p.Id)shot.Expires=Tick;
        foreach(var mine in mines)if(mine.Payload.Owner==p.Id)mine.Payload.Cancelled=true;
        pendingEnergy.RemoveAll(e=>e.Owner==p.Id);pendingCredits.RemoveAll(e=>e.Owner==p.Id);pendingModules.RemoveAll(e=>e.Owner==p.Id);
        energy.RemoveAll(e=>e.Owner==p.Id);credits.RemoveAll(e=>e.Owner==p.Id);moduleDrops.RemoveAll(e=>e.Owner==p.Id);
        foreach(var enemy in enemies)
        {if(enemy.BurnSource?.Owner==p.Id){enemy.BurnUntil=0;enemy.BurnSource=null;}if(enemy.RadiationSource?.Owner==p.Id){enemy.RadiationUntil=0;enemy.RadiationSource=null;}}
        var run=p.RunId;
        Persist(()=>profiles.ResetRun(p.Name,run),result=>
        {
            p.ResetPending=false;if(result!="ok")return;
            p.ResetComplete=true;p.Experience=0;p.Skills.Clear();p.Weapons=profiles.Get(p.Name).Weapons.ToImmutableArray();
        });
    }
}
