using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Fable.Trials;

public sealed class FourTrialHazard : ModProjectile
{
    public override string Texture => FourTrialCatalog.Root + "TrialShard";

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 8;
        Projectile.hostile = true;
        Projectile.friendly = false;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 120;
    }

    public override void AI()
    {
        int index = Math.Clamp((int)Projectile.ai[0], 0, FourTrialCatalog.Count - 1);
        Projectile.rotation += .08f + index * .02f;
        Projectile.velocity *= .994f;
        if (index == 3) Projectile.velocity.Y += .012f;
        if (!Main.dedServ && Main.rand.NextBool(5))
        {
            int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.GemDiamond,
                0, 0, 120, FourTrialCatalog.Colors[index], .45f);
            Main.dust[dust].noGravity = true;
        }
    }
}
