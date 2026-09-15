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

namespace StarfallThrone.Content.Pantheon.Equipment;

// kind: held blade, projectile, delayed projectile, finite line, field, burst, secondary bolt, accessory bolt.
public sealed class PantheonShot:ModProjectile,IVoyageWeaponProjectile
{
    public int Weapon=>(int)Projectile.ai[0];public int Kind=>(int)Projectile.ai[1];public int Variant=>(int)Projectile.ai[2];
    public int WeaponIndex=>-1;public bool Secondary=>Kind>=5;
    public Vector2 End,Origin,Initial;public int Age,SwingDuration;private bool configured,hitProc;private int bounces;
    public override string Texture=>PantheonCatalog.Root+"Weapon0";
    public override void SetDefaults(){Projectile.width=Projectile.height=20;Projectile.friendly=true;Projectile.penetrate=1;Projectile.timeLeft=240;Projectile.tileCollide=true;Projectile.ignoreWater=true;Projectile.usesLocalNPCImmunity=true;Projectile.localNPCHitCooldown=25;}
    public void Configure()
    {
        if(Weapon<0||Weapon>=36||Kind<0||Kind>7)return;
        Projectile.DamageType=Kind==7?DamageClass.Generic:PantheonArsenal.Class(PantheonArsenal.Classes[Weapon]);
        if(configured)return;configured=true;Origin=Projectile.Center;Initial=Projectile.velocity;
        switch(Kind)
        {
            case 0:SwingDuration=SwingDuration>0?Math.Clamp(SwingDuration,2,240):PantheonArsenal.UseTimes[Weapon];Projectile.tileCollide=false;Projectile.penetrate=-1;Projectile.timeLeft=SwingDuration;Projectile.localNPCHitCooldown=240;break;
            case 1:if(Weapon is 4 or 14 or 25){Projectile.penetrate=4;}break;
            case 2:Projectile.timeLeft=180+Variant;break;
            case 3:Projectile.tileCollide=false;Projectile.penetrate=-1;Projectile.timeLeft=Variant+14;Projectile.localNPCHitCooldown=60;break;
            case 4:Projectile.tileCollide=false;Projectile.penetrate=-1;Projectile.timeLeft=Weapon==26?150:180;Projectile.localNPCHitCooldown=30;break;
            case 5:Projectile.tileCollide=false;Projectile.penetrate=-1;Projectile.timeLeft=20;Projectile.localNPCHitCooldown=60;break;
            case 6:case 7:Projectile.timeLeft=90;break;
        }
    }
    public override void SendExtraAI(BinaryWriter w){w.Write(configured);w.Write(Age);w.Write((ushort)SwingDuration);w.Write(End.X);w.Write(End.Y);w.Write(Origin.X);w.Write(Origin.Y);w.Write(Initial.X);w.Write(Initial.Y);w.Write(hitProc);w.Write((byte)bounces);}
    public override void ReceiveExtraAI(BinaryReader r)
    {
        if(r.BaseStream.CanSeek&&r.BaseStream.Length-r.BaseStream.Position<33)return;
        bool c=r.ReadBoolean();int age=r.ReadInt32(),duration=r.ReadUInt16();Vector2 end=new(r.ReadSingle(),r.ReadSingle()),origin=new(r.ReadSingle(),r.ReadSingle()),initial=new(r.ReadSingle(),r.ReadSingle());bool hit=r.ReadBoolean();int bounce=r.ReadByte();
        if(Weapon<0||Weapon>=36||Kind<0||Kind>7||age<0||age>2000||bounce>3||Kind==0&&(duration<2||duration>240||age>duration)||Kind!=0&&duration!=0||!PantheonArsenal.Finite(end)||!PantheonArsenal.Finite(origin)||!PantheonArsenal.Finite(initial)||initial.LengthSquared()>1000000)return;
        SwingDuration=duration;Configure();configured=c;Age=age;End=end;Origin=origin;Initial=initial;hitProc=hit;bounces=bounce;
        if(Kind==0)Projectile.timeLeft=Math.Max(1,SwingDuration-Age);
    }
    public override bool? CanCutTiles()=>false;
    public override bool ShouldUpdatePosition()=>Kind is 1 or 6 or 7||Kind==2&&Age>Variant||Kind==4&&Weapon==26;
    public override bool? CanDamage()=>Kind==2&&Age<=Variant||Kind==3&&Age<=Variant||Kind==0&&Age<Math.Max(1,Math.Min(3,SwingDuration/4))?false:null;
    public override bool? CanHitNPC(NPC target)
    {
        Vector2 from=Kind==0&&Projectile.owner>=0&&Projectile.owner<Main.maxPlayers?Main.player[Projectile.owner].Center:Projectile.Center;
        return Collision.CanHitLine(from,1,1,target.position,target.width,target.height)?null:false;
    }
    public override void AI()
    {
        if(Weapon<0||Weapon>=36||Kind<0||Kind>7||Projectile.owner<0||Projectile.owner>=Main.maxPlayers){Projectile.Kill();return;}
        Player p=Main.player[Projectile.owner];if(!p.active||p.dead||!PantheonArsenal.Finite(Projectile.position)||!PantheonArsenal.Finite(Projectile.velocity)){Projectile.Kill();return;}
        Configure();Age++;bool owner=Projectile.owner==Main.myPlayer;
        switch(Kind)
        {
            case 0:
                if(Age>SwingDuration){Projectile.Kill();return;}
                float t=Math.Clamp(Age/(float)SwingDuration,0,1),angle=Initial.ToRotation()+MathHelper.Lerp(-1.25f,1.25f,t)*(Variant%2==0?1:-1);
                float reach=Weapon==4?70+MathF.Sin(t*MathHelper.Pi)*210:Weapon is 20 or 24?100:110;
                Projectile.Center=p.MountedCenter+angle.ToRotationVector2()*reach;Projectile.rotation=angle+MathHelper.PiOver4;
                p.heldProj=Projectile.whoAmI;
                if(owner&&Age==Math.Max(1,SwingDuration/2))BladeFollowup(p);
                break;
            case 1:
                if(Weapon==25)Projectile.velocity*=Projectile.velocity.Length()<38?1.08f:1;
                if(Weapon==29)Projectile.velocity=Initial.RotatedBy(MathF.Sin(Age*.18f+Variant)*.22f);
                if((Weapon is 9 or 17)&&(Weapon!=17||Variant==1)&&Age>10)Home(.08f,Weapon==17?16:20);
                if(Weapon==14&&Age%14==0&&owner&&Age<=42)Child(6,.18f,Projectile.Center,-Projectile.velocity.RotatedBy(.3f));
                Projectile.rotation=Projectile.velocity.ToRotation()+MathHelper.PiOver4;break;
            case 2:Projectile.rotation=Initial.ToRotation()+MathHelper.PiOver4;break;
            case 3:Projectile.velocity=Vector2.Zero;break;
            case 4:
                if(Weapon==5&&Age%30==0&&owner)
                {NPC? n=PantheonArsenal.Target(Projectile.Center,Projectile.owner,300);if(n!=null)Child(6,.7f,Projectile.Center,(n.Center-Projectile.Center).SafeNormalize(Vector2.UnitX)*15);}
                if(Weapon==10&&Age%45==0&&owner)
                {for(int i=0;i<3;i++)Child(6,.6f,Projectile.Center,(i*MathHelper.TwoPi/3+Age*.02f).ToRotationVector2()*12);}
                if(Weapon==21&&Age%40==0&&owner)
                {NPC? n=PantheonArsenal.Target(Projectile.Center,Projectile.owner,650);Vector2 d=(n?.Center??Projectile.Center+Vector2.UnitX)-Projectile.Center;for(int i=-1;i<=1;i++)Child(6,.65f,Projectile.Center,d.SafeNormalize(Vector2.UnitX).RotatedBy(i*.16f)*17);}
                Projectile.rotation+=.04f;break;
            case 5:Projectile.velocity=Vector2.Zero;break;
            case 6:if(Weapon is 1 or 9 or 10 or 22 or 23 or 33 or 35)Home(.12f,20);Projectile.rotation=Projectile.velocity.ToRotation()+MathHelper.PiOver4;break;
            case 7:Home(.12f,24);Projectile.rotation+=.15f;break;
        }
        if(!Main.dedServ)Lighting.AddLight(Projectile.Center,PantheonCatalog.Colors[PantheonArsenal.Boss(Weapon)].ToVector3()*.3f);
    }
    private void Home(float strength,float speed){NPC? n=PantheonArsenal.Target(Projectile.Center,Projectile.owner,600);if(n!=null)Projectile.velocity=Vector2.Lerp(Projectile.velocity,(n.Center-Projectile.Center).SafeNormalize(Vector2.UnitX)*speed,strength);}
    private void Child(int kind,float power,Vector2 at,Vector2 velocity,int variant=0,Vector2? end=null)=>PantheonArsenal.Launch(Projectile.GetSource_FromThis(),Projectile.owner,Weapon,kind,at,velocity,(int)(Projectile.damage*power),variant,end);
    private void BladeFollowup(Player p)
    {
        Vector2 d=Initial.SafeNormalize(Vector2.UnitX);
        switch(Weapon)
        {
            case 0:Child(6,.55f,Projectile.Center,-d*15);break;
            case 4:Child(3,.65f,p.Center,Vector2.Zero,0,Projectile.Center);break;
            case 8:for(int i=-1;i<=1;i++)Child(6,.28f,Projectile.Center,d.RotatedBy(i*.55f)*16);break;
            case 16:Child(6,.6f,Projectile.Center,d.RotatedBy(Variant%2==0?.4f:-.4f)*20);break;
            case 20:Child(5,.8f,Projectile.Center,Vector2.Zero,100);break;
            case 24:for(int i=-1;i<=1;i++)Child(3,.6f,PantheonArsenal.Aim(p,Projectile.Center+new Vector2(i*80,100)),Vector2.Zero,8+8*(i+1),PantheonArsenal.Aim(p,Projectile.Center+new Vector2(i*80,-100)));break;
            case 28:for(int i=-3;i<=3;i++)Child(6,.2f,Projectile.Center,d.RotatedBy(i*.16f)*23,i+3);break;
            case 32:if(Variant==2){Child(3,1.4f,p.Center,Vector2.Zero,8,PantheonArsenal.Aim(p,p.Center+d*600));}else Child(6,.6f,Projectile.Center,d*24);break;
        }
    }
    public override bool? Colliding(Rectangle box,Rectangle target)
    {
        float point=0;
        if(Kind==0){Player p=Main.player[Projectile.owner];return Collision.CheckAABBvLineCollision(target.TopLeft(),target.Size(),p.MountedCenter,Projectile.Center,Weapon is 20 or 24?42:24,ref point);}
        if(Kind==3)return Collision.CheckAABBvLineCollision(target.TopLeft(),target.Size(),Projectile.Center,End,18,ref point);
        if(Kind is 4 or 5)
        {float radius=Kind==5?30+Age*4:Weapon==26?55:75;return Vector2.DistanceSquared(Projectile.Center,Vector2.Clamp(Projectile.Center,target.TopLeft(),target.BottomRight()))<=radius*radius;}
        return null;
    }
    public override void OnHitNPC(NPC target,NPC.HitInfo hit,int damageDone)
    {
        if(Secondary||hitProc||Projectile.owner!=Main.myPlayer)return;hitProc=true;Projectile.netUpdate=true;
        switch(Weapon)
        {
            case 1:if(Variant==2)for(int i=-1;i<=1;i+=2)Child(6,.55f,target.Center,new Vector2(i*10,-9));break;
            case 6:Child(5,.35f,target.Center,Vector2.Zero);break;
            case 14:Child(6,.45f,target.Center,-Projectile.velocity.RotatedBy(.45));break;
            case 22:for(int i=0;i<4;i++)Child(6,.35f,target.Center,(i*MathHelper.PiOver2).ToRotationVector2()*12);break;
            case 25:Child(5,.65f,target.Center,Vector2.Zero);break;
            case 33:if(Variant==1)for(int i=-1;i<=1;i+=2)Child(6,.45f,target.Center,new Vector2(i*12,-10));break;
        }
    }
    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        if(Weapon!=14||bounces>=3)return true;bounces++;
        if(Projectile.velocity.X!=oldVelocity.X)Projectile.velocity.X=-oldVelocity.X;if(Projectile.velocity.Y!=oldVelocity.Y)Projectile.velocity.Y=-oldVelocity.Y;Projectile.netUpdate=true;return false;
    }
    public override bool PreDraw(ref Color lightColor)
    {
        if(Weapon<0||Weapon>=36)return false;Texture2D tex=ModContent.Request<Texture2D>(PantheonCatalog.Root+"Weapon"+Weapon).Value;
        Vector2 at=Projectile.Center-Main.screenPosition;Color c=PantheonCatalog.Colors[PantheonArsenal.Boss(Weapon)];
        if(Kind==3){CombatDrawing.Line(Main.spriteBatch,at,End-Main.screenPosition,c*(Age<=Variant?.35f:.9f),Age<=Variant?2:14);CombatDrawing.Circle(Main.spriteBatch,at,12,c,2);}
        else if(Kind is 4 or 5){float radius=Kind==5?30+Age*4:Weapon==26?55:75;CombatDrawing.Circle(Main.spriteBatch,at,radius,c*.75f,3,Age*.05f,.25f);for(int i=0;i<5;i++){float a=Age*.025f+i*MathHelper.TwoPi/5;CombatDrawing.Line(Main.spriteBatch,at+a.ToRotationVector2()*radius,at+(a+MathHelper.Pi*.8f).ToRotationVector2()*radius,c*.25f,1);}}
        else if(Kind==0)CombatDrawing.Line(Main.spriteBatch,Main.player[Projectile.owner].MountedCenter-Main.screenPosition,at,c*.5f,4);
        float scale=Kind==0?1.35f:Kind is 4 or 5?.6f:.4f;
        Main.spriteBatch.Draw(tex,at,null,Color.White*(Kind==2&&Age<=Variant?.4f:1),Projectile.rotation,tex.Size()/2,scale,SpriteEffects.None,0);return false;
    }
}
