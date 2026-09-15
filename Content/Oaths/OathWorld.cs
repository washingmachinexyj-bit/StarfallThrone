using System;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Localization;
using StarfallThrone.Content.Fable;
using StarfallThrone.Content.Divine;
using StarfallThrone.Content.Ascendant;
using StarfallThrone.Content.Pantheon;

namespace StarfallThrone.Content.Oaths;
public sealed class OathWorld:ModSystem
{
    public static bool[] SeedWon=new bool[3],SupremeWon=new bool[3];
    public static bool SkipLocked=>SeedWon.Any(x=>x);
    public static bool SeedOpen(int i)=>OathCatalog.Valid(i)&&!FableWorld.Skipped&&(!FableWorld.Downed[0]||FableWorld.Completed&&SeedWon[i]);
    public static bool SupremeOpen(int i)=>OathCatalog.Valid(i)&&PantheonWorld.Downed[16]&&SeedWon[i]&&DivineWorld.Downed[i]&&AscendantWorld.Downed[i];
    public static bool ClosingSeeds=>!FableWorld.Downed[0]&&SeedWon.Any(x=>!x)&&!FableWorld.Skipped;
    public override void ClearWorld(){SeedWon=new bool[3];SupremeWon=new bool[3];}
    private static byte Mask(bool[] a){byte v=0;for(int i=0;i<3;i++)if(a[i])v|=(byte)(1<<i);return v;}
    private static bool[] Flags(int v)=>new[]{(v&1)!=0,(v&2)!=0,(v&4)!=0};
    public override void SaveWorldData(TagCompound tag){tag["Seeds"]=(int)Mask(SeedWon);tag["Supreme"]=(int)Mask(SupremeWon);}
    public override void LoadWorldData(TagCompound tag){SeedWon=Flags(tag.GetInt("Seeds"));SupremeWon=Flags(tag.GetInt("Supreme"));}
    public override void NetSend(BinaryWriter w){w.Write(Mask(SeedWon));w.Write(Mask(SupremeWon));}
    public override void NetReceive(BinaryReader r){int seeds=r.ReadByte(),supreme=r.ReadByte();SeedWon=Flags(seeds);SupremeWon=Flags(supreme);}
    public static void Notice(Player p,string key)
    {if(Main.netMode==0)Main.NewText(OathCatalog.Text(key),Microsoft.Xna.Framework.Color.Wheat);else if(Main.netMode==2)Terraria.Chat.ChatHelper.SendChatMessageToClient(NetworkText.FromKey("Mods.StarfallThrone.Oaths."+key),Microsoft.Xna.Framework.Color.Wheat,p.whoAmI);}
    public static void RecordSupreme(NPC npc,int i)
    {
        if(Main.netMode==1||!SupremeOpen(i))return;
        bool first=!SupremeWon[i];SupremeWon[i]=true;
        if(first)foreach(Player p in Main.ActivePlayers)if(npc.playerInteraction[p.whoAmI])FableWorld.Give(p,OathCatalog.Item("SupremeChoice"+i));
        Mining.OathMiningWorld.Unlock(i);FableWorld.Sync();
    }
    public static void RecordSeed(NPC npc,int i,bool[] participants)
    {
        if(Main.netMode==1||!SeedOpen(i))return;
        bool first=!SkipLocked;SeedWon[i]=true;
        for(int j=0;j<Main.maxPlayers;j++)if(Main.player[j].active&&participants[j])
        {Player p=Main.player[j];p.GetModPlayer<FablePlayer>().End();FableWorld.Give(p,OathCatalog.Item("SeedToken"+i));FableWorld.Give(p,OathCatalog.Item("SeedWeapon"+i));if(first)Notice(p,"OathSealed");}
        FableWorld.Sync();
    }
    public static void Request(int action,int i=0)
    {
        if(Main.netMode==1){var p=ModContent.GetInstance<OathWorld>().Mod.GetPacket();p.Write((byte)90);p.Write((byte)action);p.Write((byte)i);p.Send();}
        else Execute(Main.LocalPlayer,action,i);
    }
    public static void ReceivePacket(BinaryReader r,int sender)
    {int action=r.ReadByte(),i=r.ReadByte();if(Main.netMode==2&&sender>=0&&sender<Main.maxPlayers)Execute(Main.player[sender],action,i);}
    private static void Execute(Player p,int action,int i)
    {
        if(!p.active||p.dead||p.ghost||!FableWorld.HasBook(p))return;
        var state=p.GetModPlayer<OathPlayer>();if(state.Cooldown>0&&action!=2&&action!=5)return;state.Cooldown=20;
        if(action==1&&OathCatalog.Valid(i)&&SeedOpen(i))
        {if(!SkipLocked&&!FableWorld.CanAdmin(p)){Notice(p,"HostConsent");return;}state.SeedConsent=i;state.ConsentTicks=600;}
        else if(action==2&&state.ConsentTicks>0&&state.SeedConsent==i)
        {state.ConsentTicks=0;Seeds.SeedBossNPC.TrySummon(p,i);}
        else if(action==3&&OathCatalog.Valid(i)&&SeedWon[i])
        {if(!p.HasItem(OathCatalog.Item("SeedToken"+i)))FableWorld.Give(p,OathCatalog.Item("SeedToken"+i));if(!p.HasItem(OathCatalog.Item("SeedWeapon"+i)))FableWorld.Give(p,OathCatalog.Item("SeedWeapon"+i));}
        else if(action==4&&ClosingSeeds){state.ClosureWindow=600;}
        else if(action==5&&state.ClosureWindow>0){state.ClosureApproved=true;FableWorld.TrySummon(p,0);state.ClosureApproved=false;state.ClosureWindow=0;}
    }
}
public sealed class OathPlayer:ModPlayer
{
    public int SeedConsent=-1,ConsentTicks,Cooldown,ClosureWindow;
    public bool ClosureApproved;
    public override void OnEnterWorld(){ConsentTicks=Cooldown=ClosureWindow=0;ClosureApproved=false;SeedConsent=-1;}
    public override void PostUpdate(){if(ConsentTicks>0)ConsentTicks--;if(Cooldown>0)Cooldown--;if(ClosureWindow>0)ClosureWindow--;}
}
