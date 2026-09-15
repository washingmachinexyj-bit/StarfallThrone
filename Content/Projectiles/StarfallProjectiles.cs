using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Projectiles;

public sealed class StarfallBolt : ModProjectile
{
    public override string Texture => "StarfallThrone/Content/Assets/Projectiles/StarfallBolt";

    public override void SetStaticDefaults() => Main.projFrames[Type] = 1;

    public override void SetDefaults()
    {
        Projectile.width = 20;
        Projectile.height = 20;
        Projectile.hostile = true;
        Projectile.friendly = false;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 360;
        Projectile.alpha = 10;
        Projectile.extraUpdates = 1;
    }

    public override void AI()
    {
        int bossIndex = Math.Clamp((int)Projectile.ai[0], 0, StarfallWorld.BossCount - 1);
        int phase = (int)Projectile.ai[1];
        int attackStyle = (int)Projectile.ai[2];
        Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        Lighting.AddLight(Projectile.Center, BossData.Color(bossIndex).ToVector3() * 0.7f);
        Dust.NewDustPerfect(Projectile.Center, DustID.ShimmerSpark, -Projectile.velocity * 0.03f, 0, BossData.Color(bossIndex), 0.8f);

        switch (attackStyle % 5)
        {
            case 1:
                Projectile.velocity.Y += (float)Math.Sin(Main.GlobalTimeWrappedHourly * 18f + Projectile.whoAmI) * 0.035f;
                break;
            case 2:
                Projectile.velocity = Projectile.velocity.RotatedBy(0.008f * (Projectile.direction == 0 ? 1 : Projectile.direction));
                break;
            case 3:
                Projectile.velocity = Projectile.velocity.RotatedBy(Math.Sin(Projectile.timeLeft * 0.04f) * 0.012f);
                break;
            case 4:
                if (Projectile.timeLeft % 45 == 0)
                    Projectile.velocity *= 1.12f;
                break;
        }

        if (attackStyle == 6)
        {
            // Narrow “beam” signatures accelerate instead of homing, so they stay readable and punish panic movement.
            Projectile.velocity *= 1.012f;
        }
        else if (attackStyle == 13)
        {
            // The final boss leaves a slow spiral that tightens as it approaches its target.
            Projectile.velocity = Projectile.velocity.RotatedBy(0.018f * Math.Sign(Projectile.velocity.X == 0f ? 1f : Projectile.velocity.X));
        }

        if (Projectile.timeLeft < 250 && attackStyle != 6)
        {
            Player target = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
            Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
            float homing = phase >= 2 ? 0.018f : 0.009f;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * Projectile.velocity.Length(), homing);
        }
    }

    public override void OnKill(int timeLeft)
    {
        if (Projectile.ai[1] < 2f || Main.netMode == NetmodeID.MultiplayerClient)
            return;

        int bossIndex = Math.Clamp((int)Projectile.ai[0], 0, StarfallWorld.BossCount - 1);
        for (int i = 0; i < 3; i++)
        {
            Vector2 velocity = (MathHelper.TwoPi * i / 3f).ToRotationVector2() * 4.5f;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, velocity, ModContent.ProjectileType<StarfallShard>(), Projectile.damage / 2, 0.2f, Main.myPlayer, bossIndex, 0f);
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = TextureAssets.Projectile[Type].Value;
        Color color = BossData.Color(Math.Clamp((int)Projectile.ai[0], 0, StarfallWorld.BossCount - 1));
        int attackStyle = (int)Projectile.ai[2];
        float glowScale = attackStyle == 6 ? 2.65f : attackStyle == 13 ? 2.25f : 1.8f;
        Color glowColor = attackStyle == 6 ? Color.Lerp(color, Color.White, 0.45f) : color;
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
            Color.FromNonPremultiplied(glowColor.R, glowColor.G, glowColor.B, 74), Projectile.rotation,
            texture.Size() / 2f, glowScale, SpriteEffects.None);
        if (attackStyle == 13)
        {
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
                Color.FromNonPremultiplied(255, 255, 255, 70), Projectile.rotation,
                texture.Size() / 2f, 1.35f, SpriteEffects.None);
        }
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
            Color.White, Projectile.rotation, texture.Size() / 2f, attackStyle == 6 ? 1.05f : 0.85f, SpriteEffects.None);
        return false;
    }
}

public sealed class StarfallShard : ModProjectile
{
    public override string Texture => "StarfallThrone/Content/Assets/Projectiles/StarfallShard";

    public override void SetDefaults()
    {
        Projectile.width = 12;
        Projectile.height = 12;
        Projectile.hostile = true;
        Projectile.tileCollide = false;
        Projectile.timeLeft = 160;
        Projectile.penetrate = 1;
    }

    public override void AI()
    {
        Projectile.rotation += 0.14f;
        Projectile.velocity *= 1.01f;
        int bossIndex = Math.Clamp((int)Projectile.ai[0], 0, StarfallWorld.BossCount - 1);
        Lighting.AddLight(Projectile.Center, BossData.Color(bossIndex).ToVector3() * 0.45f);
    }
}
