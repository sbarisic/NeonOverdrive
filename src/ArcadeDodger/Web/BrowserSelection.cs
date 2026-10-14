using System.Numerics;
using System.Text.Json;
using ArcadeDodger.Multiplayer;
using Raylib_cs;

namespace ArcadeDodger.Web;

internal sealed record SelectableContact(
    string Key,
    int Id,
    string Name,
    string Faction,
    string Role,
    string State,
    int Level,
    int Hull,
    int MaxHull,
    int Shield,
    int MaxShield,
    float Distance,
    float U,
    float V,
    float Radius,
    float RadarU,
    float RadarV,
    bool Radio
);

internal sealed partial class BrowserArena
{
    private string selectedContact = "",
        hoveredContact = "";

    public void Select(string selected, string hovered)
    {
        selectedContact = selected;
        hoveredContact = hovered;
        world.SelectedContact = selected;
    }

    private IEnumerable<SelectableContact> Contacts()
    {
        var self = snapshot?.Players.FirstOrDefault(p => p.Id == ownId);
        if (self is null || self.Hull <= 0 || snapshot is null)
            yield break;
        SelectableContact Make(
            string key,
            int id,
            string name,
            string faction,
            string role,
            string state,
            int level,
            int hull,
            int maxHull,
            int shield,
            int maxShield,
            float x,
            float y,
            float radius
        )
        {
            var screen = new Vector2(x, y) - origin + cameraKick;
            var radar = BrowserArenaHud.ContactMap(new(x, y));
            return new(
                key,
                id,
                name,
                faction,
                role,
                state,
                level,
                hull,
                maxHull,
                shield,
                maxShield,
                Vector2.Distance(new(self.X, self.Y), new(x, y)),
                screen.X / view.X,
                screen.Y / view.Y,
                radius / view.X,
                radar.X,
                radar.Y,
                key.StartsWith("npc:")
                    && (
                        snapshot.Sector?.World?.Contacts?.Any(c => c.Id == id) == true
                        || snapshot.Sector?.Services?.Contacts.Any(c => c.Id == id) == true
                    )
            );
        }
        foreach (var p in snapshot.Players)
            if (
                p.Id != ownId
                && p.Hull > 0
                && Vector2.DistanceSquared(new(self.X, self.Y), new(p.X, p.Y))
                    <= self.DetectionRadius * self.DetectionRadius
            )
                yield return Make(
                    "player:" + p.Id,
                    p.Id,
                    p.Name,
                    "pilot",
                    "pilot",
                    p.Connected ? "connected" : "disconnected",
                    p.Level,
                    p.Hull,
                    p.MaxHull,
                    p.Shield,
                    p.MaxShield,
                    p.X,
                    p.Y,
                    ChassisCatalog.Get(p.Chassis).Radius
                );
        foreach (var e in snapshot.Enemies ?? [])
            if (
                e.Hull > 0
                && Vector2.DistanceSquared(new(self.X, self.Y), new(e.X, e.Y))
                    <= self.DetectionRadius * self.DetectionRadius
            )
                yield return Make(
                    "npc:" + e.Id,
                    e.Id,
                    snapshot.Sector?.World?.Contacts?.FirstOrDefault(c => c.Id == e.Id)?.Name
                        ?? snapshot
                            .Sector?.Services?.Contacts.FirstOrDefault(c => c.Id == e.Id)
                            ?.Name
                        ?? $"{e.Role} {e.Id}",
                    e.Faction,
                    snapshot.Sector?.Services?.Contacts.FirstOrDefault(c => c.Id == e.Id)?.Role
                        ?? e.Role,
                    e.Activity,
                    e.Level,
                    e.Hull,
                    e.MaxHull,
                    e.Shield,
                    e.MaxShield,
                    e.X,
                    e.Y,
                    e.Radius
                );
    }

    public string ContactJson() =>
        JsonSerializer.Serialize(Contacts().ToArray(), SnapshotJson.Default.SelectableContactArray);

    private void DrawSelection()
    {
        foreach (var c in Contacts())
        {
            if (c.Key != selectedContact && c.Key != hoveredContact)
                continue;
            var pos = origin - cameraKick + new Vector2(c.U * view.X, c.V * view.Y);
            var r = c.Radius * view.X + 8;
            var color =
                c.Key == selectedContact
                    ? new Color(255, 213, 105, 255)
                    : new Color(235, 250, 255, 255);
            for (var i = 0; i < 4; i++)
            {
                var corner = pos + new Vector2((i % 2 == 0 ? -1 : 1) * r, (i < 2 ? -1 : 1) * r);
                var sx = i % 2 == 0 ? 1 : -1;
                var sy = i < 2 ? 1 : -1;
                Raylib.DrawLineEx(corner, corner + new Vector2(sx * 8, 0), 2, color);
                Raylib.DrawLineEx(corner, corner + new Vector2(0, sy * 8), 2, color);
            }
            BrowserText.DrawText(
                c.Name + " · " + c.Faction,
                (int)(pos.X - r),
                (int)(pos.Y - r - 22),
                11,
                color
            );
        }
    }
}
