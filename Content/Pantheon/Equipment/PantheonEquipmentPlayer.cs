#nullable enable
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using global::StarfallThrone.Content.Ascendant.Equipment;

namespace StarfallThrone.Content.Pantheon.Equipment;

public sealed class PantheonEquipmentKeys:ModSystem
{
    public static ModKeybind? Skill;
    public override void Load()=>Skill=KeybindLoader.RegisterKeybind(Mod,"PantheonArmorSkill","G");
    public override void Unload()=>Skill=null;
}
public sealed class PantheonEquipmentPlayer:ModPlayer
{
    public readonly bool[] Expert=new bool[17];
    // 0..16 accessory cooldowns; 17..22 armor skills; 23 stance. Never reset by unequip/death/save.
    public readonly int[] Cooldowns=new int[24],Casts=new int[36];
    public int SetTier=-1,ActiveTier=-1,SkillTime,SkillShield,Stance;
    // Epoch is connection-scoped; serial changes ONLY for a newly accepted armor cast.
    // A stance switch or ordinary SyncPlayer must never turn a spent shield into a new shield.
    public Guid SkillEpoch;
    public uint SkillSerial;
    public int DewShield,ShadowShield,CrystalShield,ShadowTime,Lucidity,GateTime,CrystalSpeed,Heat,HeatTime,StormTime;
    public int SinceHurt=600,SinceHit=600,MoveCharge,StillTime,InsightTarget=-1,InsightType,InsightHits,HoneyHits,SeedHits,PrismHits;
    public int LanternHeal,LanternTime;public Vector2 LanternPosition,GatePosition;
    private ulong tickStamp=ulong.MaxValue,hitStamp=ulong.MaxValue,requestStamp=ulong.MaxValue;
    private bool primaryHurt;
    public override void ResetEffects(){Array.Clear(Expert);SetTier=-1;}
    public override void PreUpdate(){if(tickStamp==Main.GameUpdateCount)return;tickStamp=Main.GameUpdateCount;AdvanceTimers();}
    internal void AdvanceTimers()
    {
        for(int i=0;i<Cooldowns.Length;i++)if(Cooldowns[i]>0)Cooldowns[i]--;
        SinceHurt=Math.Min(36000,SinceHurt+1);SinceHit=Math.Min(36000,SinceHit+1);
        if(SkillTime>0)SkillTime--;if(ShadowTime>0)ShadowTime--;if(Lucidity>0)Lucidity--;if(GateTime>0)GateTime--;if(CrystalSpeed>0)CrystalSpeed--;if(HeatTime>0)HeatTime--;if(StormTime>0)StormTime--;if(LanternTime>0)LanternTime--;
    }
    public override void PostUpdateEquips()
    {
        if(ActiveTier>=0&&SkillTime>0&&SetTier==ActiveTier)switch(ActiveTier)
        {case 0:Player.moveSpeed+=.20f;break;case 1:Player.GetDamage(DamageClass.Generic)+=.20f;Player.lifeRegen+=20;break;case 2:Player.moveSpeed+=.15f;break;case 3:Player.GetCritChance(DamageClass.Generic)+=15;Player.GetAttackSpeed(DamageClass.Melee)+=.15f;break;case 4:Player.moveSpeed+=.35f;Player.maxMinions+=2;break;case 5:Player.GetDamage(DamageClass.Generic)+=.30f;break;}
        if(Expert[3]&&Lucidity>0){Player.moveSpeed+=.35f;Player.noKnockback=true;}
        if(Expert[6]&&GateTime>0&&Vector2.DistanceSquared(Player.Center,GatePosition)<240*240){Player.moveSpeed+=.45f;Player.maxRunSpeed+=3;}
        if(Expert[7]&&CrystalSpeed>0){Player.moveSpeed+=.25f;Player.GetDamage(DamageClass.Generic)+=.15f;}
        if(Expert[8]){if(SinceHit<180)Player.GetDamage(DamageClass.Generic)+=.14f;else{Player.moveSpeed+=.20f;Player.lifeRegen+=8;}}
        if(Expert[10]&&HeatTime>0){Player.GetDamage(DamageClass.Generic)+=.25f;Player.moveSpeed+=.15f;}
        if(Expert[12]&&StillTime>=90){Player.statDefense+=100;Player.noKnockback=true;}
        if(Expert[13]&&StormTime>0){Player.moveSpeed+=.35f;Player.GetDamage(DamageClass.Generic)+=.20f;}
        if(Expert[16])switch(Stance){case 0:Player.GetDamage(DamageClass.Generic)+=.25f;break;case 1:Player.statDefense+=150;Player.endurance+=.08f;break;case 2:Player.moveSpeed+=.40f;Player.maxRunSpeed+=3;break;}
    }
    public override void PostUpdate()
    {
        if(!Player.active||Player.dead)return;
        bool local=Player.whoAmI==Main.myPlayer;
        if(SetTier!=ActiveTier||SkillTime==0){ActiveTier=-1;SkillTime=SkillShield=0;}
        if(!Expert[0])DewShield=0;
        else if(local&&DewShield==0&&Cooldowns[0]==0&&SinceHurt>=240&&Player.velocity.LengthSquared()>=4){DewShield=180;Cooldowns[0]=900;}
        if(!Expert[2]||ShadowTime==0)ShadowShield=0;
        if(Expert[2]&&local&&Cooldowns[2]==0&&Player.velocity.LengthSquared()>=100){ShadowShield=160;ShadowTime=120;Cooldowns[2]=600;}
        if(!Expert[3])Lucidity=0;if(!Expert[6])GateTime=0;
        if(!Expert[7])CrystalShield=CrystalSpeed=0;
        else if(local&&CrystalShield==0&&Cooldowns[7]==0){CrystalShield=240;Cooldowns[7]=1200;}
        if(!Expert[10])Heat=HeatTime=0;
        if(!Expert[13])MoveCharge=StormTime=0;
        else if(local&&Cooldowns[13]==0){MoveCharge=Player.velocity.LengthSquared()>=36?Math.Min(180,MoveCharge+1):Math.Max(0,MoveCharge-2);if(MoveCharge>=180){MoveCharge=0;StormTime=300;Cooldowns[13]=900;}}
        StillTime=Expert[12]&&Player.velocity.LengthSquared()<4?Math.Min(90,StillTime+1):0;
        if(!Expert[5])LanternHeal=LanternTime=0;
        else if(local&&LanternHeal>0&&LanternTime<=240&&LanternTime>0&&Vector2.DistanceSquared(Player.Center,LanternPosition)<=64*64){Heal(LanternHeal);LanternHeal=LanternTime=0;}
        if(LanternTime==0)LanternHeal=0;
        if(SinceHit>=180){InsightHits=HoneyHits=0;InsightTarget=-1;}
        if(!Expert[1]){InsightHits=0;InsightTarget=-1;}if(!Expert[4])HoneyHits=0;if(!Expert[11])SeedHits=0;if(!Expert[14])PrismHits=0;
    }
    private void ClearTransient(){ActiveTier=-1;SkillTime=SkillShield=DewShield=ShadowShield=CrystalShield=ShadowTime=Lucidity=GateTime=CrystalSpeed=Heat=HeatTime=StormTime=MoveCharge=StillTime=InsightHits=HoneyHits=SeedHits=PrismHits=LanternHeal=LanternTime=0;InsightTarget=-1;SinceHurt=SinceHit=600;Array.Clear(Casts);}
    public override void UpdateDead(){PreUpdate();ResetEffects();ClearTransient();}
    public override void OnEnterWorld(){ClearTransient();SkillEpoch=Guid.Empty;SkillSerial=0;}
    public override void SaveData(TagCompound tag){tag["pantheonCooldowns"]=(int[])Cooldowns.Clone();tag["pantheonStance"]=Stance;}
    public override void LoadData(TagCompound tag){int[] cds=tag.GetIntArray("pantheonCooldowns");for(int i=0;i<Math.Min(cds.Length,24);i++)Cooldowns[i]=Math.Clamp(cds[i],0,3600);Stance=Math.Clamp(tag.GetInt("pantheonStance"),0,2);}
    public override void CopyClientState(ModPlayer copy)=>Array.Copy(Cooldowns,((PantheonEquipmentPlayer)copy).Cooldowns,24);
    public override void SendClientChanges(ModPlayer copy){if(Main.netMode!=NetmodeID.MultiplayerClient)return;int[] old=((PantheonEquipmentPlayer)copy).Cooldowns;for(int i=0;i<24;i++)if(Cooldowns[i]>old[i]){SendCooldowns(2,-1,-1);break;}}
    public override void SyncPlayer(int toWho,int fromWho,bool newPlayer){if(Main.netMode==NetmodeID.Server)SendState(toWho);else SendCooldowns(2,toWho,fromWho);}
    private void SendCooldowns(byte mode,int toWho,int fromWho){var packet=Mod.GetPacket();packet.Write((byte)71);packet.Write(mode);packet.Write((byte)Player.whoAmI);foreach(int cd in Cooldowns)packet.Write((ushort)Math.Clamp(cd,0,3600));packet.Send(toWho,fromWho);}
    internal void WriteSkillSnapshot(BinaryWriter writer)
    {
        if(SkillEpoch==Guid.Empty)SkillEpoch=Guid.NewGuid();
        writer.Write(SkillEpoch.ToByteArray());writer.Write(SkillSerial);
        writer.Write((byte)(ActiveTier<0?255:ActiveTier));writer.Write((ushort)SkillTime);writer.Write((ushort)SkillShield);writer.Write((byte)Stance);
        foreach(int cd in Cooldowns)writer.Write((ushort)Math.Clamp(cd,0,3600));
    }
    private void SendState(int toWho=-1){var packet=Mod.GetPacket();packet.Write((byte)71);packet.Write((byte)1);packet.Write((byte)Player.whoAmI);WriteSkillSnapshot(packet);packet.Send(toWho);}
    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        if(!Player.active||Player.dead||PantheonEquipmentKeys.Skill?.JustPressed!=true)return;
        if(Main.netMode==NetmodeID.MultiplayerClient){var packet=Mod.GetPacket();packet.Write((byte)71);packet.Write((byte)0);packet.Write((byte)Player.whoAmI);packet.Send();}else TrySkill();
    }
    public bool TrySkill()
    {
        if(Main.netMode==NetmodeID.MultiplayerClient||!Player.active||Player.dead)return false;
        int tier=PantheonEquipmentData.EquippedTier(Player);bool used=false;
        if(tier>=0&&Cooldowns[17+tier]==0){if(SkillEpoch==Guid.Empty)SkillEpoch=Guid.NewGuid();SkillSerial++;if(SkillSerial==0)SkillSerial=1;ActiveTier=tier;SkillTime=PantheonEquipmentData.SkillDuration[tier];SkillShield=tier==0?300:tier==5?600:0;Cooldowns[17+tier]=PantheonEquipmentData.SkillCooldown[tier];used=true;}
        if(Expert[16]&&Cooldowns[23]==0){Stance=(Stance+1)%3;Cooldowns[23]=90;used=true;}
        if(used&&Main.netMode==NetmodeID.Server)SendState();return used;
    }
    public static void ReceivePacket(BinaryReader reader,int sender)
    {
        if(reader.BaseStream.CanSeek&&reader.BaseStream.Length-reader.BaseStream.Position<2)return;
        byte mode=reader.ReadByte(),who=reader.ReadByte();int size=mode switch{0=>0,1=>74,2 or 3=>48,_=>-1};
        if(size<0||who>=Main.maxPlayers||reader.BaseStream.CanSeek&&reader.BaseStream.Length-reader.BaseStream.Position<size)return;
        var s=Main.player[who].GetModPlayer<PantheonEquipmentPlayer>();
        if(mode==0)
        {if(Main.netMode!=NetmodeID.Server||sender!=who||!s.Player.active||s.Player.dead||s.requestStamp!=ulong.MaxValue&&Main.GameUpdateCount-s.requestStamp<8)return;s.requestStamp=Main.GameUpdateCount;s.TrySkill();return;}
        if(mode==1)
        {
            if(Main.netMode!=NetmodeID.MultiplayerClient)return;
            Guid epoch=new(reader.ReadBytes(16));uint serial=reader.ReadUInt32();
            int tier=reader.ReadByte(),time=reader.ReadUInt16(),shield=reader.ReadUInt16(),stance=reader.ReadByte();int[] cds=ReadCooldowns(reader);
            if(epoch==Guid.Empty||(tier!=255&&tier>5)||stance>2||Array.Exists(cds,x=>x>3600))return;
            int maxTime=tier==255?0:PantheonEquipmentData.SkillDuration[tier],maxShield=tier==0?300:tier==5?600:0;
            if(time>maxTime||shield>maxShield||time==0&&shield!=0||serial==0&&tier!=255)return;
            if(s.SkillEpoch!=Guid.Empty&&s.SkillEpoch!=epoch)return;
            if(s.SkillEpoch==Guid.Empty){s.SkillEpoch=epoch;s.SkillSerial=0;}
            bool newer=unchecked((int)(serial-s.SkillSerial))>0;
            if(serial!=s.SkillSerial&&!newer)return;
            if(newer){s.SkillSerial=serial;s.ActiveTier=tier==255?-1:tier;s.SkillTime=time;s.SkillShield=shield;}
            else
            {
                // Same cast: server may still have its pre-hurt shield/time. Tighten only.
                s.SkillTime=Math.Min(s.SkillTime,time);s.SkillShield=Math.Min(s.SkillShield,shield);
                if(tier==255||s.SkillTime==0){s.ActiveTier=-1;s.SkillTime=s.SkillShield=0;}
            }
            s.Stance=stance;for(int i=0;i<24;i++)s.Cooldowns[i]=Math.Max(s.Cooldowns[i],cds[i]);return;
        }
        if(mode==2&&(Main.netMode!=NetmodeID.Server||sender!=who||!s.Player.active)||mode==3&&Main.netMode!=NetmodeID.MultiplayerClient)return;
        int[] values=ReadCooldowns(reader);if(Array.Exists(values,x=>x>3600))return;for(int i=0;i<24;i++)s.Cooldowns[i]=Math.Max(s.Cooldowns[i],values[i]);if(mode==2)s.SendCooldowns(3,-1,-1);
    }
    private static int[] ReadCooldowns(BinaryReader reader){int[] cds=new int[24];for(int i=0;i<24;i++)cds[i]=reader.ReadUInt16();return cds;}
    public override void ModifyHurt(ref Player.HurtModifiers modifiers)
    {
        primaryHurt=false;
        modifiers.ModifyHurtInfo+=(ref Player.HurtInfo info)=>
        {
            if(!AscendantEquipmentPlayer.EnemyHit(Player,info))return;primaryHurt=true;int original=info.Damage;int reduction=0;
            if(Expert[9]&&Cooldowns[9]==0&&SinceHurt>=360){reduction+=Math.Min(300,(int)(original*.3f));Cooldowns[9]=900;}
            if(Expert[15]&&Cooldowns[15]==0&&original>=200){reduction+=Math.Min(500,(int)(original*.5f));Cooldowns[15]=1800;}
            if(SetTier==2&&ActiveTier==2&&SkillTime>0)reduction+=Math.Min(240,(int)(original*.30f));
            int budget=Math.Max(0,original-1-reduction);
            void Absorb(ref int shield){int value=Math.Min(shield,budget);shield-=value;budget-=value;reduction+=value;}
            if(Expert[0])Absorb(ref DewShield);if(Expert[2]&&ShadowTime>0)Absorb(ref ShadowShield);
            if(Expert[7]&&CrystalShield>0){int before=CrystalShield;Absorb(ref CrystalShield);if(before>0&&CrystalShield==0){CrystalSpeed=240;Burst(7,.45f);}}
            if(ActiveTier==SetTier&&SkillTime>0)Absorb(ref SkillShield);
            info.Damage=Math.Max(1,original-Math.Min(reduction,original-1));
        };
    }
    public override void OnHurt(Player.HurtInfo info)
    {
        SinceHurt=0;PrismHits=0;
        if(!primaryHurt&&!AscendantEquipmentPlayer.EnemyHit(Player,info))return;
        if(Expert[3]&&Cooldowns[3]==0&&info.Damage>=100){Lucidity=180;Cooldowns[3]=600;}
        if(Expert[5]&&Cooldowns[5]==0&&!Player.dead&&Player.statLife>0){LanternHeal=Math.Min(300,(int)(info.Damage*.35f));LanternPosition=Player.Center;LanternTime=300;Cooldowns[5]=1200;}
        if(Expert[6]&&Cooldowns[6]==0){GatePosition=Player.Center;GateTime=180;Cooldowns[6]=600;}
    }
    public override void OnHitNPCWithItem(Item item,NPC target,NPC.HitInfo hit,int damageDone)=>RecordHit(target,damageDone);
    public override void OnHitNPCWithProj(Projectile q,NPC target,NPC.HitInfo hit,int damageDone)
    {if(q.owner==Player.whoAmI&&!(q.ModProjectile is PantheonShot shot&&shot.Secondary)&&global::StarfallThrone.Content.Voyage.Equipment.VoyageEquipmentProjectileOrigin.IsOwnedPrimary(Player,q))RecordHit(target,damageDone);}
    internal void RecordHit(NPC target,int damage)
    {
        if(Player.whoAmI!=Main.myPlayer||!Player.active||Player.dead||damage<=0||target.friendly||target.type==NPCID.TargetDummy||!target.CanBeChasedBy()||hitStamp!=ulong.MaxValue&&Main.GameUpdateCount-hitStamp<10)return;
        hitStamp=Main.GameUpdateCount;CountHit(target);
    }
    internal void CountHit(NPC target)
    {
        SinceHit=0;
        if(Expert[1])
        {if(InsightTarget!=target.whoAmI||InsightType!=target.type){InsightTarget=target.whoAmI;InsightType=target.type;InsightHits=0;}if(++InsightHits>=10){InsightHits=0;FireAt(target,1,1f);}}
        if(Expert[4]&&Cooldowns[4]==0&&++HoneyHits>=30){HoneyHits=0;Heal(120);Cooldowns[4]=600;}
        if(Expert[10]&&HeatTime==0&&Cooldowns[10]==0){Heat=Math.Min(100,Heat+5);if(Heat==100){Heat=0;HeatTime=480;Cooldowns[10]=1200;Burst(10,.8f);}}
        if(Expert[11]&&Cooldowns[11]==0&&++SeedHits>=40){SeedHits=0;Heal(220);Cooldowns[11]=900;}
        if(Expert[14]&&Cooldowns[14]==0&&SinceHurt>=180&&Player.velocity.LengthSquared()>=16&&++PrismHits>=12){PrismHits=0;FireAt(target,14,1.5f);Cooldowns[14]=600;}
    }
    private void Heal(int amount){if(Player.whoAmI!=Main.myPlayer||Player.dead||Player.statLife<=0)return;int heal=Math.Min(Math.Clamp(amount,0,300),Math.Max(0,Player.statLifeMax2-Player.statLife));if(heal>0){Player.statLife+=heal;Player.HealEffect(heal);}}
    private int ProcDamage(int boss,float scale)=>(int)Math.Clamp(Player.GetDamage(DamageClass.Generic).ApplyTo(PantheonArsenal.Damage[boss==16?32:boss*2]*scale),1,2000000);
    private void FireAt(NPC target,int boss,float scale)=>PantheonArsenal.Launch(Player.GetSource_Misc("PantheonExpert"),Player.whoAmI,boss==16?32:boss*2,7,Player.Center,(target.Center-Player.Center).SafeNormalize(Vector2.UnitX)*24,ProcDamage(boss,scale));
    private void Burst(int boss,float scale){if(Player.whoAmI!=Main.myPlayer)return;for(int i=0;i<4;i++)PantheonArsenal.Launch(Player.GetSource_Misc("PantheonExpert"),Player.whoAmI,boss==16?32:boss*2,7,Player.Center,(i*MathHelper.PiOver2).ToRotationVector2()*18,ProcDamage(boss,scale));}
}
