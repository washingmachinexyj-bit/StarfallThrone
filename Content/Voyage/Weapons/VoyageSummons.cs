#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using global::StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Voyage.Weapons;

public abstract class VoyageWhip : ModProjectile, IVoyageWeaponProjectile
{
    public abstract int WeaponIndex { get; }
    public bool Secondary => false;
    public override string Texture => VoyageArsenal.Art(WeaponIndex);
    public override void SetStaticDefaults() => ProjectileID.Sets.IsAWhip[Type] = true;
    public override void SetDefaults()
    {
        Projectile.DefaultToWhip(); Projectile.WhipSettings.Segments = WeaponIndex == 7 ? 24 : 30;
        Projectile.WhipSettings.RangeMultiplier = WeaponIndex == 7 ? 1.25f : 1.55f;
    }
    public override bool PreAI()
    {
        Player p = Main.player[Projectile.owner];
        if (!p.active || p.dead || p.HeldItem.type != VoyageCatalog.Weapon(WeaponIndex)) { Projectile.Kill(); return false; }
        return true; // Native whip AI runs at Projectile.MaxUpdates, not once per game tick.
    }
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        target.GetGlobalNPC<VoyageTagDamage>().Tag(Projectile.owner, WeaponIndex == 23);
        target.AddBuff(WeaponIndex == 7 ? ModContent.BuffType<VoyageNeuralTag>() : ModContent.BuffType<VoyageRootTag>(), WeaponIndex == 7 ? 240 : 360);
        Main.player[Projectile.owner].MinionAttackTargetNPC = target.whoAmI;
        Projectile.damage = Math.Max(1, (int)(Projectile.damage * .86f));
    }
    public override bool PreDraw(ref Color lightColor)
    {
        var points = new List<Vector2>(); Projectile.FillWhipControlPoints(Projectile, points);
        Color color = VoyageArsenal.Color(WeaponIndex);
        for (int n = 1; n < points.Count; n++)
        {
            CombatDrawing.Line(Main.spriteBatch, points[n - 1] - Main.screenPosition, points[n] - Main.screenPosition, color, WeaponIndex == 7 ? 3 : 5);
            if (n % 3 == 0 || n == points.Count - 1)
                CombatDrawing.Circle(Main.spriteBatch, points[n] - Main.screenPosition, n == points.Count - 1 ? 9 : 4, Color.Lerp(color, Color.White, .35f), 2);
            if (WeaponIndex == 23 && n % 2 == 0)
            {
                Vector2 delta = (points[n] - points[n - 1]).SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2) * 7;
                CombatDrawing.Line(Main.spriteBatch, points[n] - Main.screenPosition - delta, points[n] - Main.screenPosition + delta, color, 3);
            }
        }
        return false;
    }
}
public sealed class VoyageNeuralWhip : VoyageWhip { public override int WeaponIndex => 7; }
public sealed class VoyageRootWhip : VoyageWhip { public override int WeaponIndex => 23; }
public sealed class VoyageNeuralTag : ModBuff
{
    public override string Texture => VoyageArsenal.Art(7);
    public override void SetStaticDefaults() { Main.debuff[Type] = true; Main.buffNoSave[Type] = true; BuffID.Sets.IsATagBuff[Type] = true; }
}
public sealed class VoyageRootTag : ModBuff
{
    public override string Texture => VoyageArsenal.Art(23);
    public override void SetStaticDefaults() { Main.debuff[Type] = true; Main.buffNoSave[Type] = true; BuffID.Sets.IsATagBuff[Type] = true; }
}
public sealed class VoyageTagDamage : GlobalNPC
{
    public override bool InstancePerEntity => true;
    private readonly ulong[] neuralUntil = new ulong[Main.maxPlayers];
    private readonly ulong[] rootUntil = new ulong[Main.maxPlayers];
    public void Tag(int owner, bool roots)
    {
        if (owner < 0 || owner >= Main.maxPlayers) return;
        if (roots) rootUntil[owner] = Main.GameUpdateCount + 360;
        else neuralUntil[owner] = Main.GameUpdateCount + 240;
    }
    public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
    {
        int owner = projectile.owner;
        if (owner < 0 || owner >= Main.maxPlayers || projectile.npcProj || projectile.trap || !(projectile.minion || projectile.sentry || ProjectileID.Sets.MinionShot[projectile.type])) return;
        float bonus = rootUntil[owner] > Main.GameUpdateCount ? .10f : neuralUntil[owner] > Main.GameUpdateCount ? .06f : 0;
        if (bonus > 0) modifiers.SourceDamage += bonus * ProjectileID.Sets.SummonTagDamageMultiplier[projectile.type];
    }
}

public abstract class VoyageAllyBuff : ModBuff
{
    public abstract int WeaponIndex { get; }
    public override string Texture => VoyageArsenal.Art(WeaponIndex);
    public override void SetStaticDefaults() { Main.buffNoSave[Type] = true; Main.buffNoTimeDisplay[Type] = true; }
    public override void Update(Player p, ref int buffIndex)
    {
        if (p.ownedProjectileCounts[VoyageArsenal.AllyType(WeaponIndex)] > 0) p.buffTime[buffIndex] = 18000;
        else { p.DelBuff(buffIndex); buffIndex--; }
    }
}
public sealed class VoyageObserverBuff : VoyageAllyBuff { public override int WeaponIndex => 3; }
public sealed class VoyageCrystalBuff : VoyageAllyBuff { public override int WeaponIndex => 15; }
public sealed class VoyageExecutorBuff : VoyageAllyBuff { public override int WeaponIndex => 19; }
public sealed class VoyageLeviathanBuff : VoyageAllyBuff { public override int WeaponIndex => 27; }
public sealed class VoyageArkBuff : VoyageAllyBuff { public override int WeaponIndex => 35; }

public abstract class VoyageAlly : ModProjectile, IVoyageWeaponProjectile
{
    public abstract int WeaponIndex { get; }
    public bool Secondary => true; // Their attacks must not recharge cast-triggered equipment effects.
    private bool Sentry => VoyageArsenal.Sentry(WeaponIndex);
    public override string Texture => VoyageArsenal.Art(WeaponIndex);
    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.MinionTargettingFeature[Type] = true; ProjectileID.Sets.MinionSacrificable[Type] = !Sentry;
        Main.projPet[Type] = !Sentry;
    }
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = WeaponIndex is 19 or 27 or 35 ? 44 : 28;
        Projectile.friendly = true; Projectile.ignoreWater = true; Projectile.tileCollide = false; Projectile.penetrate = -1;
        Projectile.minion = !Sentry; Projectile.sentry = Sentry; Projectile.minionSlots = Sentry ? 0 : VoyageArsenal.Slots(WeaponIndex);
        Projectile.DamageType = DamageClass.Summon; Projectile.timeLeft = Sentry ? 7200 : 18000;
        Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 24;
    }
    public override bool MinionContactDamage() => WeaponIndex is 19 or 27;
    public override bool? CanDamage() => WeaponIndex is 19 or 27 && Projectile.ai[1] > 0 ? null : false;
    public override bool? CanCutTiles() => false;
    public override bool? CanHitNPC(NPC target) => Collision.CanHitLine(Projectile.Center, 1, 1, target.position, target.width, target.height) ? null : false;
    private void Fire(VoyageShotKind kind, float damage, Vector2 at, Vector2 velocity, int target = -1, Vector2? focus = null)
        => VoyageArsenal.Launch(Projectile.GetSource_FromThis(), Projectile.owner, WeaponIndex, kind, at, velocity,
            (int)(Projectile.damage * damage), Projectile.knockBack, 100, focus: focus, target: target, parent: Projectile);
    private float PortalReach(Vector2 aim, NPC? target)
        => Math.Min(Math.Max(0, VoyageShot.Clip(Projectile.Center, aim, 180) - 15),
            target == null ? 165 : Projectile.Distance(target.Center) * .5f);
    public override void AI()
    {
        if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) { Projectile.Kill(); return; }
        Player p = Main.player[Projectile.owner];
        if (!p.active || p.dead) { Projectile.Kill(); return; }
        if (!Sentry)
        {
            if (!p.HasBuff(VoyageArsenal.Buff(WeaponIndex))) { Projectile.Kill(); return; }
            Projectile.timeLeft = 2;
        }
        Projectile.ai[0] = (Projectile.ai[0] + 1) % 3600;
        bool owner = Main.myPlayer == Projectile.owner;
        NPC? target = VoyageShot.FindTarget(Projectile.Center, Sentry ? 950 : 850, p.MinionAttackTargetNPC);
        if (target == null) Projectile.ai[1] = 0;
        Vector2 desired = p.Center + new Vector2(-p.direction * (65 + Projectile.minionPos * 42), -75 - Projectile.minionPos % 2 * 26);
        if (target != null && !Sentry) desired = target.Center + new Vector2(-p.direction * (WeaponIndex == 27 ? 175 : 160), WeaponIndex == 19 ? -25 : -95);
        if (!Sentry)
        {
            if (Projectile.Distance(p.Center) > 1800 && owner) { Projectile.Center = p.Center - new Vector2(0, 70); Projectile.velocity = Vector2.Zero; Projectile.netUpdate = true; }
            if (Projectile.ai[1] > 0) Projectile.ai[1]--;
            else Projectile.velocity = Vector2.Lerp(Projectile.velocity, (desired - Projectile.Center).SafeNormalize(Vector2.Zero) * Math.Min(15, Projectile.Distance(desired) / 8), .14f);
        }
        else Projectile.velocity = Vector2.Zero;
        if (target == null) return;
        int tick = (int)Projectile.ai[0];
        Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
        switch (WeaponIndex)
        {
            case 3:
                if (tick % 84 is 34 or 42 or 50 && owner) Fire(VoyageShotKind.Pulse, .6f, Projectile.Center, dir * 23, target.whoAmI);
                break;
            case 11:
                if (tick % 42 == 0 && owner)
                    Fire(VoyageShotKind.Anchor, .8f, Projectile.Center + new Vector2(tick % 84 == 0 ? -25 : 25, 0), dir * 12, target.whoAmI);
                break;
            case 15:
                if (tick % 65 == 0 && owner) Fire(VoyageShotKind.CrystalRing, 1, Projectile.Center, dir * 16, target.whoAmI);
                break;
            case 19:
                if (tick % 80 == 20 && Projectile.Distance(target.Center) < 210 && owner)
                { Projectile.velocity = dir * 15; Projectile.ai[1] = 18; Projectile.netUpdate = true; }
                else if (tick % 80 is 20 or 37 && Projectile.ai[1] <= 0 && owner) Fire(VoyageShotKind.Pulse, .8f, Projectile.Center, dir * 19, target.whoAmI);
                break;
            case 27:
                if (tick % 88 == 35 && owner) { Projectile.velocity = dir * 23; Projectile.ai[1] = 23; Projectile.netUpdate = true; }
                if (Projectile.ai[1] > 0 && tick % 7 == 0 && owner) Fire(VoyageShotKind.Tail, .2f, Projectile.Center, Vector2.Zero);
                break;
            case 31:
                if (tick % 54 is 18 or 27 or 36 && owner)
                {
                    Vector2 exit = Projectile.Center + dir * PortalReach(dir, target);
                    if (!Collision.SolidCollision(exit - new Vector2(8), 16, 16)
                        && Collision.CanHitLine(Projectile.Center, 1, 1, exit, 1, 1))
                        Fire(VoyageShotKind.Shard, .62f, exit, dir * 20, target.whoAmI);
                }
                break;
            case 35:
                int phase = tick % 180;
                if (phase is 18 or 34 or 50 && owner)
                {
                    Vector2 launch = Projectile.Center + dir.RotatedBy(MathHelper.PiOver2) * (phase - 34) * 1.5f;
                    Fire(VoyageShotKind.Escort, .46f, launch, dir * 15, target.whoAmI);
                }
                if (phase == 100 && owner) Fire(VoyageShotKind.Beam, 1.2f, Projectile.Center, dir, target.whoAmI);
                break;
        }
        Projectile.rotation = WeaponIndex == 27 ? Projectile.velocity.ToRotation() : Projectile.velocity.X * .018f;
        if (owner && tick % 30 == 0) { Projectile.ai[2] = target.whoAmI + 1; Projectile.netUpdate = true; }
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
        Vector2 at = Projectile.Center - Main.screenPosition;
        Color color = VoyageArsenal.Color(WeaponIndex);
        int targetId = (int)Projectile.ai[2] - 1;
        NPC? target = targetId >= 0 && targetId < Main.maxNPCs && Main.npc[targetId].active ? Main.npc[targetId] : null;
        Vector2 aim = target == null ? Vector2.UnitX : (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
        if (WeaponIndex == 3)
        {
            CombatDrawing.Circle(Main.spriteBatch, at, 25, color, 3, Projectile.ai[0] * .02f, .3f);
            if (target != null && Projectile.ai[0] % 84 < 34) CombatDrawing.Line(Main.spriteBatch, at, target.Center - Main.screenPosition, color * .2f, 1);
        }
        if (WeaponIndex == 15) CombatDrawing.Circle(Main.spriteBatch, at, 31, color, 4, Projectile.ai[0] * .04f, .4f);
        if (WeaponIndex is 11 or 19)
            for (int n = 0; n < (WeaponIndex == 19 ? 4 : 2); n++)
            {
                Vector2 end = at + (n * MathHelper.PiOver2 + Projectile.ai[0] * .012f).ToRotationVector2() * 42;
                CombatDrawing.Line(Main.spriteBatch, at, end, color * .8f, 5); CombatDrawing.Circle(Main.spriteBatch, end, 8, color, 3);
            }
        if (WeaponIndex == 31)
        {
            float reach = PortalReach(aim, target);
            CombatDrawing.Circle(Main.spriteBatch, at, 27, color, 3);
            CombatDrawing.Circle(Main.spriteBatch, at + aim * reach, 30, color, 3);
            CombatDrawing.Line(Main.spriteBatch, at, at + aim * reach, color * .15f, 1);
            float t = Projectile.ai[0] % 9 / 9f;
            if (Projectile.ai[0] % 54 is >= 9 and <= 36) CombatDrawing.Circle(Main.spriteBatch, at - aim * (1 - t) * 30, 4, color, 2);
        }
        if (WeaponIndex == 35)
        {
            CombatDrawing.Circle(Main.spriteBatch, at, 44, color * .6f, 2, Projectile.ai[0] * .01f, .6f);
            if (Projectile.ai[0] % 180 >= 125)
                for (int n = 0; n < 3; n++) CombatDrawing.Circle(Main.spriteBatch, at + new Vector2((n - 1) * 25, -30), 5, color, 3);
        }
        Main.EntitySpriteDraw(texture, at, null, Color.Lerp(lightColor, Color.White, .5f), Projectile.rotation,
            texture.Size() / 2, (WeaponIndex is 19 or 27 or 35 ? 65f : 42f) / Math.Max(texture.Width, texture.Height), SpriteEffects.None);
        return false;
    }
}
public sealed class VoyageObserver : VoyageAlly { public override int WeaponIndex => 3; }
public sealed class VoyageAnchorSentry : VoyageAlly { public override int WeaponIndex => 11; }
public sealed class VoyageCrystalMinion : VoyageAlly { public override int WeaponIndex => 15; }
public sealed class VoyageExecutor : VoyageAlly { public override int WeaponIndex => 19; }
public sealed class VoyageLeviathan : VoyageAlly { public override int WeaponIndex => 27; }
public sealed class VoyagePortalSentry : VoyageAlly { public override int WeaponIndex => 31; }
public sealed class VoyageArkMinion : VoyageAlly { public override int WeaponIndex => 35; }
