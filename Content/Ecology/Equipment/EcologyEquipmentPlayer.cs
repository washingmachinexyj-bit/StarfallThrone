using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using global::StarfallThrone.Content.Voyage.Equipment;

namespace StarfallThrone.Content.Ecology.Equipment;

public sealed class EcologyEquipmentKeys:ModSystem
{
    public static ModKeybind TideStance;
    public override void Load()=>TideStance=KeybindLoader.RegisterKeybind(Mod,"EcologyTideStance","N");
    public override void Unload()=>TideStance=null;
}
public sealed class EcologyEquipmentPlayer:ModPlayer
{
    public readonly bool[] Expert=new bool[10];
    // 0..9 expert, 10..19 armor, 20 stance, 21 orbit expert attack. No reset on unequip/death.
    public readonly int[] Cooldowns=new int[22];
    public int SetBiome=-1,Combo,Cadence,Calm,Still,MistTime,HeatTime,SetHeatTime,HeatHits;
    public int Target=-1,TargetType,TargetHits,RhythmHits,HiveHits;
    public bool Assault;
    private ulong tick=ulong.MaxValue,lastHit=ulong.MaxValue;
    public override void ResetEffects(){Array.Clear(Expert);SetBiome=-1;}
    public override void PreUpdate()
    {
        if(tick==Main.GameUpdateCount)return;tick=Main.GameUpdateCount;
        for(int i=0;i<Cooldowns.Length;i++)if(Cooldowns[i]>0)Cooldowns[i]--;
        if(MistTime>0)MistTime--;if(HeatTime>0)HeatTime--;if(SetHeatTime>0)SetHeatTime--;
        Calm=Math.Min(600,Calm+1);
        Still=Math.Abs(Player.velocity.X)<.2f&&Math.Abs(Player.velocity.Y)<.2f?Math.Min(180,Still+1):0;
    }
    public override void PostUpdateEquips()
    {
        if(Expert[0]&&MistTime>0)Player.moveSpeed+=.25f;
        if(Expert[4]&&HeatTime>0)Player.GetDamage(DamageClass.Generic)+=.12f;
        if(SetBiome==4&&SetHeatTime>0)Player.GetDamage(DamageClass.Generic)+=.10f;
        if(SetBiome!=4){HeatHits=0;SetHeatTime=0;}
        if(SetBiome==5&&Still>=90)Player.GetDamage(DamageClass.Generic)+=.08f;
        if(SetBiome==6&&Still>=60)Player.lifeRegen+=2;
        if(Expert[8]||SetBiome==8)
        {
            if(Assault)Player.GetDamage(DamageClass.Generic)+=(Expert[8]?.12f:0)+(SetBiome==8?.06f:0);
            else Player.moveSpeed+=(Expert[8]?.20f:0)+(SetBiome==8?.12f:0);
        }
        if(!Expert[2]){Target=-1;TargetHits=0;}
        if(SetBiome!=2)HiveHits=0;
    }
    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        if(EcologyEquipmentKeys.TideStance!=null&&EcologyEquipmentKeys.TideStance.JustPressed)TrySwitchStance();
    }
    public bool TrySwitchStance()
    {
        if(Player.whoAmI!=Main.myPlayer||!Player.active||Player.dead||(!Expert[8]&&SetBiome!=8)||Cooldowns[20]>0)return false;
        Assault=!Assault;Cooldowns[20]=120;
        if(!Main.dedServ)CombatText.NewText(Player.Hitbox,EcologyCatalog.Colors[8],
            Language.GetTextValue("Mods.StarfallThrone.EcologyEquipment."+(Assault?"Assault":"Flow")));
        return true;
    }
    public override void UpdateDead()
    {
        PreUpdate();ResetEffects();Calm=Still=MistTime=HeatTime=SetHeatTime=HeatHits=HiveHits=RhythmHits=TargetHits=0;
        Target=-1;Combo=Cadence=0;
    }
    public override void OnEnterWorld(){Calm=Still=MistTime=HeatTime=SetHeatTime=HeatHits=TargetHits=HiveHits=RhythmHits=0;Target=-1;}
    public override void SaveData(TagCompound tag)
    {tag["ecologyGearCooldowns"]=(int[])Cooldowns.Clone();tag["ecologyAssault"]=Assault;}
    public override void LoadData(TagCompound tag)
    {
        int[] data=tag.GetIntArray("ecologyGearCooldowns");Array.Clear(Cooldowns);
        for(int i=0;i<Math.Min(data.Length,Cooldowns.Length);i++)Cooldowns[i]=Math.Clamp(data[i],0,3600);
        Assault=tag.GetBool("ecologyAssault");
    }
    public override void ModifyHurt(ref Player.HurtModifiers modifiers)
    {
        if(Player.whoAmI!=Main.myPlayer||modifiers.PvP)return;
        float fromX=-modifiers.HitDirection;
        if(modifiers.DamageSource!=null&&modifiers.DamageSource.TryGetCausingEntity(out Entity source)&&source!=null)
            fromX=source.Center.X-Player.Center.X;
        bool front=fromX*Player.direction>0;
        modifiers.ModifyHurtInfo+=(ref Player.HurtInfo info)=>
        {
            if(info.Damage<=0)return;
            if(Expert[1]&&Cooldowns[1]==0)
            {
                int reduction=Math.Min(20,info.Damage/2);
                if(reduction>0){info.Damage=Math.Max(1,info.Damage-reduction);Cooldowns[1]=720;}
            }
            if(Expert[7]&&front&&Cooldowns[7]==0)
            {
                int reduction=Math.Min(45,(int)(info.Damage*.30f));
                if(reduction>0){info.Damage=Math.Max(1,info.Damage-reduction);Cooldowns[7]=480;}
            }
            if(Expert[9]&&info.Damage>=80&&Cooldowns[9]==0)
            {info.Damage=Math.Max(1,info.Damage-Math.Min(60,info.Damage/4));Cooldowns[9]=900;}
            if(SetBiome==7&&Calm>=300&&Cooldowns[17]==0)
            {
                int reduction=Math.Min(30,(int)(info.Damage*.20f));
                if(reduction>0){info.Damage=Math.Max(1,info.Damage-reduction);Cooldowns[17]=600;}
            }
        };
    }
    public override void OnHurt(Player.HurtInfo info)
    {
        Calm=0;HeatHits=Math.Max(0,HeatHits-2);
        if(Player.whoAmI!=Main.myPlayer||info.PvP||info.Damage<=0)return;
        if(Expert[0]&&Cooldowns[0]==0){MistTime=180;Cooldowns[0]=600;}
        if(Expert[4]&&Cooldowns[4]==0){HeatTime=240;Cooldowns[4]=720;}
    }
    public override void OnHitNPCWithItem(Item item,NPC target,NPC.HitInfo hit,int damageDone)
    {
        if(item.noMelee||item.damage<=0)return;RecordHit(target,damageDone);
    }
    public override void OnHitNPCWithProj(Projectile projectile,NPC target,NPC.HitInfo hit,int damageDone)
    {
        if(!VoyageEquipmentProjectileOrigin.IsOwnedPrimary(Player,projectile))return;RecordHit(target,damageDone);
    }
    private void Proc(NPC target,int biome,int damage,EcologyShotKind kind=EcologyShotKind.EquipmentProc,int count=1)
    {
        Vector2 d=(target.Center-Player.Center).SafeNormalize(Vector2.UnitX);
        for(int i=0;i<count;i++)
        {
            Vector2 at=kind is EcologyShotKind.Burst or EcologyShotKind.Echo or EcologyShotKind.RootBud?target.Center:Player.Center;
            Vector2 speed=kind is EcologyShotKind.Burst or EcologyShotKind.Echo or EcologyShotKind.RootBud?Vector2.Zero:d.RotatedBy((i-(count-1)/2f)*.10f)*14;
            EcologyArsenal.Launch(Player.GetSource_Misc("EcologyGear"),Player.whoAmI,biome*4,kind,at,speed,damage,1,secondary:true);
        }
    }
    public void RecordHit(NPC target,int damage)
    {
        if(Player.whoAmI!=Main.myPlayer||Main.netMode==NetmodeID.Server||!Player.active||Player.dead||damage<=0||
            target.friendly||target.type==NPCID.TargetDummy||!target.CanBeChasedBy()||Vector2.DistanceSquared(Player.Center,target.Center)>1100*1100||
            !Collision.CanHitLine(Player.Center,1,1,target.Center,1,1)||
            lastHit!=ulong.MaxValue&&Main.GameUpdateCount-lastHit<12)return;
        lastHit=Main.GameUpdateCount;
        if(Expert[2])
        {
            if(Target!=target.whoAmI||TargetType!=target.type){Target=target.whoAmI;TargetType=target.type;TargetHits=0;}
            TargetHits=Math.Min(6,TargetHits+1);
            if(TargetHits>=6&&Cooldowns[2]==0){TargetHits=0;Cooldowns[2]=180;Proc(target,2,12,count:3);}
        }
        if(Expert[3]&&++RhythmHits>=4&&Cooldowns[3]==0)
        {RhythmHits=0;Cooldowns[3]=120;Proc(target,3,40,EcologyShotKind.Echo);}
        if(Expert[5]&&Cooldowns[5]==0){Cooldowns[5]=180;Proc(target,5,45);}
        if(Expert[6]&&Cooldowns[6]==0&&Player.statLife<Player.statLifeMax2)
        {Cooldowns[6]=300;int amount=Math.Min(4,Player.statLifeMax2-Player.statLife);Player.statLife+=amount;Player.HealEffect(amount);}
        if(Expert[9]&&Cooldowns[21]==0){Cooldowns[21]=180;Proc(target,9,70);}
        if(SetBiome<0)return;
        int b=SetBiome,cd=10+b;
        switch(b)
        {
            case 0:
                if(Calm>=360&&Cooldowns[cd]==0){Calm=0;Cooldowns[cd]=360;Proc(target,0,12);}break;
            case 1:
                if(Cooldowns[cd]==0){Cooldowns[cd]=240;Proc(target,1,18,EcologyShotKind.Burst);}break;
            case 2:
                if(++HiveHits>=3&&Cooldowns[cd]==0){HiveHits=0;Cooldowns[cd]=180;Proc(target,2,8,count:3);}break;
            case 3:
                if(Cooldowns[cd]==0){Cooldowns[cd]=180;Proc(target,3,26,EcologyShotKind.Echo);}break;
            case 4:
                if(SetHeatTime==0&&Cooldowns[cd]==0&&++HeatHits>=6){HeatHits=0;SetHeatTime=240;Cooldowns[cd]=600;}break;
            case 5:
                if(Still>=90&&Cooldowns[cd]==0){Cooldowns[cd]=240;Proc(target,5,46);}break;
            case 6:
                if(Cooldowns[cd]==0){Cooldowns[cd]=300;Proc(target,6,56,EcologyShotKind.RootBud);}break;
            case 9:
                // Separate armor timer from the expert timer: wearing both never resets either cooldown.
                if(Cooldowns[19]==0){Cooldowns[19]=180;Proc(target,9,32,count:3);}break;
        }
    }
}
