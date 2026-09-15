#nullable enable
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using global::StarfallThrone.Content.Systems;
using global::StarfallThrone.Content.Voyage.Weapons;

namespace StarfallThrone.Content.Ecology.Equipment;

public enum EcologyShotKind
{
    Swing, Thrust, Vine, Gear, SunDisc, TideBlade, OrbitBlade,
    MirrorArrow, MirrorBolt, WaxArrow, Needle, SoundBolt, HeatBullet, Rivet, Seed, SunSlug, TideArrow, PhaseRound,
    Node, SoundRing, RisingFlame, SunArray, Bubble, Gate,
    Crescent, Burst, Echo, RootBud, MinionBolt, EquipmentProc
}

public class EcologyShot : ModProjectile, IVoyageWeaponProjectile
{
    public int Weapon=>Math.Clamp((int)Projectile.ai[0],0,39);
    public int Biome=>Weapon/4;
    public EcologyShotKind Kind=>(EcologyShotKind)(int)Projectile.ai[1];
    public int WeaponIndex=>-1;
    public bool ExplicitSecondary;
    public bool Secondary=>ExplicitSecondary||Kind is EcologyShotKind.Crescent or EcologyShotKind.Burst or EcologyShotKind.Echo or EcologyShotKind.RootBud or EcologyShotKind.MinionBolt or EcologyShotKind.EquipmentProc;
    public Vector2 Focus;
    public int Age;
    private bool initialized,hitEffect;
    private float angle;
    private int duration;
    private int bounces;
    private Vector2 origin;
    public override string Texture=>EcologyCatalog.Root+"Weapon0";
    public override void SetDefaults()
    {
        Projectile.width=Projectile.height=18;Projectile.friendly=true;Projectile.ignoreWater=true;
        Projectile.penetrate=1;Projectile.timeLeft=180;Projectile.tileCollide=true;
        Projectile.usesLocalNPCImmunity=true;Projectile.localNPCHitCooldown=-1;
    }
    private void Initialize()
    {
        if(initialized)return;initialized=true;angle=Projectile.velocity.ToRotation();origin=Projectile.Center;
        Projectile.DamageType=Kind==EcologyShotKind.EquipmentProc?DamageClass.Generic:EcologyEquipmentData.Class(Weapon%4);
        duration=Kind switch
        {
            EcologyShotKind.Swing or EcologyShotKind.Thrust or EcologyShotKind.Vine=>EcologyEquipmentData.UseTimes[Weapon],
            EcologyShotKind.Node=>360,EcologyShotKind.RootBud=>120,EcologyShotKind.Burst=>42,EcologyShotKind.Echo=>28,
            EcologyShotKind.SoundRing=>65,EcologyShotKind.SunArray=>54,EcologyShotKind.Gate=>48,
            EcologyShotKind.Gear or EcologyShotKind.SunDisc=>90,EcologyShotKind.OrbitBlade=>80,
            EcologyShotKind.TideBlade=>38,EcologyShotKind.RisingFlame=>48,EcologyShotKind.Bubble=>55,
            EcologyShotKind.Crescent=>34,EcologyShotKind.EquipmentProc=>60,_=>120
        };
        Projectile.timeLeft=duration;
        if(Kind is EcologyShotKind.Swing or EcologyShotKind.Thrust or EcologyShotKind.Vine or EcologyShotKind.Gear or EcologyShotKind.SunDisc or EcologyShotKind.OrbitBlade or EcologyShotKind.TideBlade or EcologyShotKind.Node or EcologyShotKind.SoundRing or EcologyShotKind.SunArray or EcologyShotKind.Burst or EcologyShotKind.Echo or EcologyShotKind.RootBud or EcologyShotKind.Gate)
        {Projectile.penetrate=-1;Projectile.tileCollide=false;}
        if(Kind is EcologyShotKind.Gear or EcologyShotKind.SunDisc)Projectile.localNPCHitCooldown=25;
        if(Kind==EcologyShotKind.SoundBolt){Projectile.penetrate=3;Projectile.width=Projectile.height=30;}
        if(Kind==EcologyShotKind.Rivet&&Projectile.ai[2]==2)Projectile.penetrate=2;
        if(Kind==EcologyShotKind.HeatBullet&&Projectile.ai[2]==0)Projectile.penetrate=2;
        if(Kind==EcologyShotKind.SunArray)Projectile.localNPCHitCooldown=18;
        if(Kind==EcologyShotKind.MirrorArrow)Projectile.penetrate=2;
        if(Kind==EcologyShotKind.Bubble)Projectile.penetrate=-1;
        if(Kind==EcologyShotKind.PhaseRound)Projectile.penetrate=3;
    }
    public override void AI()
    {
        Initialize();
        if(!Enum.IsDefined(Kind)||Projectile.owner<0||Projectile.owner>=Main.maxPlayers||
            !Main.player[Projectile.owner].active||Main.player[Projectile.owner].dead){Projectile.Kill();return;}
        Player p=Main.player[Projectile.owner];Age++;
        Projectile.rotation=Projectile.velocity.ToRotation();
        Vector2 d=angle.ToRotationVector2();
        switch(Kind)
        {
            case EcologyShotKind.Swing:
                if(p.HeldItem.type!=EcologyCatalog.Weapon(Weapon)){Projectile.Kill();return;}
                float arc=MathHelper.Lerp(-1.25f,1.25f,Age/(float)duration)*(Projectile.ai[2]==1?-1:1);
                Projectile.rotation=angle+arc;Projectile.Center=p.MountedCenter+Projectile.rotation.ToRotationVector2()*(Biome==2?58:46);
                p.heldProj=Projectile.whoAmI;
                if(Age==duration/2)
                {
                    if(Biome==0)Child(EcologyShotKind.Crescent,Projectile.Center,d*11,.45f);
                    if(Biome==3)Child(EcologyShotKind.Echo,Projectile.Center,Vector2.Zero,.55f);
                }
                break;
            case EcologyShotKind.Thrust:
                if(p.HeldItem.type!=EcologyCatalog.Weapon(Weapon)){Projectile.Kill();return;}
                Projectile.rotation=angle;Projectile.Center=p.MountedCenter+d*(28+MathF.Sin(Age/(float)duration*MathHelper.Pi)*85);
                p.heldProj=Projectile.whoAmI;
                if(Biome==4&&Age==duration/2)Child(EcologyShotKind.RisingFlame,Projectile.Center,new Vector2(0,-7),.45f);
                break;
            case EcologyShotKind.Vine:
                if(p.HeldItem.type!=EcologyCatalog.Weapon(Weapon)){Projectile.Kill();return;}
                Projectile.rotation=angle;Projectile.Center=p.MountedCenter+d*(30+MathF.Sin(Age/(float)duration*MathHelper.Pi)*200);
                p.heldProj=Projectile.whoAmI;break;
            case EcologyShotKind.Gear:
            case EcologyShotKind.SunDisc:
                Projectile.rotation=Age*(Kind==EcologyShotKind.Gear?.32f:.18f);
                if(Age>22)
                {
                    Projectile.velocity=(p.MountedCenter-Projectile.Center).SafeNormalize(Vector2.UnitX)*19;
                    if(Vector2.DistanceSquared(p.MountedCenter,Projectile.Center)<24*24){Projectile.Kill();return;}
                }
                if(Kind==EcologyShotKind.Gear&&Age is 10 or 18)
                    Child(EcologyShotKind.Rivet,Projectile.Center,d.RotatedBy(Age==10?.7f:-.7f)*12,.35f);
                if(Kind==EcologyShotKind.SunDisc&&Age==22)
                    for(int i=0;i<3;i++)Child(EcologyShotKind.MinionBolt,Projectile.Center,(angle+(i-1)*.30f).ToRotationVector2()*12,.3f);
                break;
            case EcologyShotKind.OrbitBlade:
                if(Age<=24)
                {
                    float a=angle+Age*.23f;Projectile.Center=p.MountedCenter+a.ToRotationVector2()*72;
                    Projectile.rotation=a+MathHelper.PiOver2;Projectile.velocity=Vector2.Zero;
                }
                else if(Age==25){Projectile.velocity=d*23;Projectile.netUpdate=true;}
                break;
            case EcologyShotKind.TideBlade:
                Projectile.velocity=d*13+d.RotatedBy(MathHelper.PiOver2)*MathF.Cos(Age*.22f)*5;
                Projectile.rotation=angle+MathF.Sin(Age*.22f)*.5f;break;
            case EcologyShotKind.MirrorBolt:
                if(Age<=20)Projectile.velocity=Projectile.velocity.RotatedBy(Projectile.ai[2]==0?.016f:-.016f);break;
            case EcologyShotKind.MirrorArrow:Projectile.rotation+=MathHelper.PiOver2;break;
            case EcologyShotKind.Seed:Projectile.velocity.Y+=.18f;Projectile.rotation+=Age*.1f;break;
            case EcologyShotKind.HeatBullet:
                if(Projectile.ai[2]==1){Projectile.scale=1.35f;Projectile.velocity*=.994f;}break;
            case EcologyShotKind.TideArrow:
                Projectile.velocity=d*18+d.RotatedBy(MathHelper.PiOver2)*MathF.Cos(Age*.22f)*3;
                if(Age==24)
                    for(int i=-1;i<=1;i+=2)Child(EcologyShotKind.Needle,Projectile.Center,d.RotatedBy(i*.23f)*16,.30f);
                break;
            case EcologyShotKind.PhaseRound:
                if(Age==18)
                {
                    Vector2 next=EcologyArsenal.Aim(p,Projectile.Center+d*160,1100);
                    if(Vector2.DistanceSquared(next,Projectile.Center)<180*180&&Vector2.Dot(next-Projectile.Center,d)>0&&Collision.CanHitLine(Projectile.Center,1,1,next,1,1))
                    {Focus=Projectile.Center;Projectile.Center=next;Projectile.netUpdate=true;}
                }
                break;
            case EcologyShotKind.Node:NodeAI(p);break;
            case EcologyShotKind.RootBud:
                Projectile.velocity=Vector2.Zero;
                if(Age is 30 or 65 or 100)FireAtTarget(p,EcologyShotKind.Needle,.35f,11,360);
                break;
            case EcologyShotKind.SoundRing:
                if(Age<Projectile.ai[2])Projectile.velocity=Vector2.Zero;
                else Projectile.velocity=d*7;
                Projectile.scale=.6f+Math.Max(0,Age-Projectile.ai[2])*.012f;break;
            case EcologyShotKind.RisingFlame:Projectile.velocity.Y=Math.Max(-12,Projectile.velocity.Y-.08f);break;
            case EcologyShotKind.Bubble:
                Projectile.velocity*=.965f;
                if(Age==42)
                {
                    NPC? target=FindTarget(Projectile.Center,p,600);
                    Vector2 aim=target==null?d:(target.Center-Projectile.Center).SafeNormalize(d);
                    for(int i=-1;i<=1;i++)Child(EcologyShotKind.Needle,Projectile.Center,aim.RotatedBy(i*.32f)*16,.55f);
                    Projectile.Kill();return;
                }break;
            case EcologyShotKind.Gate:
                Projectile.Center=origin;Projectile.velocity=Vector2.Zero;
                if(Age is 18 or 26 or 34)
                {
                    Vector2 exit=Focus;
                    if(Vector2.DistanceSquared(p.Center,exit)<=650*650&&Collision.CanHitLine(p.Center,1,1,exit,1,1)&&!Collision.SolidCollision(exit-new Vector2(9),18,18))
                    {
                        NPC? target=FindTarget(exit,p,650);Vector2 aim=target==null?d:(target.Center-exit).SafeNormalize(d);
                        Child(EcologyShotKind.MinionBolt,exit,aim*22,.50f);
                    }
                }break;
            case EcologyShotKind.SunArray:
            case EcologyShotKind.Burst:
            case EcologyShotKind.Echo:Projectile.velocity=Vector2.Zero;break;
            case EcologyShotKind.EquipmentProc:
            case EcologyShotKind.MinionBolt:
                Projectile.rotation=Projectile.velocity.ToRotation();break;
        }
        if(Age>=duration)Projectile.Kill();
        if(!Main.dedServ)Lighting.AddLight(Projectile.Center,EcologyCatalog.Colors[Biome].ToVector3()*.25f);
    }
    private void NodeAI(Player p)
    {
        Projectile.velocity=Vector2.Zero;
        switch(Biome)
        {
            case 1:if(Age%54==0)Child(EcologyShotKind.Echo,Projectile.Center,Vector2.Zero,.65f);break;
            case 2:if(Age%65==0)FireAtTarget(p,EcologyShotKind.Needle,.55f,12,480);break;
            case 5:
                if(Age%42==0)FireAtTarget(p,EcologyShotKind.Rivet,.50f,15,560);
                // This node never bends hostile or foreign-mod projectiles.
                if(Projectile.owner==Main.myPlayer)
                    foreach(Projectile q in Main.ActiveProjectiles)
                        if(q.owner==Projectile.owner&&q.ModProjectile is EcologyShot s&&s.Weapon==Weapon&&s.Kind==EcologyShotKind.Rivet&&Vector2.DistanceSquared(q.Center,Projectile.Center)<170*170)
                        {
                            NPC? target=FindTarget(q.Center,p,500);
                            if(target!=null){q.velocity=Vector2.Lerp(q.velocity,(target.Center-q.Center).SafeNormalize(Vector2.UnitX)*15,.045f);if(Age%20==0)q.netUpdate=true;}
                        }
                break;
            case 6:if(Age%52==0)FireAtTarget(p,EcologyShotKind.Needle,.65f,13,520);break;
        }
    }
    private void FireAtTarget(Player p,EcologyShotKind kind,float scale,float speed,float range)
    {
        NPC? target=FindTarget(Projectile.Center,p,range);
        if(target!=null)Child(kind,Projectile.Center,(target.Center-Projectile.Center).SafeNormalize(Vector2.UnitX)*speed,scale);
    }
    public static NPC? FindTarget(Vector2 center,Player p,float range)
    {
        int preferred=p.MinionAttackTargetNPC;
        if(preferred>=0&&preferred<Main.maxNPCs&&ValidTarget(Main.npc[preferred],center,p,range))return Main.npc[preferred];
        NPC? best=null;float limit=range*range;
        foreach(NPC n in Main.ActiveNPCs)
        {
            float dist=Vector2.DistanceSquared(n.Center,center);
            if(dist<limit&&ValidTarget(n,center,p,range)){limit=dist;best=n;}
        }
        return best;
    }
    private static bool ValidTarget(NPC n,Vector2 center,Player p,float range)=>n.CanBeChasedBy()&&Vector2.DistanceSquared(center,n.Center)<range*range&&
        Vector2.DistanceSquared(p.Center,n.Center)<1100*1100&&Collision.CanHitLine(center,1,1,n.Center,1,1);
    private void Child(EcologyShotKind kind,Vector2 at,Vector2 speed,float scale)
        =>EcologyArsenal.Launch(Projectile.GetSource_FromThis(),Projectile.owner,Weapon,kind,at,speed,(int)(Projectile.damage*scale),Projectile.knockBack,secondary:true);
    public override bool ShouldUpdatePosition()=>Kind is not(EcologyShotKind.Swing or EcologyShotKind.Thrust or EcologyShotKind.Vine or EcologyShotKind.Node or EcologyShotKind.RootBud or EcologyShotKind.SunArray or EcologyShotKind.Burst or EcologyShotKind.Echo or EcologyShotKind.Gate)&&!(Kind==EcologyShotKind.OrbitBlade&&Age<=24);
    public override bool? CanDamage()
    {
        if(!initialized)return false;
        if(Kind is EcologyShotKind.Node or EcologyShotKind.RootBud or EcologyShotKind.Gate)return false;
        if(Kind==EcologyShotKind.Bubble)return false;
        if(Kind==EcologyShotKind.Burst)return Age is >=30 and <=36?null:false;
        if(Kind==EcologyShotKind.Echo)return Age is >=10 and <=22?null:false;
        if(Kind==EcologyShotKind.SunArray)return Age is >=24 and <=48?null:false;
        if(Kind==EcologyShotKind.SoundRing&&Age<Projectile.ai[2])return false;
        if(Kind==EcologyShotKind.OrbitBlade&&Age<8)return false;
        return null;
    }
    public override bool? CanCutTiles()=>false;
    public override bool? CanHitNPC(NPC target)
    {
        if(Projectile.owner<0||Projectile.owner>=Main.maxPlayers)return false;
        Player p=Main.player[Projectile.owner];
        if(!Collision.CanHitLine(Projectile.Center,1,1,target.Center,1,1))return false;
        if(Kind is EcologyShotKind.Swing or EcologyShotKind.Thrust or EcologyShotKind.Vine or EcologyShotKind.Gear or EcologyShotKind.SunDisc or EcologyShotKind.OrbitBlade or EcologyShotKind.SunArray)
            return Collision.CanHitLine(p.Center,1,1,target.Center,1,1)?null:false;
        return null;
    }
    public static bool Segment(Rectangle box,Vector2 a,Vector2 b,float width)
    {
        // Native line helper alone misses a segment wholly inside a large target.
        if(box.Contains(a.ToPoint())||box.Contains(b.ToPoint()))return true;
        float point=0;return Collision.CheckAABBvLineCollision(box.TopLeft(),box.Size(),a,b,width,ref point);
    }
    public Vector2 ArrayPoint(int i)=>Projectile.Center+(i*MathHelper.TwoPi/3-MathHelper.PiOver2).ToRotationVector2()*88;
    public override bool? Colliding(Rectangle projHitbox,Rectangle targetHitbox)
    {
        if(Kind is EcologyShotKind.Swing or EcologyShotKind.Thrust or EcologyShotKind.Vine)
            return Segment(targetHitbox,Main.player[Projectile.owner].MountedCenter,Projectile.Center+Projectile.rotation.ToRotationVector2()*24,Kind==EcologyShotKind.Vine?12:24);
        if(Kind is EcologyShotKind.Burst or EcologyShotKind.Echo or EcologyShotKind.SoundRing)
        {
            float radius=Kind==EcologyShotKind.Burst?64:Kind==EcologyShotKind.Echo?68:24+Math.Max(0,Age-Projectile.ai[2])*.8f;
            Vector2 near=Vector2.Clamp(Projectile.Center,targetHitbox.TopLeft(),targetHitbox.BottomRight());
            return Vector2.DistanceSquared(near,Projectile.Center)<=radius*radius;
        }
        if(Kind==EcologyShotKind.SunArray)
        {
            for(int i=0;i<3;i++)
            {
                Vector2 a=ArrayPoint(i),b=ArrayPoint((i+1)%3);
                if(Collision.CanHitLine(Projectile.Center,1,1,a,1,1)&&Collision.CanHitLine(a,1,1,b,1,1)&&Segment(targetHitbox,a,b,12))return true;
            }
            return false;
        }
        if(Kind is EcologyShotKind.Gear or EcologyShotKind.SunDisc or EcologyShotKind.TideBlade or EcologyShotKind.OrbitBlade)
        {Rectangle enlarged=projHitbox;enlarged.Inflate(14,14);return enlarged.Intersects(targetHitbox);}
        return null;
    }
    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        if(Kind==EcologyShotKind.MirrorArrow&&bounces<2)
        {
            bounces++;if(Projectile.velocity.X!=oldVelocity.X)Projectile.velocity.X=-oldVelocity.X;
            if(Projectile.velocity.Y!=oldVelocity.Y)Projectile.velocity.Y=-oldVelocity.Y;
            Projectile.netUpdate=true;return false;
        }
        if(Kind==EcologyShotKind.Seed&&!hitEffect){hitEffect=true;Child(EcologyShotKind.RootBud,Projectile.Center-oldVelocity,Vector2.Zero,.60f);}
        return true;
    }
    public override void OnHitNPC(NPC target,NPC.HitInfo hit,int damageDone)
    {
        if(Kind==EcologyShotKind.HeatBullet&&Projectile.ai[2]==1||Kind==EcologyShotKind.RisingFlame)target.AddBuff(BuffID.OnFire,180);
        if(Kind==EcologyShotKind.Needle&&Biome==2)target.AddBuff(BuffID.Poisoned,120);
        if(hitEffect||Secondary)return;
        hitEffect=true;
        if(Kind==EcologyShotKind.WaxArrow||Kind==EcologyShotKind.Thrust&&Biome==1)
            Child(EcologyShotKind.Burst,target.Center,Vector2.Zero,.65f);
        else if(Kind==EcologyShotKind.Swing&&Biome==2)
            for(int i=-1;i<=1;i+=2)Child(EcologyShotKind.Needle,Projectile.Center,(target.Center-Projectile.Center).SafeNormalize(Vector2.UnitX).RotatedBy(i*.3f)*13,.3f);
        else if(Kind==EcologyShotKind.Seed)
            Child(EcologyShotKind.RootBud,target.Center,Vector2.Zero,.70f);
        else if(Kind==EcologyShotKind.SunSlug)
            for(int i=-1;i<=1;i+=2)Child(EcologyShotKind.MinionBolt,target.Center,new Vector2(0,i*15),.35f);
    }
    public override void SendExtraAI(BinaryWriter w)
    {w.Write(Focus.X);w.Write(Focus.Y);w.Write(ExplicitSecondary);w.Write(Age);w.Write(initialized);w.Write(angle);w.Write(origin.X);w.Write(origin.Y);w.Write(duration);w.Write((byte)bounces);w.Write(hitEffect);}
    public override void ReceiveExtraAI(BinaryReader r)
    {
        Vector2 focus=new(r.ReadSingle(),r.ReadSingle());bool secondary=r.ReadBoolean();int age=r.ReadInt32();bool init=r.ReadBoolean();float a=r.ReadSingle();
        Vector2 at=new(r.ReadSingle(),r.ReadSingle());int life=r.ReadInt32();int bounce=r.ReadByte();bool effect=r.ReadBoolean();
        if(!float.IsFinite(focus.X)||!float.IsFinite(focus.Y)||!float.IsFinite(at.X)||!float.IsFinite(at.Y)||!float.IsFinite(a)||age<0||age>600||life<0||life>600||bounce>2){Projectile.Kill();return;}
        Focus=focus;ExplicitSecondary=secondary;Age=age;
        // Apply mode defaults before restoring the synchronized clock.
        if(init&&!initialized)Initialize();
        angle=a;origin=at;duration=life;initialized=init;bounces=bounce;hitEffect=effect;
    }
    public override bool PreDraw(ref Color lightColor)
    {
        if(Projectile.owner<0||Projectile.owner>=Main.maxPlayers)return false;
        Color c=EcologyCatalog.Colors[Biome];Vector2 at=Projectile.Center-Main.screenPosition;
        var batch=Main.spriteBatch;float fade=Math.Clamp(Projectile.timeLeft/8f,0,1);
        if(Kind==EcologyShotKind.SunArray)
        {
            for(int i=0;i<3;i++)
            {
                Vector2 a=ArrayPoint(i),b=ArrayPoint((i+1)%3);
                if(Collision.CanHitLine(Projectile.Center,1,1,a,1,1)&&Collision.CanHitLine(a,1,1,b,1,1))
                    CombatDrawing.Line(batch,a-Main.screenPosition,b-Main.screenPosition,c*(Age<24?.35f:.9f)*fade,Age<24?2:7);
                CombatDrawing.Circle(batch,a-Main.screenPosition,8,c*fade,2);
            }
        }
        else if(Kind==EcologyShotKind.Gate)
        {
            CombatDrawing.Circle(batch,at,20,c*fade,3);CombatDrawing.Circle(batch,Focus-Main.screenPosition,26,c*fade,3);
            CombatDrawing.Line(batch,at,Focus-Main.screenPosition,c*.2f*fade,2);
        }
        else if(Kind is EcologyShotKind.Burst or EcologyShotKind.Echo or EcologyShotKind.SoundRing)
        {
            float radius=Kind==EcologyShotKind.Burst?64:Kind==EcologyShotKind.Echo?68:24+Math.Max(0,Age-Projectile.ai[2])*.8f;
            CombatDrawing.Circle(batch,at,radius,c*(CanDamage()==false?.3f:.9f)*fade,CanDamage()==false?2:5);
        }
        else
        {
            string art=Kind is EcologyShotKind.Node or EcologyShotKind.RootBud?EcologyCatalog.Root+"Core"+Biome:
                Kind==EcologyShotKind.EquipmentProc?EcologyCatalog.Root+"Expert"+Biome:EcologyCatalog.Root+"Weapon"+Weapon;
            Texture2D tex=ModContent.Request<Texture2D>(art).Value;
            bool held=Kind is EcologyShotKind.Swing or EcologyShotKind.Thrust or EcologyShotKind.Vine;
            float scale=held?1:Kind is EcologyShotKind.Gear or EcologyShotKind.SunDisc or EcologyShotKind.OrbitBlade or EcologyShotKind.TideBlade?.8f:Kind is EcologyShotKind.Node or EcologyShotKind.RootBud?.55f:.32f;
            if(held)CombatDrawing.Line(batch,Main.player[Projectile.owner].MountedCenter-Main.screenPosition,at,c*.65f,Kind==EcologyShotKind.Vine?6:3);
            batch.Draw(tex,at,null,Color.White*fade,Projectile.rotation+(held?MathHelper.PiOver4:0),tex.Size()/2,scale,SpriteEffects.None,0);
            if(Kind is EcologyShotKind.Node or EcologyShotKind.Bubble or EcologyShotKind.RootBud)CombatDrawing.Circle(batch,at,Kind==EcologyShotKind.Bubble?20:28,c*.7f*fade,2);
        }
        return false;
    }
}
public sealed class EcologySummonShot:EcologyShot
{
    public override void SetStaticDefaults()=>ProjectileID.Sets.MinionShot[Type]=true;
    public override void SetDefaults(){base.SetDefaults();Projectile.DamageType=DamageClass.Summon;}
}

