using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace StarfallThrone.Content.Oaths.Mining;

public static class OathMiningCatalog
{
    public const string Root = "StarfallThrone/Content/Assets/Oaths/";
    public const int RequiredPick = 600;
    public static readonly int[] PickPower = { 610, 630, 650 };
    public static readonly Color[] Colors = { new(111, 238, 195), new(255, 145, 61), new(218, 210, 255) };
    public static int Item(string name) => ModContent.Find<ModItem>("StarfallThrone/" + name).Type;
    public static int Tile(string name) => ModContent.Find<ModTile>("StarfallThrone/" + name).Type;
    public static Condition Won(int i) => new(Language.GetText("Mods.StarfallThrone.OathsMining.Requires" + i), () => OathWorld.SupremeWon[i]);
}

public abstract class OathOreBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => OathMiningCatalog.Root + "OathOre" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 100;
    public override void SetDefaults() { Item.DefaultToPlaceableTile(OathMiningCatalog.Tile("OathOreTile" + Index)); Item.width = Item.height = 30; Item.maxStack = 9999; Item.rare = ItemRarityID.Red; Item.value = 0; }
    public override void AddRecipes() => CreateRecipe(8).AddIngredient(OathCatalog.Material(Index)).AddIngredient(ItemID.StoneBlock, 10).AddTile(OathCatalog.Station(Index)).AddCondition(OathMiningCatalog.Won(Index)).Register();
}
public abstract class OathBarBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => OathMiningCatalog.Root + "OathBar" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 25;
    public override void SetDefaults() { Item.width = 30; Item.height = 22; Item.maxStack = 9999; Item.material = true; Item.rare = ItemRarityID.Red; Item.value = 0; }
    public override void AddRecipes() => CreateRecipe().AddIngredient(OathMiningCatalog.Item("OathOre" + Index), 4).AddTile(OathCatalog.Station(Index)).AddCondition(OathMiningCatalog.Won(Index)).Register();
}
public abstract class OathPickBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => OathMiningCatalog.Root + "OathPick" + Index;
    public override void SetDefaults()
    {
        Item.width = Item.height = 38; Item.damage = 12000 + Index * 2000; Item.DamageType = DamageClass.Melee;
        Item.pick = OathMiningCatalog.PickPower[Index]; Item.useTime = 4; Item.useAnimation = 16;
        Item.useStyle = ItemUseStyleID.Swing; Item.autoReuse = true; Item.useTurn = true;
        Item.knockBack = 5; Item.UseSound = SoundID.Item1; Item.rare = ItemRarityID.Red; Item.value = 0; Item.tileBoost = 5;
    }
    public override void AddRecipes() => CreateRecipe().AddIngredient(OathCatalog.Material(Index), 10).AddTile(OathCatalog.BaseStation).AddCondition(OathMiningCatalog.Won(Index)).Register();
}
public abstract class OathStationBase : ModItem
{
    public abstract int Index { get; }
    public override string Texture => OathMiningCatalog.Root + "OathStation" + Index;
    public override void SetDefaults() { Item.DefaultToPlaceableTile(OathCatalog.Station(Index)); Item.width = 48; Item.height = 48; Item.maxStack = 99; Item.rare = ItemRarityID.Red; Item.value = 0; }
    public override void AddRecipes() => CreateRecipe().AddIngredient(OathCatalog.Material(Index), 20).AddIngredient(OathMiningCatalog.Item("PantheonMaterial16"), 10).AddIngredient(ItemID.StoneBlock, 30).AddTile(OathCatalog.BaseStation).AddCondition(OathMiningCatalog.Won(Index)).Register();
}
public abstract class OathOreTileBase : ModTile
{
    public abstract int Index { get; }
    public override string Texture => OathMiningCatalog.Root + "OathOreTile" + Index;
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = Main.tileBlockLight[Type] = Main.tileSpelunker[Type] = Main.tileLighted[Type] = true;
        TileID.Sets.Ore[Type] = true; TileID.Sets.CanBeClearedDuringGeneration[Type] = false;
        Main.tileOreFinderPriority[Type] = (short)(900 + Index);
        MinPick = OathMiningCatalog.RequiredPick; MineResist = 6; DustType = DustID.Stone; HitSound = SoundID.Tink;
        RegisterItemDrop(OathMiningCatalog.Item("OathOre" + Index));
        AddMapEntry(OathMiningCatalog.Colors[Index], Language.GetText("Mods.StarfallThrone.Items.OathOre" + Index + ".DisplayName"));
    }
    public override bool CanExplode(int i, int j) => false;
    public override bool CanReplace(int i, int j, int tileTypeBeingPlaced) => false;
    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) { Vector3 c = OathMiningCatalog.Colors[Index].ToVector3() * .16f; r = c.X; g = c.Y; b = c.Z; }
    // Full native 18px atlas: Terraria retains its normal connected ore framing.
}
public abstract class OathStationTileBase : ModTile
{
    public abstract int Index { get; }
    public override string Texture => OathMiningCatalog.Root + "OathStationTile" + Index;
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = Main.tileNoAttach[Type] = Main.tileTable[Type] = Main.tileLighted[Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3); TileObjectData.newTile.LavaDeath = false; TileObjectData.addTile(Type);
        AdjTiles = new int[] { TileID.WorkBenches }; AddToArray(ref TileID.Sets.RoomNeeds.CountsAsTable);
        AddMapEntry(OathMiningCatalog.Colors[Index], Language.GetText("Mods.StarfallThrone.Items.OathStation" + Index + ".DisplayName"));
        DustType = DustID.Electric; HitSound = SoundID.Tink;
    }
    public override IEnumerable<Item> GetItemDrops(int i, int j) { yield return new Item(OathMiningCatalog.Item("OathStation" + Index)); }
    public override void MouseOver(int i, int j) { Main.LocalPlayer.noThrow = 2; Main.LocalPlayer.cursorItemIconEnabled = true; Main.LocalPlayer.cursorItemIconID = OathMiningCatalog.Item("OathStation" + Index); }
    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) { Vector3 c = OathMiningCatalog.Colors[Index].ToVector3() * .3f; r = c.X; g = c.Y; b = c.Z; }
}
public sealed class OathOre0 : OathOreBase { public override int Index => 0; }
public sealed class OathOre1 : OathOreBase { public override int Index => 1; }
public sealed class OathOre2 : OathOreBase { public override int Index => 2; }
public sealed class OathBar0 : OathBarBase { public override int Index => 0; }
public sealed class OathBar1 : OathBarBase { public override int Index => 1; }
public sealed class OathBar2 : OathBarBase { public override int Index => 2; }
public sealed class OathPick0 : OathPickBase { public override int Index => 0; }
public sealed class OathPick1 : OathPickBase { public override int Index => 1; }
public sealed class OathPick2 : OathPickBase { public override int Index => 2; }
public sealed class OathStation0 : OathStationBase { public override int Index => 0; }
public sealed class OathStation1 : OathStationBase { public override int Index => 1; }
public sealed class OathStation2 : OathStationBase { public override int Index => 2; }
public sealed class OathOreTile0 : OathOreTileBase { public override int Index => 0; }
public sealed class OathOreTile1 : OathOreTileBase { public override int Index => 1; }
public sealed class OathOreTile2 : OathOreTileBase { public override int Index => 2; }
public sealed class OathStationTile0 : OathStationTileBase { public override int Index => 0; }
public sealed class OathStationTile1 : OathStationTileBase { public override int Index => 1; }
public sealed class OathStationTile2 : OathStationTileBase { public override int Index => 2; }
