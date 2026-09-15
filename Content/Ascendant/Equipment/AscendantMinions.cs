#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using global::StarfallThrone.Content.Systems;
using global::StarfallThrone.Content.Voyage.Weapons;

namespace StarfallThrone.Content.Ascendant.Equipment;

public abstract class AscendantMinionBuff:ModBuff
{
    public abstract int Tier{get;}
    public override string Texture=>AscendantArsenal.Art(Tier*4+3);
    public override void SetStaticDefaults(){Main.buffNoSave[Type]=true;Main.buffNoTimeDisplay[Type]=true;}
    public override void Update(Player p,ref int buffIndex)
    {if(p.ownedProjectileCounts[AscendantArsenal.Minion(Tier)]>0)p.buffTime[buffIndex]=18000;else{p.DelBuff(buffIndex);buffIndex--;}}
}
public sealed class AscendantMinionBuff0:AscendantMinionBuff{public override int Tier=>0;}
public sealed class AscendantMinionBuff1:AscendantMinionBuff{public override int Tier=>1;}
public sealed class AscendantMinionBuff2:AscendantMinionBuff{public override int Tier=>2;}

public abstract class AscendantMinion:ModProjectile,IVoyageWeaponProjectile
{
    public abstract int Tier{get;}
    public int WeaponIndex=>-1;
    public bool Secondary=>false;
    public override string Texture=>AscendantCatalog.Root+"Minion"+Tier;
    public override void SetStaticDefaults()
    {Main.projPet[Type]=true;ProjectileID.Sets.MinionSacrificable[Type]=true;ProjectileID.Sets.MinionTargettingFeature[Type]=true;}
    public override void SetDefaults()
    {
        Projectile.width=Projectile.height=Tier==0?26:40;Projectile.friendly=true;Projectile.minion=true;Projectile.minionSlots=Tier==0?1:2;
        Projectile.DamageType=DamageClass.Summon;Projectile.penetrate=-1;Projectile.tileCollide=false;Projectile.ignoreWater=true;Projectile.timeLeft=18000;
        Projectile.usesLocalNPCImmunity=true;Projectile.localNPCHitCooldown=30;
    }
    public override bool MinionContactDamage()=>true;
    public override bool? CanCutTiles()=>false;
    public override bool? CanDamage()
    {
        int phase=(int)Projectile.ai[0];
        return Tier==0?phase is >=24 and <42?null:false:Tier==1?phase is >=30 and <49?null:false:phase is >=30 and <50?null:false;
    }
    public override bool? CanHitNPC(NPC target)=>Collision.CanHitLine(Projectile.Center,1,1,target.position,target.width,target.height)?null:false;
    private bool Valid(NPC n,Player p,float range)=>n.CanBeChasedBy()&&Vector2.DistanceSquared(n.Center,p.Center)<range*range&&Collision.CanHitLine(Projectile.Center,1,1,n.Center,1,1);
    private NPC? Target(Player p)
    {
        int preferred=p.MinionAttackTargetNPC;
        if(preferred>=0&&preferred<Main.maxNPCs&&Valid(Main.npc[preferred],p,900))return Main.npc[preferred];
        NPC? best=null;float limit=750*750;
        foreach(NPC n in Main.ActiveNPCs)
        {float d=Vector2.DistanceSquared(Projectile.Center,n.Center);if(d<limit&&Valid(n,p,900)){best=n;limit=d;}}
        return best;
    }
    public override void AI()
    {
        if(Projectile.owner<0||Projectile.owner>=Main.maxPlayers){Projectile.Kill();return;}
        Player p=Main.player[Projectile.owner];
        if(!p.active||p.dead||!p.HasBuff(AscendantArsenal.Buff(Tier))){Projectile.Kill();return;}
        Projectile.timeLeft=2;
        if(Vector2.DistanceSquared(p.Center,Projectile.Center)>1400*1400)
        {Projectile.Center=p.Center;Projectile.velocity=Vector2.Zero;Projectile.netUpdate=true;}
        bool owner=Projectile.owner==Main.myPlayer;
        if(owner)
        {
            NPC? chosen=Target(p);int next=chosen?.whoAmI??-1;
            if((int)Projectile.ai[1]!=next+1)
            {Projectile.ai[1]=next+1;Projectile.ai[2]=chosen?.type??0;Projectile.ai[0]=0;Projectile.netUpdate=true;}
        }
        int targetIndex=(int)Projectile.ai[1]-1;
        NPC? target=targetIndex>=0&&targetIndex<Main.maxNPCs&&Main.npc[targetIndex].type==(int)Projectile.ai[2]&&Valid(Main.npc[targetIndex],p,1000)?Main.npc[targetIndex]:null;
        if(target==null)
        {Projectile.ai[0]=0;Move(p.Center+new Vector2(-p.direction*(55+Projectile.minionPos*28),-55-Projectile.minionPos%2*25),11);return;}
        int period=Tier==0?105:Tier==1?160:320;
        Projectile.ai[0]=(Projectile.ai[0]+1)%period;int phase=(int)Projectile.ai[0];
        void Bolt(float power,float speed,float spread=0,int variant=0)
            =>AscendantArsenal.Launch(Projectile.GetSource_FromThis(),Projectile.owner,Tier*4+3,AscendantShotKind.MinionBolt,Projectile.Center,
                (target.Center-Projectile.Center).SafeNormalize(Vector2.UnitX).RotatedBy(spread)*speed,(int)(Projectile.damage*power),2,variant);
        if(Tier==0)
        {
            if(phase<24)Move(target.Center+new Vector2(-p.direction*80,-35),12);
            else if(phase==24)Projectile.velocity=(target.Center-Projectile.Center).SafeNormalize(Vector2.UnitX)*20;
            else if(phase>=42)Move(target.Center+new Vector2(-p.direction*130,-80),12);
            if(owner&&phase==72){Bolt(.40f,11,-.20f);Bolt(.40f,11,.20f);}
        }
        else if(Tier==1)
        {
            if(phase<30)Move(target.Center+new Vector2(-p.direction*95,-10),11);
            else if(phase==30)Projectile.velocity=(target.Center-Projectile.Center).SafeNormalize(Vector2.UnitX)*22;
            else if(phase>=49)Move(target.Center+new Vector2(-p.direction*155,-60),12);
            if(owner&&phase==70)Bolt(.60f,18,variant:1);
            if(owner&&phase==100){Bolt(.30f,13,-.13f,2);Bolt(.30f,13,.13f,2);}
            if(owner&&phase==130)Bolt(.80f,10,variant:3);
        }
        else
        {
            if(phase<30)Move(target.Center+new Vector2(-p.direction*120,-40),15);
            else if(phase==30)Projectile.velocity=(target.Center-Projectile.Center).SafeNormalize(Vector2.UnitX)*26;
            else if(phase>=50&&phase<160)Move(target.Center+new Vector2(-p.direction*180,-90),15);
            else if(phase>=160)Move(p.Center+new Vector2(-p.direction*(90+Projectile.minionPos*25),-95),16);
            if(owner&&phase is 80 or 110)Bolt(.35f,22);
            if(owner&&phase is 180 or 202 or 224 or 246 or 268)
            {
                int law=(phase-180)/22;
                if(law==2){Bolt(.22f,18,-.15f,law);Bolt(.22f,18,.15f,law);}
                else Bolt(law==3?.65f:.45f,law==3?12:22,variant:law);
            }
        }
        Projectile.rotation=Math.Clamp(Projectile.velocity.X*.016f,-.4f,.4f);
        if(!Main.dedServ)Lighting.AddLight(Projectile.Center,AscendantEquipmentData.Color(Tier).ToVector3()*.35f);
    }
    private void Move(Vector2 at,float speed)
    {Vector2 d=at-Projectile.Center;Projectile.velocity=(Projectile.velocity*7+(d.Length()<speed?d:d.SafeNormalize(Vector2.UnitX)*speed))/8;}
    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture=ModContent.Request<Texture2D>(Texture).Value;Vector2 at=Projectile.Center-Main.screenPosition;Color c=AscendantEquipmentData.Color(Tier);
        Main.spriteBatch.Draw(texture,at,null,Color.White,Projectile.rotation,texture.Size()/2,Tier==0?.65f:1f,SpriteEffects.None,0);
        int wheels=Tier==0?3:Tier==1?4:5;
        for(int i=0;i<wheels;i++)
        {
            float angle=i*MathHelper.TwoPi/wheels+Projectile.ai[0]*.025f;
            CombatDrawing.Circle(Main.spriteBatch,at+angle.ToRotationVector2()*(Tier==0?20:34),Tier==0?3:6,c*.75f,2);
        }
        if(CanDamage()!=false)CombatDrawing.Circle(Main.spriteBatch,at,Tier==0?21:30,c,3);
        return false;
    }
}
public sealed class AscendantMinion0:AscendantMinion{public override int Tier=>0;}
public sealed class AscendantMinion1:AscendantMinion{public override int Tier=>1;}
public sealed class AscendantMinion2:AscendantMinion{public override int Tier=>2;}
