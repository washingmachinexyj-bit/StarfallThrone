using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.GameContent.ItemDropRules;

namespace StarfallThrone.Content.Ecology;

public abstract class EcologyMaterialBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => EcologyCatalog.Root + "Material" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 25;
    public override void SetDefaults()
    {
        Item.width = Item.height = 28; Item.maxStack = 9999; Item.material = true;
        Item.rare = EcologyInfrastructure.Rarity(Index / 4);
        Item.value = Item.sellPrice(copper: (Index / 4 + 1) * (Index % 4 == 3 ? 120 : 25));
    }
}
public abstract class EcologyCoreBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => EcologyCatalog.Root + "Core" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 25;
    public override void SetDefaults()
    {
        Item.width = Item.height = 32; Item.maxStack = 9999; Item.material = true;
        Item.rare = EcologyInfrastructure.Rarity(Index); Item.value = Item.sellPrice(silver: 3 + Index * 2);
    }
}
public abstract class EcologyBarBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => EcologyCatalog.Root + "Bar" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 25;
    public override void SetDefaults()
    {
        Item.width = 30; Item.height = 20; Item.maxStack = 9999; Item.material = true;
        Item.rare = EcologyInfrastructure.Rarity(Index); Item.value = Item.sellPrice(silver: 1 + Index);
    }
    public override void AddRecipes() => CreateRecipe().AddIngredient(EcologyCatalog.Ore(Index), Index < 4 ? 3 : 4)
        .AddTile(EcologyCatalog.StationTile(EcologyCatalog.Station(Index))).AddCondition(EcologyCatalog.DownedCondition(Index)).Register();
}
public abstract class EcologyComponentBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => EcologyCatalog.Root + "Component" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 25;
    public override void SetDefaults()
    {
        Item.width = Item.height = 28; Item.maxStack = 9999; Item.material = true;
        Item.rare = EcologyInfrastructure.Rarity(Index); Item.value = Item.sellPrice(silver: 2 + Index * 2);
    }
    public override void AddRecipes() => CreateRecipe().AddIngredient(EcologyCatalog.Bar(Index), 2).AddIngredient(EcologyCatalog.Material(Index), 3)
        .AddTile(EcologyCatalog.StationTile(EcologyCatalog.Station(Index))).AddCondition(EcologyCatalog.DownedCondition(Index)).Register();
}
/// <summary>Consumed exclusively by the authoritative mineral-bed transaction, never on client use.</summary>
public abstract class EcologyMineralSeedBase : ModItem
{
    public const int OreYield = 9;
    public abstract int Index { get; }
    public override string Texture => EcologyCatalog.Root + "Core" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 25;
    public override void SetDefaults()
    {
        Item.width = Item.height = 24; Item.maxStack = 9999; Item.material = true;
        Item.rare = EcologyInfrastructure.Rarity(Index); Item.value = 0;
    }
    public override void AddRecipes() => CreateRecipe().AddIngredient(EcologyCatalog.Core(Index))
        .AddIngredient(ItemID.StoneBlock, 30).AddTile(EcologyCatalog.StationTile(EcologyCatalog.Station(Index)))
        .AddCondition(EcologyCatalog.DownedCondition(Index)).Register();
}
public abstract class EcologyBagBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => EcologyCatalog.Root + "Bag" + Index;
    public override void SetStaticDefaults()
    {
        ItemID.Sets.BossBag[Type] = true; ItemID.Sets.OpenableBag[Type] = true; Item.ResearchUnlockCount = 3;
    }
    public override void SetDefaults()
    {
        Item.width = Item.height = 36; Item.maxStack = 9999; Item.expert = true; Item.rare = ItemRarityID.Expert;
    }
    public override bool CanRightClick() => true;
    public override void ModifyItemLoot(ItemLoot loot)
    {
        loot.Add(ItemDropRule.Common(EcologyCatalog.Core(Index), 1, 30, 40));
        loot.Add(ItemDropRule.OneFromOptions(1, Enumerable.Range(Index * 4, 4).Select(EcologyCatalog.Weapon).ToArray()));
        loot.Add(ItemDropRule.Common(EcologyCatalog.Expert(Index)));
        loot.Add(ItemDropRule.Common(EcologyCatalog.Mask(Index), 7));
        loot.Add(ItemDropRule.Common(ItemID.GoldCoin, 1, 1 + Index, 2 + Index * 2));
        loot.Add(ItemDropRule.Common(EcologyLoot.HealingPotion(Index), 1, 5, 10));
    }
}
public static class EcologyLoot
{
    public static int HealingPotion(int index) => index < 2 ? ItemID.LesserHealingPotion
        : index < 4 ? ItemID.HealingPotion : index < 9 ? ItemID.GreaterHealingPotion : ItemID.SuperHealingPotion;
    public static void Configure(NPCLoot loot, int index)
    {
        loot.Add(ItemDropRule.Common(EcologyCatalog.Trophy(index), 10));
        var normal = new LeadingConditionRule(new Conditions.NotExpert());
        normal.OnSuccess(ItemDropRule.Common(EcologyCatalog.Core(index), 1, 24, 32));
        normal.OnSuccess(ItemDropRule.OneFromOptions(1, Enumerable.Range(index * 4, 4).Select(EcologyCatalog.Weapon).ToArray()));
        normal.OnSuccess(ItemDropRule.Common(EcologyCatalog.Mask(index), 7));
        loot.Add(normal);
        loot.Add(ItemDropRule.BossBag(EcologyCatalog.Bag(index)));
        loot.Add(ItemDropRule.MasterModeCommonDrop(EcologyCatalog.Relic(index)));
        loot.Add(ItemDropRule.MasterModeDropOnAllPlayers(EcologyCatalog.Pet(index), 4));
    }
}
public sealed class EcologyRecipeGroups : ModSystem
{
    public override void AddRecipeGroups()
    {
        RecipeGroup.RegisterGroup("StarfallThrone:EcologyGold", new RecipeGroup(
            () => Language.GetTextValue("Mods.StarfallThrone.EcologyContent.AnyGold"), ItemID.GoldBar, ItemID.PlatinumBar));
        RecipeGroup.RegisterGroup("StarfallThrone:EcologyFragment", new RecipeGroup(
            () => Language.GetTextValue("Mods.StarfallThrone.EcologyContent.AnyFragment"),
            ItemID.FragmentSolar, ItemID.FragmentVortex, ItemID.FragmentNebula, ItemID.FragmentStardust));
        // Any owned catalyst can build the common tuner. The recipe's Wall condition still applies.
        RecipeGroup.RegisterGroup("StarfallThrone:EcologyCatalyst", new RecipeGroup(
            () => Language.GetTextValue("Mods.StarfallThrone.EcologyContent.AnyCatalyst"),
            Enumerable.Range(0, 10).Select(i => EcologyCatalog.Item("EcologyCatalyst" + i)).ToArray()));
    }
}
