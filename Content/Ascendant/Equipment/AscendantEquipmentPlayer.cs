#nullable enable
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using global::StarfallThrone.Content.Divine.Equipment;
using global::StarfallThrone.Content.Voyage.Equipment;

namespace StarfallThrone.Content.Ascendant.Equipment;

public sealed class AscendantEquipmentPlayer:ModPlayer
{
    // 0..2 accessories, 3 dew set, 4 furnace set, 5 fate set. Never stored in removable buffs.
    public readonly int[] Cooldowns=new int[6];
    public readonly bool[] Expert=new bool[3];
    public int SetTier=-1,Shield,ShieldSpeed,DewCharges,DewSpeed,Heat,HeatTime,MarkTime,ReturnPower;
    public int Refund,RefundWait,RefundTick;
    public bool FurnaceGuard;
    public Vector2 MarkPosition;
    public Guid MarkWorld;
    public int SlashCombo,BranchCharges,BranchHitCombo,FurnacePressure,GunCadence,MagicCadence,BowTarget=-1,BowTargetType,BowHits;
    public bool BowReady;
    private ulong tickStamp=ulong.MaxValue,lastChargeTick=ulong.MaxValue,lastRequestTick=ulong.MaxValue;
    private bool partialShield;
    public override void ResetEffects(){Array.Clear(Expert);SetTier=-1;}
    private void Tick()
    {
        if(tickStamp==Main.GameUpdateCount)return;tickStamp=Main.GameUpdateCount;
        for(int i=0;i<Cooldowns.Length;i++)if(Cooldowns[i]>0)Cooldowns[i]--;
    }
    public override void PreUpdate()=>Tick();
    public override void PostUpdateEquips()
    {
        // Covers modded accessory slots and edited saves as well as vanilla slots. Never alters the old accessory.
        var old=Player.GetModPlayer<DivineEquipmentPlayer>();
        for(int i=0;i<3;i++)if(Expert[i]&&old.Expert[i])
        {Expert[i]=false;if(i<2)Player.statLifeMax2-=i==0?35:60;else Player.GetDamage(DamageClass.Generic)-=.12f;}
        if(Expert[0]&&ShieldSpeed>0)Player.moveSpeed+=.15f;
        if(SetTier==0&&DewSpeed>0)Player.moveSpeed+=.15f;
        if(SetTier==1&&HeatTime>0)
        {Player.GetDamage(CurrentClass())+=.22f;Player.moveSpeed+=.12f;}
        if(SetTier==2&&ReturnPower>0)Player.GetDamage(CurrentClass())+=.15f;
    }
    private DamageClass CurrentClass()=>Player.armor[0].ModItem is AscendantArmor a&&a.Part<4?AscendantEquipmentData.Class(a.Part):DamageClass.Generic;
    public override void PostUpdate()
    {
        if(!Player.active||Player.dead)return;
        if(!Expert[0])Shield=ShieldSpeed=0;
        else if(Shield==0&&Cooldowns[0]==0){Shield=50;Cooldowns[0]=900;}
        if(ShieldSpeed>0)ShieldSpeed--;
        if(SetTier!=0)DewCharges=DewSpeed=0;
        if(DewSpeed>0)DewSpeed--;
        if(SetTier!=1){Heat=HeatTime=0;FurnaceGuard=false;}
        if(HeatTime>0)HeatTime--;
        if(HeatTime==0)FurnaceGuard=false;
        if(SetTier!=2)MarkTime=ReturnPower=0;
        if(MarkTime>0)MarkTime--;
        if(ReturnPower>0)ReturnPower--;
        if(!Expert[1])Refund=RefundWait=RefundTick=0;
        if(RefundWait>0)RefundWait--;
        else if(Refund>0&&++RefundTick>=4&&Player.whoAmI==Main.myPlayer)
        {
            RefundTick=0;Refund--;
            if(Player.statLife>0&&Player.statLife<Player.statLifeMax2){Player.statLife++;Player.HealEffect(1);}
        }
        if(Player.whoAmI==Main.myPlayer&&!Main.dedServ&&Main.GameUpdateCount%120==0)
        {
            string? key=MarkTime>0?"ReturnReady":SetTier==1&&Heat==100?"HeatReady":SetTier==0&&DewCharges==3&&Cooldowns[3]==0?"DewReady":null;
            if(key!=null)CombatText.NewText(Player.Hitbox,AscendantEquipmentData.Color(Math.Max(0,SetTier)),Language.GetTextValue("Mods.StarfallThrone.AscendantEquipment."+key),false,true);
        }
    }
    public override void UpdateDead()
    {
        Tick();ResetEffects();ClearTransient();
    }
    private void ClearTransient()
    {
        Shield=ShieldSpeed=DewCharges=DewSpeed=Heat=HeatTime=MarkTime=ReturnPower=Refund=RefundWait=RefundTick=0;FurnaceGuard=false;
        SlashCombo=BranchCharges=BranchHitCombo=FurnacePressure=GunCadence=MagicCadence=BowHits=0;BowTarget=-1;BowReady=false;
    }
    public override void SaveData(TagCompound tag)=>tag["ascendantEquipmentCooldowns"]=(int[])Cooldowns.Clone();
    public override void LoadData(TagCompound tag)
    {
        int[] saved=tag.GetIntArray("ascendantEquipmentCooldowns");
        for(int i=0;i<Math.Min(saved.Length,Cooldowns.Length);i++)Cooldowns[i]=Math.Clamp(saved[i],0,3600);
    }
    public override void OnEnterWorld()=>ClearTransient();
    public override void CopyClientState(ModPlayer targetCopy)=>Array.Copy(Cooldowns,((AscendantEquipmentPlayer)targetCopy).Cooldowns,6);
    public override void SendClientChanges(ModPlayer clientPlayer)
    {
        if(Main.netMode!=NetmodeID.MultiplayerClient)return;
        int[] prior=((AscendantEquipmentPlayer)clientPlayer).Cooldowns;
        for(int i=0;i<6;i++)if(Cooldowns[i]>prior[i]){SendCooldowns(2,-1,-1);break;}
    }
    public override void SyncPlayer(int toWho,int fromWho,bool newPlayer)=>SendCooldowns(Main.netMode==NetmodeID.Server?(byte)3:(byte)2,toWho,fromWho);
    private void SendCooldowns(byte mode,int toWho,int fromWho)
    {
        var packet=Mod.GetPacket();packet.Write((byte)61);packet.Write(mode);packet.Write((byte)Player.whoAmI);
        foreach(int cd in Cooldowns)packet.Write((ushort)Math.Clamp(cd,0,3600));packet.Send(toWho,fromWho);
    }
    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        if(Player.dead||AscendantEquipmentData.EquippedTier(Player)<0||DivineEquipmentKeys.Skill==null||!DivineEquipmentKeys.Skill.JustPressed)return;
        if(Main.netMode==NetmodeID.MultiplayerClient)
        {var packet=Mod.GetPacket();packet.Write((byte)61);packet.Write((byte)0);packet.Write((byte)Player.whoAmI);packet.Send();}
        else TrySkill();
    }
    public bool TrySkill()
    {
        if(!Player.active||Player.dead||Main.netMode==NetmodeID.MultiplayerClient)return false;
        int tier=AscendantEquipmentData.EquippedTier(Player),release=0;bool warped=false;
        switch(tier)
        {
            case 0:
                if(Cooldowns[3]>0||DewCharges<=0)return false;
                release=DewCharges;DewCharges=0;DewSpeed=240;Cooldowns[3]=720;
                if(Main.netMode==NetmodeID.SinglePlayer)FireDew(release);
                break;
            case 1:
                if(Cooldowns[4]>0||Heat<100)return false;
                Heat=0;HeatTime=600;FurnaceGuard=true;Cooldowns[4]=2100;
                break;
            case 2:
                if(MarkTime>0)
                {
                    MarkTime=0;
                    if(!CanReturn(MarkPosition))return false;
                    bool immune=Player.immune,blink=Player.immuneNoBlink;int immuneTime=Player.immuneTime;int[] hurt=(int[])Player.hurtCooldowns.Clone();
                    if(Main.netMode==NetmodeID.Server)RemoteClient.CheckSection(Player.whoAmI,MarkPosition);
                    Player.Teleport(MarkPosition,TeleportationStyleID.RodOfDiscord);
                    Player.immune=immune;Player.immuneNoBlink=blink;Player.immuneTime=immuneTime;Array.Copy(hurt,Player.hurtCooldowns,hurt.Length);
                    Player.fallStart=(int)(Player.position.Y/16);ReturnPower=360;warped=true;
                }
                else
                {
                    if(Cooldowns[5]>0)return false;
                    MarkPosition=Player.position;MarkWorld=Main.ActiveWorldFileData.UniqueId;MarkTime=360;Cooldowns[5]=1800;
                }
                break;
            default:return false;
        }
        if(Main.netMode==NetmodeID.Server)SendState(release,warped);
        return true;
    }
    public bool CanReturn(Vector2 position)=>MarkWorld==Main.ActiveWorldFileData.UniqueId&&AscendantArsenal.Finite(position)&&
        Vector2.DistanceSquared(Player.position,position)<=1600*1600&&position.X>=32&&position.Y>=32&&
        position.X+Player.width<Main.maxTilesX*16-32&&position.Y+Player.height<Main.maxTilesY*16-32&&!Collision.SolidCollision(position,Player.width,Player.height);
    private void FireDew(int count)
    {
        if(Player.whoAmI!=Main.myPlayer)return;
        for(int i=0;i<Math.Clamp(count,0,3);i++)
            AscendantArsenal.Launch(Player.GetSource_Misc("AscendantDewSkill"),Player.whoAmI,0,AscendantShotKind.DewSkill,Player.Center,
                new Vector2(Player.direction*10,(i-(count-1)*.5f)*3),(int)Player.GetDamage(DamageClass.Generic).ApplyTo(32),2);
    }
    private void SendState(int release,bool warped)
    {
        var packet=Mod.GetPacket();packet.Write((byte)61);packet.Write((byte)1);packet.Write((byte)Player.whoAmI);
        packet.Write((byte)release);packet.Write(warped);packet.Write(Player.position.X);packet.Write(Player.position.Y);
        packet.Write((byte)DewCharges);packet.Write((byte)Heat);packet.Write((ushort)HeatTime);packet.Write((ushort)DewSpeed);
        packet.Write((ushort)MarkTime);packet.Write((ushort)ReturnPower);packet.Write(MarkPosition.X);packet.Write(MarkPosition.Y);
        foreach(int cd in Cooldowns)packet.Write((ushort)Math.Clamp(cd,0,3600));packet.Send();
    }
    public static void ReceivePacket(BinaryReader reader,int sender)
    {
        // Native ModPacket streams are seekable. Reject short frames before any reads or state writes,
        // so malicious input does not generate first-chance EndOfStream warnings in ordinary play.
        if(reader.BaseStream.CanSeek&&reader.BaseStream.Length-reader.BaseStream.Position<2)return;
        byte mode=reader.ReadByte(),who=reader.ReadByte();if(who>=Main.maxPlayers)return;
        int required=mode switch{0=>0,1=>40,2 or 3=>12,4=>6,5=>2,6=>1,_=>-1};
        if(required<0||reader.BaseStream.CanSeek&&reader.BaseStream.Length-reader.BaseStream.Position<required)return;
        var state=Main.player[who].GetModPlayer<AscendantEquipmentPlayer>();
        if(mode==0)
        {
            if(Main.netMode!=NetmodeID.Server||sender!=who||!state.Player.active||state.Player.dead||state.lastRequestTick!=ulong.MaxValue&&Main.GameUpdateCount-state.lastRequestTick<8)return;
            state.lastRequestTick=Main.GameUpdateCount;state.TrySkill();
        }
        else if(mode==1&&Main.netMode==NetmodeID.MultiplayerClient)
        {
            int release=reader.ReadByte();bool warped=reader.ReadBoolean();Vector2 position=new(reader.ReadSingle(),reader.ReadSingle());
            int dew=reader.ReadByte(),heat=reader.ReadByte(),heatTime=reader.ReadUInt16(),dewSpeed=reader.ReadUInt16(),mark=reader.ReadUInt16(),power=reader.ReadUInt16();
            Vector2 markPosition=new(reader.ReadSingle(),reader.ReadSingle());int[] cds=new int[6];for(int i=0;i<6;i++)cds[i]=reader.ReadUInt16();
            if(release>3||dew>3||heat>100||heatTime>600||dewSpeed>240||mark>360||power>360||Array.Exists(cds,cd=>cd>3600)||
                !AscendantArsenal.Finite(position)||!AscendantArsenal.Finite(markPosition)||position.X<0||position.Y<0||position.X>Main.maxTilesX*16||position.Y>Main.maxTilesY*16)return;
            if(warped){state.Player.position=position;state.Player.fallStart=(int)(position.Y/16);}
            state.DewCharges=dew;state.Heat=heat;state.HeatTime=heatTime;state.DewSpeed=dewSpeed;state.MarkTime=mark;state.ReturnPower=power;
            state.FurnaceGuard=heatTime>0;state.MarkPosition=markPosition;state.MarkWorld=Main.ActiveWorldFileData.UniqueId;
            for(int i=0;i<6;i++)state.Cooldowns[i]=Math.Max(state.Cooldowns[i],cds[i]);
            if(release>0)state.FireDew(release);
        }
        else if(mode is 2 or 3)
        {
            if(mode==2&&(Main.netMode!=NetmodeID.Server||sender!=who)||mode==3&&Main.netMode!=NetmodeID.MultiplayerClient)return;
            int[] cds=new int[6];for(int i=0;i<6;i++)cds[i]=reader.ReadUInt16();
            if(Array.Exists(cds,cd=>cd>3600))return;
            for(int i=0;i<6;i++)state.Cooldowns[i]=Math.Max(state.Cooldowns[i],cds[i]);
            if(mode==2)state.SendCooldowns(3,-1,-1);
        }
        else if(mode==4)
        {
            int target=reader.ReadInt16(),identity=reader.ReadInt32();
            if(Main.netMode!=NetmodeID.Server||sender!=who||!state.Player.active||state.Player.dead||target<0||target>=Main.maxNPCs)return;
            int tier=AscendantEquipmentData.EquippedTier(state.Player);NPC npc=Main.npc[target];
            if(tier is not (0 or 1)||!npc.CanBeChasedBy()||npc.type==NPCID.TargetDummy)return;
            bool witness=false;
            if(identity==-1)
                witness=state.Player.itemAnimation>0&&!state.Player.HeldItem.noMelee&&state.Player.HeldItem.damage>0&&Vector2.DistanceSquared(state.Player.Center,npc.Center)<180*180&&Collision.CanHitLine(state.Player.Center,1,1,npc.Center,1,1);
            else foreach(Projectile q in Main.ActiveProjectiles)
            {
                if(q.owner!=who||q.identity!=identity||q.damage<=0||!VoyageEquipmentProjectileOrigin.IsOwnedPrimary(state.Player,q)||q.ModProjectile?.CanDamage()==false)continue;
                Rectangle tolerance=npc.Hitbox;tolerance.Inflate(48,48);
                witness=(q.ModProjectile?.Colliding(q.Hitbox,tolerance)??q.Hitbox.Intersects(tolerance))&&Collision.CanHitLine(q.Center,1,1,npc.Center,1,1);break;
            }
            if(witness&&state.AwardCharge(tier))
            {var packet=state.Mod.GetPacket();packet.Write((byte)61);packet.Write((byte)5);packet.Write(who);packet.Write((byte)state.DewCharges);packet.Write((byte)state.Heat);packet.Send(who);}
        }
        else if(mode==5&&Main.netMode==NetmodeID.MultiplayerClient)
        {
            int dew=reader.ReadByte(),heat=reader.ReadByte();if(dew>3||heat>100)return;
            state.DewCharges=dew;state.Heat=heat;
        }
        else if(mode==6)
        {
            int heat=reader.ReadByte();
            // Client-authoritative vanilla hurt may only remove furnace resource, never add it.
            if(Main.netMode==NetmodeID.Server&&sender==who&&state.Player.active)state.Heat=Math.Min(state.Heat,Math.Clamp(heat,0,100));
        }
    }
    public static bool EnemyHit(Player player,Player.HurtInfo info)
    {
        if(info.PvP||info.Damage<=0||info.DamageSource==null||!info.DamageSource.TryGetCausingEntity(out Entity source))return false;
        if(source is NPC npc)return !npc.friendly&&npc.damage>0;
        return source is Projectile q&&q.hostile&&!q.friendly&&!q.trap&&q.damage>0&&
            (q.owner!=player.whoAmI||q.GetGlobalProjectile<AscendantHostileOrigin>().FromEnemy);
    }
    public override void ModifyHurt(ref Player.HurtModifiers modifiers)
    {
        partialShield=false;
        modifiers.ModifyHurtInfo+=(ref Player.HurtInfo info)=>
        {
            if(!EnemyHit(Player,info))return;
            if(Expert[2]&&Cooldowns[2]==0&&info.Damage>=60)
            {info.Damage-=Math.Min(180,(int)(info.Damage*.6f));Cooldowns[2]=1500;Shatter(2);}
            if(SetTier==1&&HeatTime>0&&FurnaceGuard)
            {info.Damage-=Math.Min(60,(int)(info.Damage*.25f));FurnaceGuard=false;Shatter(1);}
            if(Expert[0]&&Shield>0&&info.Damage>Shield)
            {info.Damage-=Shield;Shield=0;partialShield=true;ShieldSpeed=180;Shatter(0);}
        };
    }
    public override bool ConsumableDodge(Player.HurtInfo info)
    {
        if(!EnemyHit(Player,info)||!Expert[0]||partialShield||Shield<info.Damage)return false;
        Shield-=info.Damage;if(Shield==0)ShieldSpeed=180;
        Refund=RefundWait=RefundTick=0;Shatter(0);return true; // Finite absorption only; no added immunity.
    }
    public override void OnHurt(Player.HurtInfo info)
    {
        Refund=RefundWait=RefundTick=0;Heat=Math.Max(0,Heat-25);
        if(Main.netMode==NetmodeID.MultiplayerClient&&Player.whoAmI==Main.myPlayer&&SetTier==1)
        {var packet=Mod.GetPacket();packet.Write((byte)61);packet.Write((byte)6);packet.Write((byte)Player.whoAmI);packet.Write((byte)Heat);packet.Send();}
    }
    public override void PostHurt(Player.HurtInfo info)
    {
        if(!Expert[1]||Cooldowns[1]>0||Player.dead||Player.statLife<=0||!EnemyHit(Player,info))return;
        Refund=Math.Min(90,(int)(info.Damage*.4f));RefundWait=240;RefundTick=0;
        if(Refund>0)Cooldowns[1]=1200;
    }
    private void Shatter(int tier)
    {
        if(Main.dedServ)return;
        for(int i=0;i<16;i++){var d=Dust.NewDustPerfect(Player.Center,DustID.MagicMirror,(i*MathHelper.TwoPi/16).ToRotationVector2()*4,100,AscendantEquipmentData.Color(tier));d.noGravity=true;}
    }
    public bool AwardCharge(int tier)
    {
        if(tier is not (0 or 1)||tier==1&&HeatTime>0)return false;
        ulong budget=tier==0?60UL:6UL;
        if(lastChargeTick!=ulong.MaxValue&&Main.GameUpdateCount-lastChargeTick<budget)return false;
        if(tier==0){if(DewCharges>=3)return false;DewCharges++;}
        else{if(Heat>=100)return false;Heat=Math.Min(100,Heat+5);}
        lastChargeTick=Main.GameUpdateCount;return true;
    }
    public override void OnHitNPCWithItem(Item item,NPC target,NPC.HitInfo hit,int damageDone)=>RecordHit(target,damageDone,-1);
    public override void OnHitNPCWithProj(Projectile projectile,NPC target,NPC.HitInfo hit,int damageDone)
    {if(VoyageEquipmentProjectileOrigin.IsOwnedPrimary(Player,projectile))RecordHit(target,damageDone,projectile.identity);}
    private void RecordHit(NPC target,int damage,int identity)
    {
        int tier=AscendantEquipmentData.EquippedTier(Player);
        if(tier is not (0 or 1)||!Player.active||Player.dead||damage<=0||target.friendly||target.type==NPCID.TargetDummy||!target.CanBeChasedBy())return;
        if(Main.netMode==NetmodeID.MultiplayerClient)
        {
            if(Player.whoAmI!=Main.myPlayer)return;
            var packet=Mod.GetPacket();packet.Write((byte)61);packet.Write((byte)4);packet.Write((byte)Player.whoAmI);packet.Write((short)target.whoAmI);packet.Write(identity);packet.Send();
        }
        else AwardCharge(tier);
    }
}
