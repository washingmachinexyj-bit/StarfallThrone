using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Ascendant;

public sealed class AscendantChoiceUI : ModSystem
{
    internal static int Tier=-1,Slot=-1;
    public static void Open(int tier,int slot){if(tier is >=0 and <3&&slot is >=0 and <58){Tier=tier;Slot=slot;}}
    public override void OnWorldUnload(){Tier=Slot=-1;}
    public override void Unload(){Tier=Slot=-1;}
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {int at=layers.FindIndex(layer=>layer.Name=="Vanilla: Mouse Text");layers.Insert(at<0?layers.Count:at,new LegacyGameInterfaceLayer("StarfallThrone: Ascendant gift",Draw,InterfaceScaleType.UI));}
    internal static bool Draw()
    {
        if(Tier<0)return true;
        Player player=Main.LocalPlayer;
        if(Main.gameMenu||player.dead||Slot<0||player.inventory[Slot].type!=AscendantCatalog.Cache(Tier)){Tier=Slot=-1;return true;}
        Vector2 screen=StarfallUI.ScreenSizeUi;float sw=screen.X,sh=screen.Y;
        int width=(int)Math.Min(760,sw-24),height=214,left=(int)(sw-width)/2,top=(int)(sh-height)/2;
        Rectangle panel=new(left,top,width,height);Point mouse=StarfallUI.MouseUiPoint;
        var batch=Main.spriteBatch;batch.Draw(TextureAssets.MagicPixel.Value,panel,new Color(16,23,32,245));
        Utils.DrawBorderString(batch,Language.GetTextValue("Mods.StarfallThrone.Ascendant.Choose"),new Vector2(left+16,top+12),AscendantCatalog.Colors[Tier],.9f);
        Utils.DrawBorderString(batch,Language.GetTextValue("Mods.StarfallThrone.Ascendant.ChooseHint"),new Vector2(left+16,top+42),Color.Silver,.65f);
        if(panel.Contains(mouse))player.mouseInterface=true;
        bool click=Main.mouseLeft&&Main.mouseLeftRelease;
        for(int choice=0;choice<4;choice++)
        {
            int cardWidth=(width-40)/4;Rectangle card=new(left+12+choice*(cardWidth+4),top+76,cardWidth,114);
            bool hover=card.Contains(mouse);batch.Draw(TextureAssets.MagicPixel.Value,card,hover?new Color(63,78,90):new Color(29,42,54));
            Item item=new(AscendantCatalog.Weapon(Tier*4+choice));Texture2D tex=TextureAssets.Item[item.type].Value;
            float scale=Math.Min(44f/tex.Width,44f/tex.Height);
            batch.Draw(tex,new Vector2(card.Center.X,card.Y+33),null,Color.White,0,tex.Size()/2,scale,SpriteEffects.None,0);
            Utils.DrawBorderString(batch,item.Name,new Vector2(card.Center.X,card.Y+64),Color.White,.62f,.5f);
            Utils.DrawBorderString(batch,Language.GetTextValue("Mods.StarfallThrone.Ascendant.Class"+choice),new Vector2(card.Center.X,card.Y+89),AscendantCatalog.Colors[Tier],.62f,.5f);
            if(hover&&click)
            {
                Main.mouseLeftRelease=false;
                if(Main.netMode==NetmodeID.MultiplayerClient)
                {ModPacket packet=ModContent.GetInstance<AscendantChoiceUI>().Mod.GetPacket();packet.Write((byte)60);packet.Write((byte)2);packet.Write((byte)Slot);packet.Write((byte)Tier);packet.Write((byte)choice);packet.Send();}
                else Claim(player,Slot,Tier,choice);
                Tier=Slot=-1;return true;
            }
        }
        if(Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape)||(click&&!panel.Contains(mouse)))Tier=Slot=-1;
        return true;
    }
    public static bool Claim(Player player,int slot,int tier,int choice)
    {
        if(Main.netMode==NetmodeID.MultiplayerClient||!player.active||player.dead||player.ghost||slot is <0 or >=58||tier is <0 or >=3||choice is <0 or >=4)return false;
        Item cache=player.inventory[slot];if(cache.type!=AscendantCatalog.Cache(tier)||cache.stack<=0)return false;
        // Decrement before item creation: repeated requests cannot redeem the same cache.
        cache.stack--;if(cache.stack==0)cache.TurnToAir();
        AscendantWorld.Give(player,AscendantCatalog.Weapon(tier*4+choice));
        if(Main.netMode==NetmodeID.Server)NetMessage.SendData(MessageID.SyncEquipment,-1,-1,null,player.whoAmI,slot);
        return true;
    }
}
