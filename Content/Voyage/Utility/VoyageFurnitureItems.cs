using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Voyage.Utility;

public abstract class VoyageFurnitureItem : ModItem
{
    public abstract int Index { get; }
    public int Theme => Index / 5;
    public int Kind => Index % 5;
    public override string Texture => VoyageCatalog.Root + "Material" + Theme;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = Kind >= 3 ? 100 : 1;
    public override void SetDefaults()
    {
        int tile = Kind switch
        {
            0 => ModContent.TileType<VoyagePlaqueTile>(),
            1 => ModContent.TileType<VoyageLampTile>(),
            2 => ModContent.TileType<VoyageStatueTile>(),
            3 => ModContent.Find<ModTile>("StarfallThrone", "VoyagePlatform" + Theme + "Tile").Type,
            _ => ModContent.Find<ModTile>("StarfallThrone", "VoyageBlock" + Theme + "Tile").Type
        };
        Item.DefaultToPlaceableTile(tile, Kind < 3 ? Theme : 0);
        Item.width = Item.height = 32;
        Item.rare = ItemRarityID.Cyan;
        Item.value = Kind >= 3 ? Item.sellPrice(copper: 30) : Item.sellPrice(silver: 20);
    }
    public override void AddRecipes()
    {
        CreateRecipe(Kind >= 3 ? 25 : 1).AddIngredient(VoyageCatalog.Material(Theme), Kind == 2 ? 8 : 4)
            .AddTile(VoyageCatalog.Workbench).Register();
    }
    public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame,
        Color drawColor, Color itemColor, Vector2 origin, float scale)
    {
        VoyageFurnitureDrawing.Draw(spriteBatch, position, Theme, Kind, .76f * scale, drawColor);
        return false;
    }
    public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor,
        ref float rotation, ref float scale, int whoAmI)
    {
        VoyageFurnitureDrawing.Draw(spriteBatch, Item.Center - Main.screenPosition, Theme, Kind, .68f * scale, lightColor);
        return false;
    }
}

public sealed class VoyageFurniture0 : VoyageFurnitureItem { public override int Index => 0; }
public sealed class VoyageFurniture1 : VoyageFurnitureItem { public override int Index => 1; }
public sealed class VoyageFurniture2 : VoyageFurnitureItem { public override int Index => 2; }
public sealed class VoyageFurniture3 : VoyageFurnitureItem { public override int Index => 3; }
public sealed class VoyageFurniture4 : VoyageFurnitureItem { public override int Index => 4; }
public sealed class VoyageFurniture5 : VoyageFurnitureItem { public override int Index => 5; }
public sealed class VoyageFurniture6 : VoyageFurnitureItem { public override int Index => 6; }
public sealed class VoyageFurniture7 : VoyageFurnitureItem { public override int Index => 7; }
public sealed class VoyageFurniture8 : VoyageFurnitureItem { public override int Index => 8; }
public sealed class VoyageFurniture9 : VoyageFurnitureItem { public override int Index => 9; }
public sealed class VoyageFurniture10 : VoyageFurnitureItem { public override int Index => 10; }
public sealed class VoyageFurniture11 : VoyageFurnitureItem { public override int Index => 11; }
public sealed class VoyageFurniture12 : VoyageFurnitureItem { public override int Index => 12; }
public sealed class VoyageFurniture13 : VoyageFurnitureItem { public override int Index => 13; }
public sealed class VoyageFurniture14 : VoyageFurnitureItem { public override int Index => 14; }
public sealed class VoyageFurniture15 : VoyageFurnitureItem { public override int Index => 15; }
public sealed class VoyageFurniture16 : VoyageFurnitureItem { public override int Index => 16; }
public sealed class VoyageFurniture17 : VoyageFurnitureItem { public override int Index => 17; }
public sealed class VoyageFurniture18 : VoyageFurnitureItem { public override int Index => 18; }
public sealed class VoyageFurniture19 : VoyageFurnitureItem { public override int Index => 19; }
public sealed class VoyageFurniture20 : VoyageFurnitureItem { public override int Index => 20; }
public sealed class VoyageFurniture21 : VoyageFurnitureItem { public override int Index => 21; }
public sealed class VoyageFurniture22 : VoyageFurnitureItem { public override int Index => 22; }
public sealed class VoyageFurniture23 : VoyageFurnitureItem { public override int Index => 23; }
public sealed class VoyageFurniture24 : VoyageFurnitureItem { public override int Index => 24; }
public sealed class VoyageFurniture25 : VoyageFurnitureItem { public override int Index => 25; }
public sealed class VoyageFurniture26 : VoyageFurnitureItem { public override int Index => 26; }
public sealed class VoyageFurniture27 : VoyageFurnitureItem { public override int Index => 27; }
public sealed class VoyageFurniture28 : VoyageFurnitureItem { public override int Index => 28; }
public sealed class VoyageFurniture29 : VoyageFurnitureItem { public override int Index => 29; }
public sealed class VoyageFurniture30 : VoyageFurnitureItem { public override int Index => 30; }
public sealed class VoyageFurniture31 : VoyageFurnitureItem { public override int Index => 31; }
public sealed class VoyageFurniture32 : VoyageFurnitureItem { public override int Index => 32; }
public sealed class VoyageFurniture33 : VoyageFurnitureItem { public override int Index => 33; }
public sealed class VoyageFurniture34 : VoyageFurnitureItem { public override int Index => 34; }
public sealed class VoyageFurniture35 : VoyageFurnitureItem { public override int Index => 35; }
public sealed class VoyageFurniture36 : VoyageFurnitureItem { public override int Index => 36; }
public sealed class VoyageFurniture37 : VoyageFurnitureItem { public override int Index => 37; }
public sealed class VoyageFurniture38 : VoyageFurnitureItem { public override int Index => 38; }
public sealed class VoyageFurniture39 : VoyageFurnitureItem { public override int Index => 39; }
public sealed class VoyageFurniture40 : VoyageFurnitureItem { public override int Index => 40; }
public sealed class VoyageFurniture41 : VoyageFurnitureItem { public override int Index => 41; }
public sealed class VoyageFurniture42 : VoyageFurnitureItem { public override int Index => 42; }
public sealed class VoyageFurniture43 : VoyageFurnitureItem { public override int Index => 43; }
public sealed class VoyageFurniture44 : VoyageFurnitureItem { public override int Index => 44; }
public sealed class VoyageFurniture45 : VoyageFurnitureItem { public override int Index => 45; }
public sealed class VoyageFurniture46 : VoyageFurnitureItem { public override int Index => 46; }
public sealed class VoyageFurniture47 : VoyageFurnitureItem { public override int Index => 47; }
public sealed class VoyageFurniture48 : VoyageFurnitureItem { public override int Index => 48; }
public sealed class VoyageFurniture49 : VoyageFurnitureItem { public override int Index => 49; }
public sealed class VoyageFurniture50 : VoyageFurnitureItem { public override int Index => 50; }
public sealed class VoyageFurniture51 : VoyageFurnitureItem { public override int Index => 51; }
public sealed class VoyageFurniture52 : VoyageFurnitureItem { public override int Index => 52; }
public sealed class VoyageFurniture53 : VoyageFurnitureItem { public override int Index => 53; }
public sealed class VoyageFurniture54 : VoyageFurnitureItem { public override int Index => 54; }
public sealed class VoyageFurniture55 : VoyageFurnitureItem { public override int Index => 55; }
public sealed class VoyageFurniture56 : VoyageFurnitureItem { public override int Index => 56; }
public sealed class VoyageFurniture57 : VoyageFurnitureItem { public override int Index => 57; }
public sealed class VoyageFurniture58 : VoyageFurnitureItem { public override int Index => 58; }
public sealed class VoyageFurniture59 : VoyageFurnitureItem { public override int Index => 59; }
public sealed class VoyageFurniture60 : VoyageFurnitureItem { public override int Index => 60; }
public sealed class VoyageFurniture61 : VoyageFurnitureItem { public override int Index => 61; }
public sealed class VoyageFurniture62 : VoyageFurnitureItem { public override int Index => 62; }
public sealed class VoyageFurniture63 : VoyageFurnitureItem { public override int Index => 63; }
public sealed class VoyageFurniture64 : VoyageFurnitureItem { public override int Index => 64; }
public sealed class VoyageFurniture65 : VoyageFurnitureItem { public override int Index => 65; }
public sealed class VoyageFurniture66 : VoyageFurnitureItem { public override int Index => 66; }
public sealed class VoyageFurniture67 : VoyageFurnitureItem { public override int Index => 67; }
public sealed class VoyageFurniture68 : VoyageFurnitureItem { public override int Index => 68; }
public sealed class VoyageFurniture69 : VoyageFurnitureItem { public override int Index => 69; }
public sealed class VoyageFurniture70 : VoyageFurnitureItem { public override int Index => 70; }
public sealed class VoyageFurniture71 : VoyageFurnitureItem { public override int Index => 71; }
public sealed class VoyageFurniture72 : VoyageFurnitureItem { public override int Index => 72; }
public sealed class VoyageFurniture73 : VoyageFurnitureItem { public override int Index => 73; }
public sealed class VoyageFurniture74 : VoyageFurnitureItem { public override int Index => 74; }
public sealed class VoyageFurniture75 : VoyageFurnitureItem { public override int Index => 75; }
public sealed class VoyageFurniture76 : VoyageFurnitureItem { public override int Index => 76; }
public sealed class VoyageFurniture77 : VoyageFurnitureItem { public override int Index => 77; }
public sealed class VoyageFurniture78 : VoyageFurnitureItem { public override int Index => 78; }
public sealed class VoyageFurniture79 : VoyageFurnitureItem { public override int Index => 79; }
public sealed class VoyageFurniture80 : VoyageFurnitureItem { public override int Index => 80; }
public sealed class VoyageFurniture81 : VoyageFurnitureItem { public override int Index => 81; }
public sealed class VoyageFurniture82 : VoyageFurnitureItem { public override int Index => 82; }
public sealed class VoyageFurniture83 : VoyageFurnitureItem { public override int Index => 83; }
public sealed class VoyageFurniture84 : VoyageFurnitureItem { public override int Index => 84; }

public sealed class VoyageBlock0Tile : VoyageBlockTile { public override int Theme => 0; }
public sealed class VoyagePlatform0Tile : VoyagePlatformTile { public override int Theme => 0; }
public sealed class VoyageBlock1Tile : VoyageBlockTile { public override int Theme => 1; }
public sealed class VoyagePlatform1Tile : VoyagePlatformTile { public override int Theme => 1; }
public sealed class VoyageBlock2Tile : VoyageBlockTile { public override int Theme => 2; }
public sealed class VoyagePlatform2Tile : VoyagePlatformTile { public override int Theme => 2; }
public sealed class VoyageBlock3Tile : VoyageBlockTile { public override int Theme => 3; }
public sealed class VoyagePlatform3Tile : VoyagePlatformTile { public override int Theme => 3; }
public sealed class VoyageBlock4Tile : VoyageBlockTile { public override int Theme => 4; }
public sealed class VoyagePlatform4Tile : VoyagePlatformTile { public override int Theme => 4; }
public sealed class VoyageBlock5Tile : VoyageBlockTile { public override int Theme => 5; }
public sealed class VoyagePlatform5Tile : VoyagePlatformTile { public override int Theme => 5; }
public sealed class VoyageBlock6Tile : VoyageBlockTile { public override int Theme => 6; }
public sealed class VoyagePlatform6Tile : VoyagePlatformTile { public override int Theme => 6; }
public sealed class VoyageBlock7Tile : VoyageBlockTile { public override int Theme => 7; }
public sealed class VoyagePlatform7Tile : VoyagePlatformTile { public override int Theme => 7; }
public sealed class VoyageBlock8Tile : VoyageBlockTile { public override int Theme => 8; }
public sealed class VoyagePlatform8Tile : VoyagePlatformTile { public override int Theme => 8; }
public sealed class VoyageBlock9Tile : VoyageBlockTile { public override int Theme => 9; }
public sealed class VoyagePlatform9Tile : VoyagePlatformTile { public override int Theme => 9; }
public sealed class VoyageBlock10Tile : VoyageBlockTile { public override int Theme => 10; }
public sealed class VoyagePlatform10Tile : VoyagePlatformTile { public override int Theme => 10; }
public sealed class VoyageBlock11Tile : VoyageBlockTile { public override int Theme => 11; }
public sealed class VoyagePlatform11Tile : VoyagePlatformTile { public override int Theme => 11; }
public sealed class VoyageBlock12Tile : VoyageBlockTile { public override int Theme => 12; }
public sealed class VoyagePlatform12Tile : VoyagePlatformTile { public override int Theme => 12; }
public sealed class VoyageBlock13Tile : VoyageBlockTile { public override int Theme => 13; }
public sealed class VoyagePlatform13Tile : VoyagePlatformTile { public override int Theme => 13; }
public sealed class VoyageBlock14Tile : VoyageBlockTile { public override int Theme => 14; }
public sealed class VoyagePlatform14Tile : VoyagePlatformTile { public override int Theme => 14; }
public sealed class VoyageBlock15Tile : VoyageBlockTile { public override int Theme => 15; }
public sealed class VoyagePlatform15Tile : VoyagePlatformTile { public override int Theme => 15; }
public sealed class VoyageBlock16Tile : VoyageBlockTile { public override int Theme => 16; }
public sealed class VoyagePlatform16Tile : VoyagePlatformTile { public override int Theme => 16; }

