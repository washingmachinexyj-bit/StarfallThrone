using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using StarfallThrone.Content.Fable;

namespace StarfallThrone.Content.Oaths.Seeds;
public abstract class SeedBossNPC:ModNPC
{
    public abstract int Index{get;}
    public bool Cancelled;
    public Vector2 ArenaCenter;
    public float ArenaFloor;
    public bool[] Participants=new bool[Main.maxPlayers];
    public override string Texture=>OathCatalog.Root+"SeedBoss"+Index;
    public override string BossHeadTexture=>Texture+"_Head_Boss";
    public override void SetStaticDefaults(){Main.npcFrameCount[Type]=1;NPCID.Sets.MPAllowedEnemies[Type]=true;}
    public override void SetDefaults()
    {Participants=new bool[Main.maxPlayers];Cancelled=false;NPC.width=18;NPC.height=22;NPC.lifeMax=(Index+1)*10;NPC.damage=Index==2?2:1;NPC.defense=0;NPC.boss=false;NPC.aiStyle=-1;NPC.noGravity=NPC.noTileCollide=NPC.lavaImmune=NPC.netAlways=true;NPC.knockBackResist=.2f;NPC.value=0;NPC.HitSound=SoundID.NPCHit1;NPC.DeathSound=SoundID.NPCDeath1;NPC.timeLeft=36000;}
    public override void ApplyDifficultyAndPlayerScaling(int n,float balance,float adjustment)
    {NPC.lifeMax=(Index+1)*(Main.masterMode?20:Main.expertMode?15:10);NPC.damage=Index==2?2:1;NPC.defense=0;}
    public override bool CheckActive()=>false;
    public override void SetBestiary(BestiaryDatabase db,BestiaryEntry entry)
    {entry.Info.Add(BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface);entry.Info.Add(new FlavorTextBestiaryInfoElement("Mods.StarfallThrone.Oaths.SeedHint"+Index));}
    public override void SendExtraAI(BinaryWriter w){w.Write(ArenaCenter.X);w.Write(ArenaCenter.Y);w.Write(ArenaFloor);w.Write(Cancelled);}
    public override void ReceiveExtraAI(BinaryReader r){ArenaCenter=new(r.ReadSingle(),r.ReadSingle());ArenaFloor=r.ReadSingle();Cancelled=r.ReadBoolean();}
    public static bool TrySummon(Player p,int i)
    {
        if(Main.netMode==1||!p.active||p.dead||p.ghost||!OathWorld.SeedOpen(i)||!FableWorld.HasBook(p)||FableWorld.AnyEncounter)return false;
        if(!OathWorld.SkipLocked&&!FableWorld.CanAdmin(p)){OathWorld.Notice(p,"HostConsent");return false;}
        if(Main.invasionType>0||Main.pumpkinMoon||Main.snowMoon||Terraria.GameContent.Events.DD2Event.Ongoing)return false;
        if(FableWorld.Sealed&&!FablePlayer.CleanEquipment(p)){FableWorld.Say(p,"RemoveGear");return false;}
        if(!FableWorld.FindArena(p,out var c,out float floor)){FableWorld.Say(p,"NoSpace");return false;}
        int slot=NPC.NewNPC(p.GetSource_ItemUse(p.HeldItem),(int)c.X+90,(int)floor-30,OathCatalog.Seed(i));
        if(slot>=Main.maxNPCs||Main.npc[slot].ModNPC is not SeedBossNPC b)return false;
        b.ArenaCenter=c;b.ArenaFloor=floor;b.Participants[p.whoAmI]=true;b.NPC.target=p.whoAmI;b.NPC.ai[3]=Main.rand.Next(1,16000000);
        // No involuntary participation in a world-wide irreversible oath.
        p.GetModPlayer<FablePlayer>().Begin(slot,c,floor);b.NPC.netUpdate=true;
        if(Main.netMode==2)NetMessage.SendData(MessageID.SyncNPC,-1,-1,null,slot);return true;
    }
    public void CancelEncounter()
    {
        Cancelled=true;NPC.active=false;NPC.netUpdate=true;
        foreach(Projectile q in Main.ActiveProjectiles)if(q.ModProjectile is SeedHazard&&q.ai[0]==NPC.whoAmI&&q.ai[2]==NPC.ai[3])q.Kill();
        if(Main.netMode!=1)foreach(Player p in Main.ActivePlayers)if(p.GetModPlayer<FablePlayer>().EncounterSlot==NPC.whoAmI)p.GetModPlayer<FablePlayer>().End();
        if(Main.netMode==2)NetMessage.SendData(MessageID.SyncNPC,-1,-1,null,NPC.whoAmI);
    }
    public override void AI()
    {
        if(Cancelled)return;
        Player p=NPC.target>=0&&NPC.target<Main.maxPlayers?Main.player[NPC.target]:null;
        if(Main.netMode!=1&&(p==null||!p.active||p.dead||p.GetModPlayer<FablePlayer>().EncounterSlot!=NPC.whoAmI||!OathWorld.SeedOpen(Index)||Main.npc.Any(n=>n.active&&n.whoAmI!=NPC.whoAmI&&n.boss))){CancelEncounter();return;}
        if(p==null)return;Participants[p.whoAmI]=true;NPC.playerInteraction[p.whoAmI]=true;
        int t=(int)NPC.ai[0]++%240;float x=ArenaCenter.X+MathF.Sin(NPC.ai[0]*.012f)*75;
        float y=ArenaFloor-NPC.height/2;
        if(Index==0)y-=Math.Max(0,MathF.Sin(NPC.ai[0]*.035f))*18;
        if(Index==1&&t>60)x=NPC.Center.X;
        if(Index==2){y-=36+MathF.Sin(NPC.ai[0]*.025f)*8;if(NPC.life<NPC.lifeMax/2&&t<40)x+=35;}
        NPC.velocity=(new Vector2(x,y)-NPC.Center)*.07f;NPC.timeLeft=36000;
        if(t==100||Index==1&&NPC.life<NPC.lifeMax/2&&t==145)
        {
            if(Main.netMode!=1)Projectile.NewProjectile(NPC.GetSource_FromAI(),NPC.Center,(p.Center-NPC.Center).SafeNormalize(Vector2.UnitX)*(Index==0?1:1.4f),ModContent.ProjectileType<SeedHazard>(),1,0,Main.myPlayer,NPC.whoAmI,Index,NPC.ai[3]);
        }
        if(!Main.dedServ)Lighting.AddLight(NPC.Center,OathCatalog.Colors[Index].ToVector3()*.2f);
    }
    public override bool CanHitPlayer(Player p,ref int slot)=>!Cancelled&&p.GetModPlayer<FablePlayer>().EncounterSlot==NPC.whoAmI;
    public override bool? CanBeHitByItem(Player p,Item item)=>!Cancelled&&p.GetModPlayer<FablePlayer>().EncounterSlot==NPC.whoAmI&&(!FableWorld.Sealed||FableCatalog.StarterWeapon(item));
    public override bool? CanBeHitByProjectile(Projectile q)=>!Cancelled&&q.owner>=0&&q.owner<Main.maxPlayers&&Main.player[q.owner].GetModPlayer<FablePlayer>().EncounterSlot==NPC.whoAmI&&(!FableWorld.Sealed||q.ModProjectile is SeedShot);
    public override bool PreKill()=>!Cancelled;
    public override void ModifyNPCLoot(NPCLoot loot)
    {loot.Add(ItemDropRule.ByCondition(new ParticipantReward(),OathCatalog.Item("SeedToken"+Index)));loot.Add(ItemDropRule.ByCondition(new ParticipantReward(),OathCatalog.Item("SeedWeapon"+Index)));}
    private sealed class ParticipantReward:IItemDropRuleCondition
    {public bool CanDrop(DropAttemptInfo info)=>false;public bool CanShowItemDropInUI()=>true;public string GetConditionDescription()=>FableCatalog.Text("ParticipantReward");}
    public override void OnKill(){if(!Cancelled)OathWorld.RecordSeed(NPC,Index,Participants);}
}
[AutoloadBossHead] public sealed class SeedBoss0:SeedBossNPC{public override int Index=>0;}
[AutoloadBossHead] public sealed class SeedBoss1:SeedBossNPC{public override int Index=>1;}
[AutoloadBossHead] public sealed class SeedBoss2:SeedBossNPC{public override int Index=>2;}
public sealed class SeedHazard:ModProjectile
{
    public override string Texture=>OathCatalog.Root+"Spark";
    public override void SetDefaults(){Projectile.width=Projectile.height=6;Projectile.hostile=true;Projectile.tileCollide=false;Projectile.timeLeft=100;Projectile.penetrate=1;}
    public override void AI(){int slot=(int)Projectile.ai[0];if(slot<0||slot>=Main.maxNPCs||!Main.npc[slot].active||Main.npc[slot].ModNPC is not SeedBossNPC||Main.npc[slot].ai[3]!=Projectile.ai[2]){Projectile.Kill();return;}Projectile.rotation+=.03f;}
    public override bool CanHitPlayer(Player p)=>p.GetModPlayer<FablePlayer>().EncounterSlot==(int)Projectile.ai[0];
    public override void ModifyHitPlayer(Player p,ref Player.HurtModifiers modifiers)=>modifiers.SourceDamage*=.5f;
    public override Color? GetAlpha(Color light)=>OathCatalog.Colors[Math.Clamp((int)Projectile.ai[1],0,2)];
}
