using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace StarfallThrone.Content.Tiles;

public sealed class MiniDisplayTile : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true; Main.tileLavaDeath[Type] = false; Main.tileLighted[Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
        TileObjectData.newTile.StyleHorizontal = true; TileObjectData.newTile.StyleWrapLimit = 8;
        TileObjectData.addTile(Type);
        AddMapEntry(new Color(172, 152, 109), Language.GetText("Mods.StarfallThrone.Tiles.MiniDisplayTile.MapEntry"));
        DustType = DustID.WoodFurniture;
    }
    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
    {
        if (Main.tile[i, j].TileFrameX / 54 == 6) { r = 0.12f; g = 0.30f; b = 0.45f; }
    }
    public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile tile = Main.tile[i, j];
        if (!Terraria.GameContent.Drawing.TileDrawing.IsVisible(tile)) return false;
        if (tile.TileColor != 0) return true;
        if (tile.TileFrameX / 54 != 7 || tile.TileFrameY >= 36) return true;
        // Move only the two upper rows; the pot's base remains anchored to its tile.
        float sway = System.MathF.Sin((float)Main.GlobalTimeWrappedHourly * 1.6f + (i - tile.TileFrameX % 54 / 18) * 0.25f) * (tile.TileFrameY == 0 ? 1.2f : 0.6f);
        Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        var texture = Terraria.GameContent.TextureAssets.Tile[Type].Value;
        spriteBatch.Draw(texture, new Vector2(i * 16 + sway, j * 16) - Main.screenPosition + offset,
            new Rectangle(tile.TileFrameX, tile.TileFrameY, 16, 16), Lighting.GetColor(i, j));
        return false;
    }
}
public sealed class MiniLanternTile : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true; Main.tileLighted[Type] = true; Main.tileLavaDeath[Type] = false;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x2Top);
        TileObjectData.addTile(Type);
        AddMapEntry(new Color(242, 196, 81), Language.GetText("Mods.StarfallThrone.Tiles.MiniLanternTile.MapEntry"));
        DustType = DustID.GoldFlame;
        AddToArray(ref TileID.Sets.RoomNeeds.CountsAsTorch);
    }
    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) { r = 0.65f; g = 0.43f; b = 0.14f; }
}
