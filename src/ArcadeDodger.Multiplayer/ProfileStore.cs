using System.Text.Json;

namespace ArcadeDodger.Multiplayer;

/// <summary>Single simulation writer; persist a complete transaction before publishing it.</summary>
public sealed class ProfileStore
{
    private readonly string? path;
    private Dictionary<string,PilotProfile> profiles = new(StringComparer.Ordinal);
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
    public string Ensure(string name) => profiles.ContainsKey(FriendBook.Key(name))?"ok":Commit(name,PilotProfile.Starter());
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
    private static bool Valid(PilotProfile p)=>p is not null&&p.Credits>=0&&p.Owned is {Length:29}&&p.Owned.All(n=>n>=0)&&ValidLoadout(p.Weapons,p.Owned);
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
