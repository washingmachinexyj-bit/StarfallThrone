#nullable enable
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Terraria.GameContent;
using StarfallThrone.Content.Oaths.Support;

namespace StarfallThrone.Content.Oaths.Equipment;

public sealed class SupremeShot:ModProjectile
{
    public int Weapon=>(int)Projectile.ai[0];public int Kind=>(int)Projectile.ai[1];public int Variant=>(int)Projectile.ai[2];
    private int age,duration=32;private bool hit,branched;
    public override string Texture=>OathSupport.Root+"EquipmentBolt";
    public override void SetDefaults(){Projectile.width=Projectile.height=14;Projectile.friendly=true;Projectile.aiStyle=-1;Projectile.timeLeft=80;Projectile.penetrate=1;Projectile.ignoreWater=true;Projectile.usesLocalNPCImmunity=true;Projectile.localNPCHitCooldown=-1;}
    public override void OnSpawn(IEntitySource source)=>Configure();
    public void Configure()
    {
        if(Weapon<0||Weapon>=12||Kind<0||Kind>3){Projectile.Kill();return;}
        Projectile.DamageType=SupremeArsenal.Class(Weapon);Projectile.tileCollide=Kind==1;
        if(Kind!=1){Projectile.penetrate=-1;Projectile.timeLeft=90;}
        if(Kind==0){duration=SupremeArsenal.Speed[Weapon];if(Projectile.owner>=0&&Projectile.owner<Main.maxPlayers&&Main.player[Projectile.owner].itemAnimationMax>0)duration=Math.Clamp(Main.player[Projectile.owner].itemAnimationMax,4,120);}
    }
    public override void SendExtraAI(BinaryWriter w){w.Write((byte)Math.Clamp(duration,4,120));}
    public override void ReceiveExtraAI(BinaryReader r)=>duration=Math.Clamp((int)r.ReadByte(),4,120);
    public override bool ShouldUpdatePosition()=>Kind==1;
    public override void AI()
    {
        if(Weapon<0||Weapon>=12||Projectile.owner<0||Projectile.owner>=Main.maxPlayers||!SupremeArsenal.Finite(Projectile.position)||!SupremeArsenal.Finite(Projectile.velocity)){Projectile.Kill();return;}
        Player p=Main.player[Projectile.owner];if(!p.active||p.dead){Projectile.Kill();return;}age++;
        if(Kind==0)
        {
            if(p.HeldItem.type!=OathCatalog.Weapon(Weapon)){Projectile.Kill();return;}
            Projectile.Center=p.MountedCenter;float progress=age/(float)duration;
            Projectile.rotation=Projectile.velocity.ToRotation()+MathHelper.Lerp(-1.25f,1.25f,progress)*p.direction;
            p.heldProj=Projectile.whoAmI;
            if(Weapon==0&&!branched&&progress>=.45f){branched=true;for(int n=-1;n<=1;n+=2)SupremeArsenal.Launch(Projectile.GetSource_FromThis(),Projectile.owner,0,1,Projectile.Center+Projectile.rotation.ToRotationVector2()*95,Projectile.rotation.ToRotationVector2().RotatedBy(n*.28)*21,(int)(Projectile.damage*.28f));}
            if(age>=duration)Projectile.Kill();
        }
        else if(Kind==1){Projectile.rotation=Projectile.velocity.ToRotation();if(Weapon==1)Projectile.velocity.Y+=.035f;if(Weapon==8&&age>=40)Projectile.Kill();}
        else if(Kind==2){Projectile.rotation=Projectile.velocity.ToRotation();if(age>=30)Projectile.Kill();}
        else if(age>=38)Projectile.Kill();
        if(!Main.dedServ)Lighting.AddLight(Projectile.Center,OathCatalog.Colors[Weapon/4].ToVector3()*.2f);
    }
    public override bool? CanDamage()=>Kind==0?age>duration*.15f&&age<duration*.85f:Kind==2?age>=18:Kind==3?age>=30:null;
    public override bool? CanHitNPC(NPC target)
    {
        if(Projectile.owner<0||Projectile.owner>=Main.maxPlayers)return false;
        Player p=Main.player[Projectile.owner];
        if(!Collision.CanHitLine(p.position,p.width,p.height,target.position,target.width,target.height)||!Collision.CanHitLine(Projectile.position,Projectile.width,Projectile.height,target.position,target.width,target.height))return false;
        return null;
    }
    public override bool? Colliding(Rectangle own,Rectangle target)
    {
        if(Kind==1)return null;
        if(Kind==3){Vector2 closest=Vector2.Clamp(Projectile.Center,target.TopLeft(),target.BottomRight());return Vector2.DistanceSquared(closest,Projectile.Center)<=100*100;}
        float collision=0,range=Kind==0?(Variant==1?220:180):650;
        return Collision.CheckAABBvLineCollision(target.TopLeft(),target.Size(),Projectile.Center,Projectile.Center+Projectile.rotation.ToRotationVector2()*range,Kind==0?32:14,ref collision);
    }
    public override void OnHitNPC(NPC target,NPC.HitInfo info,int damageDone)
    {
        if(Projectile.owner!=Main.myPlayer||damageDone<=0||target.friendly||target.type==NPCID.TargetDummy)return;
        hit=true;var state=Main.player[Projectile.owner].GetModPlayer<OathEquipmentPlayer>();
        if(Kind==0&&Variant==0){if(Weapon==4)state.EmberCharges=Math.Min(3,state.EmberCharges+1);if(Weapon==8)state.FateCharges=Math.Min(3,state.FateCharges+1);}
        if(Weapon==9){if(state.PrecisionTarget!=target.whoAmI||state.PrecisionType!=target.type){state.Precision=0;state.PrecisionTarget=target.whoAmI;state.PrecisionType=target.type;}state.Precision=Math.Min(10,state.Precision+1);state.PrecisionIdle=0;}
    }
    public override void OnKill(int timeLeft){if(Weapon==9&&!hit&&Projectile.owner==Main.myPlayer){var state=Main.player[Projectile.owner].GetModPlayer<OathEquipmentPlayer>();state.Precision=Math.Max(0,state.Precision-2);}}
    internal static void Line(Vector2 from,Vector2 to,Color c,float width){var d=to-from;Main.EntitySpriteDraw(TextureAssets.MagicPixel.Value,from-Main.screenPosition,null,c,d.ToRotation(),new Vector2(0,.5f),new Vector2(d.Length(),width),SpriteEffects.None);}
    internal static void Ring(Vector2 at,float radius,Color c,float width=2){for(int i=0;i<32;i++)Line(at+(i*MathHelper.TwoPi/32).ToRotationVector2()*radius,at+((i+1)*MathHelper.TwoPi/32).ToRotationVector2()*radius,c,width);}
    public override bool PreDraw(ref Color lightColor)
    {
        Color c=OathCatalog.Colors[Math.Clamp(Weapon/4,0,2)];Vector2 at=Projectile.Center;
        if(Kind==0){float length=Variant==1?220:180;Line(at,at+Projectile.rotation.ToRotationVector2()*length,c,12);Line(at,at+Projectile.rotation.ToRotationVector2()*(length-12),Color.White*.8f,3);for(int n=1;n<=3;n++)Line(at,at+(Projectile.rotation-n*.15f*Main.player[Projectile.owner].direction).ToRotationVector2()*length,c*(.22f/n),8);}
        else if(Kind==2)Line(at,at+Projectile.rotation.ToRotationVector2()*650,c*(age<18?.25f:1),age<18?2:14);
        else if(Kind==3){Ring(at,100,c*(age<30?.4f:.9f));if(age>=30){Ring(at,(age-29)*12,c);Line(at-new Vector2(90,0),at+new Vector2(90,0),Color.White,5);}}
        else{var tex=TextureAssets.Projectile[Type].Value;Main.EntitySpriteDraw(tex,at-Main.screenPosition,null,c,Projectile.rotation,tex.Size()/2,Weapon==8?2.2f:1.2f,SpriteEffects.None);Line(at-Projectile.velocity*1.3f,at,c*.35f,4);}
        return false;
    }
}
public abstract class SupremeMinionBuff:ModBuff
{
    public abstract int Index{get;}public override string Texture=>OathSupport.Root+"SupremeWeapon"+(Index*4+3);
    public override void SetStaticDefaults(){Main.buffNoSave[Type]=true;Main.buffNoTimeDisplay[Type]=true;}
    public override void Update(Player p,ref int buffIndex){int type=ModContent.Find<ModProjectile>(Mod.Name,"SupremeMinion"+Index).Type;if(p.ownedProjectileCounts[type]>0)p.buffTime[buffIndex]=18000;else{p.DelBuff(buffIndex);buffIndex--;}}
}
public abstract class SupremeMinion:ModProjectile
{
    public abstract int Index{get;}public override string Texture=>OathSupport.Root+"SupremeMinion"+Index;
    public override void SetStaticDefaults(){ProjectileID.Sets.MinionSacrificable[Type]=true;ProjectileID.Sets.MinionTargettingFeature[Type]=true;}
    public override void SetDefaults(){Projectile.width=Projectile.height=40;Projectile.aiStyle=-1;Projectile.minion=true;Projectile.minionSlots=Index==2?4:2;Projectile.friendly=true;Projectile.DamageType=DamageClass.Summon;Projectile.tileCollide=false;Projectile.ignoreWater=true;Projectile.penetrate=-1;Projectile.timeLeft=18000;Projectile.netImportant=true;}
    public override bool MinionContactDamage()=>false;
    public override void AI()
    {
        if(Projectile.owner<0||Projectile.owner>=Main.maxPlayers){Projectile.Kill();return;}
        Player p=Main.player[Projectile.owner];int buff=ModContent.Find<ModBuff>(Mod.Name,"SupremeMinionBuff"+Index).Type;
        if(!p.active||p.dead){p.ClearBuff(buff);Projectile.Kill();return;}if(p.HasBuff(buff))Projectile.timeLeft=2;
        NPC? target=global::StarfallThrone.Content.Pantheon.Equipment.PantheonArsenal.Target(Projectile.Center,Projectile.owner,850);
        Vector2 home=p.Center+new Vector2((Projectile.minionPos%5-2)*55,-100-Projectile.minionPos/5*35);
        Vector2 at=target==null?home:Vector2.Lerp(home,target.Center+new Vector2(0,-160),.55f);
        if(Vector2.DistanceSquared(Projectile.Center,p.Center)>1600*1600){Projectile.Center=home;Projectile.velocity=Vector2.Zero;Projectile.netUpdate=true;}
        Projectile.velocity=Vector2.Lerp(Projectile.velocity,(at-Projectile.Center)*.08f,.15f);if(Projectile.velocity.Length()>16)Projectile.velocity=Projectile.velocity.SafeNormalize(Vector2.Zero)*16;
        Projectile.rotation=Projectile.velocity.X*.012f;
        int period=Index==0?90:Index==1?72:60;
        if(Index==1&&p.GetModPlayer<OathEquipmentPlayer>().CommandTime>0)period=42;
        if(target!=null&&++Projectile.ai[0]>=period&&Projectile.owner==Main.myPlayer)
        {
            Projectile.ai[0]=0;Vector2 dir=(target.Center-Projectile.Center).SafeNormalize(Vector2.UnitX);int weapon=Index*4+3;
            if(Index==0)for(int n=-1;n<=1;n++)SupremeArsenal.Launch(Projectile.GetSource_FromThis(),Projectile.owner,weapon,1,Projectile.Center+new Vector2(n*20,0),(target.Center-Projectile.Center-new Vector2(n*20,0)).SafeNormalize(dir)*22,(int)(Projectile.damage*.55f));
            else SupremeArsenal.Launch(Projectile.GetSource_FromThis(),Projectile.owner,weapon,1,Projectile.Center,dir*(Index==1?26:30),(int)(Projectile.damage*(Index==1?1.7f:3f)));
            Projectile.netUpdate=true;
        }
    }
    public override bool PreDraw(ref Color lightColor)
    {
        var tex=TextureAssets.Projectile[Type].Value;Color c=OathCatalog.Colors[Index];Main.EntitySpriteDraw(tex,Projectile.Center-Main.screenPosition,null,Color.Lerp(lightColor,Color.White,.5f),Projectile.rotation,tex.Size()/2,1,SpriteEffects.None);
        if(Index==2)for(int n=0;n<3;n++){Vector2 at=Projectile.Center+((float)Main.GlobalTimeWrappedHourly*.8f+n*MathHelper.TwoPi/3).ToRotationVector2()*50;SupremeShot.Ring(at,7,c*.65f);}
        return false;
    }
}
