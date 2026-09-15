using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Voyage.Utility;

public sealed class VoyageSearchlightPet : ModProjectile
{
    public override string Texture => VoyageCatalog.Root + "Utility1";
    public override void SetStaticDefaults()
    {
        Main.projPet[Type] = true;
        ProjectileID.Sets.LightPet[Type] = true;
    }
    public override void SetDefaults()
    {
        Projectile.width = 24; Projectile.height = 24;
        Projectile.aiStyle = -1;
        Projectile.friendly = true; Projectile.damage = 0;
        Projectile.tileCollide = false; Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
    }
    public override bool? CanDamage() => false;
    public override void AI()
    {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead || !owner.HasBuff(ModContent.BuffType<VoyageSearchlightBuff>()))
        { Projectile.Kill(); return; }
        Projectile.timeLeft = 2;
        Vector2 target = owner.Center + new Vector2(owner.direction * 50, -62 + MathF.Sin((float)Main.GlobalTimeWrappedHourly * 2) * 5);
        Vector2 delta = target - Projectile.Center;
        if (delta.LengthSquared() > 1200 * 1200)
        { Projectile.Center = target; Projectile.velocity = Vector2.Zero; Projectile.netUpdate = true; }
        else Projectile.velocity = (Projectile.velocity * 15 + delta * .12f) / 16;
        Projectile.spriteDirection = owner.direction;
        Projectile.rotation = Projectile.velocity.X * .025f;
        if (Main.dedServ) return;
        Lighting.AddLight(Projectile.Center, .8f, 1f, 1.15f);
        // A short visible light cone, not a combat beam and not a map-reveal cheat.
        for (int step = 1; step <= 7; step++)
        {
            Vector2 point = Projectile.Center + new Vector2(owner.direction * step * 24, step * 5);
            if (!Collision.CanHitLine(Projectile.Center, 1, 1, point, 1, 1)) break;
            float power = 1 - step / 9f;
            Lighting.AddLight(point, .55f * power, .75f * power, 1.0f * power);
        }
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = TextureAssets.Projectile[Type].Value;
        float scale = 34f / Math.Max(texture.Width, texture.Height);
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, Color.White,
            Projectile.rotation, texture.Size() / 2, scale, Projectile.spriteDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
        return false;
    }
}

public sealed class VoyageEngineeringDrill : ModProjectile
{
    public override string Texture => VoyageCatalog.Root + "Utility2";
    public override void SetDefaults()
    {
        Projectile.CloneDefaults(ProjectileID.SolarFlareDrill);
        AIType = ProjectileID.SolarFlareDrill;
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = TextureAssets.Projectile[Type].Value;
        Player owner = Main.player[Projectile.owner];
        Vector2 offset = Projectile.Center - owner.MountedCenter;
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor,
            offset.ToRotation() + MathHelper.PiOver4, texture.Size() / 2,
            54f / Math.Max(texture.Width, texture.Height), SpriteEffects.None);
        return false;
    }
}
