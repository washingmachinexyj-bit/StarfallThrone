using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Fable;

public sealed class FableUI : ModSystem
{
    public static bool Opened;
    internal static bool ConfirmSkip;
    private static int page;
    public override void OnWorldUnload(){Opened=false;ConfirmSkip=false;page=0;global::StarfallThrone.Content.Fable.Trials.FourTrialUI.Opened=false;}
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {int at=layers.FindIndex(x=>x.Name=="Vanilla: Mouse Text");layers.Insert(at<0?layers.Count:at,new LegacyGameInterfaceLayer("StarfallThrone: Fable book",Draw,InterfaceScaleType.UI));}
    public static void Request(int action,int index=0)
    {
        if(action==1&&index==0&&global::StarfallThrone.Content.Oaths.OathWorld.ClosingSeeds){global::StarfallThrone.Content.Oaths.OathUI.Confirm=3;global::StarfallThrone.Content.Oaths.OathWorld.Request(4);return;}
        Player p=Main.LocalPlayer;
        if(Main.netMode==NetmodeID.MultiplayerClient){ModPacket packet=ModContent.GetInstance<FableUI>().Mod.GetPacket();packet.Write((byte)80);packet.Write((byte)action);packet.Write((byte)index);packet.Send();return;}
        if(action==1){if(FableWorld.TrySummon(p,index))Opened=false;}
        else if(action==2)FableWorld.Claim(p,index);
        else if(action==4)FableWorld.Skip(p);
        else if(action==5 && !FableWorld.AnyEncounter)p.GetModPlayer<FablePlayer>().RestTicks=1;
        else if(action==6 && !p.HasItem(FableCatalog.Item("PracticeSword")))FableWorld.Give(p,FableCatalog.Item("PracticeSword"));
    }
    internal static bool Draw()
    {
        if(!Opened || Main.gameMenu)return true;
        if(global::StarfallThrone.Content.Fable.Trials.FourTrialUI.Opened)return global::StarfallThrone.Content.Fable.Trials.FourTrialUI.Draw();
        if(global::StarfallThrone.Content.Oaths.OathUI.SeedTab||global::StarfallThrone.Content.Oaths.OathUI.Confirm!=0)return global::StarfallThrone.Content.Oaths.OathUI.Draw();
        Player p=Main.LocalPlayer;if(p.dead){Opened=false;return true;}
        Vector2 screen=StarfallUI.ScreenSizeUi;float sw=screen.X,sh=screen.Y;
        int width=(int)Math.Min(790,sw-20),height=(int)Math.Min(600,sh-20),left=(int)(sw-width)/2,top=(int)(sh-height)/2;
        Rectangle rect=new(left,top,width,height);Point mouse=StarfallUI.MouseUiPoint;
        var batch=Main.spriteBatch;batch.Draw(TextureAssets.MagicPixel.Value,rect,new Color(28,30,40,245));p.mouseInterface=true;
        bool click=Main.mouseLeft && Main.mouseLeftRelease;
        void Label(string text,int x,int y,float scale=.75f,Color? color=null)=>Utils.DrawBorderString(batch,text,new Vector2(x,y),color??Color.Wheat,scale);
        bool Button(string text,int x,int y,int w,int h=30,bool enabled=true)
        {
            Rectangle box=new(x,y,w,h);bool hover=box.Contains(mouse);
            batch.Draw(TextureAssets.MagicPixel.Value,box,!enabled?new Color(43,43,48):hover?new Color(90,82,64):new Color(57,58,67));
            Label(text,x+8,y+5,.66f,enabled?Color.White:Color.Gray);
            if(hover && click && enabled){Main.mouseLeftRelease=false;click=false;return true;}return false;
        }
        Label(FableCatalog.Text("Title"),left+18,top+14,.95f);
        if(Button(FableCatalog.Text("FourTrialTab"),left+width-480,top+10,200)){global::StarfallThrone.Content.Fable.Trials.FourTrialUI.Opened=true;global::StarfallThrone.Content.Oaths.OathUI.SeedTab=false;global::StarfallThrone.Content.Oaths.OathUI.Confirm=0;ConfirmSkip=false;return true;}
        if(Button(global::StarfallThrone.Content.Oaths.OathCatalog.Text("SeedTab"),left+width-260,top+10,200)){global::StarfallThrone.Content.Oaths.OathUI.SeedTab=true;global::StarfallThrone.Content.Fable.Trials.FourTrialUI.Opened=false;ConfirmSkip=false;return true;}
        if(Button("×",left+width-42,top+10,30)){Opened=false;ConfirmSkip=false;return true;}
        Label(FableCatalog.Text(FableWorld.Skipped?"Skipped":FableWorld.Completed?"Completed":"Sealed"),left+18,top+49,.68f);
        if(ConfirmSkip)
        {
            Label(FableCatalog.Text("Confirm1"),left+18,top+105,.7f);
            Label(FableCatalog.Text("Confirm2"),left+18,top+143,.7f);
            Label(FableCatalog.Text("Confirm3"),left+18,top+181,.7f);
            if(Button(FableCatalog.Text("Back"),left+20,top+235,130)){ConfirmSkip=false;}
            if(Button(FableCatalog.Text("ConfirmSkip"),left+180,top+235,210)){Request(4);ConfirmSkip=false;}
        }
        else
        {
            int visible=1;while(visible<18 && FableWorld.Visible(visible))visible++;
            int rowHeight=60,rows=Math.Max(1,(height-220)/rowHeight);page=Math.Clamp(page,0,(visible-1)/rows);
            for(int j=0;j<rows;j++)
            {
                int i=page*rows+j;if(i>=visible)break;int y=top+90+j*rowHeight;
                Texture2D tex=ModContent.Request<Texture2D>(FableCatalog.Root+"Boss"+i+"_Head_Boss").Value;
                batch.Draw(tex,new Vector2(left+28,y+18),null,Color.White,0,tex.Size()/2,1,SpriteEffects.None,0);
                Label((i+1).ToString("00")+"  "+FableCatalog.Name(i),left+50,y,.68f);
                Label(FableCatalog.Text("Hint"+i),left+50,y+24,.51f,Color.Silver);
                if(Button(FableCatalog.Text(FableWorld.Downed[i]?"Replay":"Challenge"),left+width-170,y,78,28,FableWorld.Unlocked(i) && !FableWorld.AnyEncounter))Request(1,i);
                if(Button(FableCatalog.Text("Claim"),left+width-84,y,65,28,FableWorld.Downed[i]&&!FableWorld.Skipped))Request(2,i);
            }
            int bottom=top+height-116;
            if(Button("<",left+18,bottom,35,28,page>0))page--;
            Label((page+1)+" / "+((visible-1)/rows+1),left+64,bottom+5,.65f);
            if(Button(">",left+135,bottom,35,28,(page+1)*rows<visible))page++;
            if(Button(FableCatalog.Text("Practice"),left+190,bottom,135,28,!FableWorld.Skipped))Request(6);
            if(Button(FableCatalog.Text("Rest"),left+340,bottom,105,28,!FableWorld.AnyEncounter))Request(5);
            if(Button(FableCatalog.Text("Skip"),left+width-220,bottom+40,200,30,FableWorld.Sealed&&!global::StarfallThrone.Content.Oaths.OathWorld.SkipLocked)){ConfirmSkip=true;Request(3);}
            Label(FableCatalog.Text("BookHint"),left+18,bottom+46,.55f,Color.Silver);
        }
        if(Main.keyState.IsKeyDown(Keys.Escape)){Opened=false;ConfirmSkip=false;}
        return true;
    }
    public override void PostDrawTiles()
    {
        var boss=FableWorld.ActiveBoss;
        global::StarfallThrone.Content.Oaths.Seeds.SeedBossNPC seed=null;
        if(boss==null)foreach(NPC n in Main.ActiveNPCs)if(n.ModNPC is global::StarfallThrone.Content.Oaths.Seeds.SeedBossNPC s&&!s.Cancelled){seed=s;break;}
        if(boss==null&&seed==null)return;
        Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone,null,Main.GameViewMatrix.TransformationMatrix);
        Vector2 pos=new((boss?.ArenaCenter.X??seed.ArenaCenter.X)-192,boss?.ArenaFloor??seed.ArenaFloor);
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,new Rectangle((int)(pos.X-Main.screenPosition.X),(int)(pos.Y-Main.screenPosition.Y),384,3),new Color(230,219,188,190));
        Main.spriteBatch.End();
    }
}

public sealed class FableCommand : ModCommand
{
    public override CommandType Type=>CommandType.World|CommandType.Console;
    public override string Command=>"fable";
    public override string Usage=>"/fable [skip confirm]";
    public override string Description=>FableCatalog.Text("Title");
    public override void Action(CommandCaller caller,string input,string[] args)
    {
        if(args.Length==2 && args[0]=="skip" && args[1]=="confirm")
        {if(!FableWorld.Skip(caller.Player??Main.player[0],caller.CommandType==CommandType.Console))caller.Reply(FableCatalog.Text("HostOnly"));return;}
        caller.Reply(FableCatalog.Text(FableWorld.Skipped?"Skipped":FableWorld.Completed?"Completed":"Sealed"));
    }
}
