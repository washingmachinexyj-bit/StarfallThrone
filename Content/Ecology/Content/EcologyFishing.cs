using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;

namespace StarfallThrone.Content.Ecology;

public abstract class EcologyFishBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => EcologyCatalog.Root + "Fish" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 10;
    public override void SetDefaults()
    {
        Item.width = 30; Item.height = 20; Item.maxStack = 9999; Item.material = true;
        Item.rare = EcologyInfrastructure.Rarity(Index); Item.value = Item.sellPrice(silver: 1 + Index);
    }
    public override void AddRecipes()
    {
        Recipe.Create(ItemID.CookedFish, 2).AddIngredient(Type).AddTile(TileID.CookingPots).Register();
        // An alternate ordinary-material source, never a source of boss cores or rare-elite drops.
        Recipe.Create(EcologyCatalog.Material(Index), 2).AddIngredient(Type, 2)
            .AddTile(EcologyCatalog.StationTile(EcologyCatalog.Station(Index)))
            .AddCondition(EcologyCatalog.BossCondition(Index)).Register();
    }
}
public abstract class EcologyCrateBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => EcologyCatalog.Root + "Crate" + Index;
    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 10; ItemID.Sets.IsFishingCrate[Type] = true; ItemID.Sets.OpenableBag[Type] = true;
        ItemID.Sets.IsFishingCrateHardmode[Type] = Index >= 4;
    }
    public override void SetDefaults()
    {
        Item.width = Item.height = 32; Item.maxStack = 9999;
        Item.rare = EcologyInfrastructure.Rarity(Index); Item.value = Item.sellPrice(silver: 8 + Index * 3);
    }
    public override bool CanRightClick() => true;
    public override void ModifyItemLoot(ItemLoot loot)
    {
        // Stable branch-independent contents even if an old crate is opened in a different world.
        loot.Add(ItemDropRule.Common(EcologyCatalog.Material(Index), 1, 5, 9));
        loot.Add(ItemDropRule.Common(EcologyCatalog.Material(Index, 1), 2, 3, 6));
        loot.Add(ItemDropRule.Common(EcologyCatalog.Material(Index, 2), 3, 2, 5));
        loot.Add(ItemDropRule.Common(EcologyCatalog.Item("EcologyFish" + Index), 2, 1, 3));
        loot.Add(ItemDropRule.Common(ItemID.SilverCoin, 1, 12 + Index * 3, 24 + Index * 5));
        loot.Add(ItemDropRule.Common(ItemID.HealingPotion, 3, 2, 4));
        loot.Add(ItemDropRule.Common(ItemID.ApprenticeBait, 3, 2, 4));
    }
}
public sealed class EcologyFishingPlayer : ModPlayer
{
    public override void CatchFish(FishingAttempt attempt, ref int itemDrop, ref int npcSpawn,
        ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition)
    {
        if (attempt.inLava || attempt.inHoney || itemDrop <= 0 || npcSpawn > 0 ||
            attempt.waterTilesCount < attempt.waterNeededToFish) return;
        if (ContentSamples.ItemsByType.TryGetValue(itemDrop, out Item original) && original.questItem) return;
        int biome = EcologyWorld.ActiveBiome(Player);
        if (biome < 0 || !EcologyCatalog.VanillaUnlocked(biome)) return;
        var region = EcologyWorld.RegionAt(new Point(attempt.X, attempt.Y));
        if (region == null || !region.Ecology || region.Biome != biome || region.TileCount < 300) return;
        if (attempt.crate)
        {
            if (Main.rand.NextBool(2)) itemDrop = EcologyCatalog.Item("EcologyCrate" + biome);
        }
        else if (attempt.uncommon || (attempt.common && Main.rand.NextBool(3)))
            itemDrop = EcologyCatalog.Item("EcologyFish" + biome);
    }
}

