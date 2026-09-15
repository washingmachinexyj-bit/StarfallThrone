using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Mining;

public abstract class MiningAccessory : ModItem
{
    public abstract int Index { get; }
    public override string Texture => MiningCatalog.Root + "MiningAccessory" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;
    public override void SetDefaults()
    {
        Item.width = Item.height = 32; Item.accessory = true;
        Item.rare = Index < 2 ? ItemRarityID.Blue : ItemRarityID.Red;
        Item.value = Index < 2 ? Item.sellPrice(silver: 20) : Item.sellPrice(gold: 10);
    }
    public override void UpdateAccessory(Player player, bool hideVisual)
    {
        MiningPlayer state = player.GetModPlayer<MiningPlayer>();
        byte tier = Index switch { 0 => 1, 1 => 2, 4 => 3, _ => 0 };
        if (tier > state.GatherTier) state.GatherTier = tier;
        if (Index is 2 or 4) state.AccessoryMagnet = true;
        if (Index == 3) state.Spectrum = true;
    }
    public override void AddRecipes()
    {
        Recipe recipe = CreateRecipe();
        int station;
        switch (Index)
        {
            case 0:
                recipe.AddIngredient(MiningCatalog.BarType(0), 4).AddIngredient<ShellPatternFragment>(2).AddIngredient(ItemID.Rope, 20); station = 0; break;
            case 1:
                recipe.AddIngredient<MiningAccessory0>().AddIngredient(MiningCatalog.ComponentType(3), 3); station = 2; break;
            case 2:
                recipe.AddIngredient(MiningCatalog.ComponentType(6), 3).AddIngredient(MiningCatalog.BossMaterial(6), 4); station = 5; break;
            case 3:
                recipe.AddIngredient(MiningCatalog.ComponentType(9), 3).AddIngredient(MiningCatalog.ComponentType(2)).AddIngredient(ItemID.Glass, 8); station = 8; break;
            default:
                recipe.AddIngredient<MiningAccessory1>().AddIngredient<MiningAccessory2>()
                    .AddIngredient(MiningCatalog.ComponentType(12), 4).AddIngredient(MiningCatalog.ComponentType(11), 2); station = 11; break;
        }
        recipe.AddTile(MiningCatalog.StationTileType(station)).Register();
    }
}
public sealed class MiningAccessory0 : MiningAccessory { public override int Index => 0; }
public sealed class MiningAccessory1 : MiningAccessory { public override int Index => 1; }
public sealed class MiningAccessory2 : MiningAccessory { public override int Index => 2; }
public sealed class MiningAccessory3 : MiningAccessory { public override int Index => 3; }
public sealed class MiningAccessory4 : MiningAccessory { public override int Index => 4; }

public abstract class MiningSurvey : ModItem
{
    public abstract int Index { get; }
    public override string Texture => MiningCatalog.Root + "MiningSurvey" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;
    public override void SetDefaults()
    {
        Item.width = Item.height = 32;
        Item.useTime = Item.useAnimation = 25; Item.useStyle = ItemUseStyleID.HoldUp;
        Item.UseSound = SoundID.MenuTick; Item.rare = Index == 0 ? ItemRarityID.Blue : ItemRarityID.Red;
        Item.value = Index == 0 ? Item.sellPrice(silver: 25) : Item.sellPrice(gold: 8);
    }
    public override bool AltFunctionUse(Player player) => true;
    public override bool? UseItem(Player player)
    {
        if (player.whoAmI != Main.myPlayer) return true;
        MiningPlayer state = player.GetModPlayer<MiningPlayer>();
        if (Index == 3) state.MarkConstruction(new Point(Player.tileTargetX, Player.tileTargetY), player.altFunctionUse == 2);
        else if (player.altFunctionUse == 2) state.SelectNextOre(Index == 0 ? 3 : 12);
        else if (Index == 2) state.RememberNearest();
        return true;
    }
    public override void AddRecipes()
    {
        Recipe recipe = CreateRecipe(); int station;
        switch (Index)
        {
            case 0: recipe.AddIngredient(MiningCatalog.ComponentType(1), 2).AddIngredient(ItemID.Glass, 4); station = 1; break;
            case 1: recipe.AddIngredient(MiningCatalog.ComponentType(7), 3).AddIngredient(MiningCatalog.ComponentType(6), 2).AddIngredient(ItemID.Glass, 8); station = 5; break;
            case 2: recipe.AddIngredient<MiningSurvey1>().AddIngredient(MiningCatalog.ComponentType(11), 3).AddIngredient(MiningCatalog.ComponentType(9), 2); station = 10; break;
            default: recipe.AddIngredient(MiningCatalog.ComponentType(11), 3).AddIngredient(ItemID.Wire, 10); station = 10; break;
        }
        recipe.AddTile(MiningCatalog.StationTileType(station)).Register();
    }
}
public sealed class MiningSurvey0 : MiningSurvey { public override int Index => 0; }
public sealed class MiningSurvey1 : MiningSurvey { public override int Index => 1; }
public sealed class MiningSurvey2 : MiningSurvey { public override int Index => 2; }
public sealed class MiningSurvey3 : MiningSurvey { public override int Index => 3; }

public abstract class MiningUtilityItem : ModItem
{
    public abstract int Index { get; }
    public override string Texture => MiningCatalog.Root + "MiningUtilityItem" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = Index is 0 or 13 or 14 ? 20 : 1;
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(MiningUtilityTile.TypeFor(Index));
        Item.width = Item.height = 32;
        Item.rare = Index <= 5 || Index is 7 or 12 ? ItemRarityID.Blue : ItemRarityID.Red;
        Item.value = Item.sellPrice(silver: Index is 0 or 13 or 14 ? 1 : 10);
    }
    public override void AddRecipes()
    {
        Recipe recipe = CreateRecipe(Index switch { 0 => 4, 13 => 20, 14 => 40, _ => 1 }); int station;
        switch (Index)
        {
            case 0: recipe.AddIngredient(MiningCatalog.BarType(0)).AddIngredient(ItemID.Wood, 4).AddIngredient(ItemID.Torch); station = 0; break;
            case 1: recipe.AddIngredient(MiningCatalog.ComponentType(0)).AddIngredient(ItemID.Glass, 2).AddIngredient(ItemID.Wood, 4); station = 0; break;
            case 2: recipe.AddIngredient<ShellPatternFragment>().AddIngredient(ItemID.Torch).AddIngredient(ItemID.Wood, 4); station = 0; break;
            case 3: recipe.AddIngredient(MiningCatalog.BarType(0), 2).AddIngredient(ItemID.ClayBlock, 6).AddIngredient(ItemID.Acorn); station = 0; break;
            case 4: recipe.AddIngredient(MiningCatalog.ComponentType(2), 2).AddIngredient(ItemID.Glass, 6).AddIngredient(ItemID.Torch, 4); station = 2; break;
            case 5: recipe.AddIngredient(MiningCatalog.ComponentType(3), 3).AddIngredient(ItemID.StoneBlock, 20).AddIngredient(ItemID.Torch, 8); station = 2; break;
            case 6: recipe.AddIngredient(MiningCatalog.ComponentType(5), 3).AddIngredient(ItemID.StoneBlock, 20).AddIngredient(ItemID.Torch, 8); station = 4; break;
            case 7: recipe.AddIngredient(MiningCatalog.BarType(0), 6).AddIngredient(ItemID.Wood, 10).AddIngredient(ItemID.StoneBlock, 12); station = 0; break;
            case 8: recipe.AddIngredient(MiningCatalog.ComponentType(10), 4).AddIngredient(ItemID.Glass, 20).AddIngredient<MiningUtilityItem7>(); station = 9; break;
            case 9: recipe.AddIngredient(MiningCatalog.ComponentType(8), 4).AddRecipeGroup(RecipeGroupID.IronBar, 6).AddIngredient<MiningUtilityItem7>(); station = 7; break;
            case 10: recipe.AddIngredient(MiningCatalog.ComponentType(12), 4).AddIngredient<MiningUtilityItem8>().AddIngredient<MiningUtilityItem9>(); station = 11; break;
            case 11: recipe.AddIngredient(MiningCatalog.ComponentType(9), 3).AddIngredient(ItemID.Glass, 8); station = 8; break;
            case 12:
                recipe.AddIngredient(ItemID.StoneBlock, 30).AddIngredient(ItemID.Glass, 12).AddRecipeGroup(RecipeGroupID.IronBar, 3)
                    .AddTile(TileID.WorkBenches).Register(); return;
            case 13: recipe.AddIngredient(MiningCatalog.ComponentType(4)).AddIngredient(ItemID.StoneBlock, 10); station = 3; break;
            default: recipe.AddIngredient(MiningCatalog.ComponentType(8)).AddIngredient(ItemID.Wood, 20).AddIngredient(ItemID.StoneBlock, 20); station = 7; break;
        }
        recipe.AddTile(MiningCatalog.StationTileType(station)).Register();
        if (Index == 10)
        {
            // Postgame construction only: no ores, boss materials, coins or rare drops are transmuted.
            Recipe.Create(ItemID.GrayBrick, 999).AddIngredient(ItemID.StoneBlock, 150).AddIngredient(MiningCatalog.ComponentType(12))
                .AddTile(MiningCatalog.StationTileType(11)).Register();
            Recipe.Create(ItemID.Glass, 999).AddIngredient(ItemID.SandBlock, 150).AddIngredient(MiningCatalog.ComponentType(12))
                .AddTile(MiningCatalog.StationTileType(11)).Register();
        }
    }
}
public sealed class MiningUtilityItem0 : MiningUtilityItem { public override int Index => 0; }
public sealed class MiningUtilityItem1 : MiningUtilityItem { public override int Index => 1; }
public sealed class MiningUtilityItem2 : MiningUtilityItem { public override int Index => 2; }
public sealed class MiningUtilityItem3 : MiningUtilityItem { public override int Index => 3; }
public sealed class MiningUtilityItem4 : MiningUtilityItem { public override int Index => 4; }
public sealed class MiningUtilityItem5 : MiningUtilityItem { public override int Index => 5; }
public sealed class MiningUtilityItem6 : MiningUtilityItem { public override int Index => 6; }
public sealed class MiningUtilityItem7 : MiningUtilityItem { public override int Index => 7; }
public sealed class MiningUtilityItem8 : MiningUtilityItem { public override int Index => 8; }
public sealed class MiningUtilityItem9 : MiningUtilityItem { public override int Index => 9; }
public sealed class MiningUtilityItem10 : MiningUtilityItem { public override int Index => 10; }
public sealed class MiningUtilityItem11 : MiningUtilityItem { public override int Index => 11; }
public sealed class MiningUtilityItem12 : MiningUtilityItem { public override int Index => 12; }
public sealed class MiningUtilityItem13 : MiningUtilityItem { public override int Index => 13; }
public sealed class MiningUtilityItem14 : MiningUtilityItem { public override int Index => 14; }

public abstract class MiningSeed : ModItem
{
    public abstract int Index { get; }
    public override string Texture => MiningCatalog.Root + "MiningSeed" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 5;
    public override void SetDefaults()
    {
        Item.width = Item.height = 24; Item.maxStack = 9999;
        Item.rare = Index < 4 ? ItemRarityID.Blue : ItemRarityID.Red;
        Item.value = 0;
    }
    public override void AddRecipes()
    {
        Recipe recipe = CreateRecipe().AddIngredient(MiningCatalog.BossMaterial(Index), Index < 4 ? 1 : 8);
        if (Index == 0) recipe.AddIngredient(ItemID.StoneBlock, 12);
        else if (Index == 1) recipe.AddRecipeGroup(RecipeGroupID.IronBar, 4);
        else if (Index == 4) recipe.AddIngredient(ItemID.LunarBar, 4);
        else recipe.AddIngredient(MiningCatalog.BarType(Index - 1), 4);
        // Previous station: the fallback works even when no natural vein was generated.
        int station = Index == 0 ? 0 : MiningCatalog.ToolStation(Index);
        recipe.AddTile(MiningCatalog.StationTileType(station))
            .AddCondition(new Condition(Language.GetText("Mods.StarfallThrone.MiningToolsUI.SeedUnlocked"), () => MiningCatalog.Unlocked(Index))).Register();
    }
}
public sealed class MiningSeed0 : MiningSeed { public override int Index => 0; }
public sealed class MiningSeed1 : MiningSeed { public override int Index => 1; }
public sealed class MiningSeed2 : MiningSeed { public override int Index => 2; }
public sealed class MiningSeed3 : MiningSeed { public override int Index => 3; }
public sealed class MiningSeed4 : MiningSeed { public override int Index => 4; }
public sealed class MiningSeed5 : MiningSeed { public override int Index => 5; }
public sealed class MiningSeed6 : MiningSeed { public override int Index => 6; }
public sealed class MiningSeed7 : MiningSeed { public override int Index => 7; }
public sealed class MiningSeed8 : MiningSeed { public override int Index => 8; }
public sealed class MiningSeed9 : MiningSeed { public override int Index => 9; }
public sealed class MiningSeed10 : MiningSeed { public override int Index => 10; }
public sealed class MiningSeed11 : MiningSeed { public override int Index => 11; }
public sealed class MiningSeed12 : MiningSeed { public override int Index => 12; }
