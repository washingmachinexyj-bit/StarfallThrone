using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace StarfallThrone.Content.Pantheon;

public sealed class PantheonWorld : ModSystem
{
    public static bool[] Downed = new bool[17];
    public static bool IsDowned(int i)=>i>=0&&i<17&&Downed[i];
    public static bool CanSummon(int i)=>i>=0&&i<17&&(i==0?Voyage.VoyageWorld.IsDowned(16):IsDowned(i-1));
    public static void SetDowned(int i)
    {
        if(Main.netMode==NetmodeID.MultiplayerClient||i<0||i>=17||Downed[i])return;
        Downed[i]=true;
        if(Main.netMode==NetmodeID.Server)NetMessage.SendData(MessageID.WorldData);
        int station=Array.IndexOf(PantheonRecipes.StationUnlocks,i);
        if(station>=0&&!Main.gameMenu)
        {
            string key="Mods.StarfallThrone.Pantheon.StationUnlocked";
            string name=Language.GetTextValue("Mods.StarfallThrone.Items.PantheonStation"+station+".DisplayName");
            if(Main.netMode==NetmodeID.Server)Terraria.Chat.ChatHelper.BroadcastChatMessage(NetworkText.FromKey(key,name),Color.LightGoldenrodYellow);
            else Main.NewText(Language.GetTextValue(key,name),Color.LightGoldenrodYellow);
        }
    }
    public static bool TrySummon(int i,int playerIndex)
    {
        if(Main.netMode==NetmodeID.MultiplayerClient||playerIndex<0||playerIndex>=Main.maxPlayers)return false;
        Player p=Main.player[playerIndex];
        if(!PantheonSummonBase.Valid(p,i)||p.HeldItem.type!=PantheonCatalog.Summon(i))return false;
        var state=p.GetModPlayer<PantheonSummonPlayer>();if(state.Cooldown>0)return false;
        state.Cooldown=120;
        Vector2 at=p.Center+new Vector2(-p.direction*440,-180);
        at.X=MathHelper.Clamp(at.X,192,Math.Max(193,Main.maxTilesX*16-192));
        at.Y=MathHelper.Clamp(at.Y,192,Math.Max(193,Main.maxTilesY*16-192));
        int type=PantheonCatalog.BossType(i);NPC.SpawnBoss((int)at.X,(int)at.Y,type,playerIndex);
        return NPC.AnyNPCs(type);
    }
    public override void OnWorldLoad()=>Downed=new bool[17];
    public override void OnWorldUnload()=>Downed=new bool[17];
    public override void SaveWorldData(TagCompound tag){for(int i=0;i<17;i++)if(Downed[i])tag["PantheonDowned"+i]=true;}
    public override void LoadWorldData(TagCompound tag){Downed=new bool[17];for(int i=0;i<17;i++)Downed[i]=tag.GetBool("PantheonDowned"+i);}
    public override void NetSend(BinaryWriter w){for(int i=0;i<17;i++)w.Write(Downed[i]);}
    public override void NetReceive(BinaryReader r)
    {
        if(r.BaseStream.Length-r.BaseStream.Position<17)return;
        bool[] next=new bool[17];for(int i=0;i<17;i++)next[i]=r.ReadBoolean();Downed=next;
    }
}
public sealed class PantheonSummonPlayer : ModPlayer
{
    public int Cooldown;
    public override void PostUpdate(){if(Cooldown>0)Cooldown--;}
    public override void Initialize()=>Cooldown=0;
}
public sealed class PantheonCommand : ModCommand
{
    public override string Command=>"pantheon";
    public override CommandType Type=>CommandType.Chat;
    public override string Usage=>"/pantheon";
    public override string Description=>Language.GetTextValue("Mods.StarfallThrone.Pantheon.CommandDescription");
    public override void Action(CommandCaller caller,string input,string[] args)
    {
        caller.Reply(Language.GetTextValue("Mods.StarfallThrone.Pantheon.CommandHeader"),Color.LightGoldenrodYellow);
        for(int i=0;i<17;i++)
        {
            string status=Language.GetTextValue("Mods.StarfallThrone.Pantheon."+(PantheonWorld.IsDowned(i)?"Defeated":PantheonWorld.CanSummon(i)?"Available":"Locked"));
            caller.Reply((i+1)+". "+Language.GetTextValue("Mods.StarfallThrone.Pantheon.Name"+i)+" — "+status,
                PantheonWorld.IsDowned(i)?Color.LightGreen:PantheonWorld.CanSummon(i)?Color.White:Color.Gray);
        }
    }
}
