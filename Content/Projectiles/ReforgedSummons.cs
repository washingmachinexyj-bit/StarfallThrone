using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.Items;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Projectiles;

public sealed class ReforgedTongueWhip : ModProjectile
{
    public override string Texture => ReforgedWeapons.Art(9);
    public override void SetStaticDefaults() => ProjectileID.Sets.IsAWhip[Type] = true;
    public override void SetDefaults()
    {
        Projectile.DefaultToWhip(); Projectile.WhipSettings.Segments = 18; Projectile.WhipSettings.RangeMultiplier = .8f;
    }
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        target.AddBuff(ModContent.BuffType<TongueTag>(), 240);
        Main.player[Projectile.owner].MinionAttackTargetNPC = target.whoAmI;
        Projectile.damage = (int)(Projectile.damage * .8f);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        var points = new List<Vector2>(); Projectile.FillWhipControlPoints(Projectile, points);
        for (int i = 1; i < points.Count; i++) CombatDrawing.Line(Main.spriteBatch, points[i - 1] - Main.screenPosition, points[i] - Main.screenPosition, new Color(207, 128, 158), i == points.Count - 1 ? 7 : 4);
        return false;
    }
}
public sealed class TongueTag : ModBuff
{
    public override string Texture => ReforgedWeapons.Art(9);
    public override void SetStaticDefaults() { Main.debuff[Type] = true; Main.buffNoSave[Type] = true; BuffID.Sets.IsATagBuff[Type] = true; }
}
public sealed class TongueTagDamage : GlobalNPC
{
    public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
    {
        bool summoned = projectile.minion || projectile.sentry || ProjectileID.Sets.MinionShot[projectile.type] || projectile.ModProjectile is ReforgedWeaponShot { WeaponId: 32 or 36 };
        if (summoned && !projectile.npcProj && !projectile.trap && npc.HasBuff<TongueTag>())
            modifiers.FlatBonusDamage += 3 * ProjectileID.Sets.SummonTagDamageMultiplier[projectile.type];
    }
}
public abstract class ReforgedMinionBuff : ModBuff
{
    protected abstract int AllyType { get; }
    public override void SetStaticDefaults() { Main.buffNoSave[Type] = true; Main.buffNoTimeDisplay[Type] = true; }
    public override void Update(Player p, ref int index)
    {
        if (p.ownedProjectileCounts[AllyType] > 0) p.buffTime[index] = 18000;
        else { p.DelBuff(index); index--; }
    }
}
public sealed class WaxBeeBuff : ReforgedMinionBuff
{
    public override string Texture => ReforgedWeapons.Art(11);
    protected override int AllyType => ModContent.ProjectileType<WaxBeeMinion>();
}
public sealed class ThornBloomBuff : ReforgedMinionBuff
{
    public override string Texture => ReforgedWeapons.Art(32);
    protected override int AllyType => ModContent.ProjectileType<ThornBloomMinion>();
}

public abstract class ReforgedAlly : ModProjectile
{
    protected abstract int WeaponId { get; }
    protected bool Sentry => WeaponId == 36;
    public override string Texture => WeaponId == 11 ? "StarfallThrone/Content/Assets/BossHeads/Primordial/WaxenArbiter_Head_Boss" : ReforgedWeapons.Art(WeaponId);
    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.MinionTargettingFeature[Type] = true;
        ProjectileID.Sets.MinionSacrificable[Type] = !Sentry;
        Main.projPet[Type] = !Sentry;
    }
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 28; Projectile.tileCollide = false; Projectile.friendly = true;
        Projectile.minion = !Sentry; Projectile.sentry = Sentry; Projectile.minionSlots = Sentry ? 0 : 1;
        Projectile.DamageType = DamageClass.Summon; Projectile.penetrate = -1;
        Projectile.timeLeft = Sentry ? 7200 : 18000; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 25;
    }
    public override bool MinionContactDamage() => WeaponId == 11;
    public override bool? CanDamage() => WeaponId == 11 && Projectile.ai[1] > 0 ? null : false;
    public override bool? CanCutTiles() => false;
    public override void AI()
    {
        Player p = Main.player[Projectile.owner];
        if (!p.active || p.dead) { Projectile.Kill(); return; }
        if (!Sentry)
        {
            int buff = WeaponId == 11 ? ModContent.BuffType<WaxBeeBuff>() : ModContent.BuffType<ThornBloomBuff>();
            if (!p.HasBuff(buff)) { Projectile.Kill(); return; }
            Projectile.timeLeft = 2;
        }
        Projectile.ai[0]++;
        NPC enemy = ReforgedWeaponShot.FindTarget(Projectile.Center, WeaponId == 11 ? 550 : 850, p.MinionAttackTargetNPC);
        if (enemy == null) Projectile.ai[1] = 0;
        if (!Sentry)
        {
            Vector2 desired = p.Center + new Vector2(-p.direction * (50 + Projectile.minionPos * 38), -65);
            if (Projectile.Distance(p.Center) > 1600 && Main.myPlayer == p.whoAmI) { Projectile.Center = desired; Projectile.netUpdate = true; }
            if (WeaponId == 11 && enemy != null)
            {
                if (Projectile.ai[1] <= 0 && Projectile.ai[0] % 60 == 0)
                { Projectile.velocity = (enemy.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 13; Projectile.ai[1] = 20; Projectile.netUpdate = true; }
                if (Projectile.ai[1] > 0) Projectile.ai[1]--;
                else desired = enemy.Center + new Vector2(-p.direction * 90, -55);
            }
            if (Projectile.ai[1] <= 0)
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, (desired - Projectile.Center).SafeNormalize(Vector2.Zero) * Math.Min(10, Projectile.Distance(desired) / 10), .12f);
        }
        else Projectile.velocity = Vector2.Zero;
        if (enemy != null && WeaponId != 11 && (int)Projectile.ai[0] % (Sentry ? 65 : 52) == 0 && Projectile.owner == Main.myPlayer)
        {
            Vector2 direction = (enemy.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            if (Sentry)
                for (int i = 0; i < 4; i++) ReforgedWeapons.Launch(Projectile.GetSource_FromThis(), p.whoAmI, WeaponId, WeaponShape.Thorn,
                    Projectile.Center + (MathHelper.PiOver2 * i).ToRotationVector2() * 36, direction * 10, (int)(Projectile.damage * .35f), Projectile.knockBack, 100);
            else for (int i = -1; i <= 1; i++) ReforgedWeapons.Launch(Projectile.GetSource_FromThis(), p.whoAmI, WeaponId, WeaponShape.Thorn,
                Projectile.Center, direction.RotatedBy(i * .2f) * 8, (int)(Projectile.damage * .45f), Projectile.knockBack, 100);
        }
        Projectile.rotation = Sentry ? Projectile.ai[0] * .01f : Projectile.velocity.X * .025f;
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = TextureAssets.Projectile[Type].Value;
        Vector2 at = Projectile.Center - Main.screenPosition;
        if (Sentry) CombatDrawing.Circle(Main.spriteBatch, at, 40, new Color(189, 127, 250) * .75f, 3);
        Main.EntitySpriteDraw(texture, at, null, lightColor, Projectile.rotation, texture.Size() / 2, (Sentry ? 45f : 34f) / texture.Width, SpriteEffects.None);
        return false;
    }
}
public sealed class WaxBeeMinion : ReforgedAlly { protected override int WeaponId => 11; }
public sealed class ThornBloomMinion : ReforgedAlly { protected override int WeaponId => 32; }
public sealed class AstralSentry : ReforgedAlly { protected override int WeaponId => 36; }
