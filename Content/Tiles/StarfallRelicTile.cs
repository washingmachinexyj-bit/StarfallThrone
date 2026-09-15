using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace StarfallThrone.Content.Tiles;

public sealed class StarfallRelicTile : ModTile
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
        TileObjectData.addTile(Type);

        AddMapEntry(new Color(175, 110, 255), Language.GetText("Mods.StarfallThrone.Tiles.StarfallRelicTile.MapEntry"));
        DustType = DustID.ShimmerSpark;
    }
}
