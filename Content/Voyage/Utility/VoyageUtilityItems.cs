using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Voyage.Utility;

public abstract class VoyageUtilityItem : ModItem
{
    public abstract int Index { get; }
    public override string Texture => VoyageCatalog.Root + "Utility" + Index;
    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 1;
        if (Index == 2) ItemID.Sets.IsDrill[Type] = true;
    }
    public override void SetDefaults()
    {
        Item.width = 36; Item.height = 36;
        Item.rare = ItemRarityID.Red; Item.value = Item.sellPrice(gold: 12);
        switch (Index)
        {
            case 1:
                Item.DefaultToVanitypet(ModContent.ProjectileType<VoyageSearchlightPet>(), ModContent.BuffType<VoyageSearchlightBuff>());
                Item.rare = ItemRarityID.Red; Item.value = Item.sellPrice(gold: 12);
                break;
            case 2:
                Item.CloneDefaults(ItemID.SolarFlareDrill);
                Item.pick = 350; Item.tileBoost = 6; Item.damage = 130;
                Item.shoot = ModContent.ProjectileType<VoyageEngineeringDrill>();
                Item.value = Item.sellPrice(gold: 15); Item.rare = ItemRarityID.Red;
                break;
            case 4:
                Item.useTime = Item.useAnimation = 20; Item.useStyle = ItemUseStyleID.HoldUp;
                Item.channel = true; Item.UseSound = SoundID.Item6;
                break;
            case 6:
                Item.accessory = true;
                break;
            default:
                Item.DefaultToPlaceableTile(VoyageUtilityTiles.TypeFor(Index));
                Item.width = 36; Item.height = 36; Item.rare = ItemRarityID.Red;
                Item.value = Item.sellPrice(gold: 12);
                break;
        }
    }
    public override bool CanUseItem(Player player)
        => Index != 4 || (!VoyageCatalog.AnyEncounter() && player.GetModPlayer<VoyageUtilityPlayer>().RecallTicks == 0 && !player.dead);

    public override bool? UseItem(Player player)
    {
        if (Index == 4 && player.whoAmI == Main.myPlayer)
            player.GetModPlayer<VoyageUtilityPlayer>().RequestRecall();
        return true;
    }
    public override void UpdateAccessory(Player player, bool hideVisual)
    {
        if (Index != 6) return;
        player.blockRange += 4;
        player.tileSpeed += .5f; player.wallSpeed += .5f;
        player.GetModPlayer<VoyageUtilityPlayer>().Builder = true;
    }
    public override void AddRecipes()
    {
        Recipe recipe = CreateRecipe();
        switch (Index)
        {
            case 0: recipe.AddIngredient(VoyageCatalog.Material(0), 20).AddIngredient(VoyageCatalog.Core(0)).AddIngredient(VoyageCatalog.Alloy, 8); break;
            case 1: recipe.AddIngredient(VoyageCatalog.Material(1), 18).AddIngredient(VoyageCatalog.Core(1)).AddIngredient(VoyageCatalog.Alloy, 6); break;
            case 2: recipe.AddIngredient(VoyageCatalog.Material(2), 24).AddIngredient(VoyageCatalog.Core(2)).AddIngredient(VoyageCatalog.Alloy, 12); break;
            case 3: recipe.AddIngredient(VoyageCatalog.Material(4), 20).AddIngredient(VoyageCatalog.Core(4)).AddIngredient(VoyageCatalog.Alloy, 10); break;
            case 4: recipe.AddIngredient(VoyageCatalog.Material(6), 20).AddIngredient(VoyageCatalog.Core(6)).AddIngredient(VoyageCatalog.Alloy, 12); break;
            case 5: recipe.AddIngredient(VoyageCatalog.Material(11), 16).AddIngredient(VoyageCatalog.Material(7), 8).AddIngredient(VoyageCatalog.Alloy, 6); break;
            case 6: recipe.AddIngredient(VoyageCatalog.Material(12), 24).AddIngredient(VoyageCatalog.Core(12)).AddIngredient(VoyageCatalog.Alloy, 16); break;
            case 7: recipe.AddIngredient(VoyageCatalog.Material(15), 24).AddIngredient(VoyageCatalog.Core(15)).AddIngredient(VoyageCatalog.Plate(3), 8); break;
            case 8: recipe.AddIngredient(VoyageCatalog.Material(16), 24).AddIngredient(VoyageCatalog.Core(16)).AddIngredient(VoyageCatalog.Alloy, 16); break;
        }
        recipe.AddTile(VoyageCatalog.Workbench).Register();
    }
}

public sealed class VoyageUtility0 : VoyageUtilityItem { public override int Index => 0; }
public sealed class VoyageUtility1 : VoyageUtilityItem { public override int Index => 1; }
public sealed class VoyageUtility2 : VoyageUtilityItem { public override int Index => 2; }
public sealed class VoyageUtility3 : VoyageUtilityItem { public override int Index => 3; }
public sealed class VoyageUtility4 : VoyageUtilityItem { public override int Index => 4; }
public sealed class VoyageUtility5 : VoyageUtilityItem { public override int Index => 5; }
public sealed class VoyageUtility6 : VoyageUtilityItem { public override int Index => 6; }
public sealed class VoyageUtility7 : VoyageUtilityItem { public override int Index => 7; }
public sealed class VoyageUtility8 : VoyageUtilityItem { public override int Index => 8; }

public sealed class VoyageRepairBuff : ModBuff
{
    public override string Texture => VoyageCatalog.Root + "Utility3";
    public override void Update(Player player, ref int buffIndex)
    {
        player.statDefense += 10;
        player.statManaMax2 += 40;
    }
}

public sealed class VoyageSearchlightBuff : ModBuff
{
    public override string Texture => VoyageCatalog.Root + "Utility1";
    public override void SetStaticDefaults()
    {
        Main.buffNoTimeDisplay[Type] = true;
        Main.lightPet[Type] = true;
        Main.buffNoSave[Type] = false;
    }
    public override void Update(Player player, ref int buffIndex)
    {
        player.buffTime[buffIndex] = 18000;
        int type = ModContent.ProjectileType<VoyageSearchlightPet>();
        if (!player.dead && player.whoAmI == Main.myPlayer && player.ownedProjectileCounts[type] == 0)
            Projectile.NewProjectile(player.GetSource_Buff(buffIndex), player.Center, Vector2.Zero, type, 0, 0, player.whoAmI);
    }
}
