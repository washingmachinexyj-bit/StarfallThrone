#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Localization;
using global::StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Voyage.Weapons;

/// <summary>Equipment procs can reject Secondary projectiles without inspecting projectile AI.</summary>
public interface IVoyageWeaponProjectile { int WeaponIndex { get; } bool Secondary { get; } }

public static class VoyageArsenal
{
    public const int Count = 36;
    public static int Boss(int id) => Math.Min(16, id / 2);
    public static string Art(int id) => VoyageCatalog.Root + "Weapon" + id;
    public static Color Color(int id) => VoyageCatalog.Colors[Boss(id)];
    public static DamageClass Class(int id) => id is 7 or 23 ? DamageClass.SummonMeleeSpeed :
        id is 3 or 11 or 15 or 19 or 27 or 31 or 35 ? DamageClass.Summon :
        id is 1 or 6 or 9 or 14 or 17 or 22 or 25 or 30 or 33 ? DamageClass.Ranged :
        id is 2 or 5 or 10 or 13 or 18 or 21 or 26 or 29 or 34 ? DamageClass.Magic : DamageClass.Melee;
    public static bool Channel(int id) => id is 2 or 8 or 33 or 34;
    public static bool Ally(int id) => id is 3 or 11 or 15 or 19 or 27 or 31 or 35;
    public static bool Sentry(int id) => id is 11 or 31;
    public static int Slots(int id) => id == 35 ? 3 : id is 19 or 27 ? 2 : 1;
    public static bool Single(int id) => Channel(id) || id is 0 or 4 or 10 or 12 or 16 or 20 or 24 or 26 or 28 or 32;
    public static readonly int[] Damage = {
        12200,10500,16000,3200,13500,13600,12500,11800,16200,13400,15600,5500,
        18700,16600,16200,4500,22100,19500,22400,8900,25700,24800,22900,20600,
        30700,26400,31700,12700,35800,32300,30100,14000,41000,46000,27000,15000 };
    public static int AllyType(int id) => id switch {
        3 => ModContent.ProjectileType<VoyageObserver>(), 11 => ModContent.ProjectileType<VoyageAnchorSentry>(),
        15 => ModContent.ProjectileType<VoyageCrystalMinion>(), 19 => ModContent.ProjectileType<VoyageExecutor>(),
        27 => ModContent.ProjectileType<VoyageLeviathan>(), 31 => ModContent.ProjectileType<VoyagePortalSentry>(),
        35 => ModContent.ProjectileType<VoyageArkMinion>(), _ => 0 };
    public static int Buff(int id) => id switch {
        3 => ModContent.BuffType<VoyageObserverBuff>(), 15 => ModContent.BuffType<VoyageCrystalBuff>(),
        19 => ModContent.BuffType<VoyageExecutorBuff>(), 27 => ModContent.BuffType<VoyageLeviathanBuff>(),
        35 => ModContent.BuffType<VoyageArkBuff>(), _ => 0 };

    public static Vector2 Reach(Player p, Vector2 requested, float range = 640)
    {
        Vector2 direction = (requested - p.MountedCenter).SafeNormalize(new Vector2(p.direction, 0));
        float length = Math.Min(range, Vector2.Distance(requested, p.MountedCenter));
        for (; length > 24; length -= 12)
        {
            Vector2 point = p.MountedCenter + direction * length;
            if (Collision.CanHitLine(p.MountedCenter, 1, 1, point, 1, 1) && !Collision.SolidCollision(point - new Vector2(14), 28, 28)) return point;
        }
        return p.MountedCenter;
    }

    /// <summary>Owner-only, bounded spawner. Proc shots are intrinsically secondary and cannot fork.</summary>
    public static int Launch(IEntitySource source, int owner, int id, VoyageShotKind kind, Vector2 at, Vector2 velocity,
        int damage, float knockback = 2, int variant = 0, int delay = 0, Vector2? focus = null, int target = -1, Projectile? parent = null)
    {
        if (owner != Main.myPlayer || owner < 0 || owner >= Main.maxPlayers || id < 0 || id >= Count) return -1;
        int count = 0;
        foreach (Projectile q in Main.ActiveProjectiles) if (q.owner == owner && q.ModProjectile is VoyageShot) count++;
        if (count >= 160) return -1;
        int type = Ally(id) ? ModContent.ProjectileType<VoyageSummonShot>() : ModContent.ProjectileType<VoyageShot>();
        int slot = Projectile.NewProjectile(source, at, velocity, type, Math.Max(1, damage), knockback, owner, id, (int)kind, variant);
        if (slot >= Main.maxProjectiles) return -1;
        var shot = (VoyageShot)Main.projectile[slot].ModProjectile;
        shot.Delay += delay; shot.Projectile.timeLeft += delay;
        shot.Focus = focus ?? at; shot.Target = target;
        shot.TargetType = target >= 0 && target < Main.maxNPCs ? Main.npc[target].type : 0;
        if (parent != null) { shot.ParentSlot = parent.whoAmI; shot.ParentIdentity = parent.identity; shot.ParentType = parent.type; }
        shot.Projectile.netUpdate = true;
        return slot;
    }

    public static int LaunchProc(IEntitySource source, Player owner, int weaponId, Vector2 at, Vector2 velocity, int damage)
        => Launch(source, owner.whoAmI, weaponId, VoyageShotKind.Shard, at, velocity, damage, 0, 100);

    public static bool CanUse(Player p, int id)
    {
        if (!Single(id)) return true;
        foreach (Projectile q in Main.ActiveProjectiles)
            if (q.owner == p.whoAmI && q.ModProjectile is IVoyageWeaponProjectile shot && shot.WeaponIndex == id && !shot.Secondary) return false;
        return true;
    }

    public static bool Fire(VoyageWeapon item, Player p, EntitySource_ItemUse_WithAmmo source, Vector2 at, Vector2 velocity, int damage, float knockback)
    {
        int id = item.Index;
        if (p.whoAmI != Main.myPlayer) return false;
        if (id is 7 or 23) return true;
        if (Ally(id))
        {
            if (!Sentry(id)) p.AddBuff(item.Item.buffType, 2);
            int slot = Projectile.NewProjectile(source, Reach(p, Main.MouseWorld), Vector2.Zero, item.Item.shoot, damage, knockback, p.whoAmI);
            if (slot < Main.maxProjectiles) Main.projectile[slot].originalDamage = item.Item.damage;
            if (Sentry(id)) p.UpdateMaxTurrets();
            return false;
        }
        if (Channel(id))
        {
            Projectile.NewProjectile(source, p.MountedCenter, velocity.SafeNormalize(new Vector2(p.direction, 0)),
                ModContent.ProjectileType<VoyageChannel>(), damage, knockback, p.whoAmI, id);
            return false;
        }
        int combo = p.GetModPlayer<VoyageWeaponPlayer>().Next(id);
        Vector2 dir = velocity.SafeNormalize(new Vector2(p.direction, 0));
        Vector2 focus = Reach(p, Main.MouseWorld);
        void Shot(VoyageShotKind kind, float scale = 1, Vector2? position = null, Vector2? speed = null, int variant = 0, int delay = 0, Vector2? center = null, int target = -1)
            => Launch(source, p.whoAmI, id, kind, position ?? at, speed ?? velocity, (int)(damage * scale), knockback, variant, delay, center, target);
        switch (id)
        {
            case 0: Shot(VoyageShotKind.Swing, variant: combo % 3); break;
            case 1: Shot(VoyageShotKind.Cargo, speed: dir * 12); break;
            case 4: Shot(VoyageShotKind.Saw, speed: dir * 17); break;
            case 5:
                for (int n = 0; n < 3; n++) Shot(VoyageShotKind.OrbitRock, .48f, focus, (n * MathHelper.TwoPi / 3).ToRotationVector2(), n, center: focus);
                break;
            case 6: Shot(VoyageShotKind.Pulse, speed: dir * 21); break;
            case 9: Shot(VoyageShotKind.Arrow, speed: dir * 18); break;
            case 10: Shot(VoyageShotKind.Anchor, speed: dir * 12); break;
            case 12: Shot(VoyageShotKind.Clamp, speed: dir, center: p.MountedCenter + dir * 190); break;
            case 13: Shot(VoyageShotKind.PortalBurst, speed: dir, center: focus); break;
            case 14: Shot(VoyageShotKind.OrbitArrow, speed: dir * 19, center: p.MountedCenter + dir * 105); break;
            case 16:
                if (combo % 3 == 1) Shot(VoyageShotKind.BlueBlade, .9f, speed: dir * 18);
                else Shot(VoyageShotKind.Swing, combo % 3 == 2 ? 1.15f : 1, variant: combo % 3);
                break;
            case 17:
                if (combo % 2 == 0) Shot(VoyageShotKind.Pulse, 1.45f, speed: dir * 12, variant: 1);
                else for (int n = 0; n < 3; n++) Shot(VoyageShotKind.Pulse, .44f, speed: dir * 25, variant: 2, delay: n * 5);
                break;
            case 18:
                if (combo % 3 == 0) Shot(VoyageShotKind.Swing, 1.15f, variant: 4);
                else if (combo % 3 == 1) Shot(VoyageShotKind.Beam, 1.2f, speed: dir);
                else Shot(VoyageShotKind.MagnetBurst, 1.1f, focus, Vector2.Zero);
                break;
            case 20: Shot(VoyageShotKind.ChainSpear, .44f, speed: dir); break;
            case 21:
                Vector2 route = dir.RotatedBy(MathHelper.PiOver2);
                Vector2 routeStart = Reach(p, focus - dir * 120 - route * 150);
                Shot(VoyageShotKind.Fleet, position: routeStart, speed: route, center: focus);
                break;
            case 22: Shot(VoyageShotKind.Seed, speed: dir * 19); break;
            case 24: Shot(VoyageShotKind.Swing, variant: combo % 2 == 1 ? 2 : 0); break;
            case 25: Shot(VoyageShotKind.CoreRocket, speed: dir * 11); break;
            case 26: Shot(VoyageShotKind.Tide, .8f, speed: dir * 12); break;
            case 28: Shot(VoyageShotKind.Swing, variant: combo % 3); break;
            case 29: Shot(VoyageShotKind.Mirror, speed: dir, center: focus); break;
            case 30:
                var state = p.GetModPlayer<VoyageWeaponPlayer>();
                Shot(VoyageShotKind.GuideArrow, speed: dir * 23, variant: combo % 3, target: combo % 3 == 0 ? -1 : state.ValidMark());
                break;
            case 32: Shot(VoyageShotKind.Swing, variant: combo % 3); break;
        }
        return false;
    }
}

public sealed class VoyageWeaponPlayer : ModPlayer
{
    private readonly int[] cadence = new int[36];
    public int MarkTarget = -1, MarkType, MarkTime;
    public int Next(int index) { int n = cadence[index]; cadence[index] = (n + 1) % 6; return n; }
    public int Peek(int index) => cadence[index];
    public int ValidMark() => MarkTime > 0 && MarkTarget >= 0 && MarkTarget < Main.maxNPCs && Main.npc[MarkTarget].CanBeChasedBy() && Main.npc[MarkTarget].type == MarkType ? MarkTarget : -1;
    public void Mark(NPC npc) { MarkTarget = npc.whoAmI; MarkType = npc.type; MarkTime = 180; }
    public override void PostUpdate() { if (MarkTime > 0) MarkTime--; }
    public override void UpdateDead() { Array.Clear(cadence); MarkTime = 0; MarkTarget = -1; }
}

public abstract class VoyageWeapon : ModItem
{
    public abstract int Index { get; }
    public override string Texture => VoyageArsenal.Art(Index);
    public override void SetStaticDefaults()
    {
        if (VoyageArsenal.Ally(Index))
        {
            ItemID.Sets.GamepadWholeScreenUseRange[Type] = true;
            if (!VoyageArsenal.Sentry(Index)) ItemID.Sets.StaffMinionSlotsRequired[Type] = VoyageArsenal.Slots(Index);
        }
    }
    public override void SetDefaults()
    {
        int id = Index;
        if (id is 7 or 23)
        {
            Item.DefaultToWhip(id == 7 ? ModContent.ProjectileType<VoyageNeuralWhip>() : ModContent.ProjectileType<VoyageRootWhip>(), VoyageArsenal.Damage[id], 3, 7);
            Item.useTime = Item.useAnimation = id == 7 ? 30 : 38;
        }
        else
        {
            Item.width = Item.height = 40; Item.damage = VoyageArsenal.Damage[id]; Item.knockBack = 4;
            Item.DamageType = VoyageArsenal.Class(id); Item.useTime = Item.useAnimation = id is 1 or 25 ? 44 : id == 17 ? 26 : 34;
            Item.useStyle = ItemUseStyleID.Shoot; Item.noMelee = true; Item.autoReuse = true; Item.shootSpeed = 18;
            Item.shoot = ModContent.ProjectileType<VoyageShot>();
            Item.noUseGraphic = Item.DamageType == DamageClass.Melee || VoyageArsenal.Channel(id);
            Item.useAmmo = id is 1 or 25 ? AmmoID.Rocket : id is 6 or 17 or 33 ? AmmoID.Bullet : id is 9 or 14 or 22 or 30 ? AmmoID.Arrow : AmmoID.None;
            Item.mana = Item.DamageType == DamageClass.Magic ? 18 + VoyageArsenal.Boss(id) : 0;
            Item.channel = VoyageArsenal.Channel(id);
            if (Item.channel) { Item.autoReuse = false; Item.shoot = ModContent.ProjectileType<VoyageChannel>(); }
            Item.UseSound = Item.DamageType == DamageClass.Melee ? SoundID.Item1 : Item.DamageType == DamageClass.Magic ? SoundID.Item20 : Item.useAmmo == AmmoID.Arrow ? SoundID.Item5 : SoundID.Item11;
            if (VoyageArsenal.Ally(id))
            {
                Item.useStyle = ItemUseStyleID.Swing; Item.useTime = Item.useAnimation = 36; Item.UseSound = SoundID.Item44;
                Item.shoot = VoyageArsenal.AllyType(id); Item.mana = 20; Item.buffType = VoyageArsenal.Buff(id); Item.sentry = VoyageArsenal.Sentry(id);
            }
        }
        Item.rare = ItemRarityID.Red; Item.value = Item.sellPrice(gold: 30 + VoyageArsenal.Boss(id) * 3);
    }
    public override bool MeleePrefix() => Index is 7 or 23 || base.MeleePrefix();
    public override bool CanUseItem(Player player) => VoyageArsenal.CanUse(player, Index);
    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        => VoyageArsenal.Fire(this, player, source, position, velocity, damage, knockback);
    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        if (Index != 18) return;
        int next = Main.LocalPlayer.GetModPlayer<VoyageWeaponPlayer>().Peek(18) % 3;
        string arm = Language.GetTextValue("Mods.StarfallThrone.VoyageWeapons.Arm" + next);
        tooltips.Add(new TooltipLine(Mod, "NextArmament", Language.GetTextValue("Mods.StarfallThrone.VoyageWeapons.NextArm", arm)));
    }
    public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
    {
        if (Index != 18) return;
        int next = Main.LocalPlayer.GetModPlayer<VoyageWeaponPlayer>().Peek(18) % 3;
        for (int n = 0; n < 3; n++)
        {
            Vector2 at = position + new Vector2((n - 1) * 8, 14) * scale;
            CombatDrawing.Circle(spriteBatch, at, n == next ? 3.5f : 2, n == next ? Color.White : Color.Gray, n == next ? 2 : 1);
        }
    }
    public override void AddRecipes()
    {
        int boss = VoyageArsenal.Boss(Index);
        CreateRecipe().AddIngredient(VoyageCatalog.Material(boss), boss == 16 ? 36 : 24).AddIngredient(VoyageCatalog.Core(boss))
            .AddIngredient(VoyageCatalog.Alloy, boss == 16 ? 16 : 8).AddTile(VoyageCatalog.Workbench).Register();
    }
}

public sealed class VoyageWeapon0 : VoyageWeapon { public override int Index => 0; }
public sealed class VoyageWeapon1 : VoyageWeapon { public override int Index => 1; }
public sealed class VoyageWeapon2 : VoyageWeapon { public override int Index => 2; }
public sealed class VoyageWeapon3 : VoyageWeapon { public override int Index => 3; }
public sealed class VoyageWeapon4 : VoyageWeapon { public override int Index => 4; }
public sealed class VoyageWeapon5 : VoyageWeapon { public override int Index => 5; }
public sealed class VoyageWeapon6 : VoyageWeapon { public override int Index => 6; }
public sealed class VoyageWeapon7 : VoyageWeapon { public override int Index => 7; }
public sealed class VoyageWeapon8 : VoyageWeapon { public override int Index => 8; }
public sealed class VoyageWeapon9 : VoyageWeapon { public override int Index => 9; }
public sealed class VoyageWeapon10 : VoyageWeapon { public override int Index => 10; }
public sealed class VoyageWeapon11 : VoyageWeapon { public override int Index => 11; }
public sealed class VoyageWeapon12 : VoyageWeapon { public override int Index => 12; }
public sealed class VoyageWeapon13 : VoyageWeapon { public override int Index => 13; }
public sealed class VoyageWeapon14 : VoyageWeapon { public override int Index => 14; }
public sealed class VoyageWeapon15 : VoyageWeapon { public override int Index => 15; }
public sealed class VoyageWeapon16 : VoyageWeapon { public override int Index => 16; }
public sealed class VoyageWeapon17 : VoyageWeapon { public override int Index => 17; }
public sealed class VoyageWeapon18 : VoyageWeapon { public override int Index => 18; }
public sealed class VoyageWeapon19 : VoyageWeapon { public override int Index => 19; }
public sealed class VoyageWeapon20 : VoyageWeapon { public override int Index => 20; }
public sealed class VoyageWeapon21 : VoyageWeapon { public override int Index => 21; }
public sealed class VoyageWeapon22 : VoyageWeapon { public override int Index => 22; }
public sealed class VoyageWeapon23 : VoyageWeapon { public override int Index => 23; }
public sealed class VoyageWeapon24 : VoyageWeapon { public override int Index => 24; }
public sealed class VoyageWeapon25 : VoyageWeapon { public override int Index => 25; }
public sealed class VoyageWeapon26 : VoyageWeapon { public override int Index => 26; }
public sealed class VoyageWeapon27 : VoyageWeapon { public override int Index => 27; }
public sealed class VoyageWeapon28 : VoyageWeapon { public override int Index => 28; }
public sealed class VoyageWeapon29 : VoyageWeapon { public override int Index => 29; }
public sealed class VoyageWeapon30 : VoyageWeapon { public override int Index => 30; }
public sealed class VoyageWeapon31 : VoyageWeapon { public override int Index => 31; }
public sealed class VoyageWeapon32 : VoyageWeapon { public override int Index => 32; }
public sealed class VoyageWeapon33 : VoyageWeapon { public override int Index => 33; }
public sealed class VoyageWeapon34 : VoyageWeapon { public override int Index => 34; }
public sealed class VoyageWeapon35 : VoyageWeapon { public override int Index => 35; }
