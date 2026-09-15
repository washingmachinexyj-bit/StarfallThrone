using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.Localization;

namespace StarfallThrone.Content.Fable.Bosses;

public abstract class FableBossNPC : ModNPC
{
    public abstract int Index { get; }
    public bool Cancelled,Ready;
    public Vector2 ArenaCenter;
    public float ArenaFloor;
    public bool[] Participants=new bool[Main.maxPlayers];
    private Vector2 aim;
    public int Serial => (int)NPC.ai[3];
    public override string Texture=>FableCatalog.Root+"Boss"+Index;
    public override string BossHeadTexture=>Texture+"_Head_Boss";
    public override void SetStaticDefaults(){Main.npcFrameCount[Type]=1;NPCID.Sets.MPAllowedEnemies[Type]=true;}
    public override void SetDefaults()
    {
        Participants=new bool[Main.maxPlayers];Cancelled=Ready=false;
        NPC.width=Index is 2 or 8 or 10?42:26;NPC.height=28;NPC.lifeMax=FableCatalog.Life[Index];
        NPC.defense=FableCatalog.Defense[Index];NPC.damage=FableCatalog.Contact[Index];NPC.knockBackResist=.05f;
        NPC.noGravity=true;NPC.noTileCollide=true;NPC.boss=false;NPC.aiStyle=-1;NPC.value=0;NPC.npcSlots=1;
        NPC.HitSound=Index is 9 or 10 or 12?SoundID.NPCHit4:SoundID.NPCHit1;NPC.DeathSound=SoundID.NPCDeath1;
        NPC.timeLeft=36000;NPC.lavaImmune=true;NPC.netAlways=true;
    }
    public override void ApplyDifficultyAndPlayerScaling(int players,float balance,float bossAdjustment)
    {
        float hp=Main.masterMode?1.5f:Main.expertMode?1.25f:1f;
        NPC.lifeMax=(int)MathF.Ceiling(FableCatalog.Life[Index]*hp*(1+.3f*Math.Max(0,players-1)));
        NPC.damage=Damage(FableCatalog.Contact[Index]);NPC.defense=FableCatalog.Defense[Index];
    }
    public static int Damage(int n)=>(int)MathF.Ceiling(n*(Main.masterMode?1.4f:Main.expertMode?1.2f:1));
    public override void SetBestiary(BestiaryDatabase database,BestiaryEntry entry)
    {entry.Info.Add(BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface);entry.Info.Add(new FlavorTextBestiaryInfoElement("Mods.StarfallThrone.Fable.Hint"+Index));}
    public override bool CheckActive()=>false;
    public override void SendExtraAI(BinaryWriter writer){writer.Write(ArenaCenter.X);writer.Write(ArenaCenter.Y);writer.Write(ArenaFloor);writer.Write(aim.X);writer.Write(aim.Y);writer.Write(Cancelled);writer.Write(Ready);}
    public override void ReceiveExtraAI(BinaryReader reader){ArenaCenter=new(reader.ReadSingle(),reader.ReadSingle());ArenaFloor=reader.ReadSingle();aim=new(reader.ReadSingle(),reader.ReadSingle());Cancelled=reader.ReadBoolean();Ready=reader.ReadBoolean();}
    public void CancelEncounter()
    {
        Cancelled=true;NPC.active=false;NPC.netUpdate=true;
        foreach(Projectile p in Main.ActiveProjectiles)if(p.ModProjectile is FableHazard h && h.ParentSlot==NPC.whoAmI && h.Serial==Serial)p.Kill();
        if(Main.netMode!=NetmodeID.MultiplayerClient)foreach(Player p in Main.ActivePlayers)if(p.GetModPlayer<FablePlayer>().EncounterSlot==NPC.whoAmI)p.GetModPlayer<FablePlayer>().End();
        if(Main.netMode==NetmodeID.Server)NetMessage.SendData(MessageID.SyncNPC,-1,-1,null,NPC.whoAmI);
    }
    public override void AI()
    {
        if(Cancelled)return;
        if(!Ready)
        {
            Ready=true;if(ArenaFloor<=0){ArenaFloor=NPC.Bottom.Y+40;ArenaCenter=new(NPC.Center.X,ArenaFloor-96);}
            if(Main.netMode!=NetmodeID.MultiplayerClient){NPC.ai[3]=Main.rand.Next(1,16000000);NPC.netUpdate=true;}
        }
        int target=-1;
        for(int i=0;i<Main.maxPlayers;i++)if(Main.player[i].active && !Main.player[i].dead && Main.player[i].GetModPlayer<FablePlayer>().EncounterSlot==NPC.whoAmI)
        {Participants[i]=true;if(target<0)target=i;}
        if(Main.netMode!=NetmodeID.MultiplayerClient)
        {
            bool other=false;foreach(NPC n in Main.ActiveNPCs)if(n.whoAmI!=NPC.whoAmI && n.boss)other=true;
            if(target<0 || !FableWorld.Unlocked(Index) || other){CancelEncounter();return;}
            NPC.target=target;
        }
        if(NPC.target<0 || NPC.target>=Main.maxPlayers)return;
        Player player=Main.player[NPC.target];
        int t=(int)(NPC.ai[0]++%180);int round=(int)NPC.ai[0]/180;
        if(t==0){aim=player.Center;NPC.ai[1]=NPC.Center.X<player.Center.X?1:-1;NPC.netUpdate=true;}
        NPC.damage=t>=45 && t<100?Damage(FableCatalog.Contact[Index]):0;
        float floor=ArenaFloor-NPC.height/2;float dir=NPC.ai[1]==0?1:NPC.ai[1];
        Vector2 destination=new(ArenaCenter.X+(NPC.Center.X<ArenaCenter.X?-95:95),floor-26);
        bool low=NPC.life<NPC.lifeMax*.5f;
        switch(Index)
        {
            case 0: case 7: case 12:
                destination=new(ArenaCenter.X+MathF.Sin(NPC.ai[0]*.025f)*100,floor);
                if(t is >=45 and <90)destination.Y-=MathF.Sin((t-45)/45f*MathF.PI)*(Index==7?75:Index==12?48:32);
                if(t==90 && Index==7){Shoot(new(-2,-1.8f),0);Shoot(new(2,-1.8f),0);}
                if(t==100 && Index==12)Shoot(new(dir*2.4f,0),1);
                break;
            case 1: case 4: case 13:
                destination=new(ArenaCenter.X-dir*115,floor-50+MathF.Sin(NPC.ai[0]*.06f)*10);
                if(t is >=45 and <75 || Index==13 && t is >=100 and <124)destination=new(ArenaCenter.X+dir*115,floor-38);
                if(t==110 && Index==4)Shoot((aim-NPC.Center).SafeNormalize(Vector2.UnitY)*2.1f,0);
                if(t==145 && Index==13)Shoot(new(-dir*1.4f,-.3f),2);
                break;
            case 2: case 10:
                destination=new(ArenaCenter.X+MathF.Sin(NPC.ai[0]*.024f)*150,floor-8-MathF.Max(0,MathF.Sin(NPC.ai[0]*.048f))*32);
                if(Index==10 && t==90)Shoot((aim-NPC.Center).SafeNormalize(Vector2.UnitY)*2,1);
                break;
            case 3:
                destination=new(ArenaCenter.X+MathF.Sin(NPC.ai[0]*.022f)*95,floor-54);
                if(t==80)Shoot((aim-NPC.Center).SafeNormalize(Vector2.UnitY)*1.8f,0);
                break;
            case 5: case 9:
                destination=new(ArenaCenter.X+MathF.Sin(NPC.ai[0]*.015f)*75,floor-35);
                if(t==60)Shoot(new(-2.2f,1),1);
                if(t==110)Shoot(new(2.2f,1),1);
                break;
            case 6:
                destination=new(ArenaCenter.X+MathF.Sin(NPC.ai[0]*.012f)*135,floor-12);
                if(t==70)Shoot((aim-NPC.Center).SafeNormalize(Vector2.UnitY)*1.9f,0);
                break;
            case 8:
                destination=new(ArenaCenter.X-dir*95,floor-55);
                if(round%2==0 && t==65)Shoot((aim-NPC.Center).SafeNormalize(Vector2.UnitY)*2.2f,1);
                if(round%2==1 && t is >=60 and <92)destination.X=ArenaCenter.X+dir*110;
                break;
            case 11:
                destination=new(ArenaCenter.X+MathF.Sin(NPC.ai[0]*.02f)*70,floor);
                if(t==60 || t==90)Shoot((aim-NPC.Center).SafeNormalize(Vector2.UnitY)*2,0);
                break;
            case 14:
                destination=new(ArenaCenter.X+MathF.Sin(NPC.ai[0]*.018f)*105,floor-70);
                if(t==65){Vector2 v=(aim-NPC.Center).SafeNormalize(Vector2.UnitY)*1.8f;Shoot(v,1);Shoot(v.RotatedBy(.65),1);Shoot(v.RotatedBy(-.65),1);}
                if(t is >=105 and <140)destination=new(ArenaCenter.X-dir*115,floor-25);
                break;
            case 15:
                destination=new(ArenaCenter.X+MathF.Sin(NPC.ai[0]*.017f)*100,floor-22);
                if(t==70 || low && t==120)Shoot((aim-NPC.Center).SafeNormalize(Vector2.UnitY)*2.1f,round%2);
                break;
            case 16:
                destination=new(ArenaCenter.X+MathF.Sin(NPC.ai[0]*.014f)*80,floor-30);
                if(t==50 || t==85)Shoot((aim-NPC.Center).SafeNormalize(Vector2.UnitY)*1.8f,1);
                if(t==115)Shoot((aim-NPC.Center).SafeNormalize(Vector2.UnitX),3);
                break;
            default:
                destination=new(ArenaCenter.X+MathF.Sin(NPC.ai[0]*.02f)*100,floor-8);
                int form=round%3;
                if(t is >=45 and <90)
                {
                    if(form==0)destination.Y-=MathF.Sin((t-45)/45f*MathF.PI)*40;
                    if(form==1)destination.X=ArenaCenter.X+dir*140;
                    if(form==2 && t==60)Shoot((aim-NPC.Center).SafeNormalize(Vector2.UnitY)*2,0);
                }
                if(NPC.life<NPC.lifeMax*.6f && t==110)Shoot((aim-NPC.Center).SafeNormalize(Vector2.UnitY)*1.7f,1);
                if(NPC.life<NPC.lifeMax*.25f && t is 60 or 80 or 100)Shoot(new(0,1.7f),4,new Vector2(ArenaCenter.X+(t-80)*5,ArenaFloor-150));
                break;
        }
        NPC.velocity=(destination-NPC.Center)*.09f;NPC.rotation=MathHelper.Clamp(NPC.velocity.X*.02f,-.18f,.18f);
        NPC.spriteDirection=NPC.velocity.X>=0?1:-1;NPC.timeLeft=36000;
    }
    private void Shoot(Vector2 velocity,int kind,Vector2? position=null)
    {
        if(Main.netMode==NetmodeID.MultiplayerClient)return;
        int count=0;foreach(Projectile p in Main.ActiveProjectiles)if(p.ModProjectile is FableHazard h && h.ParentSlot==NPC.whoAmI)count++;
        if(count>=6)return;
        int id=Projectile.NewProjectile(NPC.GetSource_FromAI(),position??NPC.Center,velocity,ModContent.ProjectileType<FableHazard>(),Damage(kind==4?13:FableCatalog.Shot[Index]),0,Main.myPlayer,NPC.whoAmI,kind,Serial);
        if(id<Main.maxProjectiles){Main.projectile[id].netUpdate=true;}
    }
    public override bool CanHitPlayer(Player target,ref int cooldownSlot)=>!Cancelled && NPC.damage>0 && target.GetModPlayer<FablePlayer>().EncounterSlot==NPC.whoAmI;
    public override bool? CanBeHitByItem(Player player,Item item)=>!Cancelled && player.GetModPlayer<FablePlayer>().EncounterSlot==NPC.whoAmI && (!FableWorld.Sealed || FableCatalog.StarterWeapon(item));
    public override bool? CanBeHitByProjectile(Projectile projectile)
    {
        if(Cancelled || projectile.owner<0 || projectile.owner>=Main.maxPlayers)return false;
        Player p=Main.player[projectile.owner];
        bool starter=projectile.ModProjectile is global::StarfallThrone.Content.Oaths.Seeds.SeedShot or Equipment.FableWhip || projectile.ModProjectile is Equipment.FableShot or Equipment.FableMinion && FableCatalog.Valid((int)projectile.ai[0]);
        return p.GetModPlayer<FablePlayer>().EncounterSlot==NPC.whoAmI && (!FableWorld.Sealed || starter);
    }
    public override void ModifyNPCLoot(NPCLoot loot){loot.Add(ItemDropRule.ByCondition(new ParticipantRewardDisplay(),FableCatalog.Weapon(Index)));}
    private sealed class ParticipantRewardDisplay : IItemDropRuleCondition
    {
        // Report rewards to the bestiary/checklist. Actual per-participant delivery is in OnKill.
        public bool CanDrop(DropAttemptInfo info)=>false;
        public bool CanShowItemDropInUI()=>true;
        public string GetConditionDescription()=>FableCatalog.Text("ParticipantReward");
    }
    public override bool PreKill()=>!Cancelled;
    public override void OnKill()
    {if(!Cancelled && Ready)FableWorld.RecordVictory(NPC,Index,Participants);}
    public override bool? DrawHealthBar(byte hbPosition,ref float scale,ref Vector2 position){scale=.8f;return true;}
    public override bool PreDraw(SpriteBatch batch,Vector2 screenPos,Color drawColor)
    {
        Texture2D tex=TextureAssets.Npc[Type].Value;
        float pulse=1f+MathF.Sin(NPC.ai[0]*.07f)*.025f;
        batch.Draw(tex,NPC.Center-screenPos,null,Color.White,NPC.rotation,tex.Size()/2,pulse,SpriteEffects.None,0);
        int t=(int)NPC.ai[0]%180;
        if(t>=15 && t<45)Utils.DrawBorderString(batch,"!",NPC.Top-screenPos-new Vector2(0,14),Color.Gold,.65f,.5f);
        if(Index is 3 or 15 && NPC.life<NPC.lifeMax*.5f)
            batch.Draw(tex,NPC.Center-screenPos+new Vector2(60,0),null,Color.White*.22f,0,tex.Size()/2,1,SpriteEffects.None,0);
        return false;
    }
}

[AutoloadBossHead] public sealed class FableBoss0 : FableBossNPC { public override int Index=>0; }

[AutoloadBossHead] public sealed class FableBoss1 : FableBossNPC { public override int Index=>1; }

[AutoloadBossHead] public sealed class FableBoss2 : FableBossNPC { public override int Index=>2; }

[AutoloadBossHead] public sealed class FableBoss3 : FableBossNPC { public override int Index=>3; }

[AutoloadBossHead] public sealed class FableBoss4 : FableBossNPC { public override int Index=>4; }

[AutoloadBossHead] public sealed class FableBoss5 : FableBossNPC { public override int Index=>5; }

[AutoloadBossHead] public sealed class FableBoss6 : FableBossNPC { public override int Index=>6; }

[AutoloadBossHead] public sealed class FableBoss7 : FableBossNPC { public override int Index=>7; }

[AutoloadBossHead] public sealed class FableBoss8 : FableBossNPC { public override int Index=>8; }

[AutoloadBossHead] public sealed class FableBoss9 : FableBossNPC { public override int Index=>9; }

[AutoloadBossHead] public sealed class FableBoss10 : FableBossNPC { public override int Index=>10; }

[AutoloadBossHead] public sealed class FableBoss11 : FableBossNPC { public override int Index=>11; }

[AutoloadBossHead] public sealed class FableBoss12 : FableBossNPC { public override int Index=>12; }

[AutoloadBossHead] public sealed class FableBoss13 : FableBossNPC { public override int Index=>13; }

[AutoloadBossHead] public sealed class FableBoss14 : FableBossNPC { public override int Index=>14; }

[AutoloadBossHead] public sealed class FableBoss15 : FableBossNPC { public override int Index=>15; }

[AutoloadBossHead] public sealed class FableBoss16 : FableBossNPC { public override int Index=>16; }

[AutoloadBossHead] public sealed class FableBoss17 : FableBossNPC { public override int Index=>17; }
