#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using global::StarfallThrone.Content.Systems;
using global::StarfallThrone.Content.Voyage.Weapons;

namespace StarfallThrone.Content.Ecology.Equipment;

public abstract class EcologyMinionBuff:ModBuff
{
    public abstract int Biome{get;}
    public override string Texture=>EcologyCatalog.Root+"Weapon"+(Biome*4+3);
    public override void SetStaticDefaults(){Main.buffNoSave[Type]=true;Main.buffNoTimeDisplay[Type]=true;}
    public override void Update(Player p,ref int buffIndex)
    {
        if(p.ownedProjectileCounts[EcologyArsenal.Minion(Biome)]>0)p.buffTime[buffIndex]=18000;
        else{p.DelBuff(buffIndex);buffIndex--;}
    }
}
public abstract class EcologyMinion:ModProjectile,IVoyageWeaponProjectile
{
    public abstract int Biome{get;}
    public int WeaponIndex=>-1;
    public bool Secondary=>false;
    // Theme creature art is reused deliberately: these are domesticated versions of their ecology mobs.
    public override string Texture=>EcologyCatalog.Root+"Mob"+(Biome*4);
    public override void SetStaticDefaults()
    {
        Main.projPet[Type]=true;ProjectileID.Sets.MinionSacrificable[Type]=true;ProjectileID.Sets.MinionTargettingFeature[Type]=true;
    }
    public override void SetDefaults()
    {
        Projectile.width=Projectile.height=26;Projectile.friendly=true;Projectile.minion=true;Projectile.minionSlots=1;
        Projectile.DamageType=DamageClass.Summon;Projectile.penetrate=-1;Projectile.tileCollide=false;Projectile.ignoreWater=true;
        Projectile.timeLeft=18000;Projectile.usesLocalNPCImmunity=true;Projectile.localNPCHitCooldown=28;
    }
    public int Period=>Biome switch{0=>84,1=>110,2=>90,3=>102,4=>100,5=>108,6=>96,7=>126,8=>104,_=>100};
    public override bool MinionContactDamage()=>true;
    public override bool? CanDamage()
    {
        int phase=(int)Projectile.ai[0];
        if(Projectile.ai[1]<=0)return false;
        return Biome switch
        {
            0=>phase is >=30 and <48?null:false,
            1=>phase is >=66 and <85?null:false,
            2=>false,
            3=>phase is >=58 and <76?null:false,
            4=>phase is >=24 and <40 or >=58 and <74?null:false,
            5=>false,
            6=>phase is >=42 and <60?null:false,
            7=>phase is >=60 and <83?null:false,
            8=>phase is >=65 and <84?null:false,
            _=>phase is >=44 and <65?null:false
        };
    }
    public override bool? CanCutTiles()=>false;
    public override bool? CanHitNPC(NPC target)=>Collision.CanHitLine(Projectile.Center,1,1,target.Center,1,1)?null:false;
    private bool Valid(NPC n,Player p)=>n.CanBeChasedBy()&&Vector2.DistanceSquared(n.Center,p.Center)<1000*1000&&Collision.CanHitLine(Projectile.Center,1,1,n.Center,1,1);
    public override void AI()
    {
        if(Projectile.owner<0||Projectile.owner>=Main.maxPlayers){Projectile.Kill();return;}
        Player p=Main.player[Projectile.owner];
        if(!p.active||p.dead||!p.HasBuff(EcologyArsenal.Buff(Biome))){Projectile.Kill();return;}
        Projectile.timeLeft=2;
        bool owner=Projectile.owner==Main.myPlayer;
        if(Vector2.DistanceSquared(Projectile.Center,p.Center)>1400*1400)
        {Projectile.Center=p.Center;Projectile.velocity=Vector2.Zero;Projectile.ai[0]=0;Projectile.netUpdate=true;}
        if(owner)
        {
            NPC? selected=EcologyShot.FindTarget(Projectile.Center,p,800);int next=selected?.whoAmI??-1;
            if((int)Projectile.ai[1]!=next+1||next>=0&&(int)Projectile.ai[2]!=selected!.type)
            {Projectile.ai[1]=next+1;Projectile.ai[2]=selected?.type??0;Projectile.ai[0]=0;Projectile.netUpdate=true;}
        }
        int index=(int)Projectile.ai[1]-1;
        NPC? target=index>=0&&index<Main.maxNPCs&&Main.npc[index].type==(int)Projectile.ai[2]&&Valid(Main.npc[index],p)?Main.npc[index]:null;
        if(target==null)
        {
            Projectile.ai[0]=0;
            Move(p.Center+new Vector2(-p.direction*(45+Projectile.minionPos*34),-58-(Projectile.minionPos%3)*18),10);
            return;
        }
        Projectile.ai[0]=(Projectile.ai[0]+1)%Period;int phase=(int)Projectile.ai[0];
        Vector2 standby=target.Center+new Vector2(-p.direction*(90+Projectile.minionPos%3*18),-60);
        void Dash(float speed)
        {Projectile.velocity=(target.Center-Projectile.Center).SafeNormalize(Vector2.UnitX)*speed;Projectile.netUpdate=true;}
        void Shoot(float scale,EcologyShotKind kind=EcologyShotKind.MinionBolt,float spread=0)
        {
            if(!owner)return;
            EcologyArsenal.Launch(Projectile.GetSource_FromThis(),Projectile.owner,Biome*4+3,kind,Projectile.Center,
                (target.Center-Projectile.Center).SafeNormalize(Vector2.UnitX).RotatedBy(spread)*(12+Biome*.5f),(int)(Projectile.damage*scale),2,secondary:true);
        }
        switch(Biome)
        {
            case 0:
                if(phase<30)Move(target.Center+new Vector2(-p.direction*90,-50).RotatedBy(phase*.025f),10);
                else if(phase==30)Dash(17);
                else if(phase>=48)Move(p.Center+new Vector2(-p.direction*65,-55),11);
                break;
            case 1:
                if(phase<50){Move(standby,7);Projectile.scale=.85f;}
                else if(phase==50){Shoot(.7f);Projectile.scale=1.1f;}
                else if(phase==66)Dash(18);
                else if(phase>=85){Projectile.scale=1;Move(standby,9);}
                break;
            case 2:
                Move(target.Center+new Vector2(-p.direction*145,-100)+new Vector2(MathF.Sin(phase*.07f)*32,0),10);
                if(phase is 20 or 38 or 56)Shoot(.55f,EcologyShotKind.Needle,(phase-38)*.005f);
                break;
            case 3:
                if(phase<58)Move(standby+new Vector2(0,-25),8);
                if(phase==28)Shoot(.55f,EcologyShotKind.SoundBolt);
                if(phase==58)Dash(19);
                if(phase>=76)Move(standby,11);
                break;
            case 4:
                if(phase<24)Move(standby,11);
                else if(phase is 24 or 58)Dash(21);
                else if(phase is >=40 and <58)Move(target.Center+new Vector2(p.direction*95,-35),16);
                else if(phase>=74)Move(standby,10);
                break;
            case 5:
                Move(target.Center+new Vector2(-p.direction*210,-40),phase<18?12:4);
                if(phase is 28 or 38 or 48 or 72)Shoot(.45f,EcologyShotKind.Rivet);
                break;
            case 6:
                if(phase<42)Move(target.Center+new Vector2(-p.direction*60,75),9);
                else if(phase==42)Dash(18);
                else if(phase>=60)Move(standby,10);
                if(phase==75)Shoot(.6f,EcologyShotKind.Needle);
                break;
            case 7:
                if(phase<60)Move(target.Center+new Vector2(-p.direction*130,-10),7);
                else if(phase==60)Dash(22);
                else if(phase>=83)Move(standby,12);
                if(phase==100)Shoot(.6f);
                break;
            case 8:
                if(phase<65)Move(target.Center+new Vector2(MathF.Sin(phase*.055f)*150,-110),13);
                if(phase is 20 or 42)Shoot(.45f,EcologyShotKind.Needle);
                if(phase==65)Dash(23);
                if(phase>=84)Move(standby,13);
                break;
            case 9:
                if(phase<44)
                {
                    Vector2 orbit=target.Center+(phase*.09f+Projectile.minionPos).ToRotationVector2()*135;
                    Move(orbit,15);
                }
                if(phase==44)Dash(25);
                if(phase==70&&owner)
                {
                    Vector2 exit=target.Center+new Vector2(-p.direction*160,-90);
                    if(Collision.CanHitLine(p.Center,1,1,exit,1,1)&&!Collision.SolidCollision(exit-new Vector2(13),26,26))
                    {Projectile.Center=exit;Projectile.velocity=Vector2.Zero;Projectile.netUpdate=true;}
                }
                if(phase is 76 or 86)Shoot(.45f);
                if(phase>=90)Move(standby,12);
                break;
        }
        Projectile.rotation=Math.Clamp(Projectile.velocity.X*.018f,-.45f,.45f);
        Projectile.spriteDirection=Projectile.velocity.X<0?-1:1;
        if(!Main.dedServ)Lighting.AddLight(Projectile.Center,EcologyCatalog.Colors[Biome].ToVector3()*.2f);
    }
    private void Move(Vector2 at,float speed)
    {
        Vector2 diff=at-Projectile.Center,wanted=diff.Length()<speed?diff:diff.SafeNormalize(Vector2.UnitX)*speed;
        Projectile.velocity=(Projectile.velocity*7+wanted)/8;
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D tex=ModContent.Request<Texture2D>(Texture).Value;Vector2 at=Projectile.Center-Main.screenPosition;
        float scale=40f/Math.Max(tex.Width,tex.Height)*Projectile.scale;
        Main.spriteBatch.Draw(tex,at,null,Color.White,Projectile.rotation,tex.Size()/2,scale,Projectile.spriteDirection<0?SpriteEffects.FlipHorizontally:SpriteEffects.None,0);
        Color c=EcologyCatalog.Colors[Biome];
        if(CanDamage()==false&&Projectile.ai[1]>0)CombatDrawing.Circle(Main.spriteBatch,at,23,c*.5f,2);
        if(Biome==9)CombatDrawing.Circle(Main.spriteBatch,at,30,c*.65f,2,Projectile.ai[0]*.1f,.8f);
        return false;
    }
}
public sealed class EcologyMinionBuff0:EcologyMinionBuff{public override int Biome=>0;}
public sealed class EcologyMinion0:EcologyMinion{public override int Biome=>0;}
public sealed class EcologyMinionBuff1:EcologyMinionBuff{public override int Biome=>1;}
public sealed class EcologyMinion1:EcologyMinion{public override int Biome=>1;}
public sealed class EcologyMinionBuff2:EcologyMinionBuff{public override int Biome=>2;}
public sealed class EcologyMinion2:EcologyMinion{public override int Biome=>2;}
public sealed class EcologyMinionBuff3:EcologyMinionBuff{public override int Biome=>3;}
public sealed class EcologyMinion3:EcologyMinion{public override int Biome=>3;}
public sealed class EcologyMinionBuff4:EcologyMinionBuff{public override int Biome=>4;}
public sealed class EcologyMinion4:EcologyMinion{public override int Biome=>4;}
public sealed class EcologyMinionBuff5:EcologyMinionBuff{public override int Biome=>5;}
public sealed class EcologyMinion5:EcologyMinion{public override int Biome=>5;}
public sealed class EcologyMinionBuff6:EcologyMinionBuff{public override int Biome=>6;}
public sealed class EcologyMinion6:EcologyMinion{public override int Biome=>6;}
public sealed class EcologyMinionBuff7:EcologyMinionBuff{public override int Biome=>7;}
public sealed class EcologyMinion7:EcologyMinion{public override int Biome=>7;}
public sealed class EcologyMinionBuff8:EcologyMinionBuff{public override int Biome=>8;}
public sealed class EcologyMinion8:EcologyMinion{public override int Biome=>8;}
public sealed class EcologyMinionBuff9:EcologyMinionBuff{public override int Biome=>9;}
public sealed class EcologyMinion9:EcologyMinion{public override int Biome=>9;}

