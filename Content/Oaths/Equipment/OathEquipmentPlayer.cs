#nullable enable
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using StarfallThrone.Content.Oaths.Support;
using StarfallThrone.Content.Oaths.Bosses;

namespace StarfallThrone.Content.Oaths.Equipment;

public sealed class OathEquipmentKeys:ModSystem
{
    public static ModKeybind? Rewind;
    public override void Load()=>Rewind=KeybindLoader.RegisterKeybind(Mod,"OathRewind","V");
    public override void Unload(){Rewind=null;OathEquipmentPlayer.RewindBoundary=null;}
}
public sealed class OathEquipmentPlayer:ModPlayer
{
    public readonly bool[] Expert=new bool[3];
    public readonly int[] Cooldowns=new int[3];
    public int SummonCooldown,Reservoir,LostLife,ShieldSpeed,DewCharges,EmberCharges,FateCharges,FireMode,FateMode,Heat,HeatIdle,HeatDamage,CommandTime,CommandCooldown,Precision,PrecisionTarget=-1,PrecisionType,PrecisionIdle;
    public Vector2 HeatDirection;
    public static Func<Player,Vector2,bool>? RewindBoundary;
    private readonly Vector2[] history=new Vector2[121];private int cursor,samples,encounterHash;
    private ulong tickStamp=ulong.MaxValue,historyStamp=ulong.MaxValue,chargeStamp=ulong.MaxValue,requestStamp=ulong.MaxValue;
    public override void ResetEffects()=>Array.Clear(Expert);
    public override void PreUpdate()
    {
        if(tickStamp==Main.GameUpdateCount)return;tickStamp=Main.GameUpdateCount;
        AdvanceTimers();
    }
    internal void AdvanceTimers()
    {
        for(int i=0;i<3;i++)if(Cooldowns[i]>0)Cooldowns[i]--;
        if(SummonCooldown>0)SummonCooldown--;if(ShieldSpeed>0)ShieldSpeed--;if(CommandTime>0)CommandTime--;if(CommandCooldown>0)CommandCooldown--;
    }
    public override void PostUpdateEquips()
    {
        // Earlier versions win if an edited save or modded slot bypasses the ordinary mutual exclusion.
        var old=Player.GetModPlayer<global::StarfallThrone.Content.Divine.Equipment.DivineEquipmentPlayer>();
        var enhanced=Player.GetModPlayer<global::StarfallThrone.Content.Ascendant.Equipment.AscendantEquipmentPlayer>();
        for(int i=0;i<3;i++)if(Expert[i]&&(old.Expert[i]||enhanced.Expert[i])){Expert[i]=false;if(i>0)Player.GetDamage(DamageClass.Generic)-=i==1?.12f:.15f;}
        if(Expert[0])Player.statLifeMax2+=(int)(Player.statLifeMax2*.15f);
        if(Expert[1]&&ShieldSpeed>0){Player.GetAttackSpeed(DamageClass.Melee)+=.15f;Player.GetAttackSpeed(DamageClass.Ranged)+=.15f;Player.GetAttackSpeed(DamageClass.Magic)+=.15f;}
    }
    public override void PostUpdate()
    {
        if(!Player.active||Player.dead)return;
        if(!Expert[0])Reservoir=LostLife=0;
        if(!Expert[1])ShieldSpeed=0;
        if(!Expert[2]){samples=cursor=0;}else RecordPosition();
        int held=Player.HeldItem.ModItem is SupremeWeaponBase w?w.Index:-1;
        if(held!=2)DewCharges=0;
        if(held!=4)EmberCharges=0;
        if(held!=8)FateCharges=0;
        if(held!=9||++PrecisionIdle>180){Precision=0;PrecisionTarget=-1;}
        if(Heat>0&&++HeatIdle>=45)
        {
            if(held==5&&Player.whoAmI==Main.myPlayer)SupremeArsenal.Launch(Player.GetSource_Misc("OathHeatVent"),Player.whoAmI,5,1,Player.Center,HeatDirection*20,(int)(HeatDamage*.75f));
            Heat=HeatDamage=HeatIdle=0;
        }
        if(held!=5)Heat=HeatDamage=HeatIdle=0;
    }
    private void Transient(){Reservoir=LostLife=ShieldSpeed=DewCharges=EmberCharges=FateCharges=Heat=HeatIdle=HeatDamage=CommandTime=Precision=samples=cursor=0;PrecisionTarget=-1;historyStamp=ulong.MaxValue;}
    public override void UpdateDead(){PreUpdate();ResetEffects();Transient();}
    public override void OnEnterWorld(){Transient();requestStamp=ulong.MaxValue;}
    public override void SaveData(TagCompound tag){tag["oathCooldowns"]=(int[])Cooldowns.Clone();tag["oathCommandCooldown"]=CommandCooldown;}
    public override void LoadData(TagCompound tag){var data=tag.GetIntArray("oathCooldowns");for(int i=0;i<Math.Min(3,data.Length);i++)Cooldowns[i]=Math.Clamp(data[i],0,2700);CommandCooldown=Math.Clamp(tag.GetInt("oathCommandCooldown"),0,600);}
    public override void CopyClientState(ModPlayer copy)=>Array.Copy(Cooldowns,((OathEquipmentPlayer)copy).Cooldowns,3);
    public override void SendClientChanges(ModPlayer copy){if(Main.netMode!=NetmodeID.MultiplayerClient)return;var old=(OathEquipmentPlayer)copy;for(int i=0;i<3;i++)if(Cooldowns[i]>old.Cooldowns[i]){SendCooldowns(2,-1);break;}}
    public override void SyncPlayer(int toWho,int fromWho,bool newPlayer)=>SendCooldowns(Main.netMode==NetmodeID.Server?(byte)3:(byte)2,toWho);
    private void SendCooldowns(byte mode,int toWho)
    {
        if(Main.netMode==NetmodeID.SinglePlayer)return;var packet=Mod.GetPacket();packet.Write((byte)93);packet.Write(mode);packet.Write((byte)Player.whoAmI);foreach(int cd in Cooldowns)packet.Write((ushort)Math.Clamp(cd,0,2700));packet.Send(toWho);
    }
    private static bool Enemy(Player p,Player.HurtInfo info)=>global::StarfallThrone.Content.Ascendant.Equipment.AscendantEquipmentPlayer.EnemyHit(p,info);
    public override void ModifyHurt(ref Player.HurtModifiers modifiers)
    {
        modifiers.ModifyHurtInfo+=(ref Player.HurtInfo info)=>
        {
            if(Player.whoAmI!=Main.myPlayer||!Expert[1]||Cooldowns[1]>0||!Enemy(Player,info))return;
            info.Damage=Math.Max(1,(int)(info.Damage*.7f));Cooldowns[1]=1500;ShieldSpeed=240;
        };
    }
    public override void OnHurt(Player.HurtInfo info)
    {
        if(Player.whoAmI!=Main.myPlayer||!Expert[0]||!Enemy(Player,info)||Player.dead||Player.statLife<=0)return;
        LostLife=Math.Min(Player.statLifeMax2,LostLife+info.Damage);
        if(Cooldowns[0]>0||LostLife<Player.statLifeMax2*.1f||Reservoir<=0)return;
        int amount=Math.Min(Reservoir,Math.Min((int)(Player.statLifeMax2*.12f),Player.statLifeMax2-Player.statLife));
        if(amount<=0)return;Player.statLife+=amount;Player.HealEffect(amount);Reservoir-=amount;LostLife=0;Cooldowns[0]=1800;
    }
    public override void OnHitNPCWithItem(Item item,NPC target,NPC.HitInfo hit,int damageDone)=>Charge(target,damageDone);
    public override void OnHitNPCWithProj(Projectile q,NPC target,NPC.HitInfo hit,int damageDone){if(q.owner==Player.whoAmI)Charge(target,damageDone);}
    internal void Charge(NPC target,int damage)=>ChargeAt(target,damage,Main.GameUpdateCount);
    internal void ChargeAt(NPC target,int damage,ulong tick)
    {
        if(Player.whoAmI!=Main.myPlayer||!Player.active||Player.dead||!Expert[0]||damage<=0||!target.active||!target.boss||target.friendly||target.dontTakeDamage||target.type==NPCID.TargetDummy)return;
        if(chargeStamp!=ulong.MaxValue&&tick-chargeStamp<30)return;chargeStamp=tick;
        Reservoir=Math.Min((int)(Player.statLifeMax2*.12f),Reservoir+Math.Max(1,(int)(Player.statLifeMax2*.02f)));
    }
    private int EncounterHash()
    {
        int hash=17;foreach(NPC n in Main.ActiveNPCs)if(n.boss){hash=unchecked(hash*31+n.whoAmI*17+n.type);if(n.ModNPC is SupremeBossNPC b)hash=unchecked(hash*31+b.Serial);}return hash;
    }
    internal void RecordPosition()=>RecordPositionAt(Main.GameUpdateCount);
    internal void RecordPositionAt(ulong tick)
    {
        if(historyStamp==tick)return;historyStamp=tick;
        int hash=EncounterHash();if(hash!=encounterHash){samples=cursor=0;encounterHash=hash;}
        if(samples>0&&Vector2.DistanceSquared(history[(cursor+120)%121],Player.position)>1000*1000)samples=cursor=0;
        history[cursor]=Player.position;cursor=(cursor+1)%121;samples=Math.Min(121,samples+1);
    }
    public static bool SafeRewind(Player p,Vector2 destination)
    {
        if(!SupremeArsenal.Finite(destination)||destination.X<32||destination.Y<32||destination.X+p.width>Main.maxTilesX*16-32||destination.Y+p.height>Main.maxTilesY*16-32||Vector2.DistanceSquared(destination,p.position)>1000*1000)return false;
        Vector2 endCenter=destination+p.Size/2;
        foreach(NPC n in Main.ActiveNPCs)
        {
            if(n.ModNPC is global::StarfallThrone.Content.Fable.Bosses.FableBossNPC||n.ModNPC is global::StarfallThrone.Content.NPCs.MiniBossNPC||n.ModNPC?.GetType().Name.StartsWith("SeedBoss")==true)return false;
            if(!n.boss)continue;
            if(n.ModNPC is not SupremeBossNPC b||!b.Ready||b.Cancelled||Vector2.DistanceSquared(p.Center,b.Arena)>600*600||Vector2.DistanceSquared(endCenter,b.Arena)>600*600)return false;
        }
        if(RewindBoundary!=null&&!RewindBoundary(p,destination))return false;
        int steps=Math.Max(1,(int)Math.Ceiling(Vector2.Distance(p.position,destination)/12));
        for(int j=0;j<=steps;j++)
        {
            Vector2 at=Vector2.Lerp(p.position,destination,j/(float)steps);
            if(Collision.SolidCollision(at,p.width,p.height))return false;
            Rectangle body=new((int)at.X,(int)at.Y,p.width,p.height);
            foreach(Projectile q in Main.ActiveProjectiles)if(q.hostile&&q.ModProjectile is SupremeHazard h&&h.CanDamage()!=false&&(h.Colliding(q.Hitbox,body)??q.Hitbox.Intersects(body)))return false;
        }
        return true;
    }
    public bool TryRewind()
    {
        if(Main.netMode==NetmodeID.MultiplayerClient||!Player.active||Player.dead||Player.ghost||!Expert[2]||Cooldowns[2]>0||samples<121||Player.mount.Active||Player.grapCount>0)return false;
        Vector2 at=history[cursor];if(!SafeRewind(Player,at))return false;
        Cooldowns[2]=2700;samples=cursor=0;Player.Teleport(at,TeleportationStyleID.RodOfDiscord);Player.velocity=Vector2.Zero;Player.fallStart=(int)(at.Y/16);Player.immune=true;Player.immuneTime=Math.Max(Player.immuneTime,8);
        if(Main.netMode==NetmodeID.Server){NetMessage.SendData(MessageID.TeleportEntity,-1,-1,null,0,Player.whoAmI,at.X,at.Y,TeleportationStyleID.RodOfDiscord);var packet=Mod.GetPacket();packet.Write((byte)93);packet.Write((byte)1);packet.Write((byte)Player.whoAmI);packet.Send();SendCooldowns(3,-1);}return true;
    }
    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        if(OathEquipmentKeys.Rewind?.JustPressed!=true||!Player.active||Player.dead||!Expert[2])return;
        if(Main.netMode==NetmodeID.MultiplayerClient){var packet=Mod.GetPacket();packet.Write((byte)93);packet.Write((byte)0);packet.Write((byte)Player.whoAmI);packet.Send();}else TryRewind();
    }
    public static void ReceivePacket(BinaryReader reader,int sender)
    {
        if(reader.BaseStream.Length-reader.BaseStream.Position<2)return;
        int mode=reader.ReadByte(),who=reader.ReadByte();int needed=mode is 0 or 1?0:mode is 2 or 3?6:-1;
        if(needed<0||who>=Main.maxPlayers||reader.BaseStream.Length-reader.BaseStream.Position!=needed)return;
        var s=Main.player[who].GetModPlayer<OathEquipmentPlayer>();
        if(mode==0){if(Main.netMode!=NetmodeID.Server||sender!=who||!s.Player.active||s.Player.dead||s.requestStamp!=ulong.MaxValue&&Main.GameUpdateCount-s.requestStamp<15)return;s.requestStamp=Main.GameUpdateCount;s.TryRewind();return;}
        if(mode==1){if(Main.netMode!=NetmodeID.MultiplayerClient)return;s.Cooldowns[2]=Math.Max(s.Cooldowns[2],2700);s.samples=s.cursor=0;s.Player.velocity=Vector2.Zero;s.Player.immune=true;s.Player.immuneTime=Math.Max(s.Player.immuneTime,8);return;}
        if(mode==2&&(Main.netMode!=NetmodeID.Server||sender!=who||!s.Player.active)||mode==3&&Main.netMode!=NetmodeID.MultiplayerClient)return;
        int[] values={reader.ReadUInt16(),reader.ReadUInt16(),reader.ReadUInt16()};if(values[0]>1800||values[1]>1500||values[2]>2700)return;
        for(int i=0;i<3;i++)s.Cooldowns[i]=Math.Max(s.Cooldowns[i],values[i]);if(mode==2)s.SendCooldowns(3,-1);
    }
}
