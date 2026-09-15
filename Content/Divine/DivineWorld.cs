using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using StarfallThrone.Content.Divine.Bosses;

namespace StarfallThrone.Content.Divine;

public sealed class DivineWorld : ModSystem
{
    public static bool[] Downed = new bool[3];
    public static Guid WorldKey;
    internal static readonly Dictionary<Guid, byte> Claims = new();
    private static readonly bool[] observedOpen = new bool[3];
    private bool initialized;
    public static bool Open(int index) => index is >=0 and <3 && global::StarfallThrone.Content.Oaths.OathWorld.SeedWon[index] && (index switch {0 => !NPC.downedSlimeKing,1 => !Main.hardMode,2 => !NPC.downedMoonlord,_ => false});
    public static bool IsDowned(int index) => index is >=0 and <3 && Downed[index];
    public override void ClearWorld()
    { Downed = new bool[3]; WorldKey = Guid.Empty; Claims.Clear(); initialized = false; Array.Clear(observedOpen); }
    public override void OnWorldLoad() { if (Main.netMode != NetmodeID.MultiplayerClient && WorldKey == Guid.Empty) WorldKey = Guid.NewGuid(); }
    public override void SaveWorldData(TagCompound tag)
    {
        tag["DivineVersion"] = 1; tag["WorldKey"] = WorldKey.ToString("N");
        tag["Victories"] = (Downed[0]?1:0)|(Downed[1]?2:0)|(Downed[2]?4:0);
        tag["Claims"] = Claims.Select(pair => new TagCompound { ["Player"] = pair.Key.ToString("N"), ["Mask"] = (int)pair.Value }).ToList();
    }
    public override void LoadWorldData(TagCompound tag)
    {
        int saved=tag.GetInt("Victories"); Downed = new[]{(saved&1)!=0,(saved&2)!=0,(saved&4)!=0};
        WorldKey = Guid.TryParse(tag.GetString("WorldKey"),out Guid key) && key != Guid.Empty ? key : Guid.NewGuid();
        Claims.Clear();
        foreach (TagCompound entry in tag.GetList<TagCompound>("Claims"))
            if (Guid.TryParse(entry.GetString("Player"),out Guid id) && id != Guid.Empty) Claims[id] = (byte)(entry.GetInt("Mask") & 7);
        initialized = false;
    }
    public override void NetSend(BinaryWriter writer)
    { writer.Write(WorldKey.ToByteArray()); for (int i=0;i<3;i++) writer.Write(Downed[i]); }
    public override void NetReceive(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(16); if(bytes.Length !=16) return;
        bool[] incoming = {reader.ReadBoolean(),reader.ReadBoolean(),reader.ReadBoolean()};
        WorldKey = new Guid(bytes); Downed = incoming;
    }
    public override void PostUpdateWorld()
    {
        if(Main.netMode == NetmodeID.MultiplayerClient) return;
        if(!initialized) { for(int i=0;i<3;i++) observedOpen[i]=Open(i); initialized=true; return; }
        for(int i=0;i<3;i++)
        {
            bool available=Open(i);
            if(observedOpen[i]&&!available) Broadcast("Closed",DivineCatalog.Names[i]);
            observedOpen[i]=available;
        }
    }
    public static void Broadcast(string key,params object[] args)
    {
        if(Main.netMode == NetmodeID.SinglePlayer) Main.NewText(Language.GetTextValue("Mods.StarfallThrone.Divine."+key,args),Color.Gold);
        else if(Main.netMode == NetmodeID.Server) Terraria.Chat.ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Mods.StarfallThrone.Divine."+key,args),Color.Gold);
    }
    public static bool TryClaim(Guid identity,int index)
    {
        if(index is <0 or >=3||identity==Guid.Empty) return false;
        Claims.TryGetValue(identity,out byte mask); int bit=1<<index;
        if((mask&bit)!=0) return false;
        Claims[identity]=(byte)(mask|bit); return true;
    }
    public static void RecordVictory(NPC npc,int index,bool[] participants,bool[] damaged)
    {
        if(Main.netMode==NetmodeID.MultiplayerClient || !Open(index)) return;
        Downed[index]=true;
        for(int p=0;p<Main.maxPlayers;p++)
        {
            Player player=Main.player[p];
            if(!player.active||!participants[p]||!npc.playerInteraction[p]) continue;
            DivineChallengePlayer state=player.GetModPlayer<DivineChallengePlayer>();
            if(!state.IdentityBound && Main.netMode==NetmodeID.Server) continue;
            bool first=TryClaim(state.Identity,index);
            if(first) Give(player,DivineCatalog.Cache(index));
            if(!damaged[p]&&!player.dead) Give(player,DivineCatalog.Halo(index));
            Claims.TryGetValue(state.Identity,out byte mask); state.Victories=mask;
            if(first&&mask==7){Give(player,ModContent.ItemType<DivineMonument>());Give(player,ModContent.ItemType<DivineEpochTitle>());}
            state.SendRecord();
        }
        if(Main.netMode==NetmodeID.Server) NetMessage.SendData(MessageID.WorldData);
        Broadcast("Victory",DivineCatalog.Names[index]);
    }
    internal static void Give(Player player,int type)
    {
        int slot=Item.NewItem(player.GetSource_Misc("DivineReward"),player.Hitbox,type,1,noBroadcast:true);
        if(slot<0||slot>=Main.maxItems) return;
        Main.item[slot].playerIndexTheItemIsReservedFor=player.whoAmI;
        if(Main.netMode==NetmodeID.Server)
        { NetMessage.SendData(MessageID.SyncItem,-1,-1,null,slot); NetMessage.SendData(MessageID.ItemOwner,-1,-1,null,slot); }
    }
}

public sealed class DivineChallengePlayer : ModPlayer
{
    public Guid Identity=Guid.NewGuid();
    public bool IdentityBound;
    public byte Victories;
    public int HaloIndex=-1;
    private int noticeTicks,requestCooldown;
    private bool[] warned=new bool[3];
    public override void Initialize() { Identity=Guid.NewGuid(); IdentityBound=false; warned=new bool[3]; }
    public override void SaveData(TagCompound tag) => tag["DivineIdentity"]=Identity.ToString("N");
    public override void LoadData(TagCompound tag)
    { if(Guid.TryParse(tag.GetString("DivineIdentity"),out Guid id)&&id!=Guid.Empty) Identity=id; }
    public override void ResetEffects() => HaloIndex=-1;
    public override void OnEnterWorld()
    {
        noticeTicks=0; Victories=0; warned=new bool[3];
        if(Main.netMode==NetmodeID.MultiplayerClient)
        { ModPacket packet=Mod.GetPacket(); packet.Write((byte)40); packet.Write((byte)0); packet.Write(Identity.ToByteArray()); packet.Send(); }
        else { IdentityBound=true; DivineWorld.Claims.TryGetValue(Identity,out byte mask); Victories=mask; }
    }
    public override void PostUpdate()
    {
        if(requestCooldown>0) requestCooldown--;
        if(Player.whoAmI!=Main.myPlayer||Main.gameMenu)return;
        if(++noticeTicks==180)
        {
            Main.NewText(Language.GetTextValue("Mods.StarfallThrone.Divine.Intro"),Color.Gold);
            for(int i=0;i<3;i++) if(!DivineWorld.Open(i)&&!DivineWorld.IsDowned(i))
                Main.NewText(Language.GetTextValue("Mods.StarfallThrone.Divine.Missed",DivineCatalog.Names[i]),Color.Silver);
        }
        bool[] near={Player.HeldItem.type==ItemID.SlimeCrown||Main.slimeRain,
            Player.ZoneUnderworldHeight, Player.HeldItem.type==ItemID.CelestialSigil||NPC.LunarApocalypseIsUp};
        for(int i=0;i<3;i++) if(!warned[i]&&near[i]&&DivineWorld.Open(i)&&!DivineWorld.IsDowned(i))
        { warned[i]=true;Main.NewText(Language.GetTextValue("Mods.StarfallThrone.Divine.Warning"+i),Color.Gold); }
    }
    public override void OnHurt(Player.HurtInfo info)
    {
        if(info.Damage<=0)return;
        MarkDamaged();
        if(Main.netMode==NetmodeID.MultiplayerClient&&Player.whoAmI==Main.myPlayer)
        { ModPacket packet=Mod.GetPacket();packet.Write((byte)40);packet.Write((byte)3);packet.Send(); }
    }
    public override void UpdateDead() => MarkDamaged();
    private void MarkDamaged()
    { foreach(NPC npc in Main.ActiveNPCs) if(npc.ModNPC is DivineBossNPC boss&&boss.Participants[Player.whoAmI]) boss.Damaged[Player.whoAmI]=true; }
    public void SendRecord()
    {
        if(Main.netMode!=NetmodeID.Server)return;
        ModPacket packet=Mod.GetPacket();packet.Write((byte)40);packet.Write((byte)4);packet.Write(Victories);packet.Send(Player.whoAmI);
    }
    public static void ReceivePacket(BinaryReader reader,int sender)
    {
        if(reader.BaseStream.Position>=reader.BaseStream.Length)return;
        int action=reader.ReadByte();
        if(Main.netMode==NetmodeID.MultiplayerClient)
        { if(action==4) Main.LocalPlayer.GetModPlayer<DivineChallengePlayer>().Victories=(byte)(reader.ReadByte()&7);return; }
        if(Main.netMode!=NetmodeID.Server||sender<0||sender>=Main.maxPlayers||!Main.player[sender].active)return;
        Player player=Main.player[sender]; DivineChallengePlayer state=player.GetModPlayer<DivineChallengePlayer>();
        if(action==0)
        {
            byte[] bytes=reader.ReadBytes(16);if(bytes.Length!=16||state.IdentityBound)return;
            Guid id=new(bytes);if(id==Guid.Empty)return;
            // One identity binding per connection; no packet can replace another player's identity.
            state.Identity=id;state.IdentityBound=true;DivineWorld.Claims.TryGetValue(id,out byte mask);state.Victories=mask;state.SendRecord();return;
        }
        if(action==3){state.MarkDamaged();return;}
        if(state.requestCooldown>0||player.dead||player.ghost)return;
        if(action==1) { int index=reader.ReadByte();state.requestCooldown=30;DivineSummonBase.TrySummon(player,index); }
        else if(action==2)
        { int slot=reader.ReadByte(),tier=reader.ReadByte(),choice=reader.ReadByte();state.requestCooldown=15;DivineChoiceUI.Claim(player,slot,tier,choice); }
    }
}

public sealed class DivineStatusCommand : ModCommand
{
    public override CommandType Type=>CommandType.Chat|CommandType.Console;
    public override string Command=>"divine";
    public override string Usage=>"/divine";
    public override string Description=>Language.GetTextValue("Mods.StarfallThrone.Divine.StatusHelp");
    public override void Action(CommandCaller caller,string input,string[] args)
    {
        for(int i=0;i<3;i++)caller.Reply(DivineCatalog.Names[i]+"："+Language.GetTextValue("Mods.StarfallThrone.Divine."+
            (DivineWorld.IsDowned(i)?DivineWorld.Open(i)?"WonOpen":"WonClosed":DivineWorld.Open(i)?"Available":"MissedShort")),DivineCatalog.Colors[i]);
    }
}
