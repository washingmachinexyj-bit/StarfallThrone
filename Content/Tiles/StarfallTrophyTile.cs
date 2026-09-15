using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace StarfallThrone.Content.Tiles;

public sealed class StarfallTrophyTile : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileNoAttach[Type] = true;
        Main.tileLavaDeath[Type] = false;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.StyleWrapLimit = 27;
        TileObjectData.newTile.StyleLineSkip = 1;
        TileObjectData.newTile.AnchorBottom = default;
        TileObjectData.newTile.AnchorWall = true;
        TileObjectData.addTile(Type);

        AddMapEntry(new Color(255, 185, 75), Language.GetText("Mods.StarfallThrone.Tiles.StarfallTrophyTile.MapEntry"));
        DustType = DustID.GoldFlame;
    }
}
