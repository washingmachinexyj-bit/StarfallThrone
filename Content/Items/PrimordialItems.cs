using System;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.NPCs;
using StarfallThrone.Content.Projectiles;
using StarfallThrone.Content.Systems;
using StarfallThrone.Content.Tiles;

namespace StarfallThrone.Content.Items;

public abstract class PrimordialCoreItem : ModItem
{
    protected abstract int BossIndex { get; }
    public override string Texture => $"StarfallThrone/Content/Assets/Items/PrimordialCore{BossIndex}";

    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 25;

    public override void SetDefaults()
    {
        Item.width = 30;
        Item.height = 30;
        Item.maxStack = 9999;
        Item.value = Item.buyPrice(silver: 60);
        Item.rare = BossIndex < 4 ? ItemRarityID.Green : BossIndex < 7 ? ItemRarityID.Orange : ItemRarityID.LightRed;
        Item.material = true;
    }
}

public sealed class ResinCore : PrimordialCoreItem { protected override int BossIndex => 0; }
public sealed class CopperveinCore : PrimordialCoreItem { protected override int BossIndex => 1; }
public sealed class SandscorpionCore : PrimordialCoreItem { protected override int BossIndex => 2; }
public sealed class FrosthornCore : PrimordialCoreItem { protected override int BossIndex => 3; }
public sealed class MirelanternCore : PrimordialCoreItem { protected override int BossIndex => 4; }
public sealed class WaxenCore : PrimordialCoreItem { protected override int BossIndex => 5; }
public sealed class RotrootCore : PrimordialCoreItem { protected override int BossIndex => 6; }
public sealed class GravebellCore : PrimordialCoreItem { protected override int BossIndex => 7; }
public sealed class MeteorMawCore : PrimordialCoreItem { protected override int BossIndex => 8; }
public sealed class DawnshardCore : PrimordialCoreItem { protected override int BossIndex => 9; }

public abstract class PrimordialSummonItem : ModItem
{
    protected abstract int BossIndex { get; }
    public override string Texture => $"StarfallThrone/Content/Assets/Items/PrimordialSummon{BossIndex}";

    public override void SetDefaults()
    {
        Item.width = 34;
        Item.height = 34;
        Item.maxStack = 20;
        Item.consumable = true;
        Item.useStyle = ItemUseStyleID.HoldUp;
        Item.useTime = 30;
        Item.useAnimation = 30;
        Item.UseSound = SoundID.Item44;
        Item.rare = BossIndex < 4 ? ItemRarityID.Green : ItemRarityID.Orange;
        Item.value = Item.buyPrice(silver: 50);
    }

    public override bool CanUseItem(Player player)
    {
        return PrimordialWorld.CanSummon(BossIndex) && !NPC.AnyNPCs(PrimordialDropCatalog.NPCType(BossIndex));
    }

    public override bool? UseItem(Player player)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            NPC.SpawnOnPlayer(player.whoAmI, PrimordialDropCatalog.NPCType(BossIndex));
        return true;
    }
}

public sealed class ResinAcornSummon : PrimordialSummonItem
{
    protected override int BossIndex => 0;
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.Gel, 15).AddIngredient(ItemID.Wood, 5).AddIngredient(ItemID.Acorn).AddTile(TileID.WorkBenches).Register();
}

public sealed class CopperCarapaceSummon : PrimordialSummonItem
{
    protected override int BossIndex => 1;
    public override void AddRecipes() => CreateRecipe().AddIngredient<ResinCore>().AddIngredient(ItemID.CopperBar, 5).AddIngredient(ItemID.StoneBlock, 20).AddTile(TileID.Furnaces).Register();
}

public sealed class SandBellSummon : PrimordialSummonItem
{
    protected override int BossIndex => 2;
    public override void AddRecipes() => CreateRecipe().AddIngredient<CopperveinCore>().AddIngredient(ItemID.Sandstone, 20).AddIngredient(ItemID.Stinger, 2).AddTile(TileID.Furnaces).Register();
}

public sealed class FrosthornFluteSummon : PrimordialSummonItem
{
    protected override int BossIndex => 3;
    public override void AddRecipes() => CreateRecipe().AddIngredient<SandscorpionCore>().AddIngredient(ItemID.IceBlock, 25).AddIngredient(ItemID.SnowBlock, 15).AddIngredient(ItemID.Bone, 2).AddTile(TileID.WorkBenches).Register();
}

public sealed class MireLanternSummon : PrimordialSummonItem
{
    protected override int BossIndex => 4;
    public override void AddRecipes() => CreateRecipe().AddIngredient<FrosthornCore>().AddIngredient(ItemID.MudBlock, 20).AddIngredient(ItemID.GlowingMushroom, 5).AddIngredient(ItemID.Glowstick, 2).AddTile(TileID.Bottles).Register();
}

public sealed class WaxSealSummon : PrimordialSummonItem
{
    protected override int BossIndex => 5;
    public override void AddRecipes() => CreateRecipe().AddIngredient<MirelanternCore>().AddIngredient(ItemID.HoneyBlock, 10).AddIngredient(ItemID.BeeWax, 8).AddIngredient(ItemID.Stinger).AddTile(TileID.HoneyDispenser).Register();
}

public sealed class EvilRootTotemSummon : PrimordialSummonItem
{
    protected override int BossIndex => 6;
    public override void AddRecipes() => CreateRecipe().AddIngredient<WaxenCore>().AddIngredient(ItemID.VilePowder, 10).AddIngredient(ItemID.Vine, 3).AddIngredient(ItemID.GrassSeeds, 15).AddTile(TileID.DemonAltar).Register();
}

public sealed class GravebellKeySummon : PrimordialSummonItem
{
    protected override int BossIndex => 7;
    public override void AddRecipes() => CreateRecipe().AddIngredient<RotrootCore>().AddIngredient(ItemID.Bone, 30).AddIngredient(ItemID.Chain, 5).AddIngredient(ItemID.RottenChunk, 3).AddTile(TileID.Anvils).Register();
}

public sealed class MeteorGizzardSummon : PrimordialSummonItem
{
    protected override int BossIndex => 8;
    public override void AddRecipes() => CreateRecipe().AddIngredient<GravebellCore>().AddIngredient(ItemID.MeteoriteBar, 12).AddIngredient(ItemID.FallenStar, 5).AddTile(TileID.Furnaces).Register();
}

public sealed class DawnshardSigilSummon : PrimordialSummonItem
{
    protected override int BossIndex => 9;
    public override void AddRecipes() => CreateRecipe().AddIngredient<MeteorMawCore>().AddIngredient(ItemID.FallenStar, 10).AddIngredient(ItemID.GoldBar, 5).AddIngredient(ItemID.Gel, 5).AddTile(TileID.DemonAltar).Register();
}

public abstract class PrimordialTreasureBag : ModItem
{
    protected abstract int BossIndex { get; }
    public override string Texture => $"StarfallThrone/Content/Assets/Items/PrimordialBag{BossIndex}";

    public override void SetStaticDefaults()
    {
        ItemID.Sets.BossBag[Type] = true;
        ItemID.Sets.OpenableBag[Type] = true;
    }

    public override void SetDefaults()
    {
        Item.width = 36;
        Item.height = 36;
        Item.maxStack = 9999;
        Item.rare = ItemRarityID.Cyan;
        Item.expert = true;
        Item.value = Item.buyPrice(gold: 5);
    }

    public override bool CanRightClick() => true;

    public override void ModifyItemLoot(ItemLoot itemLoot)
    {
        itemLoot.Add(ItemDropRule.Common(PrimordialDropCatalog.CoreType(BossIndex), 1, 10, 16));
        itemLoot.Add(ItemDropRule.Common(PrimordialDropCatalog.WeaponOneType(BossIndex), 4));
        itemLoot.Add(ItemDropRule.Common(PrimordialDropCatalog.WeaponTwoType(BossIndex), 4));
        if (BossIndex == 9)
            itemLoot.Add(ItemDropRule.Common(PrimordialDropCatalog.WeaponThreeType(BossIndex), 4));
        itemLoot.Add(ItemDropRule.Common(PrimordialDropCatalog.AccessoryType(BossIndex)));
    }
}

public sealed class ResinWhelpBag : PrimordialTreasureBag { protected override int BossIndex => 0; }
public sealed class CopperveinScarabBag : PrimordialTreasureBag { protected override int BossIndex => 1; }
public sealed class BellscorpionMatriarchBag : PrimordialTreasureBag { protected override int BossIndex => 2; }
public sealed class FrosthornStalkerBag : PrimordialTreasureBag { protected override int BossIndex => 3; }
public sealed class LanternMiretoadBag : PrimordialTreasureBag { protected override int BossIndex => 4; }
public sealed class WaxenArbiterBag : PrimordialTreasureBag { protected override int BossIndex => 5; }
public sealed class RotrootWeaverBag : PrimordialTreasureBag { protected override int BossIndex => 6; }
public sealed class GravebellKeybearerBag : PrimordialTreasureBag { protected override int BossIndex => 7; }
public sealed class MeteorMawBag : PrimordialTreasureBag { protected override int BossIndex => 8; }
public sealed class DawnshardScionBag : PrimordialTreasureBag { protected override int BossIndex => 9; }

public abstract class PrimordialRelic : ModItem
{
    protected abstract int BossIndex { get; }
    public override string Texture => $"StarfallThrone/Content/Assets/Items/PrimordialRelic{BossIndex}";
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;
    public override void SetDefaults()
    {
        Item.width = 30;
        Item.height = 40;
        Item.maxStack = 9999;
        Item.rare = ItemRarityID.Master;
        Item.value = Item.buyPrice(gold: 5);
        Item.useStyle = ItemUseStyleID.Swing;
        Item.useAnimation = 15;
        Item.useTime = 15;
        Item.consumable = true;
        Item.createTile = ModContent.TileType<StarfallRelicTile>();
        Item.placeStyle = 17 + BossIndex;
    }
}

public abstract class PrimordialTrophy : ModItem
{
    protected abstract int BossIndex { get; }
    public override string Texture => $"StarfallThrone/Content/Assets/Items/PrimordialTrophy{BossIndex}";
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;
    public override void SetDefaults()
    {
        Item.width = 32;
        Item.height = 32;
        Item.maxStack = 99;
        Item.rare = ItemRarityID.Pink;
        Item.value = Item.buyPrice(gold: 2);
        Item.useStyle = ItemUseStyleID.Swing;
        Item.useAnimation = 15;
        Item.useTime = 15;
        Item.consumable = true;
        Item.createTile = ModContent.TileType<StarfallTrophyTile>();
        Item.placeStyle = 17 + BossIndex;
    }
}

public sealed class ResinWhelpRelic : PrimordialRelic { protected override int BossIndex => 0; }
public sealed class CopperveinScarabRelic : PrimordialRelic { protected override int BossIndex => 1; }
public sealed class BellscorpionMatriarchRelic : PrimordialRelic { protected override int BossIndex => 2; }
public sealed class FrosthornStalkerRelic : PrimordialRelic { protected override int BossIndex => 3; }
public sealed class LanternMiretoadRelic : PrimordialRelic { protected override int BossIndex => 4; }
public sealed class WaxenArbiterRelic : PrimordialRelic { protected override int BossIndex => 5; }
public sealed class RotrootWeaverRelic : PrimordialRelic { protected override int BossIndex => 6; }
public sealed class GravebellKeybearerRelic : PrimordialRelic { protected override int BossIndex => 7; }
public sealed class MeteorMawRelic : PrimordialRelic { protected override int BossIndex => 8; }
public sealed class DawnshardScionRelic : PrimordialRelic { protected override int BossIndex => 9; }

public sealed class ResinWhelpTrophy : PrimordialTrophy { protected override int BossIndex => 0; }
public sealed class CopperveinScarabTrophy : PrimordialTrophy { protected override int BossIndex => 1; }
public sealed class BellscorpionMatriarchTrophy : PrimordialTrophy { protected override int BossIndex => 2; }
public sealed class FrosthornStalkerTrophy : PrimordialTrophy { protected override int BossIndex => 3; }
public sealed class LanternMiretoadTrophy : PrimordialTrophy { protected override int BossIndex => 4; }
public sealed class WaxenArbiterTrophy : PrimordialTrophy { protected override int BossIndex => 5; }
public sealed class RotrootWeaverTrophy : PrimordialTrophy { protected override int BossIndex => 6; }
public sealed class GravebellKeybearerTrophy : PrimordialTrophy { protected override int BossIndex => 7; }
public sealed class MeteorMawTrophy : PrimordialTrophy { protected override int BossIndex => 8; }
public sealed class DawnshardScionTrophy : PrimordialTrophy { protected override int BossIndex => 9; }

public abstract class PrimordialWeapon : ModItem
{
    protected abstract int WeaponIndex { get; }
    public override string Texture => $"StarfallThrone/Content/Assets/Items/PrimordialWeapon{WeaponIndex}";

    public override void SetDefaults()
    {
        Item.width = 34;
        Item.height = 34;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.useAnimation = 30;
        Item.useTime = 30;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<PrimordialPlayerProjectile>();
        Item.shootSpeed = 11f;
        Item.UseSound = SoundID.Item20;
        Item.rare = WeaponIndex < 6 ? ItemRarityID.Orange : WeaponIndex < 14 ? ItemRarityID.LightRed : ItemRarityID.Pink;
        Item.value = Item.buyPrice(gold: 8 + WeaponIndex);

        switch (WeaponIndex)
        {
            case 0: Item.damage = 13; Item.useTime = Item.useAnimation = 30; Item.DamageType = DamageClass.Ranged; break;
            case 1: Item.damage = 12; Item.mana = 6; Item.DamageType = DamageClass.Magic; Item.useTime = Item.useAnimation = 28; break;
            case 2: Item.damage = 16; Item.DamageType = DamageClass.Ranged; Item.useTime = Item.useAnimation = 30; break;
            case 3: Item.damage = 20; Item.DamageType = DamageClass.Melee; Item.useTime = Item.useAnimation = 34; break;
            case 4: Item.damage = 22; Item.DamageType = DamageClass.Melee; Item.useTime = Item.useAnimation = 31; break;
            case 5: Item.damage = 19; Item.mana = 7; Item.DamageType = DamageClass.Magic; Item.useTime = Item.useAnimation = 34; break;
            case 6: Item.damage = 25; Item.DamageType = DamageClass.Melee; Item.useTime = Item.useAnimation = 30; break;
            case 7: Item.damage = 22; Item.mana = 8; Item.DamageType = DamageClass.Magic; Item.useTime = Item.useAnimation = 32; break;
            case 8: Item.damage = 26; Item.mana = 9; Item.DamageType = DamageClass.Magic; Item.useTime = Item.useAnimation = 24; break;
            case 9: Item.damage = 20; Item.DamageType = DamageClass.Summon; Item.useTime = Item.useAnimation = 28; break;
            case 10: Item.damage = 28; Item.DamageType = DamageClass.Ranged; Item.useTime = Item.useAnimation = 32; break;
            case 11: Item.damage = 20; Item.DamageType = DamageClass.Summon; Item.useTime = Item.useAnimation = 35; break;
            case 12: Item.damage = 22; Item.DamageType = DamageClass.Melee; Item.useTime = Item.useAnimation = 25; break;
            case 13: Item.damage = 29; Item.mana = 11; Item.DamageType = DamageClass.Magic; Item.useTime = Item.useAnimation = 30; break;
            case 14: Item.damage = 32; Item.DamageType = DamageClass.Melee; Item.useTime = Item.useAnimation = 38; break;
            case 15: Item.damage = 30; Item.DamageType = DamageClass.Ranged; Item.useTime = Item.useAnimation = 34; break;
            case 16: Item.damage = 35; Item.DamageType = DamageClass.Ranged; Item.useTime = Item.useAnimation = 45; break;
            case 17: Item.damage = 33; Item.mana = 12; Item.DamageType = DamageClass.Magic; Item.useTime = Item.useAnimation = 32; break;
            case 18: Item.damage = 38; Item.DamageType = DamageClass.Melee; Item.useTime = Item.useAnimation = 28; break;
            case 19: Item.damage = 36; Item.mana = 15; Item.DamageType = DamageClass.Magic; Item.useTime = Item.useAnimation = 34; break;
            case 20: Item.damage = 35; Item.DamageType = DamageClass.Ranged; Item.useTime = Item.useAnimation = 31; break;
            default: Item.damage = 36; Item.mana = 15; Item.DamageType = DamageClass.Magic; Item.useTime = Item.useAnimation = 34; break;
        }

        Item.knockBack = 2.5f + WeaponIndex * 0.08f;
        Item.shootSpeed += WeaponIndex * 0.15f;
        ReforgedWeapons.Configure(Item, WeaponIndex);
    }

    public override bool Shoot(Player player, Terraria.DataStructures.EntitySource_ItemUse_WithAmmo source, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Vector2 velocity, int type, int damage, float knockback)
    {
        return ReforgedWeapons.Shoot(Item, WeaponIndex, player, source, position, velocity, damage, knockback);
    }
    public override bool CanUseItem(Player player) => ReforgedWeapons.CanUse(player, WeaponIndex);
    public override bool MeleePrefix() => WeaponIndex == 9 || base.MeleePrefix();
}

public sealed class ResinShortbow : PrimordialWeapon { protected override int WeaponIndex => 0; }
public sealed class ResinCane : PrimordialWeapon { protected override int WeaponIndex => 1; }
public sealed class CopperveinRepeater : PrimordialWeapon { protected override int WeaponIndex => 2; }
public sealed class MinebreakerDrill : PrimordialWeapon { protected override int WeaponIndex => 3; }
public sealed class BellstingSpear : PrimordialWeapon { protected override int WeaponIndex => 4; }
public sealed class SandglassStaff : PrimordialWeapon { protected override int WeaponIndex => 5; }
public sealed class FrostfangBlade : PrimordialWeapon { protected override int WeaponIndex => 6; }
public sealed class FrostHowl : PrimordialWeapon { protected override int WeaponIndex => 7; }
public sealed class MireSprayer : PrimordialWeapon { protected override int WeaponIndex => 8; }
public sealed class TongueWhip : PrimordialWeapon { protected override int WeaponIndex => 9; }
public sealed class WaxenRepeater : PrimordialWeapon { protected override int WeaponIndex => 10; }
public sealed class WaxVerdict : PrimordialWeapon { protected override int WeaponIndex => 11; }
public sealed class RootlashHook : PrimordialWeapon { protected override int WeaponIndex => 12; }
public sealed class RotrootStaff : PrimordialWeapon { protected override int WeaponIndex => 13; }
public sealed class GravebellFlail : PrimordialWeapon { protected override int WeaponIndex => 14; }
public sealed class KeybearerCrossbow : PrimordialWeapon { protected override int WeaponIndex => 15; }
public sealed class MeteorMawCannon : PrimordialWeapon { protected override int WeaponIndex => 16; }
public sealed class StarEaterStaff : PrimordialWeapon { protected override int WeaponIndex => 17; }
public sealed class DawnstarEdge : PrimordialWeapon { protected override int WeaponIndex => 18; }
public sealed class FirstlightCodex : PrimordialWeapon { protected override int WeaponIndex => 19; }
public sealed class DawnpiercerBow : PrimordialWeapon { protected override int WeaponIndex => 20; }

public abstract class PrimordialAccessory : ModItem
{
    protected abstract int AccessoryIndex { get; }
    public override string Texture => $"StarfallThrone/Content/Assets/Items/PrimordialAccessory{AccessoryIndex}";

    public override void SetDefaults()
    {
        Item.width = 32;
        Item.height = 32;
        Item.accessory = true;
        Item.expert = true;
        Item.rare = ItemRarityID.Expert;
        Item.value = Item.buyPrice(gold: 4 + AccessoryIndex);
    }

    public override void UpdateAccessory(Player player, bool hideVisual)
    {
        switch (AccessoryIndex)
        {
            case 0:
                player.buffImmune[BuffID.Slow] = true;
                player.GetModPlayer<PrimordialPlayer>().ResinHeartEquipped = true;
                player.moveSpeed += 0.05f;
                break;
            case 1:
                player.pickSpeed -= 0.10f;
                break;
            case 2:
                player.moveSpeed += 0.12f;
                player.accRunSpeed += 0.5f;
                break;
            case 3:
                player.buffImmune[BuffID.Chilled] = true;
                player.moveSpeed += 0.08f;
                break;
            case 4:
                player.gills = true;
                player.accFlipper = true;
                player.buffImmune[BuffID.Poisoned] = true;
                break;
            case 5:
                player.maxMinions += 1;
                player.wingTimeMax += 30;
                break;
            case 6:
                player.GetModPlayer<PrimordialPlayer>().RootboundTalismanEquipped = true;
                break;
            case 7:
                player.endurance += 0.02f;
                break;
            case 8:
                player.noFallDmg = true;
                player.GetDamage(DamageClass.Ranged) *= 1.04f;
                player.GetDamage(DamageClass.Magic) *= 1.04f;
                break;
            default:
                player.GetModPlayer<PrimordialPlayer>().DawnshardCoreEquipped = true;
                player.GetDamage(DamageClass.Generic) *= 1.05f;
                break;
        }
    }
}

public sealed class ResinHeart : PrimordialAccessory { protected override int AccessoryIndex => 0; }
public sealed class OreseekerScanner : PrimordialAccessory { protected override int AccessoryIndex => 1; }
public sealed class SandstepAnklet : PrimordialAccessory { protected override int AccessoryIndex => 2; }
public sealed class FroststepMantle : PrimordialAccessory { protected override int AccessoryIndex => 3; }
public sealed class MirelungCharm : PrimordialAccessory { protected override int AccessoryIndex => 4; }
public sealed class WaxwingSigil : PrimordialAccessory { protected override int AccessoryIndex => 5; }
public sealed class RootboundTalisman : PrimordialAccessory { protected override int AccessoryIndex => 6; }
public sealed class GatekeeperAmulet : PrimordialAccessory { protected override int AccessoryIndex => 7; }
public sealed class MagneticHarness : PrimordialAccessory { protected override int AccessoryIndex => 8; }
public sealed class DawnshardCrown : PrimordialAccessory { protected override int AccessoryIndex => 9; }

[AutoloadEquip(EquipType.Head)]
public sealed class ResinweaveHelmet : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Items/PrimordialResinHelmet";
    public override void SetDefaults() { Item.width = 32; Item.height = 26; Item.defense = 2; Item.rare = ItemRarityID.Green; }
    public override bool IsArmorSet(Item head, Item body, Item legs) => body.type == ModContent.ItemType<ResinweaveChest>() && legs.type == ModContent.ItemType<ResinweaveGreaves>();
    public override void UpdateArmorSet(Player player) { player.setBonus = Terraria.Localization.Language.GetTextValue("Mods.StarfallThrone.Items.ResinweaveHelmet.SetBonus"); player.moveSpeed += 0.05f; player.buffImmune[BuffID.Slow] = true; }
    public override void AddRecipes() => CreateRecipe().AddIngredient<ResinCore>().AddIngredient(ItemID.Wood, 15).AddIngredient(ItemID.Gel, 10).AddTile(TileID.WorkBenches).Register();
}

[AutoloadEquip(EquipType.Body)]
public sealed class ResinweaveChest : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Items/PrimordialResinChest";
    public override void SetDefaults() { Item.width = 34; Item.height = 32; Item.defense = 3; Item.rare = ItemRarityID.Green; }
    public override void AddRecipes() => CreateRecipe().AddIngredient<ResinCore>().AddIngredient(ItemID.Wood, 20).AddIngredient(ItemID.Gel, 15).AddTile(TileID.WorkBenches).Register();
}

[AutoloadEquip(EquipType.Legs)]
public sealed class ResinweaveGreaves : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Items/PrimordialResinGreaves";
    public override void SetDefaults() { Item.width = 34; Item.height = 32; Item.defense = 2; Item.rare = ItemRarityID.Green; }
    public override void AddRecipes() => CreateRecipe().AddIngredient<ResinCore>().AddIngredient(ItemID.Wood, 15).AddIngredient(ItemID.Gel, 10).AddTile(TileID.WorkBenches).Register();
}

[AutoloadEquip(EquipType.Head)]
public sealed class CopperveinHelmet : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Items/PrimordialCopperHelmet";
    public override void SetDefaults() { Item.width = 32; Item.height = 26; Item.defense = 3; Item.rare = ItemRarityID.Green; }
    public override bool IsArmorSet(Item head, Item body, Item legs) => body.type == ModContent.ItemType<CopperveinChest>() && legs.type == ModContent.ItemType<CopperveinGreaves>();
    public override void UpdateArmorSet(Player player) { player.setBonus = Terraria.Localization.Language.GetTextValue("Mods.StarfallThrone.Items.CopperveinHelmet.SetBonus"); player.pickSpeed -= 0.15f; }
    public override void AddRecipes() => CreateRecipe().AddIngredient<CopperveinCore>().AddIngredient(ItemID.CopperBar, 10).AddIngredient(ItemID.StoneBlock, 15).AddTile(TileID.Anvils).Register();
}

[AutoloadEquip(EquipType.Body)]
public sealed class CopperveinChest : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Items/PrimordialCopperChest";
    public override void SetDefaults() { Item.width = 34; Item.height = 32; Item.defense = 5; Item.rare = ItemRarityID.Green; }
    public override void AddRecipes() => CreateRecipe().AddIngredient<CopperveinCore>().AddIngredient(ItemID.CopperBar, 15).AddIngredient(ItemID.StoneBlock, 25).AddTile(TileID.Anvils).Register();
}

[AutoloadEquip(EquipType.Legs)]
public sealed class CopperveinGreaves : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Items/PrimordialCopperGreaves";
    public override void SetDefaults() { Item.width = 34; Item.height = 32; Item.defense = 3; Item.rare = ItemRarityID.Green; }
    public override void AddRecipes() => CreateRecipe().AddIngredient<CopperveinCore>().AddIngredient(ItemID.CopperBar, 10).AddIngredient(ItemID.StoneBlock, 15).AddTile(TileID.Anvils).Register();
}

[AutoloadEquip(EquipType.Head)]
public sealed class FrostmireHood : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Items/PrimordialFrostmireHelmet";
    public override void SetDefaults() { Item.width = 32; Item.height = 26; Item.defense = 4; Item.rare = ItemRarityID.Orange; }
    public override bool IsArmorSet(Item head, Item body, Item legs) => body.type == ModContent.ItemType<FrostmireCoat>() && legs.type == ModContent.ItemType<FrostmireGreaves>();
    public override void UpdateArmorSet(Player player) { player.setBonus = Terraria.Localization.Language.GetTextValue("Mods.StarfallThrone.Items.FrostmireHood.SetBonus"); player.moveSpeed += 0.08f; player.buffImmune[BuffID.Chilled] = true; player.GetCritChance(DamageClass.Generic) += 3; }
    public override void AddRecipes() => CreateRecipe().AddIngredient<FrosthornCore>().AddIngredient(ItemID.IceBlock, 15).AddIngredient(ItemID.MudBlock, 10).AddIngredient(ItemID.GlowingMushroom, 3).AddTile(TileID.WorkBenches).Register();
}

[AutoloadEquip(EquipType.Body)]
public sealed class FrostmireCoat : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Items/PrimordialFrostmireChest";
    public override void SetDefaults() { Item.width = 34; Item.height = 32; Item.defense = 6; Item.rare = ItemRarityID.Orange; }
    public override void AddRecipes() => CreateRecipe().AddIngredient<FrosthornCore>().AddIngredient(ItemID.IceBlock, 20).AddIngredient(ItemID.MudBlock, 15).AddIngredient(ItemID.GlowingMushroom, 5).AddTile(TileID.WorkBenches).Register();
}

[AutoloadEquip(EquipType.Legs)]
public sealed class FrostmireGreaves : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Items/PrimordialFrostmireGreaves";
    public override void SetDefaults() { Item.width = 34; Item.height = 32; Item.defense = 4; Item.rare = ItemRarityID.Orange; }
    public override void AddRecipes() => CreateRecipe().AddIngredient<FrosthornCore>().AddIngredient(ItemID.IceBlock, 15).AddIngredient(ItemID.MudBlock, 10).AddIngredient(ItemID.GlowingMushroom, 3).AddTile(TileID.WorkBenches).Register();
}

[AutoloadEquip(EquipType.Head)]
public sealed class WaxguardMask : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Items/PrimordialWaxHelmet";
    public override void SetDefaults() { Item.width = 32; Item.height = 26; Item.defense = 5; Item.rare = ItemRarityID.LightRed; }
    public override bool IsArmorSet(Item head, Item body, Item legs) => body.type == ModContent.ItemType<WaxguardChest>() && legs.type == ModContent.ItemType<WaxguardGreaves>();
    public override void UpdateArmorSet(Player player) { player.setBonus = Terraria.Localization.Language.GetTextValue("Mods.StarfallThrone.Items.WaxguardMask.SetBonus"); player.maxMinions += 1; player.GetDamage(DamageClass.Summon) *= 1.05f; }
    public override void AddRecipes() => CreateRecipe().AddIngredient<WaxenCore>().AddIngredient(ItemID.BeeWax, 8).AddIngredient(ItemID.Hive, 10).AddTile(TileID.Anvils).Register();
}

[AutoloadEquip(EquipType.Body)]
public sealed class WaxguardChest : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Items/PrimordialWaxChest";
    public override void SetDefaults() { Item.width = 34; Item.height = 32; Item.defense = 7; Item.rare = ItemRarityID.LightRed; }
    public override void AddRecipes() => CreateRecipe().AddIngredient<WaxenCore>().AddIngredient(ItemID.BeeWax, 10).AddIngredient(ItemID.Hive, 15).AddTile(TileID.Anvils).Register();
}

[AutoloadEquip(EquipType.Legs)]
public sealed class WaxguardGreaves : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Items/PrimordialWaxGreaves";
    public override void SetDefaults() { Item.width = 34; Item.height = 32; Item.defense = 5; Item.rare = ItemRarityID.LightRed; }
    public override void AddRecipes() => CreateRecipe().AddIngredient<WaxenCore>().AddIngredient(ItemID.BeeWax, 8).AddIngredient(ItemID.Hive, 10).AddTile(TileID.Anvils).Register();
}

[AutoloadEquip(EquipType.Head)]
public sealed class AstralExplorerHelmet : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Items/PrimordialAstralHelmet";
    public override void SetDefaults() { Item.width = 32; Item.height = 26; Item.defense = 6; Item.rare = ItemRarityID.Pink; }
    public override bool IsArmorSet(Item head, Item body, Item legs) => body.type == ModContent.ItemType<AstralExplorerChest>() && legs.type == ModContent.ItemType<AstralExplorerGreaves>();
    public override void UpdateArmorSet(Player player) { player.setBonus = Terraria.Localization.Language.GetTextValue("Mods.StarfallThrone.Items.AstralExplorerHelmet.SetBonus"); player.GetDamage(DamageClass.Generic) *= 1.05f; player.jumpSpeedBoost += 1.2f; player.noFallDmg = true; }
    public override void AddRecipes() => CreateRecipe().AddIngredient<MeteorMawCore>(2).AddIngredient<DawnshardCore>().AddIngredient(ItemID.MeteoriteBar, 10).AddIngredient(ItemID.FallenStar, 5).AddTile(TileID.DemonAltar).Register();
}

[AutoloadEquip(EquipType.Body)]
public sealed class AstralExplorerChest : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Items/PrimordialAstralChest";
    public override void SetDefaults() { Item.width = 34; Item.height = 32; Item.defense = 8; Item.rare = ItemRarityID.Pink; }
    public override void AddRecipes() => CreateRecipe().AddIngredient<MeteorMawCore>(2).AddIngredient<DawnshardCore>().AddIngredient(ItemID.MeteoriteBar, 15).AddIngredient(ItemID.FallenStar, 5).AddTile(TileID.DemonAltar).Register();
}

[AutoloadEquip(EquipType.Legs)]
public sealed class AstralExplorerGreaves : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Items/PrimordialAstralGreaves";
    public override void SetDefaults() { Item.width = 34; Item.height = 32; Item.defense = 6; Item.rare = ItemRarityID.Pink; }
    public override void AddRecipes() => CreateRecipe().AddIngredient<MeteorMawCore>(2).AddIngredient<DawnshardCore>().AddIngredient(ItemID.MeteoriteBar, 10).AddIngredient(ItemID.FallenStar, 5).AddTile(TileID.DemonAltar).Register();
}

public static class PrimordialDropCatalog
{
    public static int CoreType(int index) => index switch
    {
        0 => ModContent.ItemType<ResinCore>(), 1 => ModContent.ItemType<CopperveinCore>(), 2 => ModContent.ItemType<SandscorpionCore>(),
        3 => ModContent.ItemType<FrosthornCore>(), 4 => ModContent.ItemType<MirelanternCore>(), 5 => ModContent.ItemType<WaxenCore>(),
        6 => ModContent.ItemType<RotrootCore>(), 7 => ModContent.ItemType<GravebellCore>(), 8 => ModContent.ItemType<MeteorMawCore>(), _ => ModContent.ItemType<DawnshardCore>()
    };

    public static int WeaponOneType(int index) => index switch
    {
        0 => ModContent.ItemType<ResinShortbow>(), 1 => ModContent.ItemType<CopperveinRepeater>(), 2 => ModContent.ItemType<BellstingSpear>(),
        3 => ModContent.ItemType<FrostfangBlade>(), 4 => ModContent.ItemType<MireSprayer>(), 5 => ModContent.ItemType<WaxenRepeater>(),
        6 => ModContent.ItemType<RootlashHook>(), 7 => ModContent.ItemType<GravebellFlail>(), 8 => ModContent.ItemType<MeteorMawCannon>(), _ => ModContent.ItemType<DawnstarEdge>()
    };

    public static int WeaponTwoType(int index) => index switch
    {
        0 => ModContent.ItemType<ResinCane>(), 1 => ModContent.ItemType<MinebreakerDrill>(), 2 => ModContent.ItemType<SandglassStaff>(),
        3 => ModContent.ItemType<FrostHowl>(), 4 => ModContent.ItemType<TongueWhip>(), 5 => ModContent.ItemType<WaxVerdict>(),
        6 => ModContent.ItemType<RotrootStaff>(), 7 => ModContent.ItemType<KeybearerCrossbow>(), 8 => ModContent.ItemType<StarEaterStaff>(), _ => ModContent.ItemType<FirstlightCodex>()
    };

    public static int WeaponThreeType(int index) => ModContent.ItemType<DawnpiercerBow>();

    public static int AccessoryType(int index) => index switch
    {
        0 => ModContent.ItemType<ResinHeart>(), 1 => ModContent.ItemType<OreseekerScanner>(), 2 => ModContent.ItemType<SandstepAnklet>(),
        3 => ModContent.ItemType<FroststepMantle>(), 4 => ModContent.ItemType<MirelungCharm>(), 5 => ModContent.ItemType<WaxwingSigil>(),
        6 => ModContent.ItemType<RootboundTalisman>(), 7 => ModContent.ItemType<GatekeeperAmulet>(), 8 => ModContent.ItemType<MagneticHarness>(), _ => ModContent.ItemType<DawnshardCrown>()
    };

    public static int BagType(int index) => index switch
    {
        0 => ModContent.ItemType<ResinWhelpBag>(), 1 => ModContent.ItemType<CopperveinScarabBag>(), 2 => ModContent.ItemType<BellscorpionMatriarchBag>(),
        3 => ModContent.ItemType<FrosthornStalkerBag>(), 4 => ModContent.ItemType<LanternMiretoadBag>(), 5 => ModContent.ItemType<WaxenArbiterBag>(),
        6 => ModContent.ItemType<RotrootWeaverBag>(), 7 => ModContent.ItemType<GravebellKeybearerBag>(), 8 => ModContent.ItemType<MeteorMawBag>(), _ => ModContent.ItemType<DawnshardScionBag>()
    };

    public static int RelicType(int index) => index switch
    {
        0 => ModContent.ItemType<ResinWhelpRelic>(), 1 => ModContent.ItemType<CopperveinScarabRelic>(), 2 => ModContent.ItemType<BellscorpionMatriarchRelic>(),
        3 => ModContent.ItemType<FrosthornStalkerRelic>(), 4 => ModContent.ItemType<LanternMiretoadRelic>(), 5 => ModContent.ItemType<WaxenArbiterRelic>(),
        6 => ModContent.ItemType<RotrootWeaverRelic>(), 7 => ModContent.ItemType<GravebellKeybearerRelic>(), 8 => ModContent.ItemType<MeteorMawRelic>(), _ => ModContent.ItemType<DawnshardScionRelic>()
    };

    public static int TrophyType(int index) => index switch
    {
        0 => ModContent.ItemType<ResinWhelpTrophy>(), 1 => ModContent.ItemType<CopperveinScarabTrophy>(), 2 => ModContent.ItemType<BellscorpionMatriarchTrophy>(),
        3 => ModContent.ItemType<FrosthornStalkerTrophy>(), 4 => ModContent.ItemType<LanternMiretoadTrophy>(), 5 => ModContent.ItemType<WaxenArbiterTrophy>(),
        6 => ModContent.ItemType<RotrootWeaverTrophy>(), 7 => ModContent.ItemType<GravebellKeybearerTrophy>(), 8 => ModContent.ItemType<MeteorMawTrophy>(), _ => ModContent.ItemType<DawnshardScionTrophy>()
    };

    public static int NPCType(int index) => index switch
    {
        0 => ModContent.NPCType<ResinWhelpNPC>(), 1 => ModContent.NPCType<CopperveinScarabNPC>(), 2 => ModContent.NPCType<BellscorpionMatriarchNPC>(),
        3 => ModContent.NPCType<FrosthornStalkerNPC>(), 4 => ModContent.NPCType<LanternMiretoadNPC>(), 5 => ModContent.NPCType<WaxenArbiterNPC>(),
        6 => ModContent.NPCType<RotrootWeaverNPC>(), 7 => ModContent.NPCType<GravebellKeybearerNPC>(), 8 => ModContent.NPCType<MeteorMawNPC>(), _ => ModContent.NPCType<DawnshardScionNPC>()
    };
}
