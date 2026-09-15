using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace StarfallThrone.Content.Mining;

public abstract class MiningUtilityTile : ModTile
{
    public abstract int Index { get; }
    public override string Texture => MiningCatalog.Root + "MiningUtilityTile" + Index;
    public static int TypeFor(int index) => ModContent.Find<ModTile>("StarfallThrone", "MiningUtilityTile" + index).Type;
    public static int ItemFor(int index) => MiningCatalog.ItemType("MiningUtilityItem" + index);
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true; Main.tileLavaDeath[Type] = false;
        Main.tileLighted[Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.LavaDeath = false;
        if (Index == 12)
            TileObjectData.newTile.HookPostPlaceMyPlayer = ModContent.GetInstance<MiningCrystallizerEntity>().Generic_HookPostPlaceMyPlayer;
        TileObjectData.addTile(Type);
        AddMapEntry(new Color(125, 170, 160), Language.GetText("Mods.StarfallThrone.Items.MiningUtilityItem" + Index + ".DisplayName"));
        if (Index is 0 or 1 or 2 or 4 or 5 or 6 or 11) AddToArray(ref TileID.Sets.RoomNeeds.CountsAsTorch);
        DustType = DustID.Stone; HitSound = SoundID.Tink;
    }
    public override IEnumerable<Item> GetItemDrops(int i, int j) { yield return new Item(ItemFor(Index)); }
    public override void KillMultiTile(int i, int j, int frameX, int frameY)
    { if (Index == 12) ModContent.GetInstance<MiningCrystallizerEntity>().Kill(i, j); }
    public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => Index is 0 or 11 or 12;
    public override void MouseOver(int i, int j)
    {
        Main.LocalPlayer.noThrow = 2; Main.LocalPlayer.cursorItemIconEnabled = true;
        Main.LocalPlayer.cursorItemIconID = ItemFor(Index);
    }
    public override bool RightClick(int i, int j)
    {
        if (Index is not (0 or 11 or 12) || !MiningPlayer.InFurnitureReach(Main.LocalPlayer, i, j)) return false;
        Main.mouseRightRelease = false; MiningPlayer.RequestFurniture(i, j); return true;
    }
    public static void Interact(Player player, int i, int j)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !MiningPlayer.InFurnitureReach(player, i, j)) return;
        Tile tile = Main.tile[i, j];
        if (!tile.HasTile || TileLoader.GetTile(tile.TileType) is not MiningUtilityTile utility || utility.Index >= 13) return;
        int x = i - tile.TileFrameX % 54 / 18, y = j - tile.TileFrameY % 54 / 18;
        if (!WorldGen.InWorld(x, y, 4)) return;
        for (int dx = 0; dx < 3; dx++)
        for (int dy = 0; dy < 3; dy++)
            if (!Main.tile[x + dx, y + dy].HasTile || Main.tile[x + dx, y + dy].TileType != tile.TileType) return;
        if (utility.Index == 12)
        {
            if (TileEntity.ByPosition.TryGetValue(new Point16(x, y), out TileEntity entity) && entity is MiningCrystallizerEntity vat)
                vat.Interact(player);
        }
        else if (utility.Index is 0 or 11)
        {
            int next = (tile.TileFrameX / 54 + 1) % (utility.Index == 0 ? 4 : 5);
            for (int dx = 0; dx < 3; dx++)
            for (int dy = 0; dy < 3; dy++) Main.tile[x + dx, y + dy].TileFrameX = (short)(next * 54 + dx * 18);
            if (Main.netMode == NetmodeID.Server) NetMessage.SendTileSquare(-1, x, y, 3, 3);
        }
    }
    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
    {
        Vector3 light = Index switch
        {
            0 => new(.12f, .3f, .12f), 1 => new(.17f, .3f, .3f), 2 => new(.3f, .22f, .12f),
            4 => new(.35f, .60f, .8f), 5 => new(.75f, .65f, .32f), 6 => new(.8f, .23f, .10f),
            8 => new(.15f, .32f, .23f), 10 => new(.32f, .28f, .38f),
            11 => PrismColor(Main.tile[i, j].TileFrameX / 54).ToVector3() * .7f,
            12 => new(.10f, .22f, .18f), _ => Vector3.Zero
        };
        r = light.X; g = light.Y; b = light.Z;
    }
    public static Color PrismColor(int state) => (state % 5) switch
    { 0 => Color.White, 1 => Color.LightSkyBlue, 2 => Color.Violet, 3 => Color.LightGreen, _ => Color.Gold };
    public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile tile = Main.tile[i, j];
        if (!Terraria.GameContent.Drawing.TileDrawing.IsVisible(tile) || tile.TileFrameX % 54 != 0 || tile.TileFrameY % 54 != 0) return false;
        Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        Vector2 at = new Vector2(i * 16, j * 16) - Main.screenPosition + offset;
        Color color = Lighting.GetColor(i + 1, j + 1);
        if (tile.TileColor != 0) color = color.MultiplyRGB(WorldGen.paintColor(tile.TileColor));
        Texture2D texture = TextureAssets.Tile[Type].Value;
        spriteBatch.Draw(texture, new Rectangle((int)at.X, (int)at.Y, 48, 48), null, color);
        if (Index == 0)
            DrawWaymarkerArrow(spriteBatch, at + new Vector2(24, 18), tile.TileFrameX / 54);
        if (Index == 11)
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)at.X + 40, (int)at.Y + 39, 3, 3), PrismColor(tile.TileFrameX / 54));
        if (Index == 12 && TileEntity.ByPosition.TryGetValue(new Point16(i, j), out TileEntity entity) && entity is MiningCrystallizerEntity vat && vat.PendingCount > 0)
        {
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)at.X + 8, (int)at.Y + 36, 32, 3), Color.DarkSlateGray);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)at.X + 8, (int)at.Y + 36, (int)(32 * vat.ProgressFraction), 3), Color.LightGreen);
        }
        return false;
    }
    internal static void DrawWaymarkerArrow(SpriteBatch batch, Vector2 center, int rotation)
    {
        Vector2 direction = (rotation * MathHelper.PiOver2).ToRotationVector2();
        DrawLine(batch, center - direction * 8, center + direction * 8, Color.LightGreen, 2);
        DrawLine(batch, center + direction * 8, center + direction * 2 + direction.RotatedBy(MathHelper.PiOver2) * 5, Color.LightGreen, 2);
        DrawLine(batch, center + direction * 8, center + direction * 2 - direction.RotatedBy(MathHelper.PiOver2) * 5, Color.LightGreen, 2);
    }
    internal static void DrawLine(SpriteBatch batch, Vector2 a, Vector2 b, Color color, int width)
        // MagicPixel's native texture is not guaranteed to be 1x1. Scale exactly one texel.
        => batch.Draw(TextureAssets.MagicPixel.Value, a, new Rectangle(0, 0, 1, 1), color, (b - a).ToRotation(), new Vector2(0, .5f), new Vector2((b - a).Length(), width), SpriteEffects.None, 0);
}
public sealed class MiningUtilityTile0 : MiningUtilityTile { public override int Index => 0; }
public sealed class MiningUtilityTile1 : MiningUtilityTile { public override int Index => 1; }
public sealed class MiningUtilityTile2 : MiningUtilityTile { public override int Index => 2; }
public sealed class MiningUtilityTile3 : MiningUtilityTile { public override int Index => 3; }
public sealed class MiningUtilityTile4 : MiningUtilityTile { public override int Index => 4; }
public sealed class MiningUtilityTile5 : MiningUtilityTile { public override int Index => 5; }
public sealed class MiningUtilityTile6 : MiningUtilityTile { public override int Index => 6; }
public sealed class MiningUtilityTile7 : MiningUtilityTile { public override int Index => 7; }
public sealed class MiningUtilityTile8 : MiningUtilityTile { public override int Index => 8; }
public sealed class MiningUtilityTile9 : MiningUtilityTile { public override int Index => 9; }
public sealed class MiningUtilityTile10 : MiningUtilityTile { public override int Index => 10; }
public sealed class MiningUtilityTile11 : MiningUtilityTile { public override int Index => 11; }
public sealed class MiningUtilityTile12 : MiningUtilityTile { public override int Index => 12; }

public abstract class MiningUtilityPlatform : MiningUtilityTile
{
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true; Main.tileSolid[Type] = true; Main.tileSolidTop[Type] = true;
        Main.tileTable[Type] = true; Main.tileNoAttach[Type] = true; Main.tileLavaDeath[Type] = false;
        TileID.Sets.Platforms[Type] = true; TileID.Sets.DisableSmartCursor[Type] = true;
        AddToArray(ref TileID.Sets.RoomNeeds.CountsAsDoor); AdjTiles = new int[] { TileID.Platforms };
        TileObjectData.newTile.CoordinateHeights = new[] { 16 };
        TileObjectData.newTile.CoordinateWidth = 16; TileObjectData.newTile.CoordinatePadding = 2;
        TileObjectData.newTile.StyleHorizontal = true; TileObjectData.newTile.StyleMultiplier = 27;
        TileObjectData.newTile.StyleWrapLimit = 27; TileObjectData.newTile.UsesCustomCanPlace = false;
        TileObjectData.newTile.LavaDeath = false; TileObjectData.addTile(Type);
        AddMapEntry(Index == 13 ? Color.MediumPurple : Color.DarkOrange,
            Language.GetText("Mods.StarfallThrone.Items.MiningUtilityItem" + Index + ".DisplayName"));
        DustType = DustID.Stone; HitSound = SoundID.Tink;
    }
    public override void PostSetDefaults() => Main.tileNoSunLight[Type] = false;
    public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile tile = Main.tile[i, j];
        if (!Terraria.GameContent.Drawing.TileDrawing.IsVisible(tile)) return false;
        Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        Vector2 at = new Vector2(i * 16, j * 16) - Main.screenPosition + offset;
        Texture2D texture = TextureAssets.Tile[Type].Value;
        Color color = Lighting.GetColor(i, j);
        if (tile.TileColor != 0) color = color.MultiplyRGB(WorldGen.paintColor(tile.TileColor));
        spriteBatch.Draw(texture, new Rectangle((int)at.X, (int)at.Y, 16, 16), null, color);
        return false;
    }
}
public sealed class MiningUtilityTile13 : MiningUtilityPlatform { public override int Index => 13; }
public sealed class MiningUtilityTile14 : MiningUtilityPlatform { public override int Index => 14; }

public abstract class MiningCrystalTile : ModTile
{
    public abstract int Index { get; }
    public override string Texture => MiningCatalog.Root + "MiningCrystalTile" + Index;
    public static int TypeFor(int index) => ModContent.Find<ModTile>("StarfallThrone", "MiningCrystalTile" + index).Type;
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true; Main.tileBlockLight[Type] = false; Main.tileLighted[Type] = true;
        MinPick = MiningCatalog.PickRequirements[Index]; MineResist = Index < 4 ? 1f : 3f;
        DustType = DustID.GemEmerald; HitSound = SoundID.Tink;
        AddMapEntry(MiningCatalog.Colors[Index], Language.GetText("Mods.StarfallThrone.Items.MiningOre" + Index + ".DisplayName"));
    }
    public override bool CanExplode(int i, int j) => false;
    public override bool CanReplace(int i, int j, int tileTypeBeingPlaced) => false;
    public override bool Slope(int i, int j) => false;
    public override bool TileFrame(int i, int j, ref bool resetFrame, ref bool noBreak)
    {
        Tile tile = Main.tile[i, j]; tile.TileFrameX = tile.TileFrameY = 0; return false;
    }
    public override IEnumerable<Item> GetItemDrops(int i, int j) { yield return new Item(MiningCatalog.OreType(Index), 4); }
    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
    {
        Vector3 c = MiningCatalog.Colors[Index].ToVector3() * .28f; r = c.X; g = c.Y; b = c.Z;
    }
    public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
    {
        if (!Terraria.GameContent.Drawing.TileDrawing.IsVisible(Main.tile[i, j])) return false;
        Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        Vector2 at = new Vector2(i * 16, j * 16) - Main.screenPosition + offset;
        spriteBatch.Draw(TextureAssets.Tile[Type].Value, new Rectangle((int)at.X, (int)at.Y, 16, 16), null, Lighting.GetColor(i, j));
        return false;
    }
}
public sealed class MiningCrystalTile0 : MiningCrystalTile { public override int Index => 0; }
public sealed class MiningCrystalTile1 : MiningCrystalTile { public override int Index => 1; }
public sealed class MiningCrystalTile2 : MiningCrystalTile { public override int Index => 2; }
public sealed class MiningCrystalTile3 : MiningCrystalTile { public override int Index => 3; }
public sealed class MiningCrystalTile4 : MiningCrystalTile { public override int Index => 4; }
public sealed class MiningCrystalTile5 : MiningCrystalTile { public override int Index => 5; }
public sealed class MiningCrystalTile6 : MiningCrystalTile { public override int Index => 6; }
public sealed class MiningCrystalTile7 : MiningCrystalTile { public override int Index => 7; }
public sealed class MiningCrystalTile8 : MiningCrystalTile { public override int Index => 8; }
public sealed class MiningCrystalTile9 : MiningCrystalTile { public override int Index => 9; }
public sealed class MiningCrystalTile10 : MiningCrystalTile { public override int Index => 10; }
public sealed class MiningCrystalTile11 : MiningCrystalTile { public override int Index => 11; }
public sealed class MiningCrystalTile12 : MiningCrystalTile { public override int Index => 12; }
