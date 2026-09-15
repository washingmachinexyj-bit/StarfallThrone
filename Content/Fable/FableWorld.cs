using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using StarfallThrone.Content.Fable.Bosses;

namespace StarfallThrone.Content.Fable;

public sealed class FableWorld : ModSystem
{
    public static bool[] Downed = new bool[18];
    public static bool Skipped;
    public static int Revision;
    public static bool Completed => Downed.All(x => x);
    public static bool Sealed => !Skipped && !Completed;
    public static bool Unlocked(int i) => FableCatalog.Valid(i) && !Skipped && (i == 0 || Downed[i-1]);
    public static bool Visible(int i) => FableCatalog.Valid(i) && (i == 0 || Downed[i-1]);
    public static FableBossNPC ActiveBoss => Main.npc.FirstOrDefault(n => n.active && n.ModNPC is FableBossNPC b && !b.Cancelled)?.ModNPC as FableBossNPC;
    public static bool AnyEncounter => Main.npc.Any(n => n.active && (n.boss || n.ModNPC is FableBossNPC or global::StarfallThrone.Content.Oaths.Seeds.SeedBossNPC || n.ModNPC is global::StarfallThrone.Content.NPCs.MiniBossNPC { Dormant: false }));
    public override void ClearWorld() { Downed = new bool[18]; Skipped = false; Revision = 0; }
    public override void SaveWorldData(TagCompound tag)
    { tag["Version"] = 1; tag["Victories"] = Mask(); tag["Skipped"] = Skipped; }
    public override void LoadWorldData(TagCompound tag)
    { Apply(tag.GetInt("Victories"), tag.GetBool("Skipped")); }
    public static int Mask() { int mask=0; for(int i=0;i<18;i++) if(Downed[i]) mask|=1<<i; return mask; }
    private static void Apply(int mask, bool skipped)
    {
        // A damaged/imported save cannot expose a non-contiguous chapter or bypass the hidden boss.
        Downed = new bool[18]; bool prefix=true;
        for(int i=0;i<18;i++) { prefix &= (mask & (1<<i)) != 0; Downed[i]=prefix; }
        Skipped = skipped && !Completed; Revision++;
    }
    public override void NetSend(BinaryWriter writer) { writer.Write(Mask()); writer.Write(Skipped); }
    public override void NetReceive(BinaryReader reader)
    { int mask=reader.ReadInt32(); bool skipped=reader.ReadBoolean(); Apply(mask,skipped); }
    public static void Sync()
    { Revision++; if(Main.netMode==NetmodeID.Server) NetMessage.SendData(MessageID.WorldData); }
    public static bool CanAdmin(Player player) => Main.netMode==NetmodeID.SinglePlayer || Main.netMode==NetmodeID.Server && NetMessage.DoesPlayerSlotCountAsAHost(player.whoAmI);
    public static void Say(Player player,string key)
    {
        if(Main.netMode==NetmodeID.SinglePlayer) Main.NewText(FableCatalog.Text(key),Color.Wheat);
        else if(Main.netMode==NetmodeID.Server) Terraria.Chat.ChatHelper.SendChatMessageToClient(NetworkText.FromKey("Mods.StarfallThrone.Fable."+key),Color.Wheat,player.whoAmI);
    }
    public static bool Skip(Player player, bool console=false)
    {
        if(Main.netMode==NetmodeID.MultiplayerClient || !Sealed || (!console && !CanAdmin(player))) return false;
        if(global::StarfallThrone.Content.Oaths.OathWorld.SkipLocked){global::StarfallThrone.Content.Oaths.OathWorld.Notice(player,"OathSealed");return false;}
        Skipped=true;
        foreach(NPC n in Main.ActiveNPCs) if(n.ModNPC is FableBossNPC b) b.CancelEncounter();
        foreach(NPC n in Main.ActiveNPCs) if(n.ModNPC is global::StarfallThrone.Content.Oaths.Seeds.SeedBossNPC b) b.CancelEncounter();
        Sync(); Say(player,"SkippedNotice"); return true;
    }
    public static bool TrySummon(Player player,int index)
    {
        if(Main.netMode==NetmodeID.MultiplayerClient || !player.active || player.dead || player.ghost || !Unlocked(index)) return false;
        if(index==0 && global::StarfallThrone.Content.Oaths.OathWorld.ClosingSeeds && !player.GetModPlayer<global::StarfallThrone.Content.Oaths.OathPlayer>().ClosureApproved){global::StarfallThrone.Content.Oaths.OathWorld.Notice(player,"Closure1");return false;}
        if(AnyEncounter || Main.invasionType>0 || Main.pumpkinMoon || Main.snowMoon || Terraria.GameContent.Events.DD2Event.Ongoing) { Say(player,"Busy"); return false; }
        if(!HasBook(player)) return false;
        if(Sealed && !FablePlayer.CleanEquipment(player)) { Say(player,"RemoveGear"); return false; }
        if(!FindArena(player,out Vector2 center,out float floor)) { Say(player,"NoSpace"); return false; }
        int slot=NPC.NewNPC(player.GetSource_ItemUse(player.HeldItem),(int)(center.X+100),(int)(floor-50),FableCatalog.BossType(index));
        if(slot<0 || slot>=Main.maxNPCs || Main.npc[slot].ModNPC is not FableBossNPC boss) return false;
        boss.ArenaCenter=center; boss.ArenaFloor=floor; Main.npc[slot].target=player.whoAmI;
        // Explicit participants only. Nearby unprepared players do not silently lose equipment effects.
        Vector2 initiatorPosition=player.Center;
        foreach(Player p in Main.ActivePlayers)
        {
            if(p.dead || p.ghost || p.Distance(initiatorPosition)>480 || Sealed && !FablePlayer.CleanEquipment(p)) continue;
            FablePlayer fp=p.GetModPlayer<FablePlayer>(); fp.Begin(slot, center, floor);
        }
        if(player.GetModPlayer<FablePlayer>().EncounterSlot!=slot) player.GetModPlayer<FablePlayer>().Begin(slot,center,floor);
        Main.npc[slot].netUpdate=true;
        if(Main.netMode==NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC,-1,-1,null,slot);
        return true;
    }
    public static bool FindArena(Player player,out Vector2 center,out float floor)
    {
        float x=MathHelper.Clamp(player.Center.X,400,Main.maxTilesX*16-400);
        float start=MathHelper.Clamp(player.Bottom.Y,480,Main.maxTilesY*16-160);
        for(float y=start;y>=320;y-=32)
        {
            Vector2 corner=new(x-224,y-224);
            if(!Collision.SolidCollision(corner,448,240) && !Collision.WetCollision(corner,448,240))
            { floor=y; center=new(x,y-96); return true; }
        }
        center=default; floor=0; return false;
    }
    public static bool HasBook(Player p) => p.inventory.Any(item=>item.type==ModContent.ItemType<FableBook>() && item.stack>0);
    public static void RecordVictory(NPC npc,int index,bool[] participants)
    {
        if(Main.netMode==NetmodeID.MultiplayerClient || !Unlocked(index)) return;
        bool first=!Downed[index]; Downed[index]=true;
        for(int i=0;i<Main.maxPlayers;i++)
        {
            Player p=Main.player[i]; if(!p.active || i>=participants.Length || !participants[i]) continue;
            p.GetModPlayer<FablePlayer>().End();
            Give(p,FableCatalog.Weapon(index));
            if(index==17) Give(p,FableCatalog.Item("FableCore"),Main.rand.Next(6,9)+(first?6:0));
        }
        Sync();
        if(index==17)
        {
            if(Main.netMode==NetmodeID.SinglePlayer) Main.NewText(FableCatalog.Text("CompleteNotice"),Color.Wheat);
            else Terraria.Chat.ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Mods.StarfallThrone.Fable.CompleteNotice"),Color.Wheat);
        }
    }
    public static void Give(Player p,int type,int count=1)
    {
        int slot=Item.NewItem(p.GetSource_Misc("FableReward"),p.Hitbox,type,count,noBroadcast:true,noGrabDelay:true);
        if(slot>=Main.maxItems) return;
        Main.item[slot].playerIndexTheItemIsReservedFor=p.whoAmI;
        if(Main.netMode==NetmodeID.Server)
        { NetMessage.SendData(MessageID.SyncItem,-1,-1,null,slot); NetMessage.SendData(MessageID.ItemOwner,-1,-1,null,slot); }
    }
    public static bool Claim(Player p,int index)
    {
        if(Main.netMode==NetmodeID.MultiplayerClient || Skipped || !FableCatalog.Valid(index) || !Downed[index] || !HasBook(p)) return false;
        int type=FableCatalog.Weapon(index);
        if(!p.inventory.Any(item=>item.type==type && item.stack>0)) Give(p,type);
        return true;
    }
    // Full payloads are read before any mutation. Requests cannot supply a player slot, position, victory mask or skip state.
    public static void ReceivePacket(BinaryReader reader,int sender)
    {
        byte action=reader.ReadByte();
        if(action==10)
        {
            int slot=reader.ReadInt16(); Vector2 center=new(reader.ReadSingle(),reader.ReadSingle()); float floor=reader.ReadSingle();Vector2 original=new(reader.ReadSingle(),reader.ReadSingle());
            if(Main.netMode==NetmodeID.MultiplayerClient && slot>=0 && slot<Main.maxNPCs && float.IsFinite(center.X) && float.IsFinite(center.Y) && float.IsFinite(floor) && float.IsFinite(original.X) && float.IsFinite(original.Y))
                Main.LocalPlayer.GetModPlayer<FablePlayer>().Begin(slot,center,floor,false,original);
            return;
        }
        if(action==11)
        {
            int slot=reader.ReadInt16();
            if(Main.netMode==NetmodeID.MultiplayerClient && Main.LocalPlayer.GetModPlayer<FablePlayer>().EncounterSlot==slot)Main.LocalPlayer.GetModPlayer<FablePlayer>().End();
            return;
        }
        int index=reader.ReadByte();
        if(Main.netMode!=NetmodeID.Server || sender<0 || sender>=Main.maxPlayers) return;
        Player p=Main.player[sender]; var state=p.GetModPlayer<FablePlayer>();
        if(!p.active || p.dead || p.ghost || !HasBook(p) || state.RequestCooldown>0 && action!=4) return;
        state.RequestCooldown=30;
        if(action==1) TrySummon(p,index);
        else if(action==2) Claim(p,index);
        else if(action==3 && CanAdmin(p)) state.SkipWindow=600;
        else if(action==4 && state.SkipWindow>0 && CanAdmin(p)) { state.SkipWindow=0; Skip(p); }
        else if(action==5 && !AnyEncounter) state.RestTicks=1;
        else if(action==6 && !p.inventory.Any(i=>i.type==FableCatalog.Item("PracticeSword"))) Give(p,FableCatalog.Item("PracticeSword"));
    }
}
