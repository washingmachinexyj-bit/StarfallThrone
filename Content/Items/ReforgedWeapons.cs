using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.Projectiles;

namespace StarfallThrone.Content.Items;

public static class ReforgedWeapons
{
    public const int Count = 39;
    public static readonly string[] Names = {
        "ResinShortbow","ResinCane","CopperveinRepeater","MinebreakerDrill","BellstingSpear","SandglassStaff","FrostfangBlade","FrostHowl","MireSprayer","TongueWhip",
        "WaxenRepeater","WaxVerdict","RootlashHook","RotrootStaff","GravebellFlail","KeybearerCrossbow","MeteorMawCannon","StarEaterStaff","DawnstarEdge","FirstlightCodex","DawnpiercerBow",
        "ImperialScepter","PhaseBreaker","DevourerLance","MindCircuit","RoyalStinger","JudgmentCodex","EternalTendril","DynastyPrism","TwinRayCannon","PrimeSaw","StarChainDrill","FlowerThornCrown","TempleRiftCannon","AbyssalHarpoon","CoronaPrismWeapon","PontiffStarbook","EndMoonWeapon","Thronebreaker" };
    public static int Type(int id) => ModContent.Find<ModItem>("StarfallThrone", Names[id]).Type;
    public static string Art(int id) => "StarfallThrone/Content/Assets/Items/" + (id < 21 ? "PrimordialWeapon" + id : id == 38 ? "WeaponBonus0" : "Weapon" + (id - 21));
    public static DamageClass Class(int id) => id == 9 ? DamageClass.SummonMeleeSpeed : id is 11 or 32 or 36 ? DamageClass.Summon :
        id is 0 or 2 or 10 or 15 or 16 or 20 or 22 or 25 or 29 or 33 or 34 or 37 ? DamageClass.Ranged :
        id is 1 or 5 or 7 or 8 or 13 or 17 or 19 or 24 or 26 or 28 or 35 ? DamageClass.Magic : DamageClass.Melee;
    public static bool Held(int id) => id is 3 or 4 or 6 or 12 or 18 or 21 or 23 or 27 or 31 or 38;
    public static bool Single(int id) => Held(id) || id is 14 or 17 or 30 or 34;
    public static void Configure(Item item, int id)
    {
        item.DamageType = Class(id); item.shoot = ModContent.ProjectileType<ReforgedWeaponShot>();
        item.noUseGraphic = Held(id) || id is 14 or 30 or 34;
        item.mana = item.DamageType == DamageClass.Magic ? (id < 21 ? Math.Max(6, item.mana) : 12 + (id - 21)) : 0;
        item.useAmmo = id is 0 or 2 or 10 or 15 or 20 or 25 or 37 ? AmmoID.Arrow : id is 22 or 29 or 33 ? AmmoID.Bullet : AmmoID.None;
        if (id >= 21) item.useTime = item.useAnimation = id is 22 or 29 or 33 ? 38 : id is 24 or 26 or 28 or 35 or 37 ? 32 : 28;
        if (id == 38) item.useTime = item.useAnimation = 30;
        if (id == 9)
        {
            item.DefaultToWhip(ModContent.ProjectileType<ReforgedTongueWhip>(), 20, 2.5f, 4f);
            item.useTime = item.useAnimation = 30;
        }
        if (id is 11 or 32 or 36)
        {
            item.mana = id == 11 ? 10 : 20; item.useTime = item.useAnimation = 36; item.UseSound = SoundID.Item44;
            item.shoot = id == 11 ? ModContent.ProjectileType<WaxBeeMinion>() : id == 32 ? ModContent.ProjectileType<ThornBloomMinion>() : ModContent.ProjectileType<AstralSentry>();
            if (id != 36) item.buffType = id == 11 ? ModContent.BuffType<WaxBeeBuff>() : ModContent.BuffType<ThornBloomBuff>();
            else item.sentry = true;
        }
        else item.UseSound = id is 3 or 31 ? SoundID.Item23 : id == 9 || item.DamageType == DamageClass.Melee ? SoundID.Item1 : item.useAmmo == AmmoID.Arrow ? SoundID.Item5 : item.DamageType == DamageClass.Magic ? SoundID.Item20 : SoundID.Item11;
    }
    public static bool CanUse(Player p, int id)
    {
        if (!Single(id)) return true;
        foreach (Projectile shot in Main.ActiveProjectiles)
            if (shot.owner == p.whoAmI && shot.ModProjectile is ReforgedWeaponShot && (int)shot.ai[0] == id && shot.ai[2] < 100) return false;
        return true;
    }
    public static int Launch(IEntitySource source, int owner, int id, WeaponShape shape, Vector2 at, Vector2 velocity, int damage, float knockback, int variant = 0, int delay = 0)
    {
        if (Main.myPlayer != owner) return -1;
        int count = 0;
        foreach (Projectile q in Main.ActiveProjectiles) if (q.owner == owner && q.ModProjectile is ReforgedWeaponShot) count++;
        if (count >= 120) return -1;
        int type = id is 32 or 36 ? ModContent.ProjectileType<ReforgedMinionShot>() : ModContent.ProjectileType<ReforgedWeaponShot>();
        int slot = Projectile.NewProjectile(source, at, velocity, type, Math.Max(1, damage), knockback, owner, id, (int)shape, variant);
        if (slot < Main.maxProjectiles)
        {
            var shot = (ReforgedWeaponShot)Main.projectile[slot].ModProjectile;
            shot.Delay += delay; shot.Projectile.timeLeft += delay; shot.Projectile.netUpdate = true;
        }
        return slot;
    }
    public static bool Shoot(Item item, int id, Player p, EntitySource_ItemUse_WithAmmo source, Vector2 at, Vector2 velocity, int damage, float knockback)
    {
        if (p.whoAmI != Main.myPlayer) return false;
        if (id == 9) return true;
        if (id is 11 or 32 or 36)
        {
            Vector2 point = Main.MouseWorld;
            if (Vector2.Distance(point, p.Center) > 650) point = p.Center + (point - p.Center).SafeNormalize(Vector2.UnitX) * 650;
            if (!Collision.CanHitLine(p.Center, 1, 1, point, 1, 1) || Collision.SolidCollision(point - new Vector2(16), 32, 32)) point = p.Center - new Vector2(0, 65);
            if (id != 36) p.AddBuff(item.buffType, 2);
            int slot = Projectile.NewProjectile(source, point, Vector2.Zero, item.shoot, damage, knockback, p.whoAmI);
            if (slot < Main.maxProjectiles) Main.projectile[slot].originalDamage = item.damage;
            if (id == 36) p.UpdateMaxTurrets();
            return false;
        }
        int combo = p.GetModPlayer<WeaponCadencePlayer>().Next(id);
        Vector2 dir = velocity.SafeNormalize(Vector2.UnitX);
        void Fire(WeaponShape shape, float factor = 1, Vector2? position = null, Vector2? speed = null, int variant = 0, int delay = 0)
            => Launch(source, p.whoAmI, id, shape, position ?? at, speed ?? velocity, (int)(damage * factor), knockback, variant, delay);
        switch (id)
        {
            case 0: Fire(WeaponShape.Arrow); break;
            case 1: Fire(WeaponShape.Sap, speed: dir * 6 + new Vector2(0, -2)); break;
            case 2: for (int n = 0; n < 3; n++) Fire(WeaponShape.Arrow, .55f, delay: n * 6); break;
            case 3: Fire(WeaponShape.Drill, .6f); break;
            case 4: Fire(WeaponShape.Spear); break;
            case 5: Fire(WeaponShape.Sand, speed: dir * 6); break;
            case 6: Fire(WeaponShape.Swing, variant: combo % 2); if (combo % 3 == 0) Fire(WeaponShape.Arrow, .5f, variant: 100); break;
            case 7: Fire(WeaponShape.Ring, .85f); break;
            case 8: for (int n = -1; n <= 1; n++) Fire(WeaponShape.Mist, .35f, speed: dir.RotatedBy(n * .18f) * 4); break;
            case 10: Fire(WeaponShape.Arrow); break;
            case 12: for (int n = -1; n <= 1; n++) Fire(WeaponShape.Spear, .48f, speed: dir.RotatedBy(n * .35f) * 10); break;
            case 13: Fire(WeaponShape.Root, speed: dir * 7); break;
            case 14: Fire(WeaponShape.Flail); break;
            case 15: Fire(WeaponShape.Seeking); break;
            case 16: Fire(WeaponShape.Meteor, speed: dir * 9); break;
            case 17: Fire(WeaponShape.Returning); break;
            case 18: Fire(WeaponShape.Swing, variant: combo % 2); if (combo % 3 == 0) Fire(WeaponShape.Seeking, .6f, variant: 100); break;
            case 19: Fire(WeaponShape.Ring); break;
            case 20: Fire(WeaponShape.Arrow); break;
            case 21: Fire(WeaponShape.Swing, variant: combo % 2); if (combo % 2 == 0) Fire(WeaponShape.Sap, .45f, speed: dir * 7, variant: 100); break;
            case 22: Fire(WeaponShape.Beam); break;
            case 23: Fire(WeaponShape.Spear); break;
            case 24:
                Vector2 focal = p.Center + dir * Math.Min(420, Vector2.Distance(Main.MouseWorld, p.Center));
                for (int n = 0; n < 3; n++) Fire(WeaponShape.Orbit, .5f, focal, (MathHelper.TwoPi * n / 3).ToRotationVector2() * 3, n);
                break;
            case 25: for (int n = -1; n <= 1; n++) Fire(WeaponShape.Arrow, .48f, speed: velocity.RotatedBy(n * .12f)); break;
            case 26:
                Vector2 mark = p.Center + dir * Math.Min(600, Vector2.Distance(Main.MouseWorld, p.Center));
                if (!Collision.CanHitLine(p.Center, 1, 1, mark, 1, 1)) mark = at + dir * 100;
                for (int n = -1; n <= 1; n++) Fire(WeaponShape.Blade, .55f, mark + new Vector2(n * 60, -190), Vector2.UnitY * 12, delay: 14 + (n + 1) * 6);
                break;
            case 27: Fire(WeaponShape.Swing, variant: combo % 2); break;
            case 28: Fire(WeaponShape.Prism, .5f, speed: dir * 7); break;
            case 29: for (int n = -1; n <= 1; n += 2) Fire(WeaponShape.Beam, .55f, at + dir.RotatedBy(MathHelper.PiOver2) * n * 16); break;
            case 30: Fire(WeaponShape.Flail); break;
            case 31: Fire(WeaponShape.Drill, .55f); break;
            case 33: Fire(WeaponShape.Meteor, speed: dir * 13); break;
            case 34: Fire(WeaponShape.Returning); break;
            case 35: for (int n = -2; n <= 2; n++) Fire(WeaponShape.Seeking, .34f, speed: velocity.RotatedBy(n * .19f)); break;
            case 37: Fire(WeaponShape.MoonArrow); break;
            case 38: Fire(WeaponShape.Swing, variant: combo % 2); if (combo % 3 == 0) Fire(WeaponShape.Beam, .75f, variant: 100); break;
        }
        return false;
    }
}

public sealed class WeaponCadencePlayer : ModPlayer
{
    private readonly int[] cadence = new int[ReforgedWeapons.Count];
    public int Next(int id) { cadence[id] = cadence[id] % 6 + 1; return cadence[id]; }
    public override void UpdateDead() => Array.Clear(cadence);
}
