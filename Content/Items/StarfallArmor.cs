using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Items;

[AutoloadEquip(EquipType.Head)]
public sealed class AstralThroneHelmet : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Items/AstralThroneHelmet";

    public override void SetDefaults()
    {
        Item.width = 32;
        Item.height = 26;
        Item.defense = 25;
        Item.rare = ItemRarityID.Red;
        Item.value = Item.buyPrice(gold: 80);
    }

    public override bool IsArmorSet(Item head, Item body, Item legs)
    {
        return body.type == ModContent.ItemType<AstralThronePlate>() && legs.type == ModContent.ItemType<AstralThroneGreaves>();
    }

    public override void UpdateArmorSet(Player player)
    {
        player.setBonus = Language.GetTextValue("Mods.StarfallThrone.Items.AstralThroneHelmet.SetBonus");
        player.statLifeMax2 += 250;
        player.statManaMax2 += 120;
        player.GetDamage(DamageClass.Generic) *= 1.15f;
        player.endurance += 0.08f;
        player.maxMinions += 1;
        player.maxTurrets += 1;
        player.noKnockback = true;
    }

    public override void AddRecipes() => CreateRecipe()
        .AddIngredient<AstralIngot>(12)
        .AddIngredient<MoonGodCore>(4)
        .AddIngredient<StarfallResidue>(20)
        .AddTile(TileID.LunarCraftingStation)
        .Register();
}

[AutoloadEquip(EquipType.Body)]
public sealed class AstralThronePlate : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Items/AstralThronePlate";

    public override void SetDefaults()
    {
        Item.width = 34;
        Item.height = 32;
        Item.defense = 32;
        Item.rare = ItemRarityID.Red;
        Item.value = Item.buyPrice(gold: 100);
    }

    public override void AddRecipes() => CreateRecipe()
        .AddIngredient<AstralIngot>(18)
        .AddIngredient<MoonGodCore>(6)
        .AddIngredient<StarfallResidue>(30)
        .AddTile(TileID.LunarCraftingStation)
        .Register();
}

[AutoloadEquip(EquipType.Legs)]
public sealed class AstralThroneGreaves : ModItem
{
    public override string Texture => "StarfallThrone/Content/Assets/Items/AstralThroneGreaves";

    public override void SetDefaults()
    {
        Item.width = 34;
        Item.height = 32;
        Item.defense = 28;
        Item.rare = ItemRarityID.Red;
        Item.value = Item.buyPrice(gold: 90);
    }

    public override void AddRecipes() => CreateRecipe()
        .AddIngredient<AstralIngot>(16)
        .AddIngredient<MoonGodCore>(5)
        .AddIngredient<StarfallResidue>(28)
        .AddTile(TileID.LunarCraftingStation)
        .Register();
}
