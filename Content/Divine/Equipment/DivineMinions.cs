#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using global::StarfallThrone.Content.Systems;
using global::StarfallThrone.Content.Voyage.Weapons;

namespace StarfallThrone.Content.Divine.Equipment;

public abstract class DivineMinionBuff : ModBuff
{
    public abstract int Tier { get; }
    public override string Texture => DivineArsenal.Art(Tier * 4 + 3);
    public override void SetStaticDefaults() { Main.buffNoSave[Type] = true; Main.buffNoTimeDisplay[Type] = true; }
    public override void Update(Player p, ref int buffIndex)
    {
        if (p.ownedProjectileCounts[DivineArsenal.Minion(Tier)] > 0) p.buffTime[buffIndex] = 18000;
        else { p.DelBuff(buffIndex); buffIndex--; }
    }
}
public sealed class DivineMinionBuff0 : DivineMinionBuff { public override int Tier => 0; }
public sealed class DivineMinionBuff1 : DivineMinionBuff { public override int Tier => 1; }
public sealed class DivineMinionBuff2 : DivineMinionBuff { public override int Tier => 2; }

public abstract class DivineMinion : ModProjectile, IVoyageWeaponProjectile
{
    public int WeaponIndex => -1;
    public bool Secondary => false; // Native minion contact hits may charge equipment; their fired lances may not.
    public abstract int Tier { get; }
    public int Weapon => Tier * 4 + 3;
    public override string Texture => DivineCatalog.Root + "Minion" + Tier;
    public override void SetStaticDefaults()
    { Main.projPet[Type] = true; ProjectileID.Sets.MinionSacrificable[Type] = true; ProjectileID.Sets.MinionTargettingFeature[Type] = true; }
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = Tier == 0 ? 24 : 38; Projectile.friendly = true;
        Projectile.minion = true; Projectile.minionSlots = Tier == 0 ? 1 : 2; Projectile.DamageType = DamageClass.Summon;
        Projectile.penetrate = -1; Projectile.tileCollide = false; Projectile.ignoreWater = true; Projectile.timeLeft = 18000;
        Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = Tier == 0 ? 35 : 50;
    }
    public override bool MinionContactDamage() => true;
    public override bool? CanDamage()
    {
        int phase = (int)Projectile.ai[0];
        return Tier == 0 ? phase is >= 36 and < 54 ? null : false : Tier == 1 ? phase is >= 50 and < 74 ? null : false : phase is >= 32 and < 49 ? null : false;
    }
    public override bool? CanCutTiles() => false;
    public override bool? CanHitNPC(NPC target) => Collision.CanHitLine(Projectile.Center, 1, 1, target.position, target.width, target.height) ? null : false;
    private NPC? Target(Player p)
    {
        int preferred = p.MinionAttackTargetNPC;
        if (preferred >= 0 && preferred < Main.maxNPCs && Valid(Main.npc[preferred], p, 800)) return Main.npc[preferred];
        NPC? best = null; float distance = 650 * 650;
        foreach (NPC n in Main.ActiveNPCs)
        {
            float d = Vector2.DistanceSquared(Projectile.Center, n.Center);
            if (d < distance && Valid(n, p, 800)) { best = n; distance = d; }
        }
        return best;
    }
    private bool Valid(NPC n, Player p, float range) => n.CanBeChasedBy() && Vector2.DistanceSquared(n.Center, p.Center) < range * range && Collision.CanHitLine(Projectile.Center, 1, 1, n.Center, 1, 1);
    public override void AI()
    {
        if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) { Projectile.Kill(); return; }
        Player p = Main.player[Projectile.owner];
        if (!p.active || p.dead || !p.HasBuff(DivineArsenal.Buff(Tier))) { Projectile.Kill(); return; }
        Projectile.timeLeft = 2;
        if (Vector2.DistanceSquared(p.Center, Projectile.Center) > 1400 * 1400) { Projectile.Center = p.Center; Projectile.velocity = Vector2.Zero; Projectile.netUpdate = true; }
        bool owner = Projectile.owner == Main.myPlayer;
        NPC? target;
        if (owner)
        {
            target = Target(p); int next = target?.whoAmI ?? -1;
            if ((int)Projectile.ai[1] != next + 1)
            { Projectile.ai[1] = next + 1; Projectile.ai[2] = target?.type ?? 0; Projectile.ai[0] = 0; Projectile.netUpdate = true; }
        }
        int targetIndex = (int)Projectile.ai[1] - 1;
        target = targetIndex >= 0 && targetIndex < Main.maxNPCs && Main.npc[targetIndex].type == (int)Projectile.ai[2] && Valid(Main.npc[targetIndex], p, 900) ? Main.npc[targetIndex] : null;
        if (target == null)
        {
            Projectile.ai[0] = 0;
            Move(p.Center + new Vector2(-p.direction * (55 + Projectile.minionPos * 34), -60 - Projectile.minionPos % 2 * 25), 9);
            return;
        }
        int period = Tier == 0 ? 90 : Tier == 1 ? 120 : 144;
        Projectile.ai[0] = (Projectile.ai[0] + 1) % period; int phase = (int)Projectile.ai[0];
        if (Tier == 0)
        {
            if (phase < 36) Move(target.Center + new Vector2(-p.direction * 85, -35), 9);
            else if (phase == 36) { Projectile.velocity = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 17; Projectile.netUpdate = true; }
            else if (phase >= 54) Move(p.Center + new Vector2(-p.direction * 55, -55), 10);
        }
        else if (Tier == 1)
        {
            if (phase < 50) Move(target.Center + new Vector2(-p.direction * 95, -8), 7);
            else if (phase == 50) { Projectile.velocity = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 19; Projectile.netUpdate = true; }
            else if (phase >= 74) Move(target.Center + new Vector2(-p.direction * 145, -45), 10);
        }
        else
        {
            if (phase < 32) Move(target.Center + new Vector2(-p.direction * 115, -35), 12);
            else if (phase == 32) { Projectile.velocity = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 23; Projectile.netUpdate = true; }
            else if (phase is >= 49 and < 75) Move(target.Center + new Vector2(-p.direction * 150, -80), 13);
            else if (phase >= 75)
            {
                Projectile.velocity *= .8f;
                if (owner && phase is 84 or 102 or 120)
                    DivineArsenal.Launch(Projectile.GetSource_FromThis(), Projectile.owner, Weapon, DivineShotKind.MinionLance,
                        Projectile.Center, (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 18, (int)(Projectile.damage * .45f), 2);
            }
        }
        Projectile.rotation = Math.Clamp(Projectile.velocity.X * .018f, -.4f, .4f);
        if (!Main.dedServ) Lighting.AddLight(Projectile.Center, DivineEquipmentData.Color(Tier).ToVector3() * .3f);
    }
    private void Move(Vector2 at, float speed)
    {
        Vector2 d = at - Projectile.Center; Vector2 wanted = d.Length() < speed ? d : d.SafeNormalize(Vector2.UnitX) * speed;
        Projectile.velocity = (Projectile.velocity * 8 + wanted) / 9;
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = ModContent.Request<Texture2D>(Texture).Value; Vector2 at = Projectile.Center - Main.screenPosition;
        Color c = DivineEquipmentData.Color(Tier); int phase = (int)Projectile.ai[0];
        Main.spriteBatch.Draw(texture, at, null, Color.White, Projectile.rotation, texture.Size() / 2, Tier == 0 ? .55f : .85f, SpriteEffects.None, 0);
        CombatDrawing.Circle(Main.spriteBatch, at, Tier == 0 ? 17 : 27, c * .7f, 2);
        if (Tier == 1 && phase < 50)
        { // A visible guard pose, intentionally no player shield or projectile deletion.
            CombatDrawing.Line(Main.spriteBatch, at + new Vector2(21, -23), at + new Vector2(21, 23), c, 5);
        }
        int target = (int)Projectile.ai[1] - 1;
        if (Tier == 0 && phase < 36 && target >= 0 && target < Main.maxNPCs && Main.npc[target].active)
            CombatDrawing.Circle(Main.spriteBatch, Main.npc[target].Center - Main.screenPosition, 15 + phase * .12f, c * .5f, 2);
        if (Tier == 2 && phase >= 75)
            for (int i = 0; i < 3; i++) CombatDrawing.Circle(Main.spriteBatch, at + (i * MathHelper.TwoPi / 3 - MathHelper.PiOver2).ToRotationVector2() * 35, 6, c, 2);
        return false;
    }
}
public sealed class DivineMinion0 : DivineMinion { public override int Tier => 0; }
public sealed class DivineMinion1 : DivineMinion { public override int Tier => 1; }
public sealed class DivineMinion2 : DivineMinion { public override int Tier => 2; }
