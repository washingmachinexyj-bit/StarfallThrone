using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Fable.Equipment;

public sealed class FableShot : ModProjectile
{
    public override string Texture=>FableCatalog.Root+"Spark";
    public override void SetDefaults(){Projectile.width=Projectile.height=6;Projectile.friendly=true;Projectile.penetrate=1;Projectile.timeLeft=32;Projectile.tileCollide=true;Projectile.ignoreWater=false;}
    public override void AI()
    {
        int index=(int)Projectile.ai[0];Projectile.DamageType=index is 1 or 7 or 13 or 15 or 20?DamageClass.Magic:index==11?DamageClass.Summon:DamageClass.Ranged;
        if(index is 0 or 4 or 8 or 10 or 11)Projectile.velocity.Y+=.07f;
        Projectile.rotation=Projectile.velocity.ToRotation();
    }
    public override bool? CanHitNPC(NPC target)
    {return Main.player[Projectile.owner].GetModPlayer<FablePlayer>().InTrial && target.ModNPC is not Bosses.FableBossNPC?false:null;}
    public override bool PreDraw(ref Color lightColor)
    {var tex=TextureAssets.Projectile[Type].Value;Main.spriteBatch.Draw(tex,Projectile.Center-Main.screenPosition,null,FableCatalog.Colors[Math.Clamp((int)Projectile.ai[0],0,17)],Projectile.rotation,tex.Size()/2,1,SpriteEffects.None,0);return false;}
}
public sealed class FableCompanionBuff : ModBuff
{
    public override string Texture=>FableCatalog.Root+"Weapon3";
    public override void SetStaticDefaults(){Main.buffNoSave[Type]=true;Main.buffNoTimeDisplay[Type]=true;}
    public override void Update(Player p,ref int index)
    {if(p.ownedProjectileCounts[ModContent.ProjectileType<FableMinion>()]>0)p.buffTime[index]=18000;else{p.DelBuff(index);index--;}}
}
public sealed class FableMinion : ModProjectile
{
    public override string Texture=>FableCatalog.Root+"Minion";
    public override void SetStaticDefaults(){Main.projPet[Type]=true;ProjectileID.Sets.MinionSacrificable[Type]=true;ProjectileID.Sets.MinionTargettingFeature[Type]=true;}
    public override void SetDefaults(){Projectile.width=Projectile.height=16;Projectile.minion=true;Projectile.minionSlots=1;Projectile.friendly=true;Projectile.DamageType=DamageClass.Summon;Projectile.penetrate=-1;Projectile.timeLeft=18000;Projectile.tileCollide=false;Projectile.usesLocalNPCImmunity=true;Projectile.localNPCHitCooldown=66;}
    public override bool MinionContactDamage()=>((int)Projectile.ai[0])!=11;
    public override void AI()
    {
        Player p=Main.player[Projectile.owner];if(!p.active || p.dead || !p.HasBuff<FableCompanionBuff>()){Projectile.Kill();return;}Projectile.timeLeft=2;
        int id=(int)Projectile.ai[0];Projectile.localNPCHitCooldown=id==3?72:66;Projectile.localAI[0]++;
        NPC target=null;float nearest=240;
        foreach(NPC n in Main.ActiveNPCs)
        {if(!n.CanBeChasedBy() || p.GetModPlayer<FablePlayer>().InTrial && n.ModNPC is not Bosses.FableBossNPC || !Collision.CanHitLine(Projectile.position,16,16,n.position,n.width,n.height))continue;float d=Vector2.Distance(p.Center,n.Center);if(d<nearest){nearest=d;target=n;}}
        Vector2 home=p.Center+new Vector2(-p.direction*28,-36);Vector2 dest=target!=null && id!=11?target.Center:home;
        if(Vector2.Distance(Projectile.Center,p.Center)>600)Projectile.Center=home;
        Projectile.velocity=(dest-Projectile.Center)*.08f;
        if(Projectile.velocity.Length()>4)Projectile.velocity=Projectile.velocity.SafeNormalize(Vector2.UnitY)*4;
        if(id==11 && target!=null && Projectile.localAI[0]%66==0 && Projectile.owner==Main.myPlayer)
            Projectile.NewProjectile(Projectile.GetSource_FromAI(),Projectile.Center,(target.Center-Projectile.Center).SafeNormalize(Vector2.UnitY)*3.5f,ModContent.ProjectileType<FableShot>(),Projectile.damage,0,p.whoAmI,11);
    }
    public override bool? CanHitNPC(NPC target)=>Main.player[Projectile.owner].GetModPlayer<FablePlayer>().InTrial && target.ModNPC is not Bosses.FableBossNPC?false:null;
    public override bool PreDraw(ref Color lightColor)
    {
        int id=(int)Projectile.ai[0];Texture2D tex=id<18?ModContent.Request<Texture2D>(FableCatalog.Root+"Boss"+id).Value:TextureAssets.Projectile[Type].Value;
        Main.spriteBatch.Draw(tex,Projectile.Center-Main.screenPosition,null,Color.White,0,tex.Size()/2,id<18?.4f:1,SpriteEffects.None,0);return false;
    }
}
public sealed class FableWhip : ModProjectile
{
    public override string Texture=>FableCatalog.Root+"Weapon6";
    public override void SetStaticDefaults()=>ProjectileID.Sets.IsAWhip[Type]=true;
    public override void SetDefaults(){Projectile.DefaultToWhip();Projectile.WhipSettings.Segments=10;Projectile.WhipSettings.RangeMultiplier=.45f;}
    public override bool? CanHitNPC(NPC target)=>Main.player[Projectile.owner].GetModPlayer<FablePlayer>().InTrial && target.ModNPC is not Bosses.FableBossNPC?false:null;
    public override bool PreDraw(ref Color lightColor)
    {
        var points=new List<Vector2>();Projectile.FillWhipControlPoints(Projectile,points);
        for(int i=1;i<points.Count;i++){Vector2 v=points[i]-points[i-1];Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,points[i-1]-Main.screenPosition,null,new Color(222,150,161),v.ToRotation(),new Vector2(0,.5f),new Vector2(v.Length(),2),SpriteEffects.None,0);}return false;
    }
}
