using System.Numerics;

namespace ArcadeDodger.Multiplayer;

public static class TerritoryGeometry
{
    public static Dictionary<int, Vector2[]> Cells(SectorDefinition sector)
    {
        var sites = sector.Locations.Where(l => l.Id is 11 or 12 || l.Kind == "wreck").ToArray();
        var result = new Dictionary<int, Vector2[]>();
        foreach (var site in sites)
        {
            List<Vector2> polygon =
            [
                Vector2.Zero,
                new(sector.Width, 0),
                new(sector.Width, sector.Height),
                new(0, sector.Height),
            ];
            foreach (var other in sites.Where(s => s.Id != site.Id))
            {
                var normal = other.Position - site.Position;
                var midpoint = (other.Position + site.Position) / 2;
                var clipped = new List<Vector2>();
                for (var i = 0; i < polygon.Count; i++)
                {
                    var a = polygon[i];
                    var b = polygon[(i + 1) % polygon.Count];
                    var da = Vector2.Dot(a - midpoint, normal);
                    var db = Vector2.Dot(b - midpoint, normal);
                    if (da <= 0)
                        clipped.Add(a);
                    if ((da <= 0) != (db <= 0))
                        clipped.Add(Vector2.Lerp(a, b, da / (da - db)));
                }
                polygon = clipped;
            }
            result[site.Id] = polygon.ToArray();
        }
        return result;
    }
}
