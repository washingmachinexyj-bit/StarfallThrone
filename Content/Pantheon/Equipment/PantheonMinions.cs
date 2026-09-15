#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using global::StarfallThrone.Content.Systems;
using global::StarfallThrone.Content.Voyage.Weapons;

namespace StarfallThrone.Content.Pantheon.Equipment;

public abstract class PantheonMinionBuff:ModBuff
{
    public abstract int Boss{get;}
    public override string Texture=>PantheonCatalog.Root+"Minion"+Boss;
    public override void SetStaticDefaults(){Main.buffNoSave[Type]=true;Main.buffNoTimeDisplay[Type]=true;}
    public override void Update(Player p,ref int buffIndex)
    {int weapon=Boss==16?35:Boss*2+1;if(p.ownedProjectileCounts[PantheonArsenal.Minion(weapon)]>0)p.buffTime[buffIndex]=18000;else{p.DelBuff(buffIndex);buffIndex--;}}
}
public abstract class PantheonMinion:ModProjectile,IVoyageWeaponProjectile
{
    public abstract int Boss{get;}public int Weapon=>Boss==16?35:Boss*2+1;public int WeaponIndex=>-1;public bool Secondary=>false;
    public int Period=>Boss switch{1=>90,3=>110,5=>120,7=>144,9=>160,11=>150,13=>120,15=>144,_=>180};
    public override string Texture=>PantheonCatalog.Root+"Minion"+Boss;
    public override void SetStaticDefaults(){Main.projPet[Type]=true;ProjectileID.Sets.MinionSacrificable[Type]=true;ProjectileID.Sets.MinionTargettingFeature[Type]=true;}
    public override void SetDefaults(){Projectile.width=Projectile.height=36;Projectile.friendly=true;Projectile.minion=true;Projectile.minionSlots=2;Projectile.penetrate=-1;Projectile.tileCollide=false;Projectile.ignoreWater=true;Projectile.timeLeft=18000;Projectile.DamageType=DamageClass.Summon;Projectile.usesLocalNPCImmunity=true;Projectile.localNPCHitCooldown=30;}
    public override bool MinionContactDamage()=>true;
    public override bool? CanCutTiles()=>false;
    public override bool? CanDamage()=>(Boss is 3 or 5 or 13 or 16)&&((int)Projectile.ai[0] is >=30 and <48)?null:false;
    public override bool? CanHitNPC(NPC target)=>Collision.CanHitLine(Projectile.Center,1,1,target.position,target.width,target.height)?null:false;
    public override void AI()
    {
        if(Projectile.owner<0||Projectile.owner>=Main.maxPlayers){Projectile.Kill();return;}
        Player p=Main.player[Projectile.owner];if(!p.active||p.dead||!p.HasBuff(PantheonArsenal.Buff(Weapon))){Projectile.Kill();return;}
        Projectile.timeLeft=2;bool owner=Projectile.owner==Main.myPlayer;
        if(Vector2.DistanceSquared(Projectile.Center,p.Center)>1600*1600){Projectile.Center=p.Center;Projectile.velocity=Vector2.Zero;Projectile.netUpdate=true;}
        if(owner)
        {
            NPC? n=PantheonArsenal.Target(Projectile.Center,Projectile.owner,900);int chosen=n?.whoAmI??-1;
            if((int)Projectile.ai[1]!=chosen+1||(int)Projectile.ai[2]!=(n?.type??0)){Projectile.ai[1]=chosen+1;Projectile.ai[2]=n?.type??0;Projectile.ai[0]=0;Projectile.netUpdate=true;}
        }
        int index=(int)Projectile.ai[1]-1;NPC? target=index>=0&&index<Main.maxNPCs?Main.npc[index]:null;
        if(target==null||!target.CanBeChasedBy()||target.type!=(int)Projectile.ai[2]||Vector2.DistanceSquared(target.Center,p.Center)>1200*1200||!Collision.CanHitLine(Projectile.Center,1,1,target.position,target.width,target.height))
        {Projectile.ai[0]=0;Move(p.Center+new Vector2(-p.direction*(60+Projectile.minionPos*35),-70-Projectile.minionPos%2*35),14);return;}
        Projectile.ai[0]=(Projectile.ai[0]+1)%Period;int phase=(int)Projectile.ai[0];Vector2 direction=(target.Center-Projectile.Center).SafeNormalize(Vector2.UnitX);
        bool diver=Boss is 3 or 5 or 13 or 16;
        if(diver&&phase==30){Projectile.velocity=direction*(Boss==13?30:24);Projectile.netUpdate=true;}
        else if(!(diver&&phase>=30&&phase<48))
        {
            Vector2 offset=Boss==7?(phase*MathHelper.TwoPi/Period).ToRotationVector2()*155:Boss==11?new Vector2(-p.direction*110,-135):new Vector2(-p.direction*(150+Projectile.minionPos%3*25),-65);
            Move(target.Center+offset,18);
        }
        void Bolt(float power,float speed=22,float angle=0,int kind=6,int delay=0,Vector2? end=null)=>PantheonArsenal.Launch(Projectile.GetSource_FromThis(),Projectile.owner,Weapon,kind,Projectile.Center,direction.RotatedBy(angle)*speed,(int)(Projectile.damage*power),delay,end);
        if(owner)switch(Boss)
        {
            case 1:if(phase==45)Bolt(1.6f,0,kind:3,delay:12,end:target.Center+direction*80);break;
            case 3:if(phase==65)Bolt(.8f,24,kind:2,delay:20);break;
            case 5:if(phase==78){Bolt(.45f,14,-.3f);Bolt(.45f,14,.3f);}break;
            case 7:if(phase%36==0){Bolt(.7f,18);if(phase==108)Bolt(.8f,0,kind:5);}break;
            case 9:
                if(phase==35)Bolt(.65f,28);if(phase==70){Bolt(.3f,18,-.2f);Bolt(.3f,18,.2f);}if(phase==105)Bolt(.8f,0,kind:3,delay:10,end:target.Center);if(phase==140)Bolt(.65f,10);break;
            case 11:if(phase is 60 or 85 or 110){Bolt(.35f,18,-.24f);Bolt(.35f,18,0);Bolt(.35f,18,.24f);}break;
            case 13:if(phase is 60 or 76 or 92)Bolt(.4f,13,MathF.Sin(phase)*.25f);break;
            case 15:if(phase is 36 or 72 or 108)Bolt(.7f,25,kind:2,delay:(phase/36)*10);break;
            case 16:if(phase==75)Bolt(1.1f,0,kind:3,delay:12,end:target.Center+direction*120);if(phase==125)for(int i=-1;i<=1;i++)Bolt(.5f,22,i*.18f);break;
        }
        Projectile.rotation=Math.Clamp(Projectile.velocity.X*.015f,-.35f,.35f);
        if(!Main.dedServ)Lighting.AddLight(Projectile.Center,PantheonCatalog.Colors[Boss].ToVector3()*.35f);
    }
    private void Move(Vector2 at,float speed){Vector2 d=at-Projectile.Center;Projectile.velocity=Vector2.Lerp(Projectile.velocity,d.Length()<speed?d:d.SafeNormalize(Vector2.UnitX)*speed,.16f);}
    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D tex=ModContent.Request<Texture2D>(Texture).Value;Vector2 at=Projectile.Center-Main.screenPosition;Color c=PantheonCatalog.Colors[Boss];
        for(int i=0;i<3;i++)CombatDrawing.Circle(Main.spriteBatch,at+new Vector2(MathF.Sin(Projectile.ai[0]*.03f+i)*8,0),24+i*4,c*(.22f-i*.04f),1,Projectile.ai[0]*.03f,.65f);
        Main.spriteBatch.Draw(tex,at,null,Color.White,Projectile.rotation,tex.Size()/2,1,SpriteEffects.None,0);return false;
    }
}
