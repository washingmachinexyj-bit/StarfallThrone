using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.Localization;
using StarfallThrone.Content.Fable;
using StarfallThrone.Content.Systems;
namespace StarfallThrone.Content.Oaths;
public sealed class OathUI:ModSystem
{
    public static bool SeedTab;
    public static int Confirm,Selected;
    public override void OnWorldUnload(){SeedTab=false;Confirm=0;Selected=0;}
    public static bool Draw()
    {
        if(!FableUI.Opened||Main.gameMenu)return true;
        var p=Main.LocalPlayer;if(p.dead){FableUI.Opened=false;Confirm=0;return true;}
        Vector2 screen=StarfallUI.ScreenSizeUi;float sw=screen.X,sh=screen.Y;
        int w=(int)Math.Min(790,sw-20),h=(int)Math.Min(600,sh-20),x=(int)(sw-w)/2,y=(int)(sh-h)/2;
        Point mouse=StarfallUI.MouseUiPoint;bool click=Main.mouseLeft&&Main.mouseLeftRelease;
        var b=Main.spriteBatch;b.Draw(TextureAssets.MagicPixel.Value,new Rectangle(x,y,w,h),new Color(23,31,37,248));p.mouseInterface=true;
        void Label(string s,int dx,int dy,float scale=.65f,Color? color=null)=>Utils.DrawBorderString(b,s,new Vector2(x+dx,y+dy),color??Color.Wheat,scale);
        bool Button(string s,int dx,int dy,int width,bool enabled=true)
        {Rectangle r=new(x+dx,y+dy,width,30);bool over=r.Contains(mouse);b.Draw(TextureAssets.MagicPixel.Value,r,!enabled?new Color(42,44,48):over?new Color(87,93,77):new Color(56,65,66));Label(s,dx+6,dy+5,.62f,enabled?Color.White:Color.Gray);if(over&&click&&enabled){click=false;Main.mouseLeftRelease=false;return true;}return false;}
        Label(OathCatalog.Text("Title"),18,14,.9f);
        if(Button("×",w-42,10,30)){FableUI.Opened=false;Confirm=0;return true;}
        if(Button(OathCatalog.Text("FableTab"),w-260,10,200)){SeedTab=false;Confirm=0;return true;}
        if(Confirm!=0)
        {
            string prefix=Confirm==3?"Closure":"Consent";
            Label(OathCatalog.Text(prefix+"1"),18,96,.67f,Color.Gold);
            Label(OathCatalog.Text(prefix+"2"),18,136);
            Label(OathCatalog.Text(prefix+"3"),18,176);
            if(Button(OathCatalog.Text("Back"),20,238,130))Confirm=0;
            if(Button(OathCatalog.Text("Accept"),180,238,220))
            {int mode=Confirm;Confirm=0;if(mode==3)OathWorld.Request(5);else OathWorld.Request(2,Selected);}
        }
        else
        {
            Label(OathCatalog.Text(OathWorld.SkipLocked?"LockedHint":"ChoiceHint"),18,54,.61f);
            for(int i=0;i<3;i++)
            {
                int row=100+i*96;
                Texture2D tex=ModContent.Request<Texture2D>(OathCatalog.Root+"SeedBoss"+i+"_Head_Boss").Value;
                b.Draw(tex,new Vector2(x+34,y+row+18),null,Color.White,0,tex.Size()/2,1,SpriteEffects.None,0);
                Label(Language.GetTextValue("Mods.StarfallThrone.NPCs.SeedBoss"+i+".DisplayName"),60,row,.7f,OathCatalog.Colors[i]);
                Label(OathCatalog.Text("SeedHint"+i),60,row+28,.52f,Color.Silver);
                Label(OathCatalog.Text(OathWorld.SeedWon[i]?"SeedMet":OathWorld.SeedOpen(i)?"Available":"Missed"),60,row+49,.53f);
                if(Button(OathCatalog.Text("Challenge"),w-180,row,85,OathWorld.SeedOpen(i)&&!FableWorld.AnyEncounter))
                {Selected=i;Confirm=1;OathWorld.Request(1,i);}
                if(Button(OathCatalog.Text("Claim"),w-88,row,70,OathWorld.SeedWon[i]))OathWorld.Request(3,i);
            }
            int bottom=Math.Max(405,h-105);
            Label(OathCatalog.Text("SeedRule1"),18,bottom,.58f);
            Label(OathCatalog.Text("SeedRule2"),18,bottom+27,.58f);
            if(Button(OathCatalog.Text("Return"),18,bottom+58,250)){SeedTab=false;Confirm=0;}
        }
        if(Main.keyState.IsKeyDown(Keys.Escape)){FableUI.Opened=false;Confirm=0;}
        return true;
    }
}
