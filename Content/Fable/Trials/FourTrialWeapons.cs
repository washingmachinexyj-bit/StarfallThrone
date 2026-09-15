using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.Fable;
using StarfallThrone.Content.Fable.Bosses;

namespace StarfallThrone.Content.Fable.Trials;

public abstract class FourTrialProjectileBase : ModProjectile
{
    public abstract int Index { get; }
    public override string Texture => FourTrialCatalog.Root + "TrialWeapon" + Index;

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 8;
        Projectile.friendly = true;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 42 + Index * 8;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
    }

    public override bool? CanHitNPC(NPC target)
    {
        Player owner = Main.player[Projectile.owner];
        if (owner.GetModPlayer<FablePlayer>().InTrial && target.ModNPC is not FableBossNPC)
            return false;
        return null;
    }

    public override void AI()
    {
        Projectile.rotation = Projectile.velocity.ToRotation();
        if (Index == 1) Projectile.velocity *= .992f;
        else Projectile.velocity.Y += .025f;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = TextureAssets.Projectile[Type].Value;
        Color tint = FourTrialCatalog.Colors[Index];
        Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, null, tint,
            Projectile.rotation, texture.Size() / 2, .55f, SpriteEffects.None, 0);
        return false;
    }
}

public sealed class FourTrialSilverNeedle : FourTrialProjectileBase
{
    public override int Index => 1;
}

public sealed class FourTrialGoldBell : FourTrialProjectileBase
{
    public override int Index => 2;
    public override void SetDefaults()
    {
        base.SetDefaults();
        Projectile.DamageType = DamageClass.Magic;
        Projectile.tileCollide = false;
        Projectile.light = .15f;
    }
}

public sealed class FourTrialMinionBuff : ModBuff
{
    public override string Texture => FourTrialCatalog.Root + "TrialWeapon3";
    public override void SetStaticDefaults()
    {
        Main.buffNoSave[Type] = true;
        Main.buffNoTimeDisplay[Type] = true;
    }

    public override void Update(Player player, ref int buffIndex)
    {
        if (player.ownedProjectileCounts[ModContent.ProjectileType<FourTrialDiamondMinion>()] > 0)
            player.buffTime[buffIndex] = 18000;
        else
        {
            player.DelBuff(buffIndex);
            buffIndex--;
        }
    }
}

public sealed class FourTrialDiamondMinion : ModProjectile
{
    public override string Texture => FourTrialCatalog.Root + "TrialWeapon3";

    public override void SetStaticDefaults()
    {
        Main.projPet[Type] = true;
        ProjectileID.Sets.MinionSacrificable[Type] = true;
        ProjectileID.Sets.MinionTargettingFeature[Type] = true;
    }

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 16;
        Projectile.minion = true;
        Projectile.minionSlots = 1;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Summon;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 18000;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 90;
    }

    public override bool MinionContactDamage() => true;

    public override bool? CanHitNPC(NPC target)
    {
        Player owner = Main.player[Projectile.owner];
        if (owner.GetModPlayer<FablePlayer>().InTrial && target.ModNPC is not FableBossNPC)
            return false;
        return null;
    }

    public override void AI()
    {
        Player player = Main.player[Projectile.owner];
        if (!player.active || player.dead || !player.HasBuff<FourTrialMinionBuff>())
        {
            Projectile.Kill();
            return;
        }
        Projectile.timeLeft = 2;
        Projectile.localAI[0]++;
        NPC target = null;
        float nearest = 240f;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!npc.CanBeChasedBy()) continue;
            if (player.GetModPlayer<FablePlayer>().InTrial && npc.ModNPC is not FableBossNPC) continue;
            if (!Collision.CanHitLine(Projectile.position, Projectile.width, Projectile.height, npc.position, npc.width, npc.height)) continue;
            float distance = Vector2.Distance(Projectile.Center, npc.Center);
            if (distance < nearest) { nearest = distance; target = npc; }
        }
        Vector2 home = player.Center + new Vector2(-player.direction * 28f, -42f);
        Vector2 destination = target?.Center ?? home;
        if (Vector2.Distance(Projectile.Center, player.Center) > 600f) Projectile.Center = home;
        Projectile.velocity = Vector2.Lerp(Projectile.velocity, (destination - Projectile.Center) * .07f, .12f);
        if (Projectile.velocity.Length() > 3f) Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY) * 3f;
        Projectile.rotation += .04f;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = TextureAssets.Projectile[Type].Value;
        Vector2 at = Projectile.Center - Main.screenPosition;
        Color tint = FourTrialCatalog.Colors[3];
        Main.spriteBatch.Draw(texture, at, null, tint * .3f, Projectile.rotation, texture.Size() / 2, 1.1f, SpriteEffects.None, 0);
        Main.spriteBatch.Draw(texture, at, null, Color.White, Projectile.rotation, texture.Size() / 2, .7f, SpriteEffects.None, 0);
        return false;
    }
}
