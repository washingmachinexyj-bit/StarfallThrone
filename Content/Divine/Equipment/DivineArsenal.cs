#nullable enable
using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Divine.Equipment;

public static class DivineArsenal
{
    public static readonly int[] Damage = {25,22,28,17,76,39,58,47,230,115,165,105};
    public static string Art(int id) => DivineCatalog.Root + "Weapon" + id;
    public static int Buff(int tier) => tier switch { 0 => ModContent.BuffType<DivineMinionBuff0>(), 1 => ModContent.BuffType<DivineMinionBuff1>(), _ => ModContent.BuffType<DivineMinionBuff2>() };
    public static int Minion(int tier) => tier switch { 0 => ModContent.ProjectileType<DivineMinion0>(), 1 => ModContent.ProjectileType<DivineMinion1>(), _ => ModContent.ProjectileType<DivineMinion2>() };
    public static Vector2 Aim(Player p, Vector2 requested, float range = 600)
    {
        if (!float.IsFinite(requested.X) || !float.IsFinite(requested.Y)) return p.Center;
        Vector2 d = requested - p.Center; float length = Math.Min(range, d.Length()); d = d.SafeNormalize(Vector2.UnitX);
        for (; length > 12; length -= 12)
        { Vector2 at = p.Center + d * length; if (Collision.CanHitLine(p.Center, 1, 1, at, 1, 1) && !Collision.SolidCollision(at - new Vector2(10), 20, 20)) return at; }
        return p.Center;
    }
    public static int Launch(IEntitySource source, int owner, int id, DivineShotKind kind, Vector2 at, Vector2 velocity,
        int damage, float knockback = 2, int variant = 0, Vector2? focus = null)
    {
        if (owner != Main.myPlayer || owner < 0 || owner >= Main.maxPlayers || id < 0 || id > 11 ||
            !float.IsFinite(at.X) || !float.IsFinite(at.Y) || !float.IsFinite(velocity.X) || !float.IsFinite(velocity.Y)) return -1;
        int count = 0; foreach (Projectile q in Main.ActiveProjectiles) if (q.owner == owner && q.ModProjectile is DivineShot) count++;
        if (count >= 80) return -1;
        int type = id % 4 == 3 ? ModContent.ProjectileType<DivineSummonShot>() : ModContent.ProjectileType<DivineShot>();
        int slot = Projectile.NewProjectile(source, at, velocity, type, Math.Max(1, damage), knockback, owner, id, (int)kind, variant);
        if (slot >= Main.maxProjectiles) return -1;
        var shot = (DivineShot)Main.projectile[slot].ModProjectile;
        shot.Focus = focus ?? at; shot.Projectile.DamageType = kind == DivineShotKind.DewProc ? DamageClass.Generic : DivineEquipmentData.Class(id % 4);
        shot.Projectile.netUpdate = true; return slot;
    }
    public static bool Single(Player p, int id)
    {
        foreach (Projectile q in Main.ActiveProjectiles)
            if (q.owner == p.whoAmI && q.ModProjectile is DivineShot s && s.Weapon == id && s.Kind is DivineShotKind.Swing or DivineShotKind.Charge) return false;
        return true;
    }
    public static bool Fire(DivineWeapon w, Player p, EntitySource_ItemUse_WithAmmo source, Vector2 at, Vector2 velocity, int damage, float knockback)
    {
        int id = w.Index; if (p.whoAmI != Main.myPlayer) return false;
        var s = p.GetModPlayer<DivineEquipmentPlayer>(); Vector2 d = velocity.SafeNormalize(new Vector2(p.direction, 0));
        void Shot(DivineShotKind kind, float scale = 1, Vector2? speed = null, int variant = 0, Vector2? focus = null)
            => Launch(source, p.whoAmI, id, kind, at, speed ?? velocity, (int)(damage * scale), knockback, variant, focus);
        if (id % 4 == 3)
        {
            p.AddBuff(w.Item.buffType, 2);
            int slot = Projectile.NewProjectile(source, Aim(p, Main.MouseWorld, 480), Vector2.Zero, w.Item.shoot, damage, knockback, p.whoAmI);
            if (slot < Main.maxProjectiles) Main.projectile[slot].originalDamage = w.Item.damage;
            return false;
        }
        switch (id)
        {
            case 0: Shot(DivineShotKind.Swing, speed: d, variant: s.MeleeCombo); break;
            case 1:
                Shot(DivineShotKind.Arrow, speed: d * 14, variant: s.ArrowReady ? 1 : 0); s.ArrowReady = false; break;
            case 2: Shot(DivineShotKind.Charge, speed: d); break;
            case 4:
                if (p.altFunctionUse == 2)
                {
                    if (s.Pressure < 100) break; s.Pressure = 0;
                    for (int i = -2; i <= 2; i++) Shot(DivineShotKind.HeatWave, .45f, d.RotatedBy(i * .15f) * 13);
                }
                else Shot(DivineShotKind.Swing, speed: d);
                break;
            case 5: Shot(DivineShotKind.Bullet, speed: d * 19, variant: s.GunCadence++ % 3 == 2 ? 1 : 0); break;
            case 6:
                int v = s.MagicCadence++ % 2; Shot(DivineShotKind.Flame, v == 0 ? .8f : 1.2f, d * (v == 0 ? 15 : 10), v); break;
            case 8: Shot(DivineShotKind.Swing, speed: d); break;
            case 9: Shot(DivineShotKind.Arrow, speed: d * 24); break;
            case 10:
                if (p.altFunctionUse == 2)
                {
                    Vector2 focus = Aim(p, Main.MouseWorld, 720); int changed = 0;
                    foreach (Projectile q in Main.ActiveProjectiles)
                        if (changed < 6 && q.owner == p.whoAmI && q.ModProjectile is DivineShot blade && blade.Weapon == 10 && blade.Kind == DivineShotKind.ClockBlade && q.ai[2] == 0)
                        { blade.Focus = focus; q.ai[2] = 1; q.velocity = (focus - q.Center).SafeNormalize(d) * 20; q.netUpdate = true; changed++; }
                }
                else
                {
                    int count = 0; Projectile? oldest = null;
                    foreach (Projectile q in Main.ActiveProjectiles)
                        if (q.owner == p.whoAmI && q.ModProjectile is DivineShot blade && blade.Weapon == 10 && blade.Kind == DivineShotKind.ClockBlade)
                        { count++; if (oldest == null || q.timeLeft < oldest.timeLeft) oldest = q; }
                    if (count >= 6) oldest?.Kill();
                    Shot(DivineShotKind.ClockBlade, speed: d * 15);
                }
                break;
        }
        return false;
    }
}

public abstract class DivineWeapon : ModItem
{
    public abstract int Index { get; }
    public override string Texture => DivineArsenal.Art(Index);
    public override void SetStaticDefaults()
    {
        if (Index % 4 == 3)
        { ItemID.Sets.GamepadWholeScreenUseRange[Type] = true; ItemID.Sets.StaffMinionSlotsRequired[Type] = Index == 3 ? 1 : 2; }
    }
    public override void SetDefaults()
    {
        Item.width = Item.height = 48; Item.damage = DivineArsenal.Damage[Index]; Item.knockBack = Index % 4 == 0 ? 5 : 2;
        Item.DamageType = DivineEquipmentData.Class(Index % 4); Item.useStyle = ItemUseStyleID.Shoot; Item.noMelee = true;
        Item.useTime = Item.useAnimation = Index switch { 0 => 22, 1 => 27, 4 => 36, 8 => 30, 9 => 24, 10 => 22, _ => 30 };
        Item.autoReuse = true; Item.shootSpeed = 16; Item.shoot = ModContent.ProjectileType<DivineShot>();
        Item.noUseGraphic = Index % 4 == 0 || Index == 2;
        Item.useAmmo = Index is 1 or 9 ? AmmoID.Arrow : Index == 5 ? AmmoID.Bullet : AmmoID.None;
        Item.mana = Index == 2 ? 8 : Index == 6 ? 12 : Index == 10 ? 18 : Index % 4 == 3 ? 10 : 0;
        Item.UseSound = Index % 4 == 0 ? SoundID.Item1 : Index % 4 == 1 ? Index == 5 ? SoundID.Item11 : SoundID.Item5 : SoundID.Item20;
        Item.rare = Index < 4 ? ItemRarityID.Orange : Index < 8 ? ItemRarityID.Pink : ItemRarityID.Red;
        Item.value = Item.sellPrice(gold: 2 + Index / 4 * 5);
        if (Index == 2) { Item.channel = true; Item.autoReuse = false; }
        if (Index == 5) { Item.useTime = 6; Item.useAnimation = 18; Item.reuseDelay = 20; }
        if (Index % 4 == 3)
        { Item.shoot = DivineArsenal.Minion(Index / 4); Item.buffType = DivineArsenal.Buff(Index / 4); Item.useStyle = ItemUseStyleID.Swing; Item.UseSound = SoundID.Item44; }
    }
    public override bool AltFunctionUse(Player player) => Index is 4 or 10;
    public override void ModifyManaCost(Player player, ref float reduce, ref float mult)
    { if (Index == 10 && player.altFunctionUse == 2) mult *= 30f / 18; }
    public override bool CanUseItem(Player p)
    {
        if (!DivineArsenal.Single(p, Index)) return false;
        if (Index == 4 && p.altFunctionUse == 2) return p.GetModPlayer<DivineEquipmentPlayer>().Pressure >= 100;
        if (Index == 10 && p.altFunctionUse == 2)
        {
            foreach (Projectile q in Main.ActiveProjectiles) if (q.owner == p.whoAmI && q.ModProjectile is DivineShot s && s.Weapon == 10 && s.Kind == DivineShotKind.ClockBlade && q.ai[2] == 0) return true;
            return false;
        }
        return true;
    }
    public override bool Shoot(Player p, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        => DivineArsenal.Fire(this, p, source, position, velocity, damage, knockback);
    public override void AddRecipes() => CreateRecipe().AddIngredient(DivineCatalog.Material(Index / 4), 18).AddTile(DivineCatalog.CraftStation(Index / 4)).Register();
}
public sealed class DivineWeapon0 : DivineWeapon { public override int Index => 0; }
public sealed class DivineWeapon1 : DivineWeapon { public override int Index => 1; }
public sealed class DivineWeapon2 : DivineWeapon { public override int Index => 2; }
public sealed class DivineWeapon3 : DivineWeapon { public override int Index => 3; }
public sealed class DivineWeapon4 : DivineWeapon { public override int Index => 4; }
public sealed class DivineWeapon5 : DivineWeapon { public override int Index => 5; }
public sealed class DivineWeapon6 : DivineWeapon { public override int Index => 6; }
public sealed class DivineWeapon7 : DivineWeapon { public override int Index => 7; }
public sealed class DivineWeapon8 : DivineWeapon { public override int Index => 8; }
public sealed class DivineWeapon9 : DivineWeapon { public override int Index => 9; }
public sealed class DivineWeapon10 : DivineWeapon { public override int Index => 10; }
public sealed class DivineWeapon11 : DivineWeapon { public override int Index => 11; }
