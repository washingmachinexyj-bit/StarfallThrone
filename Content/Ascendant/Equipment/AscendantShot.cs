#nullable enable
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using global::StarfallThrone.Content.Systems;
using global::StarfallThrone.Content.Voyage.Weapons;

namespace StarfallThrone.Content.Ascendant.Equipment;

public enum AscendantShotKind { Swing, Branch, Arrow, Seed, Flower, FlowerRing, LawWave, Bullet, LawBolt, Explosion, CutMark, CrossEcho, Star, StarLink, MinionBolt, DewSkill }

public class AscendantShot : ModProjectile, IVoyageWeaponProjectile
{
    public int Weapon => Math.Clamp((int)Projectile.ai[0],0,11);
    public int WeaponIndex => -1;
    public AscendantShotKind Kind => (AscendantShotKind)(int)Projectile.ai[1];
    public bool Secondary => Weapon%4==3 || Kind is AscendantShotKind.Branch or AscendantShotKind.Seed or AscendantShotKind.LawWave or AscendantShotKind.Explosion or AscendantShotKind.CutMark or AscendantShotKind.CrossEcho or AscendantShotKind.MinionBolt or AscendantShotKind.DewSkill;
    public Vector2 Focus;
    public int Age;
    public bool Triggered => fireAt >= 0;
    private int fireAt = -1, duration;
    private float angle;
    private bool initialized, awarded;
    public override string Texture => AscendantArsenal.Art(0);
    public override void SetDefaults()
    {
        Projectile.width=Projectile.height=18;Projectile.friendly=true;Projectile.ignoreWater=true;
        Projectile.penetrate=1;Projectile.timeLeft=120;Projectile.tileCollide=true;
        Projectile.usesLocalNPCImmunity=true;Projectile.localNPCHitCooldown=-1;
    }
    private void Initialize()
    {
        if(initialized)return;initialized=true;angle=Projectile.velocity.ToRotation();
        Projectile.DamageType=Kind==AscendantShotKind.DewSkill?DamageClass.Generic:AscendantEquipmentData.Class(Weapon%4);
        duration=Kind switch{
            AscendantShotKind.Swing=>Weapon==0?21:Weapon==4?34:28,
            AscendantShotKind.Flower or AscendantShotKind.Star=>600,AscendantShotKind.CutMark=>300,
            AscendantShotKind.FlowerRing=>30,AscendantShotKind.StarLink=>36,AscendantShotKind.Explosion=>12,
            AscendantShotKind.CrossEcho=>36,AscendantShotKind.LawWave=>90,
            AscendantShotKind.Branch=>26,AscendantShotKind.DewSkill=>90,_=>120};
        Projectile.timeLeft=duration;
        if(Kind is AscendantShotKind.Swing or AscendantShotKind.Flower or AscendantShotKind.FlowerRing or AscendantShotKind.CutMark or AscendantShotKind.CrossEcho or AscendantShotKind.Star or AscendantShotKind.StarLink or AscendantShotKind.Explosion)
        {Projectile.penetrate=-1;Projectile.tileCollide=false;}
        if(Kind==AscendantShotKind.StarLink){Projectile.usesLocalNPCImmunity=false;Projectile.usesIDStaticNPCImmunity=true;Projectile.idStaticNPCHitCooldown=12;}
        if(Kind==AscendantShotKind.LawWave){Projectile.penetrate=-1;Projectile.tileCollide=false;}
    }
    public void Trigger(int delay)
    {
        if(Triggered||Kind is not (AscendantShotKind.Flower or AscendantShotKind.CutMark))return;
        Initialize();fireAt=Age+Math.Clamp(delay,0,40)+1;Projectile.netUpdate=true;
    }
    public override void AI()
    {
        Initialize();
        if(!Enum.IsDefined(Kind)||!AscendantArsenal.Finite(Projectile.position)||!AscendantArsenal.Finite(Projectile.velocity)||
            Projectile.owner<0||Projectile.owner>=Main.maxPlayers||!Main.player[Projectile.owner].active||Main.player[Projectile.owner].dead)
        {Projectile.Kill();return;}
        Player p=Main.player[Projectile.owner];Age++;Projectile.rotation=Projectile.velocity.ToRotation();
        switch(Kind)
        {
            case AscendantShotKind.Swing:
                if(p.HeldItem.type!=AscendantCatalog.Weapon(Weapon)){Projectile.Kill();return;}
                float arc=MathHelper.Lerp(-1.15f,1.15f,Age/(float)duration)*(Weapon==0&&(int)Projectile.ai[2]==1?-1:1);
                Projectile.rotation=angle+arc;Projectile.Center=p.MountedCenter+Projectile.rotation.ToRotationVector2()*(Weapon==0?44:68);p.heldProj=Projectile.whoAmI;
                break;
            case AscendantShotKind.Seed:
                if(Age>10&&Age<65)
                {
                    NPC? target=Nearest(Projectile.Center,260);
                    if(target!=null)Projectile.velocity=(Projectile.velocity*9+(target.Center-Projectile.Center).SafeNormalize(Vector2.UnitX)*12)/10;
                }
                break;
            case AscendantShotKind.Flower:
                if(Triggered&&Age>=fireAt)
                {
                    AscendantArsenal.Launch(Projectile.GetSource_FromThis(),Projectile.owner,Weapon,AscendantShotKind.FlowerRing,Projectile.Center,Vector2.Zero,Projectile.damage,Projectile.knockBack);
                    Projectile.Kill();return;
                }
                break;
            case AscendantShotKind.LawWave:
                int law=(int)Projectile.ai[2]%4,delay=law*10;
                if(Age<=delay)Projectile.velocity=Vector2.Zero;
                else if(Age==delay+1)Projectile.velocity=angle.ToRotationVector2()*(law==3?9:13);
                if(law==2&&Age>delay)Projectile.velocity=Projectile.velocity.RotatedBy(MathF.Sin((Age-delay)*.3f)*.04f);
                if(Age>delay+28){Projectile.Kill();return;}
                break;
            case AscendantShotKind.LawBolt:
                if((int)Projectile.ai[2]==1)Projectile.velocity.Y+=.045f;
                if((int)Projectile.ai[2]==2)Projectile.velocity=Projectile.velocity.RotatedBy(MathF.Sin(Age*.3f)*.025f);
                break;
            case AscendantShotKind.CutMark:
                if(Triggered&&Age>fireAt+10){Projectile.Kill();return;}
                Projectile.rotation=angle;
                break;
            case AscendantShotKind.DewSkill:
                NPC? enemy=Nearest(Projectile.Center,600);
                if(enemy!=null)Projectile.velocity=(Projectile.velocity*14+(enemy.Center-Projectile.Center).SafeNormalize(Vector2.UnitX)*13)/15;
                break;
            case AscendantShotKind.MinionBolt:
                if((int)Projectile.ai[2]==2)Projectile.velocity=Projectile.velocity.RotatedBy(MathF.Sin(Age*.3f)*.025f);
                if((int)Projectile.ai[2]==4)Projectile.velocity=Projectile.velocity.RotatedBy(MathF.Sin(Age*.2f)*.035f);
                break;
        }
        if(Age>=duration)Projectile.Kill();
        if(!Main.dedServ)Lighting.AddLight(Projectile.Center,AscendantEquipmentData.Color(Weapon/4).ToVector3()*.28f);
    }
    private static NPC? Nearest(Vector2 at,float range)
    {
        NPC? best=null;float limit=range*range;
        foreach(NPC n in Main.ActiveNPCs)
        {
            float distance=Vector2.DistanceSquared(at,n.Center);
            if(n.CanBeChasedBy()&&distance<limit&&Collision.CanHitLine(at,1,1,n.Center,1,1)){best=n;limit=distance;}
        }
        return best;
    }
    public override bool ShouldUpdatePosition()=>Kind is not (AscendantShotKind.Swing or AscendantShotKind.Flower or AscendantShotKind.FlowerRing or AscendantShotKind.CutMark or AscendantShotKind.CrossEcho or AscendantShotKind.Star or AscendantShotKind.StarLink or AscendantShotKind.Explosion);
    public override bool? CanCutTiles()=>false;
    public override bool? CanDamage()
    {
        if(Kind is AscendantShotKind.Flower or AscendantShotKind.Star)return false;
        if(Kind==AscendantShotKind.Swing&&Age<3)return false;
        if(Kind==AscendantShotKind.CutMark&&(!Triggered||Age<fireAt))return false;
        if(Kind==AscendantShotKind.CrossEcho&&Age<20)return false;
        if(Kind==AscendantShotKind.LawWave&&Age<=(int)Projectile.ai[2]%4*10)return false;
        if(Kind==AscendantShotKind.StarLink&&Age<6)return false;
        return null;
    }
    public override bool? CanHitNPC(NPC target)
    {
        if(!Collision.CanHitLine(Projectile.Center,1,1,target.position,target.width,target.height))return false;
        if(Kind==AscendantShotKind.Swing&&!Collision.CanHitLine(Main.player[Projectile.owner].MountedCenter,1,1,target.Center,1,1))return false;
        if(Kind==AscendantShotKind.StarLink&&!Collision.CanHitLine(Projectile.Center,1,1,Focus,1,1))return false;
        return null;
    }
    public static bool SegmentHits(Rectangle box,Vector2 start,Vector2 end,float width)
    {
        if(box.Contains(start.ToPoint())||box.Contains(end.ToPoint()))return true;
        float distance=0;return Collision.CheckAABBvLineCollision(box.TopLeft(),box.Size(),start,end,width,ref distance);
    }
    public override bool? Colliding(Rectangle projHitbox,Rectangle targetHitbox)
    {
        if(CanDamage()==false)return false;
        if(Kind==AscendantShotKind.Swing)
        {Vector2 start=Main.player[Projectile.owner].MountedCenter;return SegmentHits(targetHitbox,start,start+Projectile.rotation.ToRotationVector2()*(Weapon==0?90:135),Weapon==0?20:28);}
        if(Kind is AscendantShotKind.CutMark or AscendantShotKind.CrossEcho)
        {Vector2 extent=angle.ToRotationVector2()*90;return SegmentHits(targetHitbox,Projectile.Center-extent,Projectile.Center+extent,18);}
        if(Kind==AscendantShotKind.StarLink)return SegmentHits(targetHitbox,Projectile.Center,Focus,14);
        if(Kind is AscendantShotKind.FlowerRing or AscendantShotKind.Explosion)
        {
            float radius=Kind==AscendantShotKind.FlowerRing?12+Age*4:65;
            Vector2 nearest=Vector2.Clamp(Projectile.Center,targetHitbox.TopLeft(),targetHitbox.BottomRight());
            if(Vector2.DistanceSquared(Projectile.Center,nearest)>(radius+10)*(radius+10))return false;
            if(Kind==AscendantShotKind.Explosion)return true;
            Vector2 farthest=new(Math.Max(Math.Abs(targetHitbox.Left-Projectile.Center.X),Math.Abs(targetHitbox.Right-Projectile.Center.X)),Math.Max(Math.Abs(targetHitbox.Top-Projectile.Center.Y),Math.Abs(targetHitbox.Bottom-Projectile.Center.Y)));
            return farthest.LengthSquared()>=Math.Max(0,radius-10)*Math.Max(0,radius-10);
        }
        if(Kind==AscendantShotKind.LawWave)
        {Vector2 side=angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2)*((int)Projectile.ai[2]==3?60:30);return SegmentHits(targetHitbox,Projectile.Center-side,Projectile.Center+side,18);}
        return null;
    }
    public override void OnHitNPC(NPC target,NPC.HitInfo hit,int damageDone)
    {
        if(Projectile.owner!=Main.myPlayer||Secondary||damageDone<=0||target.friendly||target.type==NPCID.TargetDummy)return;
        Player p=Main.player[Projectile.owner];var state=p.GetModPlayer<AscendantEquipmentPlayer>();
        void Proc(AscendantShotKind kind,float scale,Vector2 at,Vector2 velocity,int variant=0)
            =>AscendantArsenal.Launch(Projectile.GetSource_FromThis(),Projectile.owner,Weapon,kind,at,velocity,(int)(Projectile.damage*scale),Projectile.knockBack,variant);
        if(Kind==AscendantShotKind.Swing&&!awarded)
        {
            awarded=true;
            if(Weapon==0)
            {
                state.BranchCharges=Math.Min(3,state.BranchCharges+1);
                state.BranchHitCombo=(state.BranchHitCombo+1)%3;
                if(state.BranchHitCombo==0)
                    for(int i=-1;i<=1;i+=2)Proc(AscendantShotKind.Branch,.40f,p.Center,angle.ToRotationVector2().RotatedBy(i*.20f)*12);
            }
            if(Weapon==4)state.FurnacePressure=Math.Min(100,state.FurnacePressure+20);
            if(Weapon==8)
            {
                var marks=AscendantArsenal.Owned(p,8,AscendantShotKind.CutMark);
                if(marks.Count>=5)marks[0].Projectile.Kill();
                Proc(AscendantShotKind.CutMark,.65f,target.Center,angle.ToRotationVector2());
            }
        }
        if(Weapon==1&&Kind==AscendantShotKind.Arrow&&target.GetGlobalNPC<AscendantTargetMarks>().SeedHit(Projectile.owner))
            for(int i=-1;i<=1;i+=2)Proc(AscendantShotKind.Seed,.65f,target.Center,new Vector2(i*8,-5));
        if(Weapon==5&&Kind==AscendantShotKind.Bullet)
        {
            int stacks=target.GetGlobalNPC<AscendantTargetMarks>().GunHit(Projectile.owner,(int)Projectile.ai[2]);
            if(stacks>0)Proc(AscendantShotKind.Explosion,.25f*stacks,target.Center,Vector2.Zero);
        }
        if(Weapon==6&&Kind==AscendantShotKind.LawBolt)target.GetGlobalNPC<AscendantTargetMarks>().AddLaw(Projectile.owner,(int)Projectile.ai[2]);
        if(Weapon==9&&Kind==AscendantShotKind.Arrow)
        {
            if(Projectile.ai[2]==1)
                for(int i=-1;i<=1;i+=2)Proc(AscendantShotKind.CrossEcho,.60f,target.Center,new Vector2(1,i*.7f).SafeNormalize(Vector2.UnitX));
            if(state.BowTarget!=target.whoAmI||state.BowTargetType!=target.type){state.BowHits=0;state.BowTarget=target.whoAmI;state.BowTargetType=target.type;}
            if(++state.BowHits>=3){state.BowHits=0;state.BowReady=true;}
        }
    }
    public override void SendExtraAI(BinaryWriter w)
    {w.Write(Focus.X);w.Write(Focus.Y);w.Write((ushort)Math.Clamp(Age,0,600));w.Write(angle);w.Write(awarded);w.Write((short)fireAt);w.Write((ushort)Math.Clamp(Projectile.timeLeft,0,600));}
    public override void ReceiveExtraAI(BinaryReader r)
    {
        Vector2 focus=new(r.ReadSingle(),r.ReadSingle());int age=r.ReadUInt16();float a=r.ReadSingle();bool award=r.ReadBoolean();int fire=r.ReadInt16(),life=r.ReadUInt16();
        if(!AscendantArsenal.Finite(focus)||!float.IsFinite(a)||age>600||fire is < -1 or > 641||life>600){Projectile.Kill();return;}
        Initialize();Focus=focus;Age=age;angle=a;awarded=award;fireAt=fire;Projectile.timeLeft=life;
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Color c=AscendantEquipmentData.Color(Weapon/4);Vector2 at=Projectile.Center-Main.screenPosition;var batch=Main.spriteBatch;
        float fade=Math.Clamp(Projectile.timeLeft/10f,.1f,1),live=CanDamage()==false?.38f:1;
        if(Kind is AscendantShotKind.Flower or AscendantShotKind.FlowerRing or AscendantShotKind.Explosion or AscendantShotKind.Star)
        {
            float radius=Kind==AscendantShotKind.FlowerRing?12+Age*4:Kind==AscendantShotKind.Explosion?65:Kind==AscendantShotKind.Star?16:24;
            CombatDrawing.Circle(batch,at,radius,c*fade*live,3);
            int petals=Kind==AscendantShotKind.Star?5:8;
            for(int i=0;i<petals;i++)
            {
                Vector2 d=(i*MathHelper.TwoPi/petals+Age*.013f).ToRotationVector2();
                CombatDrawing.Line(batch,at+d*(radius*.5f),at+d*(radius+7),Color.White*fade*live,3);
            }
            if(Kind==AscendantShotKind.Flower)CombatDrawing.Circle(batch,at+new Vector2(0,9),6,c,3);
        }
        else if(Kind is AscendantShotKind.CutMark or AscendantShotKind.CrossEcho or AscendantShotKind.StarLink)
        {
            Vector2 d=angle.ToRotationVector2()*90;
            Vector2 start=Kind==AscendantShotKind.StarLink?at:at-d,end=Kind==AscendantShotKind.StarLink?Focus-Main.screenPosition:at+d;
            CombatDrawing.Line(batch,start,end,c*fade*live,live<1?2:10);
            if(live==1)CombatDrawing.Line(batch,start,end,Color.White*fade,2);
        }
        else if(Kind is AscendantShotKind.Arrow or AscendantShotKind.Bullet or AscendantShotKind.MinionBolt)
        {
            Vector2 d=Projectile.velocity.SafeNormalize(Vector2.UnitX);
            CombatDrawing.Line(batch,at-d*(Weapon>=8?30:18),at+d*10,c*fade,4);
            CombatDrawing.Line(batch,at+d*10,at+d.RotatedBy(2.5f)*9,Color.White*fade,2);
            CombatDrawing.Line(batch,at+d*10,at+d.RotatedBy(-2.5f)*9,Color.White*fade,2);
            if(Kind==AscendantShotKind.Bullet)CombatDrawing.Circle(batch,at,7+Projectile.ai[2]%4,c*.55f,2);
        }
        else
        {
            Texture2D texture=ModContent.Request<Texture2D>(Kind==AscendantShotKind.DewSkill?AscendantCatalog.Root+"Expert0":AscendantArsenal.Art(Weapon)).Value;
            batch.Draw(texture,at,null,Color.White*fade*live,Projectile.rotation+(Kind==AscendantShotKind.Swing?MathHelper.PiOver4:0),texture.Size()/2,Kind==AscendantShotKind.Swing?Weapon==0?1:1.4f:.33f,SpriteEffects.None,0);
            if(Kind==AscendantShotKind.LawWave)
            {
                int law=(int)Projectile.ai[2]%4;
                CombatDrawing.Circle(batch,at,22+law*8,c*fade*live,4,angle+MathHelper.Pi,.9f);
                if(law==3)CombatDrawing.Circle(batch,at,48,c*.4f*live,2);
            }
            if(Kind==AscendantShotKind.LawBolt)
                for(int i=0;i<=Projectile.ai[2];i++)CombatDrawing.Circle(batch,at+(i*MathHelper.PiOver2+Age*.08f).ToRotationVector2()*12,3,c,2);
        }
        return false;
    }
}
public sealed class AscendantSummonShot:AscendantShot
{
    public override void SetStaticDefaults()=>ProjectileID.Sets.MinionShot[Type]=true;
    public override void SetDefaults(){base.SetDefaults();Projectile.DamageType=DamageClass.Summon;}
}

public sealed class AscendantTargetMarks:GlobalNPC
{
    public override bool InstancePerEntity=>true;
    private readonly byte[] seeds=new byte[Main.maxPlayers],gun=new byte[Main.maxPlayers],laws=new byte[Main.maxPlayers];
    private readonly int[] round=new int[Main.maxPlayers];
    private readonly ulong[] seedUntil=new ulong[Main.maxPlayers],gunUntil=new ulong[Main.maxPlayers],lawUntil=new ulong[Main.maxPlayers];
    public override void OnSpawn(NPC npc,IEntitySource source)
    {Array.Clear(seeds);Array.Clear(gun);Array.Clear(laws);Array.Clear(round);Array.Clear(seedUntil);Array.Clear(gunUntil);Array.Clear(lawUntil);}
    public bool SeedHit(int owner)
    {
        if(owner<0||owner>=Main.maxPlayers)return false;ulong now=Main.GameUpdateCount+1;
        if(seedUntil[owner]<now)seeds[owner]=0;seedUntil[owner]=now+180;
        if(++seeds[owner]<3)return false;seeds[owner]=0;return true;
    }
    public int GunHit(int owner,int cadence)
    {
        if(owner<0||owner>=Main.maxPlayers)return 0;ulong now=Main.GameUpdateCount+1;int r=cadence/4;
        if(round[owner]!=r||gunUntil[owner]<now){gun[owner]=0;round[owner]=r;}
        gunUntil[owner]=now+90;gun[owner]=(byte)Math.Min(4,gun[owner]+1);
        if(cadence%4!=3)return 0;int result=gun[owner];gun[owner]=0;return result;
    }
    public void AddLaw(int owner,int law)
    {
        if(owner<0||owner>=Main.maxPlayers||law<0||law>3)return;ulong now=Main.GameUpdateCount+1;
        if(lawUntil[owner]<now)laws[owner]=0;lawUntil[owner]=now+240;laws[owner]|=(byte)(1<<law);
    }
    public int TakeLaws(int owner)
    {if(owner<0||owner>=Main.maxPlayers)return 0;int result=lawUntil[owner]>=Main.GameUpdateCount+1?laws[owner]:0;laws[owner]=0;return result;}
}
