using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.Projectiles;

namespace StarfallThrone.Content.Items;

public abstract class StarfallAccessory : ModItem
{
    protected abstract int BossIndex { get; }
    public override string Texture => $"StarfallThrone/Content/Assets/Items/Accessory{BossIndex}";
    public override void SetDefaults()
    {
        Item.width = 34;
        Item.height = 34;
        Item.accessory = true;
        Item.rare = ItemRarityID.Expert;
        Item.expert = true;
        Item.value = Item.buyPrice(gold: 25);
    }
    public override void UpdateAccessory(Player player, bool hideVisual)
    {
        player.statDefense += 8 + BossIndex / 3;
        player.GetDamage(DamageClass.Generic) *= 1.06f + BossIndex * 0.004f;
        player.GetCritChance(DamageClass.Generic) += 2 + BossIndex / 4;
        if (BossIndex >= 10)
            player.endurance += 0.01f;

        // These are the Expert-mode signature rewards: one distinct power hook per boss.
        switch (BossIndex)
        {
            case 0:
                player.statLifeMax2 += 100;
                player.lifeRegen += 2;
                player.noKnockback = true;
                break;
            case 1:
                player.GetCritChance(DamageClass.Generic) += 8;
                player.GetDamage(DamageClass.Generic) *= 1.08f;
                break;
            case 2:
                player.moveSpeed += 0.18f;
                player.GetDamage(DamageClass.Generic) *= 1.10f;
                break;
            case 3:
                player.endurance += 0.06f;
                player.buffImmune[BuffID.Confused] = true;
                break;
            case 4:
                player.maxMinions += 2;
                player.GetDamage(DamageClass.Summon) *= 1.14f;
                break;
            case 5:
                player.statDefense += 12;
                player.GetDamage(DamageClass.Melee) *= 1.12f;
                break;
            case 6:
                player.statManaMax2 += 100;
                player.manaCost *= 0.82f;
                break;
            case 7:
                player.wingTimeMax += 45;
                player.moveSpeed += 0.12f;
                break;
            case 8:
                player.GetDamage(DamageClass.Ranged) *= 1.15f;
                player.GetCritChance(DamageClass.Ranged) += 7;
                break;
            case 9:
                player.maxTurrets += 1;
                player.GetDamage(DamageClass.Generic) *= 1.12f;
                break;
            case 10:
                player.endurance += 0.05f;
                player.GetDamage(DamageClass.Generic) *= 1.12f;
                break;
            case 11:
                player.lifeRegen += 4;
                player.buffImmune[BuffID.Poisoned] = true;
                break;
            case 12:
                player.statLifeMax2 += 140;
                player.statDefense += 10;
                break;
            case 13:
                player.ignoreWater = true;
                player.waterWalk = true;
                player.moveSpeed += 0.22f;
                break;
            case 14:
                player.statManaMax2 += 120;
                player.GetDamage(DamageClass.Magic) *= 1.16f;
                break;
            case 15:
                player.maxMinions += 1;
                player.maxTurrets += 1;
                player.GetDamage(DamageClass.Generic) *= 1.10f;
                break;
            default:
                player.statLifeMax2 += 200;
                player.statManaMax2 += 100;
                player.endurance += 0.06f;
                player.GetDamage(DamageClass.Generic) *= 1.15f;
                break;
        }
    }
}

public sealed class ImperialGelShield : StarfallAccessory { protected override int BossIndex => 0; }
public sealed class PhaseEyeCore : StarfallAccessory { protected override int BossIndex => 1; }
public sealed class DevourerMagnet : StarfallAccessory { protected override int BossIndex => 2; }
public sealed class IllusionHeart : StarfallAccessory { protected override int BossIndex => 3; }
public sealed class HiveHeart : StarfallAccessory { protected override int BossIndex => 4; }
public sealed class JudgeMask : StarfallAccessory { protected override int BossIndex => 5; }
public sealed class SoulHinge : StarfallAccessory { protected override int BossIndex => 6; }
public sealed class PrismWing : StarfallAccessory { protected override int BossIndex => 7; }
public sealed class PolarEngine : StarfallAccessory { protected override int BossIndex => 8; }
public sealed class FourArmProtocol : StarfallAccessory { protected override int BossIndex => 9; }
public sealed class MagneticCoil : StarfallAccessory { protected override int BossIndex => 10; }
public sealed class WorldRoot : StarfallAccessory { protected override int BossIndex => 11; }
public sealed class TitanPrism : StarfallAccessory { protected override int BossIndex => 12; }
public sealed class TideEngine : StarfallAccessory { protected override int BossIndex => 13; }
public sealed class CoronaCrown : StarfallAccessory { protected override int BossIndex => 14; }
public sealed class FourPillarEdict : StarfallAccessory { protected override int BossIndex => 15; }
public sealed class CosmicHeart : StarfallAccessory { protected override int BossIndex => 16; }

public sealed class ThroneAegis : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Items/AccessoryBonus0";
    public override void SetDefaults()
    {
        Item.width = 38;
        Item.height = 38;
        Item.accessory = true;
        Item.rare = ItemRarityID.Red;
        Item.value = Item.buyPrice(platinum: 2);
    }
    public override void UpdateAccessory(Player player, bool hideVisual)
    {
        player.statLifeMax2 += 180;
        player.statDefense += 12;
        player.endurance += 0.05f;
        player.noKnockback = true;
    }
    public override void AddRecipes() => CreateRecipe()
        .AddIngredient<AstralIngot>(20)
        .AddIngredient<MoonGodCore>(12)
        .AddIngredient<StarfallResidue>(40)
        .AddTile(TileID.LunarCraftingStation)
        .Register();
}

public sealed class VoidstepInsignia : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Items/AccessoryBonus1";
    public override void SetDefaults()
    {
        Item.width = 38;
        Item.height = 38;
        Item.accessory = true;
        Item.rare = ItemRarityID.Red;
        Item.value = Item.buyPrice(platinum: 2);
    }
    public override void UpdateAccessory(Player player, bool hideVisual)
    {
        player.moveSpeed += 0.28f;
        player.wingTimeMax += 70;
        player.GetDamage(DamageClass.Generic) *= 1.12f;
    }
    public override void AddRecipes() => CreateRecipe()
        .AddIngredient<AstralIngot>(20)
        .AddIngredient<AstralDoctrine>(12)
        .AddIngredient<StarfallResidue>(40)
        .AddTile(TileID.LunarCraftingStation)
        .Register();
}

public sealed class AstralConductor : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Items/AccessoryBonus2";
    public override void SetDefaults()
    {
        Item.width = 38;
        Item.height = 38;
        Item.accessory = true;
        Item.rare = ItemRarityID.Red;
        Item.value = Item.buyPrice(platinum: 2);
    }
    public override void UpdateAccessory(Player player, bool hideVisual)
    {
        player.statManaMax2 += 160;
        player.maxMinions += 1;
        player.maxTurrets += 1;
        player.GetDamage(DamageClass.Generic) *= 1.12f;
    }
    public override void AddRecipes() => CreateRecipe()
        .AddIngredient<AstralIngot>(20)
        .AddIngredient<CoronaPrism>(12)
        .AddIngredient<StarfallResidue>(40)
        .AddTile(TileID.LunarCraftingStation)
        .Register();
}

public abstract class StarfallWeapon : ModItem
{
    protected abstract int BossIndex { get; }
    public override string Texture => $"StarfallThrone/Content/Assets/Items/Weapon{BossIndex}";
    public override void SetDefaults()
    {
        Item.width = 46;
        Item.height = 46;
        Item.damage = 850 + BossIndex * 620;
        Item.knockBack = 5f + BossIndex * 0.15f;
        Item.useTime = Math.Max(5, 22 - BossIndex / 3);
        Item.useAnimation = Item.useTime;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.noUseGraphic = false;
        Item.UseSound = SoundID.Item92;
        Item.DamageType = (BossIndex % 4) switch
        {
            0 => DamageClass.Melee,
            1 => DamageClass.Ranged,
            2 => DamageClass.Magic,
            _ => DamageClass.Summon
        };
        Item.mana = Item.DamageType == DamageClass.Magic ? 12 + BossIndex : 0;
        Item.shoot = ModContent.ProjectileType<StarfallPlayerBolt>();
        Item.shootSpeed = 13f + BossIndex * 0.2f;
        Item.rare = ItemRarityID.Red;
        Item.value = Item.buyPrice(platinum: 1 + BossIndex / 5);
        ReforgedWeapons.Configure(Item, 21 + BossIndex);
    }
    public override bool CanUseItem(Player player) => ReforgedWeapons.CanUse(player, 21 + BossIndex);
    public override bool Shoot(Player player, Terraria.DataStructures.EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        => ReforgedWeapons.Shoot(Item, 21 + BossIndex, player, source, position, velocity, damage, knockback);
}

public sealed class ImperialScepter : StarfallWeapon { protected override int BossIndex => 0; }
public sealed class PhaseBreaker : StarfallWeapon { protected override int BossIndex => 1; }
public sealed class DevourerLance : StarfallWeapon { protected override int BossIndex => 2; }
public sealed class MindCircuit : StarfallWeapon { protected override int BossIndex => 3; }
public sealed class RoyalStinger : StarfallWeapon { protected override int BossIndex => 4; }
public sealed class JudgmentCodex : StarfallWeapon { protected override int BossIndex => 5; }
public sealed class EternalTendril : StarfallWeapon { protected override int BossIndex => 6; }
public sealed class DynastyPrism : StarfallWeapon { protected override int BossIndex => 7; }
public sealed class TwinRayCannon : StarfallWeapon { protected override int BossIndex => 8; }
public sealed class PrimeSaw : StarfallWeapon { protected override int BossIndex => 9; }
public sealed class StarChainDrill : StarfallWeapon { protected override int BossIndex => 10; }
public sealed class FlowerThornCrown : StarfallWeapon { protected override int BossIndex => 11; }
public sealed class TempleRiftCannon : StarfallWeapon { protected override int BossIndex => 12; }
public sealed class AbyssalHarpoon : StarfallWeapon { protected override int BossIndex => 13; }
public sealed class CoronaPrismWeapon : StarfallWeapon { protected override int BossIndex => 14; }
public sealed class PontiffStarbook : StarfallWeapon { protected override int BossIndex => 15; }
public sealed class EndMoonWeapon : StarfallWeapon { protected override int BossIndex => 16; }

public sealed class Thronebreaker : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Items/WeaponBonus0";
    public override void SetDefaults()
    {
        Item.width = 52;
        Item.height = 52;
        Item.damage = 2400;
        Item.knockBack = 8f;
        Item.useTime = 8;
        Item.useAnimation = 8;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.UseSound = SoundID.Item92;
        Item.DamageType = DamageClass.Melee;
        Item.shoot = ModContent.ProjectileType<StarfallPlayerBolt>();
        Item.shootSpeed = 18f;
        Item.rare = ItemRarityID.Red;
        Item.value = Item.buyPrice(platinum: 3);
        ReforgedWeapons.Configure(Item, 38);
    }
    public override bool CanUseItem(Player player) => ReforgedWeapons.CanUse(player, 38);
    public override bool Shoot(Player player, Terraria.DataStructures.EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        => ReforgedWeapons.Shoot(Item, 38, player, source, position, velocity, damage, knockback);
    public override void AddRecipes() => CreateRecipe()
        .AddIngredient<AstralIngot>(30)
        .AddIngredient<MoonGodCore>(15)
        .AddIngredient<StarfallResidue>(60)
        .AddTile(TileID.LunarCraftingStation)
        .Register();
}

public abstract class StarfallFinalAccessory : StarfallAccessory
{
    protected override int BossIndex => 16;
}

public sealed class StarfallPlayerBolt : ModProjectile
{
    public override string Texture => "StarfallThrone/Content/Assets/Projectiles/PlayerBolt";
    public override void SetDefaults()
    {
        Projectile.width = 14;
        Projectile.height = 14;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Generic;
        Projectile.penetrate = 3;
        Projectile.timeLeft = 180;
        Projectile.extraUpdates = 1;
    }
    public override void AI()
    {
        Projectile.rotation = Projectile.velocity.ToRotation();
        Dust.NewDustPerfect(Projectile.Center, DustID.ShimmerSpark, -Projectile.velocity * 0.04f, 0, Color.White, 0.8f);
    }
}
