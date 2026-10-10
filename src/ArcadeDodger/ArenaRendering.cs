using System.Numerics;
using Raylib_cs;
namespace ArcadeDodger;
public sealed partial class GameApp
{
    private static readonly Color HostileColor = new(255, 75, 180, 255);
    private void DrawAliens()
    {
        foreach(var e in model.Arena.Enemies)
        {
            if(!ArenaView.Visible(e.Position,cameraOrigin,80)) continue;
            Vector2 p=e.Position; float r=e.Radius;
            Color color=e.HitFlash>0 && !reducedEffects?Ink:e.Kind==EnemyKind.Rammer?new(255,100,95,255):HostileColor;
            if(!reducedEffects)
            {
                Raylib.DrawCircleV(p,r+9,Raylib.Fade(color,.10f));
                Vector2 engine=p+new Vector2(0,r+5);
                Raylib.DrawCircleV(engine,3+2*MathF.Sin(clock*20),Raylib.Fade(color,.6f));
            }
            if(e.Kind==EnemyKind.Rammer)
            {
                Raylib.DrawTriangle(p+new Vector2(0,-r),p+new Vector2(-r,r),p+new Vector2(r,r),Raylib.Fade(color,.45f));
                Raylib.DrawTriangleLines(p+new Vector2(0,-r),p+new Vector2(-r,r),p+new Vector2(r,r),color);
                Raylib.DrawLineEx(p+new Vector2(-r,0),p+new Vector2(-r-8,-8),2,color);
                Raylib.DrawLineEx(p+new Vector2(r,0),p+new Vector2(r+8,-8),2,color);
            }
            else if(e.Kind==EnemyKind.Gunner)
            {
                Raylib.DrawPoly(p,6,r,30,Raylib.Fade(color,.35f));
                Raylib.DrawPolyLinesEx(p,6,r,30,2,color);
                Raylib.DrawLineEx(p,p+e.WarningAim*(r+14),5,color);
            }
            else
            {
                Raylib.DrawCircleLines((int)p.X,(int)p.Y,r,color);
                for(int i=0;i<4;i++)
                {
                    float a=clock+i*MathF.PI/2; Vector2 d=new(MathF.Cos(a),MathF.Sin(a));
                    Raylib.DrawLineEx(p+d*9,p+d*(r+10),4,color);
                }
            }
            Raylib.DrawCircleV(p,5,Ink);
            if(e.Telegraph>0)
            {
                Raylib.DrawCircleLines((int)p.X,(int)p.Y,r+12+e.Telegraph*12,HostileColor);
                if(e.Kind==EnemyKind.Gunner) Raylib.DrawLineEx(p,p+e.WarningAim*700,1,Raylib.Fade(HostileColor,.65f));
                Raylib.DrawText("!",(int)p.X-4,(int)p.Y-46,20,Ink);
            }
        }
        // Bullets remain solid and high contrast even with reduced effects.
        foreach(var p in model.Arena.HostileProjectiles)
        {
            if(!ArenaView.Visible(p.Position,cameraOrigin)) continue;
            Raylib.DrawCircleV(p.Position,p.Radius+2,new Color(30,0,28,255));
            Raylib.DrawCircleV(p.Position,p.Radius,HostileColor);
            Raylib.DrawCircleV(p.Position,2,Ink);
        }
    }
    private void DrawRadar(Vector2 origin)
    {
        Box radar=new(402,487,122,40);
        Raylib.DrawRectangleRec(new Rectangle(radar.X,radar.Y,radar.Width,radar.Height),new Color(5,12,17,255));
        Raylib.DrawLine(718,490,718,524,ConsoleBorder);
        for(int i=1;i<7;i++)
            Raylib.DrawLine(402+i*122/7,487,402+i*122/7,527,Raylib.Fade(ConsoleBorder,.35f));
        for(int i=1;i<3;i++)
            Raylib.DrawLine(402,487+i*40/3,524,487+i*40/3,Raylib.Fade(ConsoleBorder,.35f));
        Raylib.DrawRectangleLinesEx(new Rectangle(radar.X,radar.Y,radar.Width,radar.Height),1,ConsoleBorder);
        Vector2 top=ArenaView.Map(origin,radar),bottom=ArenaView.Map(origin+new Vector2(GameModel.Width,GameModel.Height),radar);
        Raylib.DrawRectangleLinesEx(new Rectangle(top.X,top.Y,bottom.X-top.X,bottom.Y-top.Y),1,Ink);
        Vector2 player=new(model.Player.X+14,model.Player.Y+14);
        foreach(var p in model.Combat.Pickups) Raylib.DrawCircleV(ArenaView.Map(p.Position,radar),2,ConsoleCyan);
        foreach(var e in model.Arena.Enemies)
        {
            if(Vector2.Distance(e.Position,player)>1000) continue;
            Raylib.DrawCircleV(ArenaView.Map(e.Position,radar),2,new Color(255,100,95,255));
            if(Vector2.Distance(e.Position,player)>700 || ArenaView.Visible(e.Position,origin,0)) continue;
            Vector2 screen=e.Position-origin;
            Vector2 edge=Vector2.Clamp(screen,new(24,50),new(936,465));
            Raylib.DrawCircleLines((int)edge.X,(int)edge.Y,e.Telegraph>0?12:7,HostileColor);
            Vector2 direction=Vector2.Normalize(screen-edge);
            Raylib.DrawLineEx(edge,edge+direction*12,3,HostileColor);
            if(e.Telegraph>0) Raylib.DrawText("!",(int)edge.X-4,(int)edge.Y-10,18,Ink);
        }
        Raylib.DrawCircleV(ArenaView.Map(player,radar),3,Ink);
        Raylib.DrawCircle(538,495,2,Ink);
        Raylib.DrawText("YOU",547,490,10,ConsoleMuted);
        Raylib.DrawCircle(538,509,2,ConsoleCyan);
        Raylib.DrawText("PICKUP",547,504,10,ConsoleMuted);
        Raylib.DrawCircle(538,523,2,new Color(255,100,95,255));
        Raylib.DrawText("HOSTILE",547,518,10,ConsoleMuted);
    }
}
