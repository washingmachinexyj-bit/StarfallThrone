using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent.ItemDropRules;
using StarfallThrone.Content.NPCs;
using StarfallThrone.Content.Projectiles;
using StarfallThrone.Content.Systems;
using StarfallThrone.Content.Tiles;

namespace StarfallThrone.Content.Items;

public abstract class StarfallCoreItem : ModItem
{
    protected abstract int BossIndex { get; }
    public override string Texture => $"StarfallThrone/Content/Assets/Items/Core{BossIndex}";

    public override void SetDefaults()
    {
        Item.width = 32;
        Item.height = 32;
        Item.maxStack = 9999;
        Item.value = Item.buyPrice(gold: 8);
        Item.rare = ItemRarityID.Red;
        Item.material = true;
    }
}

public sealed class ImperialGel : StarfallCoreItem { protected override int BossIndex => 0; }
public sealed class PhaseIris : StarfallCoreItem { protected override int BossIndex => 1; }
public sealed class VoidSegmentCrystal : StarfallCoreItem { protected override int BossIndex => 2; }
public sealed class CrimsonNeuralCore : StarfallCoreItem { protected override int BossIndex => 3; }
public sealed class BeeGodHoney : StarfallCoreItem { protected override int BossIndex => 4; }
public sealed class JudgmentMarrow : StarfallCoreItem { protected override int BossIndex => 5; }
public sealed class EternalFlesh : StarfallCoreItem { protected override int BossIndex => 6; }
public sealed class PrismCrown : StarfallCoreItem { protected override int BossIndex => 7; }
public sealed class TwinEyeCore : StarfallCoreItem { protected override int BossIndex => 8; }
public sealed class PrimeGear : StarfallCoreItem { protected override int BossIndex => 9; }
public sealed class StarEaterMagnet : StarfallCoreItem { protected override int BossIndex => 10; }
public sealed class WastelandFlowerCore : StarfallCoreItem { protected override int BossIndex => 11; }
public sealed class TempleStarCore : StarfallCoreItem { protected override int BossIndex => 12; }
public sealed class AbyssScaleHeart : StarfallCoreItem { protected override int BossIndex => 13; }
public sealed class CoronaPrism : StarfallCoreItem { protected override int BossIndex => 14; }
public sealed class AstralDoctrine : StarfallCoreItem { protected override int BossIndex => 15; }
public sealed class MoonGodCore : StarfallCoreItem { protected override int BossIndex => 16; }

public sealed class StarfallResidue : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Items/Residue";
    public override void SetDefaults()
    {
        Item.width = 28;
        Item.height = 28;
        Item.maxStack = 9999;
        Item.value = Item.buyPrice(silver: 50);
        Item.rare = ItemRarityID.Pink;
        Item.material = true;
    }
}

public sealed class AstralIngot : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Items/AstralIngot";
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 25;
    public override void SetDefaults()
    {
        Item.width = 32;
        Item.height = 32;
        Item.maxStack = 9999;
        Item.value = Item.buyPrice(gold: 20);
        Item.rare = ItemRarityID.Red;
        Item.material = true;
    }
    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient(ItemID.LunarBar, 5)
            .AddIngredient(ItemID.FragmentSolar, 2)
            .AddIngredient(ItemID.FragmentVortex, 2)
            .AddIngredient(ItemID.FragmentNebula, 2)
            .AddIngredient(ItemID.FragmentStardust, 2)
            .AddIngredient<StarfallResidue>(5)
            .AddTile(TileID.LunarCraftingStation)
            .Register();
    }
}

public abstract class StarfallSummonItem : ModItem
{
    protected abstract int BossIndex { get; }
    public override string Texture => $"StarfallThrone/Content/Assets/Items/Summon{BossIndex}";

    public override void SetDefaults()
    {
        Item.width = 40;
        Item.height = 40;
        Item.maxStack = 20;
        Item.consumable = true;
        Item.useStyle = ItemUseStyleID.HoldUp;
        Item.useTime = 30;
        Item.useAnimation = 30;
        Item.UseSound = SoundID.Item44;
        Item.rare = ItemRarityID.Red;
        Item.value = Item.buyPrice(gold: 10);
    }

    public override bool CanUseItem(Player player)
    {
        return StarfallWorld.CanSummon(BossIndex) && ValidLocation(player) && !NPC.AnyNPCs(BossNPCType(BossIndex));
    }

    private bool ValidLocation(Player player) => BossIndex switch
    {
        5 => Main.dayTime == false,
        6 => player.ZoneUnderworldHeight,
        11 => player.ZoneJungle && !player.ZoneOverworldHeight,
        12 => player.ZoneDungeon,
        13 => player.ZoneBeach,
        14 => player.ZoneHallow,
        15 => player.ZoneDungeon,
        _ => true
    };

    public override bool? UseItem(Player player)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            NPC.SpawnOnPlayer(player.whoAmI, BossNPCType(BossIndex));
        return true;
    }

    private static int BossNPCType(int index) => index switch
    {
        0 => ModContent.NPCType<SlimeEmperorNPC>(), 1 => ModContent.NPCType<TerminalEyeNPC>(), 2 => ModContent.NPCType<InfiniteDevourerNPC>(),
        3 => ModContent.NPCType<CrimsonMindNPC>(), 4 => ModContent.NPCType<BeeMatriarchNPC>(), 5 => ModContent.NPCType<SkeletalJudgeNPC>(),
        6 => ModContent.NPCType<EternalWallNPC>(), 7 => ModContent.NPCType<DynastyQueenNPC>(), 8 => ModContent.NPCType<TwinCalamityNPC>(),
        9 => ModContent.NPCType<PrimeVerdictNPC>(), 10 => ModContent.NPCType<StarEaterNPC>(), 11 => ModContent.NPCType<WastelandFlowerNPC>(),
        12 => ModContent.NPCType<TempleCoreNPC>(), 13 => ModContent.NPCType<AbyssalDukeNPC>(), 14 => ModContent.NPCType<CoronaEmpressNPC>(),
        15 => ModContent.NPCType<AstralPontiffNPC>(), _ => ModContent.NPCType<EndMoonNPC>()
    };
}

public sealed class EmperorCrown : StarfallSummonItem
{
    protected override int BossIndex => 0;
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.SlimeCrown).AddIngredient<AstralIngot>(4).AddIngredient(ItemID.Gel, 200).AddIngredient(ItemID.LunarBar, 5).AddTile(TileID.LunarCraftingStation).Register();
}
public sealed class TerminalEye : StarfallSummonItem
{
    protected override int BossIndex => 1;
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.SuspiciousLookingEye).AddIngredient<ImperialGel>(15).AddIngredient<AstralIngot>(4).AddTile(TileID.LunarCraftingStation).Register();
}
public sealed class InfiniteBait : StarfallSummonItem
{
    protected override int BossIndex => 2;
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.WormFood).AddIngredient<PhaseIris>(12).AddIngredient(ItemID.RottenChunk, 30).AddIngredient<AstralIngot>(5).AddTile(TileID.LunarCraftingStation).Register();
}
public sealed class CrimsonHeartSummon : StarfallSummonItem
{
    protected override int BossIndex => 3;
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.BloodySpine).AddIngredient<VoidSegmentCrystal>(12).AddIngredient(ItemID.TissueSample, 30).AddIngredient<AstralIngot>(5).AddTile(TileID.LunarCraftingStation).Register();
}
public sealed class RoyalBeeSigil : StarfallSummonItem
{
    protected override int BossIndex => 4;
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.Abeemination).AddIngredient<CrimsonNeuralCore>(12).AddIngredient(ItemID.BeeWax, 20).AddIngredient(ItemID.HoneyBlock, 50).AddIngredient<AstralIngot>(6).AddTile(TileID.LunarCraftingStation).Register();
}
public sealed class JudgmentBoneflute : StarfallSummonItem
{
    protected override int BossIndex => 5;
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.Bone, 200).AddIngredient<BeeGodHoney>(12).AddIngredient<AstralIngot>(6).AddTile(TileID.LunarCraftingStation).Register();
}
public sealed class EternalVoodooDoll : StarfallSummonItem
{
    protected override int BossIndex => 6;
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.GuideVoodooDoll).AddIngredient<JudgmentMarrow>(12).AddIngredient(ItemID.Hellstone, 100).AddIngredient<AstralIngot>(7).AddTile(TileID.LunarCraftingStation).Register();
}
public sealed class DynastyPrismSummon : StarfallSummonItem
{
    protected override int BossIndex => 7;
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.CrystalShard, 50).AddIngredient<EternalFlesh>(12).AddIngredient(ItemID.Gel, 100).AddIngredient<AstralIngot>(7).AddTile(TileID.LunarCraftingStation).Register();
}
public sealed class TwinOmen : StarfallSummonItem
{
    protected override int BossIndex => 8;
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.MechanicalEye).AddIngredient<PrismCrown>(12).AddIngredient(ItemID.SoulofSight, 20).AddIngredient<AstralIngot>(8).AddTile(TileID.LunarCraftingStation).Register();
}
public sealed class PrimeVerdictSummon : StarfallSummonItem
{
    protected override int BossIndex => 9;
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.MechanicalSkull).AddIngredient<TwinEyeCore>(12).AddIngredient(ItemID.SoulofFright, 20).AddIngredient<AstralIngot>(8).AddTile(TileID.LunarCraftingStation).Register();
}
public sealed class StarEaterWorm : StarfallSummonItem
{
    protected override int BossIndex => 10;
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.MechanicalWorm).AddIngredient<PrimeGear>(12).AddIngredient(ItemID.SoulofMight, 20).AddIngredient<AstralIngot>(9).AddTile(TileID.LunarCraftingStation).Register();
}
public sealed class WastelandBudSummon : StarfallSummonItem
{
    protected override int BossIndex => 11;
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.JungleSpores, 100).AddIngredient(ItemID.ChlorophyteBar, 50).AddIngredient<StarEaterMagnet>(12).AddIngredient<AstralIngot>(9).AddTile(TileID.LunarCraftingStation).Register();
}
public sealed class TempleStarCoreSummon : StarfallSummonItem
{
    protected override int BossIndex => 12;
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.LihzahrdPowerCell).AddIngredient<WastelandFlowerCore>(12).AddIngredient(ItemID.FragmentSolar, 20).AddIngredient<AstralIngot>(10).AddTile(TileID.LunarCraftingStation).Register();
}
public sealed class AbyssLure : StarfallSummonItem
{
    protected override int BossIndex => 13;
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.TruffleWorm).AddIngredient<TempleStarCore>(12).AddIngredient(ItemID.SharkFin, 50).AddIngredient<AstralIngot>(10).AddTile(TileID.LunarCraftingStation).Register();
}
public sealed class CoronaButterfly : StarfallSummonItem
{
    protected override int BossIndex => 14;
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.EmpressButterfly).AddIngredient<AbyssScaleHeart>(12).AddIngredient(ItemID.PixieDust, 100).AddIngredient<AstralIngot>(11).AddTile(TileID.LunarCraftingStation).Register();
}
public sealed class AstralScripture : StarfallSummonItem
{
    protected override int BossIndex => 15;
    public override void AddRecipes() => CreateRecipe().AddIngredient<CoronaPrism>(12).AddIngredient(ItemID.FragmentSolar, 15).AddIngredient(ItemID.FragmentVortex, 15).AddIngredient(ItemID.FragmentNebula, 15).AddIngredient(ItemID.FragmentStardust, 15).AddIngredient<AstralIngot>(12).AddTile(TileID.LunarCraftingStation).Register();
}
public sealed class EndMoonRite : StarfallSummonItem
{
    protected override int BossIndex => 16;
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.CelestialSigil).AddIngredient<AstralDoctrine>(20).AddIngredient<AstralIngot>(20).AddIngredient(ItemID.LunarBar, 100).AddIngredient(ItemID.FragmentSolar, 25).AddIngredient(ItemID.FragmentVortex, 25).AddIngredient(ItemID.FragmentNebula, 25).AddIngredient(ItemID.FragmentStardust, 25).AddTile(TileID.LunarCraftingStation).Register();
}

public abstract class StarfallTreasureBagBase : ModItem
{
    protected abstract int BossIndex { get; }
    public override string Texture => $"StarfallThrone/Content/Assets/Items/Bag{BossIndex}";
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
        Item.value = Item.buyPrice(gold: 20);
    }
    public override bool CanRightClick() => true;
    public override void ModifyItemLoot(ItemLoot itemLoot)
    {
        itemLoot.Add(ItemDropRule.Common(BossDropCatalog.CoreType(BossIndex), 1, 20 + BossIndex, 35 + BossIndex));
        itemLoot.Add(ItemDropRule.Common(BossDropCatalog.WeaponType(BossIndex), 1));
        itemLoot.Add(ItemDropRule.Common(BossDropCatalog.AccessoryType(BossIndex), 1));
        itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<StarfallResidue>(), 1, 20, 50));
    }
}

public sealed class SlimeEmperorBag : StarfallTreasureBagBase { protected override int BossIndex => 0; }
public sealed class TerminalEyeBag : StarfallTreasureBagBase { protected override int BossIndex => 1; }
public sealed class InfiniteDevourerBag : StarfallTreasureBagBase { protected override int BossIndex => 2; }
public sealed class CrimsonMindBag : StarfallTreasureBagBase { protected override int BossIndex => 3; }
public sealed class BeeMatriarchBag : StarfallTreasureBagBase { protected override int BossIndex => 4; }
public sealed class SkeletalJudgeBag : StarfallTreasureBagBase { protected override int BossIndex => 5; }
public sealed class EternalWallBag : StarfallTreasureBagBase { protected override int BossIndex => 6; }
public sealed class DynastyQueenBag : StarfallTreasureBagBase { protected override int BossIndex => 7; }
public sealed class TwinCalamityBag : StarfallTreasureBagBase { protected override int BossIndex => 8; }
public sealed class PrimeVerdictBag : StarfallTreasureBagBase { protected override int BossIndex => 9; }
public sealed class StarEaterBag : StarfallTreasureBagBase { protected override int BossIndex => 10; }
public sealed class WastelandFlowerBag : StarfallTreasureBagBase { protected override int BossIndex => 11; }
public sealed class TempleCoreBag : StarfallTreasureBagBase { protected override int BossIndex => 12; }
public sealed class AbyssalDukeBag : StarfallTreasureBagBase { protected override int BossIndex => 13; }
public sealed class CoronaEmpressBag : StarfallTreasureBagBase { protected override int BossIndex => 14; }
public sealed class AstralPontiffBag : StarfallTreasureBagBase { protected override int BossIndex => 15; }
public sealed class EndMoonBag : StarfallTreasureBagBase { protected override int BossIndex => 16; }

public abstract class StarfallRelicBase : ModItem
{
    protected abstract int BossIndex { get; }
    public override string Texture => $"StarfallThrone/Content/Assets/Items/Relic{BossIndex}";
    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 1;
    }
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
        Item.placeStyle = BossIndex;
    }
}

public sealed class SlimeEmperorRelic : StarfallRelicBase { protected override int BossIndex => 0; }
public sealed class TerminalEyeRelic : StarfallRelicBase { protected override int BossIndex => 1; }
public sealed class InfiniteDevourerRelic : StarfallRelicBase { protected override int BossIndex => 2; }
public sealed class CrimsonMindRelic : StarfallRelicBase { protected override int BossIndex => 3; }
public sealed class BeeMatriarchRelic : StarfallRelicBase { protected override int BossIndex => 4; }
public sealed class SkeletalJudgeRelic : StarfallRelicBase { protected override int BossIndex => 5; }
public sealed class EternalWallRelic : StarfallRelicBase { protected override int BossIndex => 6; }
public sealed class DynastyQueenRelic : StarfallRelicBase { protected override int BossIndex => 7; }
public sealed class TwinCalamityRelic : StarfallRelicBase { protected override int BossIndex => 8; }
public sealed class PrimeVerdictRelic : StarfallRelicBase { protected override int BossIndex => 9; }
public sealed class StarEaterRelic : StarfallRelicBase { protected override int BossIndex => 10; }
public sealed class WastelandFlowerRelic : StarfallRelicBase { protected override int BossIndex => 11; }
public sealed class TempleCoreRelic : StarfallRelicBase { protected override int BossIndex => 12; }
public sealed class AbyssalDukeRelic : StarfallRelicBase { protected override int BossIndex => 13; }
public sealed class CoronaEmpressRelic : StarfallRelicBase { protected override int BossIndex => 14; }
public sealed class AstralPontiffRelic : StarfallRelicBase { protected override int BossIndex => 15; }
public sealed class EndMoonRelic : StarfallRelicBase { protected override int BossIndex => 16; }

public abstract class StarfallTrophyBase : ModItem
{
    protected abstract int BossIndex { get; }
    public override string Texture => $"StarfallThrone/Content/Assets/Items/Trophy{BossIndex}";
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
        Item.placeStyle = BossIndex;
    }
}

public sealed class SlimeEmperorTrophy : StarfallTrophyBase { protected override int BossIndex => 0; }
public sealed class TerminalEyeTrophy : StarfallTrophyBase { protected override int BossIndex => 1; }
public sealed class InfiniteDevourerTrophy : StarfallTrophyBase { protected override int BossIndex => 2; }
public sealed class CrimsonMindTrophy : StarfallTrophyBase { protected override int BossIndex => 3; }
public sealed class BeeMatriarchTrophy : StarfallTrophyBase { protected override int BossIndex => 4; }
public sealed class SkeletalJudgeTrophy : StarfallTrophyBase { protected override int BossIndex => 5; }
public sealed class EternalWallTrophy : StarfallTrophyBase { protected override int BossIndex => 6; }
public sealed class DynastyQueenTrophy : StarfallTrophyBase { protected override int BossIndex => 7; }
public sealed class TwinCalamityTrophy : StarfallTrophyBase { protected override int BossIndex => 8; }
public sealed class PrimeVerdictTrophy : StarfallTrophyBase { protected override int BossIndex => 9; }
public sealed class StarEaterTrophy : StarfallTrophyBase { protected override int BossIndex => 10; }
public sealed class WastelandFlowerTrophy : StarfallTrophyBase { protected override int BossIndex => 11; }
public sealed class TempleCoreTrophy : StarfallTrophyBase { protected override int BossIndex => 12; }
public sealed class AbyssalDukeTrophy : StarfallTrophyBase { protected override int BossIndex => 13; }
public sealed class CoronaEmpressTrophy : StarfallTrophyBase { protected override int BossIndex => 14; }
public sealed class AstralPontiffTrophy : StarfallTrophyBase { protected override int BossIndex => 15; }
public sealed class EndMoonTrophy : StarfallTrophyBase { protected override int BossIndex => 16; }
