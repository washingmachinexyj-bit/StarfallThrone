using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Fable.Bosses;

public sealed class FableHazard : ModProjectile
{
    public int ParentSlot=>(int)Projectile.ai[0];
    public int Kind=>(int)Projectile.ai[1];
    public int Serial=>(int)Projectile.ai[2];
    public FableBossNPC Parent=>ParentSlot>=0 && ParentSlot<Main.maxNPCs && Main.npc[ParentSlot].active && Main.npc[ParentSlot].ModNPC is FableBossNPC b && b.Serial==Serial && !b.Cancelled?b:null;
    public override string Texture=>FableCatalog.Root+"Spark";
    public override void SetDefaults(){Projectile.width=Projectile.height=8;Projectile.hostile=true;Projectile.friendly=false;Projectile.tileCollide=false;Projectile.timeLeft=150;Projectile.penetrate=1;Projectile.ignoreWater=true;}
    public override void AI()
    {
        var b=Parent;if(b==null){Projectile.Kill();return;}
        Projectile.localAI[0]++;
        if(Kind==0)Projectile.velocity.Y+=.018f;
        if(Kind is 3 or 4)
        {
            if(Projectile.localAI[0]<45)Projectile.position-=Projectile.velocity;
            if(Kind==3 && Projectile.localAI[0]>=53)Projectile.Kill();
        }
        if(Projectile.Center.X<b.ArenaCenter.X-220 || Projectile.Center.X>b.ArenaCenter.X+220 || Projectile.Center.Y>b.ArenaFloor+10)Projectile.Kill();
        if(Kind==2)
            foreach(Projectile p in Main.ActiveProjectiles)if(p.friendly && p.owner>=0 && p.owner<Main.maxPlayers && Main.player[p.owner].GetModPlayer<FablePlayer>().EncounterSlot==ParentSlot && p.Hitbox.Intersects(Projectile.Hitbox)){Projectile.Kill();break;}
    }
    public override bool? CanDamage()=>Parent!=null && (!(Kind is 3 or 4) || Projectile.localAI[0]>=45);
    public override bool CanHitPlayer(Player target)=>Parent!=null && target.GetModPlayer<FablePlayer>().EncounterSlot==ParentSlot;
    // Terraria doubles hostile projectile damage before Player.Hurt in every difficulty.
    // The catalog already contains the intended final pre-defense damage, including difficulty.
    public override void ModifyHitPlayer(Player target,ref Player.HurtModifiers modifiers)=>modifiers.SourceDamage*=.5f;
    public override bool? Colliding(Rectangle projHitbox,Rectangle targetHitbox)
    {
        if(Kind!=3)return null;
        float point=0;return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(),targetHitbox.Size(),Projectile.Center,Projectile.Center+Projectile.velocity.SafeNormalize(Vector2.UnitX)*120,5,ref point);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        var b=Parent;if(b==null)return false;Color color=FableCatalog.Colors[b.Index];Vector2 p=Projectile.Center-Main.screenPosition;
        if(Kind==3)
        {
            Vector2 v=Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,p,null,color*(Projectile.localAI[0]<45?.35f:1),v.ToRotation(),new Vector2(0,.5f),new Vector2(120,Projectile.localAI[0]<45?1:5),SpriteEffects.None,0);
        }
        else
        {
            Texture2D tex=TextureAssets.Projectile[Type].Value;
            Main.spriteBatch.Draw(tex,p,null,color*(Kind==4 && Projectile.localAI[0]<45?.4f:1),Projectile.rotation,tex.Size()/2,Kind==2?1.5f:1,SpriteEffects.None,0);
            if(Kind==4 && Projectile.localAI[0]<45)Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,new Rectangle((int)p.X-8,(int)(b.ArenaFloor-Main.screenPosition.Y)-2,16,2),Color.Gold);
        }
        return false;
    }
}
