using System.Text.Json;

namespace ArcadeDodger.Multiplayer;

/// <summary>Ordered persistence worker; publish each complete transaction after durable replacement.</summary>
public sealed class ProfileStore
{
    private readonly string? path;
    public bool BackgroundWrites { get; set; }
    private readonly object writer = new();
    private Task tail = Task.CompletedTask;
    public Task<string> Schedule(Func<string> action)
    {
        lock(writer) { var next = tail.ContinueWith(_ => action(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default); tail=next; return next; }
    }
    public Task FlushAsync() { lock(writer) return tail; }
    public string SaveRun(string name,string runId,RunCheckpoint checkpoint)
    {var p=Get(name);return p.RunId!=runId?"stale_run":Commit(name,p with {Run=checkpoint});}
    public string ResetRun(string name,string previous)
    {var p=Get(name);if(p.RunId!=previous)return "ok";return Commit(name,PilotProfile.Starter() with {RunId=Guid.NewGuid().ToString("N")});}
    public string GrantModule(string name,int module)
    {if(!ModuleCatalog.Valid(module))return "invalid_item";var p=Get(name);if(p.Owned[module]==int.MaxValue)return "inventory_full";p.Owned[module]++;return Commit(name,p);}

    private volatile Dictionary<string,PilotProfile> profiles = new(StringComparer.Ordinal);
    public ProfileStore(string? path=null)
    {
        this.path=path is null?null:Path.GetFullPath(path);
        if(this.path is not null && File.Exists(this.path))
        {
            profiles=JsonSerializer.Deserialize<Dictionary<string,PilotProfile>>(File.ReadAllText(this.path))
                ?? throw new InvalidDataException("Empty profile store.");
            if(profiles.Any(p=>p.Key!=FriendBook.Key(p.Key)||!Valid(p.Value)))
                throw new InvalidDataException("Invalid profile store; preserve and repair it before starting.");
        }
    }
    public PilotProfile Get(string name) => (profiles.GetValueOrDefault(FriendBook.Key(name))??PilotProfile.Starter()).Copy();
    public string Ensure(string name)
    {var p=Get(name);return profiles.ContainsKey(FriendBook.Key(name))&&p.RunId.Length>0?"ok":Commit(name,p with {RunId=Guid.NewGuid().ToString("N")});}
    public static bool ValidLoadout(WeaponConfig[]? weapons,int[] owned)
    {
        if(weapons is null||weapons.Length!=4)return false;
        var used=new int[29];
        foreach(var w in weapons)
        {
            if(w is null||w.Emitter is <0 or >4||w.Attachments is null||w.Attachments.Length!=8)return false;
            if(w.Emitter>0)used[w.Emitter]++;
            foreach(var id in w.Attachments) { if(id!=0&&id is <5 or >28)return false; if(id>0)used[id]++; }
        }
        return Enumerable.Range(1,28).All(i=>used[i]<=owned[i]);
    }
    private static bool Valid(PilotProfile p)=>p is not null&&p.Credits>=0&&p.Owned is {Length:29}&&p.Owned.All(n=>n>=0)&&ValidLoadout(p.Weapons,p.Owned)&&ValidRun(p.Run);
    private static bool ValidRun(RunCheckpoint? r)=>r is null||
        r.Experience is >=0 and <=Room.MaxExperience&&r.Skills is not null&&r.Skills.All(s=>SkillCatalog.Get(s)!=null)&&r.Hull>=0&&r.Shield>=0&&
        float.IsFinite(r.HullFraction)&&r.HullFraction is >=0 and <1&&float.IsFinite(r.ShieldFraction)&&r.ShieldFraction is >=0 and <1&&
        r.DashCooldown>=0&&r.ShieldDelay>=0&&r.HullDelay>=0&&r.WeaponCooldown is {Length:4}&&r.WeaponCooldown.All(t=>double.IsFinite(t)&&t>=0)&&
        r.PlasmaCooldown is {Length:4}&&r.PlasmaCooldown.All(t=>t>=0)&&r.MineCooldown is {Length:4}&&r.MineCooldown.All(t=>t>=0);
    public string Buy(string name,int module)
    {
        if(!ModuleCatalog.Valid(module)||module==1)return "invalid_item";
        var p=Get(name);var price=ModuleCatalog.Get(module).Price;
        if(p.Credits<price)return "insufficient_credits";
        if(p.Owned[module]==int.MaxValue)return "inventory_full";
        p.Owned[module]++;return Commit(name,p with {Credits=p.Credits-price});
    }
    public string Equip(string name,WeaponConfig[] weapons)
    {var p=Get(name);return !ValidLoadout(weapons,p.Owned)?"invalid_inventory":Commit(name,p with {Weapons=weapons.Select(w=>w.Copy()).ToArray()});}
    public string Credit(string name,int amount)
    {var p=Get(name);if(amount<=0||p.Credits>long.MaxValue-amount)return "invalid_credit";return Commit(name,p with {Credits=p.Credits+amount});}
    public string Debit(string name,int amount)
    {var p=Get(name);if(amount<=0)return "invalid_debit";return p.Credits<amount?"insufficient_credits":Commit(name,p with {Credits=p.Credits-amount});}
    private string Commit(string name,PilotProfile profile)
    {
        var next=new Dictionary<string,PilotProfile>(profiles,StringComparer.Ordinal){[FriendBook.Key(name)]=profile.Copy()};
        try
        {
            if(path is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path+".tmp",JsonSerializer.Serialize(next));
                File.Move(path+".tmp",path,true);
            }
        }
        catch(IOException){return "save_failed";}
        catch(UnauthorizedAccessException){return "save_failed";}
        profiles=next;return "ok";
    }
}
