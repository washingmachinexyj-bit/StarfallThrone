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
using StarfallThrone.Content.Ascendant.Bosses;

namespace StarfallThrone.Content.Ascendant;

public sealed class AscendantWorld : ModSystem
{
    public static bool[] Downed = new bool[3];
    public static Guid WorldKey;
    internal static readonly Dictionary<Guid, byte> Claims = new();
    internal static readonly Dictionary<Guid, byte> Pending = new();
    private static readonly bool[] observedOpen = new bool[3];
    private bool initialized;
    public static bool Open(int index) => index switch {0 => !NPC.downedSlimeKing,1 => !Main.hardMode,2 => !NPC.downedMoonlord,_ => false};
    public static bool IsDowned(int index) => index is >=0 and <3 && Downed[index];
    public static bool Prerequisites(int index)
    {
        if(index is <0 or >=3||!Divine.DivineWorld.IsDowned(index)||!global::StarfallThrone.Content.Oaths.OathWorld.SeedWon[index])return false;
        return index==0?Systems.MiniBossWorld.Downed.All(v=>v):Enumerable.Range(index==1?0:4,index==1?4:5).All(i=>Ecology.EcologyWorld.Downed[i]);
    }
    public static string RequirementText(int index)
    {
        int total=index==0?8:index==1?4:5;
        int complete=index==0?Systems.MiniBossWorld.Downed.Count(v=>v):Enumerable.Range(index==1?0:4,total).Count(i=>Ecology.EcologyWorld.Downed[i]);
        return global::StarfallThrone.Content.Oaths.OathCatalog.Text(global::StarfallThrone.Content.Oaths.OathWorld.SeedWon[index]?"SeedMet":"SeedMissing")+" — "+Language.GetTextValue("Mods.StarfallThrone.Ascendant.Requirements",complete,total,
            Language.GetTextValue("Mods.StarfallThrone.Ascendant."+(Divine.DivineWorld.IsDowned(index)?"BaseWon":"BaseMissing")));
    }
    public override void ClearWorld()
    { Downed = new bool[3]; WorldKey = Guid.Empty; Claims.Clear(); Pending.Clear(); initialized = false; Array.Clear(observedOpen); }
    public override void OnWorldLoad() { if (Main.netMode != NetmodeID.MultiplayerClient && WorldKey == Guid.Empty) WorldKey = Guid.NewGuid(); }
    public override void SaveWorldData(TagCompound tag)
    {
        tag["AscendantVersion"] = 1; tag["WorldKey"] = WorldKey.ToString("N");
        tag["Victories"] = (Downed[0]?1:0)|(Downed[1]?2:0)|(Downed[2]?4:0);
        tag["Claims"] = Claims.Select(pair => new TagCompound { ["Player"] = pair.Key.ToString("N"), ["Mask"] = (int)pair.Value }).ToList();
        tag["Pending"] = Pending.Select(pair => new TagCompound { ["Player"] = pair.Key.ToString("N"), ["Mask"] = (int)pair.Value }).ToList();
    }
    public override void LoadWorldData(TagCompound tag)
    {
        int saved=tag.GetInt("Victories"); Downed = new[]{(saved&1)!=0,(saved&2)!=0,(saved&4)!=0};
        WorldKey = Guid.TryParse(tag.GetString("WorldKey"),out Guid key) && key != Guid.Empty ? key : Guid.NewGuid();
        Claims.Clear();
        foreach (TagCompound entry in tag.GetList<TagCompound>("Claims"))
            if (Guid.TryParse(entry.GetString("Player"),out Guid id) && id != Guid.Empty) Claims[id] = (byte)(entry.GetInt("Mask") & 7);
        Pending.Clear();
        foreach(TagCompound entry in tag.GetList<TagCompound>("Pending"))
            if(Guid.TryParse(entry.GetString("Player"),out Guid id)&&id!=Guid.Empty&&Claims.TryGetValue(id,out byte claimed))Pending[id]=(byte)(entry.GetInt("Mask")&claimed&7);
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
            if(observedOpen[i]&&!available) Broadcast("Closed",AscendantCatalog.Names[i]);
            observedOpen[i]=available;
        }
    }
    public static void Broadcast(string key,params object[] args)
    {
        if(Main.netMode == NetmodeID.SinglePlayer) Main.NewText(Language.GetTextValue("Mods.StarfallThrone.Ascendant."+key,args),Color.Gold);
        else if(Main.netMode == NetmodeID.Server) Terraria.Chat.ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Mods.StarfallThrone.Ascendant."+key,args),Color.Gold);
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
        if(Main.netMode==NetmodeID.MultiplayerClient || !Open(index)||!Prerequisites(index)) return;
        Downed[index]=true;
        for(int p=0;p<Main.maxPlayers;p++)
        {
            Player player=Main.player[p];
            if(!player.active||!participants[p]||!npc.playerInteraction[p]) continue;
            AscendantChallengePlayer state=player.GetModPlayer<AscendantChallengePlayer>();
            if(!state.IdentityBound && Main.netMode==NetmodeID.Server) continue;
            bool first=TryClaim(state.Identity,index);
            if(first)
            {
                if(Main.expertMode){Pending.TryGetValue(state.Identity,out byte pending);Pending[state.Identity]=(byte)(pending|(1<<index));}
                else Give(player,AscendantCatalog.Cache(index));
            }
            if(!damaged[p]&&!player.dead) Give(player,AscendantCatalog.Halo(index));
            Claims.TryGetValue(state.Identity,out byte mask); state.Victories=mask;
            if(first&&mask==7){Give(player,ModContent.ItemType<AscendantMonument>());Give(player,ModContent.ItemType<AscendantEpochTitle>());}
            state.SendRecord();
        }
        if(Main.netMode==NetmodeID.Server) NetMessage.SendData(MessageID.WorldData);
        Broadcast("Victory",AscendantCatalog.Names[index]);
    }
    public static bool ClaimPending(Player player,int index)
    {
        if(Main.netMode==NetmodeID.MultiplayerClient||index is <0 or >=3||!player.active||player.dead||player.ghost)return false;
        var state=player.GetModPlayer<AscendantChallengePlayer>();
        if(Main.netMode==NetmodeID.Server&&!state.IdentityBound)return false;
        if(!Pending.TryGetValue(state.Identity,out byte mask)||(mask&(1<<index))==0)return false;
        Pending[state.Identity]=(byte)(mask&~(1<<index));
        Give(player,AscendantCatalog.Cache(index));return true;
    }
    internal static void Give(Player player,int type)
    {
        int slot=Item.NewItem(player.GetSource_Misc("AscendantReward"),player.Hitbox,type,1,noBroadcast:true);
        if(slot<0||slot>=Main.maxItems) return;
        Main.item[slot].playerIndexTheItemIsReservedFor=player.whoAmI;
        if(Main.netMode==NetmodeID.Server)
        { NetMessage.SendData(MessageID.SyncItem,-1,-1,null,slot); NetMessage.SendData(MessageID.ItemOwner,-1,-1,null,slot); }
    }
}

public sealed class AscendantChallengePlayer : ModPlayer
{
    public Guid Identity=Guid.NewGuid();
    public bool IdentityBound;
    public byte Victories;
    public int HaloIndex=-1;
    private int noticeTicks,requestCooldown;
    private bool[] warned=new bool[3];
    public override void Initialize() { Identity=Guid.NewGuid(); IdentityBound=false; warned=new bool[3]; }
    public override void SaveData(TagCompound tag) => tag["AscendantIdentity"]=Identity.ToString("N");
    public override void LoadData(TagCompound tag)
    { if(Guid.TryParse(tag.GetString("AscendantIdentity"),out Guid id)&&id!=Guid.Empty) Identity=id; }
    public override void ResetEffects() => HaloIndex=-1;
    public override void OnEnterWorld()
    {
        noticeTicks=0; Victories=0; warned=new bool[3];
        if(Main.netMode==NetmodeID.MultiplayerClient)
        { ModPacket packet=Mod.GetPacket(); packet.Write((byte)60); packet.Write((byte)0); packet.Write(Identity.ToByteArray()); packet.Send(); }
        else { IdentityBound=true; AscendantWorld.Claims.TryGetValue(Identity,out byte mask); Victories=mask; }
    }
    public override void PostUpdate()
    {
        if(requestCooldown>0) requestCooldown--;
        if(Player.whoAmI!=Main.myPlayer||Main.gameMenu)return;
        if(++noticeTicks==180)
        {
            Main.NewText(Language.GetTextValue("Mods.StarfallThrone.Ascendant.Intro"),Color.Gold);
            for(int i=0;i<3;i++) if(!AscendantWorld.Open(i)&&!AscendantWorld.IsDowned(i))
                Main.NewText(Language.GetTextValue("Mods.StarfallThrone.Ascendant.Missed",AscendantCatalog.Names[i]),Color.Silver);
        }
        bool[] near={Player.HeldItem.type==ItemID.SlimeCrown||Main.slimeRain,
            Player.ZoneUnderworldHeight, Player.HeldItem.type==ItemID.CelestialSigil||NPC.LunarApocalypseIsUp};
        for(int i=0;i<3;i++) if(!warned[i]&&near[i]&&AscendantWorld.Open(i)&&!AscendantWorld.IsDowned(i))
        { warned[i]=true;Main.NewText(Language.GetTextValue("Mods.StarfallThrone.Ascendant.Warning"+i),Color.Gold); }
    }
    public override void OnHurt(Player.HurtInfo info)
    {
        if(info.Damage<=0)return;
        MarkDamaged();
        if(Main.netMode==NetmodeID.MultiplayerClient&&Player.whoAmI==Main.myPlayer)
        { ModPacket packet=Mod.GetPacket();packet.Write((byte)60);packet.Write((byte)3);packet.Send(); }
    }
    public override void UpdateDead() => MarkDamaged();
    private void MarkDamaged()
    { foreach(NPC npc in Main.ActiveNPCs) if(npc.ModNPC is AscendantBossNPC boss&&boss.Participants[Player.whoAmI]) boss.Damaged[Player.whoAmI]=true; }
    public void SendRecord()
    {
        if(Main.netMode!=NetmodeID.Server)return;
        ModPacket packet=Mod.GetPacket();packet.Write((byte)60);packet.Write((byte)4);packet.Write(Victories);packet.Send(Player.whoAmI);
    }
    public static void ReceivePacket(BinaryReader reader,int sender)
    {
        if(reader.BaseStream.Position>=reader.BaseStream.Length)return;
        int action=reader.ReadByte();
        int required=action switch{0=>16,1=>1,2=>3,4=>1,5=>1,_=>0};
        if(reader.BaseStream.Length-reader.BaseStream.Position<required)return;
        if(Main.netMode==NetmodeID.MultiplayerClient)
        { if(action==4) Main.LocalPlayer.GetModPlayer<AscendantChallengePlayer>().Victories=(byte)(reader.ReadByte()&7);return; }
        if(Main.netMode!=NetmodeID.Server||sender<0||sender>=Main.maxPlayers||!Main.player[sender].active)return;
        Player player=Main.player[sender]; AscendantChallengePlayer state=player.GetModPlayer<AscendantChallengePlayer>();
        if(action==0)
        {
            byte[] bytes=reader.ReadBytes(16);if(bytes.Length!=16||state.IdentityBound)return;
            Guid id=new(bytes);if(id==Guid.Empty)return;
            // One identity binding per connection; no packet can replace another player's identity.
            state.Identity=id;state.IdentityBound=true;AscendantWorld.Claims.TryGetValue(id,out byte mask);state.Victories=mask;state.SendRecord();return;
        }
        if(action==3){state.MarkDamaged();return;}
        if(action==5){int tier=reader.ReadByte();AscendantWorld.ClaimPending(player,tier);return;}
        if(state.requestCooldown>0||player.dead||player.ghost)return;
        if(action==1) { int index=reader.ReadByte();state.requestCooldown=30;AscendantSummonBase.TrySummon(player,index); }
        else if(action==2)
        { int slot=reader.ReadByte(),tier=reader.ReadByte(),choice=reader.ReadByte();state.requestCooldown=15;AscendantChoiceUI.Claim(player,slot,tier,choice); }
    }
}

public sealed class AscendantStatusCommand : ModCommand
{
    public override CommandType Type=>CommandType.Chat|CommandType.Console;
    public override string Command=>"truegod";
    public override string Usage=>"/truegod";
    public override string Description=>Language.GetTextValue("Mods.StarfallThrone.Ascendant.StatusHelp");
    public override void Action(CommandCaller caller,string input,string[] args)
    {
        for(int i=0;i<3;i++)caller.Reply(AscendantCatalog.Names[i]+"："+Language.GetTextValue("Mods.StarfallThrone.Ascendant."+
            (AscendantWorld.IsDowned(i)?AscendantWorld.Open(i)?"WonOpen":"WonClosed":AscendantWorld.Open(i)?"Available":"MissedShort"))+"；"+AscendantWorld.RequirementText(i),AscendantCatalog.Colors[i]);
    }
}
