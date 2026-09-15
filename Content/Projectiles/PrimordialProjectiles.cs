using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Projectiles;

public sealed class PrimordialBossProjectile : ModProjectile
{
    public override string Texture => "StarfallThrone/Content/Assets/Projectiles/PrimordialBossShot";

    public override void SetDefaults()
    {
        Projectile.width = 14;
        Projectile.height = 14;
        Projectile.hostile = true;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 240;
        Projectile.extraUpdates = 1;
    }

    public override void AI()
    {
        int bossIndex = Math.Clamp((int)Projectile.ai[0], 0, PrimordialBossData.Count - 1);
        int style = (int)Projectile.ai[1];
        Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        Lighting.AddLight(Projectile.Center, PrimordialBossData.Color(bossIndex).ToVector3() * 0.45f);

        if (style == 0)
            Projectile.velocity.Y += 0.018f;
        else if (style == 1)
            Projectile.velocity = Projectile.velocity.RotatedBy(0.012f);
        else if (style == 2)
            Projectile.velocity = Projectile.velocity.RotatedBy(Math.Sin(Projectile.timeLeft * 0.06f) * 0.014f);
        else if (style == 3)
            Projectile.velocity.Y += 0.04f;
        else if (style == 4)
            Projectile.velocity *= 1.004f;
        else if (style == 5)
            Projectile.velocity = Projectile.velocity.RotatedBy(-0.01f);
        else if (style == 6 && Projectile.timeLeft < 180)
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, Projectile.velocity.SafeNormalize(Vector2.UnitX) * Projectile.velocity.Length() * 1.01f, 0.08f);
        else if (style == 7)
            Projectile.velocity = Projectile.velocity.RotatedBy(Math.Sin(Main.GlobalTimeWrappedHourly * 10f + Projectile.whoAmI) * 0.012f);
        else if (style == 8)
            Projectile.velocity.Y += 0.025f;
        else if (style == 9)
            Projectile.velocity *= 1.006f;

        for (int i = 0; i < 2; i++)
            Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, -Projectile.velocity * 0.04f, 0, PrimordialBossData.Color(bossIndex), 0.65f);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = TextureAssets.Projectile[Type].Value;
        int bossIndex = Math.Clamp((int)Projectile.ai[0], 0, PrimordialBossData.Count - 1);
        Color color = PrimordialBossData.Color(bossIndex);
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
            Color.FromNonPremultiplied(color.R, color.G, color.B, 75), Projectile.rotation,
            texture.Size() / 2f, 1.75f, SpriteEffects.None);
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, Color.White,
            Projectile.rotation, texture.Size() / 2f, 0.8f, SpriteEffects.None);
        return false;
    }
}

public sealed class PrimordialPlayerProjectile : ModProjectile
{
    public override string Texture => "StarfallThrone/Content/Assets/Projectiles/PrimordialPlayerProjectile";

    public override void SetDefaults()
    {
        Projectile.width = 16;
        Projectile.height = 16;
        Projectile.friendly = true;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 240;
        Projectile.extraUpdates = 1;
    }

    public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
    {
        int style = Math.Clamp((int)Projectile.ai[0], 0, 20);
        Projectile.DamageType = style is 1 or 5 or 7 or 8 or 13 or 17 or 19 ? DamageClass.Magic :
            style is 9 or 11 ? DamageClass.Summon :
            style is 0 or 2 or 10 or 15 or 16 ? DamageClass.Ranged : DamageClass.Melee;
    }

    public override void AI()
    {
        int style = Math.Clamp((int)Projectile.ai[0], 0, 20);
        Projectile.rotation = Projectile.velocity.ToRotation() + (style is 3 or 4 or 6 or 12 or 14 or 18 ? MathHelper.PiOver4 : MathHelper.PiOver2);

        if (style is 0 or 1)
            Projectile.velocity.Y += 0.025f;
        else if (style is 4 or 6 or 14 or 18)
            Projectile.velocity = Projectile.velocity.RotatedBy(Math.Sin(Projectile.timeLeft * 0.05f) * 0.009f);
        else if (style is 5 or 7 or 13 or 17 or 19)
            Projectile.velocity *= 0.998f;
        else if (style is 16)
            Projectile.velocity.Y += 0.018f;

        Color color = PrimordialBossData.Color(Math.Min(9, style / 2));
        Lighting.AddLight(Projectile.Center, color.ToVector3() * 0.35f);
        Dust.NewDustPerfect(Projectile.Center, DustID.ShimmerSpark, -Projectile.velocity * 0.04f, 0, color, 0.7f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        int style = Math.Clamp((int)Projectile.ai[0], 0, 20);
        if (style is 0 or 1)
            target.AddBuff(BuffID.Slow, 120);
        else if (style is 4 or 5 or 8)
            target.AddBuff(BuffID.Poisoned, 180);
        else if (style is 6 or 7)
            target.AddBuff(BuffID.Chilled, 90);
        else if (style is 10 or 11)
            target.AddBuff(BuffID.Slow, 60);
        else if (style is 18 or 19)
            target.AddBuff(BuffID.Confused, 45);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = TextureAssets.Projectile[Type].Value;
        int style = Math.Clamp((int)Projectile.ai[0], 0, 19);
        Color color = PrimordialBossData.Color(Math.Min(9, style / 2));
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
            Color.FromNonPremultiplied(color.R, color.G, color.B, 82), Projectile.rotation,
            texture.Size() / 2f, 1.8f, SpriteEffects.None);
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, Color.White,
            Projectile.rotation, texture.Size() / 2f, 0.78f, SpriteEffects.None);
        return false;
    }
}
