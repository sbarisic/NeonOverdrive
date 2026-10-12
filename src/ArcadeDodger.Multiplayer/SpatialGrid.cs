using System.Numerics;
namespace ArcadeDodger.Multiplayer;
// Broad-phase point index. Exact swept/radius tests remain authoritative at each caller.
internal sealed class SpatialGrid<T> where T:class
{
    private readonly Dictionary<(int,int),HashSet<T>> cells=[];
    private readonly Dictionary<T,(int,int)> membership=[];
    private static (int,int) Cell(Vector2 p)=>((int)MathF.Floor(p.X/512),(int)MathF.Floor(p.Y/512));
    public void Rebuild(IEnumerable<T> items,Func<T,Vector2> position)
    {cells.Clear();membership.Clear();foreach(var item in items)Move(item,position(item));}
    public void Move(T item,Vector2 p)
    {
        var key=Cell(p);if(membership.TryGetValue(item,out var old)){if(old==key)return;cells[old].Remove(item);}
        membership[item]=key;if(!cells.TryGetValue(key,out var cell))cells[key]=cell=[];cell.Add(item);
    }
    public IEnumerable<T> Query(Vector2 a,Vector2 b,float pad)
    {
        var min=Cell(Vector2.Min(a,b)-new Vector2(pad));var max=Cell(Vector2.Max(a,b)+new Vector2(pad));
        for(var x=min.Item1;x<=max.Item1;x++)for(var y=min.Item2;y<=max.Item2;y++)if(cells.TryGetValue((x,y),out var cell))foreach(var item in cell)yield return item;
    }
}
