using System;
using System.Numerics;
using ArcadeDodger.Multiplayer;
using Raylib_cs;

namespace ArcadeDodger.Web;

// Screen-space presentation only. The host owns input, menus and respawn requests.
// Uniformly scaled logical HUD, anchored to the current viewport edges.
internal static class BrowserArenaHud
{
    private static readonly Color Cyan = new(65, 245, 235, 255);
    private static readonly Color Pink = new(255, 65, 130, 255);
    private static readonly Color Amber = new(255, 210, 80, 255);
    private static readonly Color Muted = new(125, 166, 184, 255);
    private static readonly Color Border = new(40, 112, 132, 255);
    private static readonly Color Enemy = new(255, 100, 95, 255);
    private static readonly Color Ink = NeonRendering.Ink;

    private static float width=960,height=540,cell=44,headerHeight=60;
    private static Rectangle footer,grid,map;
    private static readonly List<Vector2> groups=[];
    public static string Configure(double w,double h)
    {
        width=(float)w;height=(float)h;cell=Math.Clamp(height*.05f,44,60);
        var gridWidth=3*cell+24;var gridHeight=2*cell+19;
        var stacked=width<700;
        var footerHeight=gridHeight+24+(stacked?84:0);
        footer=new(8,height-footerHeight-8,width-16,footerHeight);
        grid=new(width-20-gridWidth,height-20-gridHeight,gridWidth,gridHeight);
        map=new(width/2-90,stacked?footer.Y+12:footer.Y+(footerHeight-72)/2,180,52);
        groups.Clear();float x=20,y=18;
        foreach(var size in new float[]{90,188,60,210,140})
        {if(x+size>width-16&&x>20){x=20;y+=46;}groups.Add(new(x,y));x+=size+12;}
        headerHeight=y+38;
        return System.FormattableString.Invariant($"{grid.X},{grid.Y},{grid.Width},{grid.Height},{cell},{headerHeight+8},{footer.X},{footer.Y},{footer.Width},{footer.Height},{map.X},{map.Y},{map.Width},{map.Height}");
    }
    public static void Draw(RoomSnapshot? snapshot,int ownId,Vector2 origin,Vector2 view,Vector2 worldView,float scale,float time,bool menuOpen,bool reducedEffects)
    {
        if(groups.Count==0)Configure(960,540);
        var self=snapshot?.Players.FirstOrDefault(p=>p.Id==ownId);
        Raylib.BeginMode2D(new Camera2D{Zoom=Raylib.GetScreenWidth()/width});
        Panel(8,8,(int)width-16,(int)headerHeight-8,Cyan);
        var p=groups[0];Text($"PILOTS {snapshot?.Players.Count(p=>p.Connected)??0}/{Room.Capacity}",(int)p.X,(int)p.Y+10,12,Muted);
        if(self is not null)
        {
            p=groups[1];Text($"HULL {self.Hull}/{self.MaxHull}",(int)p.X,(int)p.Y,12,Ink);Meter((int)p.X+110,(int)p.Y+4,66,5,self.Hull/(float)self.MaxHull,Cyan);
            Text($"SHIELD {self.Shield}/{self.MaxShield}",(int)p.X,(int)p.Y+18,12,Ink);Meter((int)p.X+110,(int)p.Y+22,66,5,self.Shield/(float)self.MaxShield,new Color(100,165,255,255));
            p=groups[2];if(self.Protected)Text("SAFE",(int)p.X,(int)p.Y+10,12,Amber);
            p=groups[3];var level=Math.Clamp(self.Level,1,100);var floor=Room.ExperienceForLevel(level);var ceiling=Room.ExperienceForLevel(level+1);
            Text($"LV {level}",(int)p.X,(int)p.Y,12,Cyan);Right(level==100?"MAX":$"XP {self.Experience}/{ceiling}",(int)p.X+198,(int)p.Y,12,Muted);
            Meter((int)p.X,(int)p.Y+24,198,5,level==100?1:Math.Clamp((self.Experience-floor)/(float)(ceiling-floor),0,1),Cyan);
        }
        p=groups[4];Text($"{snapshot?.Workshop?.Credits??0} CREDITS",(int)p.X,(int)p.Y+10,12,Amber);
        Panel((int)footer.X,(int)footer.Y,(int)footer.Width,(int)footer.Height,Cyan);
        DrawRadar(snapshot,ownId,origin,worldView,time,reducedEffects);
        if(menuOpen||self is null||self.Hull<=0)Raylib.DrawRectangle(0,(int)headerHeight,(int)width,Math.Max(0,(int)(footer.Y-headerHeight)),new Color(4,12,22,165));
        Raylib.EndMode2D();
    }
    private static void DrawRadar(RoomSnapshot? snapshot,int ownId,Vector2 origin,Vector2 view,float time,bool reducedEffects)
    {
        Raylib.DrawRectangleRec(map,new Color(5,12,17,255));
        for(var i=1;i<4;i++)Raylib.DrawLine((int)(map.X+i*map.Width/4),(int)map.Y,(int)(map.X+i*map.Width/4),(int)(map.Y+map.Height),Raylib.Fade(Border,.4f));
        Raylib.DrawRectangleLinesEx(map,1,Border);
        var top=Map(origin,map);var bottom=Map(origin+view,map);
        Raylib.DrawRectangleLinesEx(new Rectangle(top.X,top.Y,bottom.X-top.X,bottom.Y-top.Y),1,Muted);
        if(snapshot is not null)
        {
            foreach(var drop in snapshot.Energy??[])Raylib.DrawCircleV(Map(new(drop.X,drop.Y),map),1.5f,Cyan);
            foreach(var enemy in snapshot.Enemies??[])if(enemy.Hull>0)Raylib.DrawCircleV(Map(new(enemy.X,enemy.Y),map),2,Enemy);
            foreach(var player in snapshot.Players)
            {
                if(player.Hull<=0)continue;var at=Map(new(player.X,player.Y),map);
                if(player.Id==ownId){Raylib.DrawCircleLines((int)at.X,(int)at.Y,4,Cyan);Raylib.DrawCircleV(at,2.5f,Ink);}
                else Raylib.DrawRectangle((int)at.X-2,(int)at.Y-2,4,4,player.Connected?Pink:Muted);
            }
        }
        var y=(int)(map.Y+map.Height+10);var x=(int)map.X;
        DotLabel(x+3,y,Ink,"YOU");DotLabel(x+48,y,Pink,"PILOT");DotLabel(x+104,y,Enemy,"NPC");DotLabel(x+150,y,Cyan,"XP");
    }

    private static Vector2 Map(Vector2 world, Rectangle map)
    {
        // Inset markers so edge spawns stay inside the radar border.
        return new Vector2(map.X + 3 + Math.Clamp(world.X / Room.Width, 0, 1) * (map.Width - 6),
            map.Y + 3 + Math.Clamp(world.Y / Room.Height, 0, 1) * (map.Height - 6));
    }

    private static void DrawOverlay(string title, string summary, string action, string hint, Color accent)
    {
        Panel(180, 167, 600, 205, accent);
        Center(title, 190, 28, accent);
        Raylib.DrawLine(204, 232, 756, 232, Border);
        Center(summary, 251, 14, Ink);
        Center(action, 290, 14, accent);
        Center(hint, 337, 10, Muted);
    }

    private static void Pips(int x, int y, int value, int max, Color color)
    {
        for (var i = 0; i < max; i++) Raylib.DrawRectangle(x + i * 11, y, 8, 7, i < value ? color : Border);
    }

    private static void Meter(int x, int y, int width, int height, float progress, Color color)
    {
        Raylib.DrawRectangle(x, y, width, height, Border);
        Raylib.DrawRectangle(x, y, (int)(width * Math.Clamp(progress, 0, 1)), height, color);
    }

    private static void DotLabel(int x, int y, Color color, string label)
    {
        Raylib.DrawCircle(x, y + 3, 2, color);
        Text(label, x + 6, y, 8, Muted);
    }

    private static void Panel(int x, int y, int width, int height, Color accent) =>
        NeonRendering.DrawConsolePanel(x, y, width, height, accent);
    private static void Text(string text, int x, int y, int size, Color color) =>
        Raylib.DrawText(text, x, y, size, color);
    private static void Right(string text, int x, int y, int size, Color color) =>
        Text(text, x - Raylib.MeasureText(text, size), y, size, color);
    private static void Center(string text, int y, int size, Color color, int width = 960) =>
        Text(text, (width - Raylib.MeasureText(text, size)) / 2, y, size, color);
}
