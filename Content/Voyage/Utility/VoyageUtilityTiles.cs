using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace StarfallThrone.Content.Voyage.Utility;

public static class VoyageUtilityTiles
{
    public static int TypeFor(int utility) => utility switch
    {
        0 => ModContent.TileType<VoyageCargoTerminalTile>(),
        3 => ModContent.TileType<VoyageRepairStationTile>(),
        5 => ModContent.TileType<VoyageEcologyLampTile>(),
        7 => ModContent.TileType<VoyageStarChartTile>(),
        8 => ModContent.TileType<VoyageArkDisplayTile>(),
        _ => throw new ArgumentOutOfRangeException(nameof(utility))
    };
}

public abstract class VoyageUtilityTile : ModTile
{
    public abstract int Utility { get; }
    public override string Texture => VoyageCatalog.Root + "Utility" + Utility;
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileLavaDeath[Type] = false;
        Main.tileLighted[Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.StyleWrapLimit = 3;
        TileObjectData.newTile.LavaDeath = false;
        TileObjectData.addTile(Type);
        AddMapEntry(new Color(90, 185, 205), Language.GetText("Mods.StarfallThrone.Items.VoyageUtility" + Utility + ".DisplayName"));
        DustType = DustID.Electric;
        HitSound = SoundID.Tink;
        if (Utility == 5) AddToArray(ref TileID.Sets.RoomNeeds.CountsAsTorch);
        else AddToArray(ref TileID.Sets.RoomNeeds.CountsAsTable);
    }
    public override IEnumerable<Item> GetItemDrops(int i, int j)
    { yield return new Item(VoyageCatalog.Item("VoyageUtility" + Utility)); }
    public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => Utility != 5;
    public override void MouseOver(int i, int j)
    {
        Player player = Main.LocalPlayer;
        player.noThrow = 2;
        player.cursorItemIconEnabled = true;
        player.cursorItemIconID = VoyageCatalog.Item("VoyageUtility" + Utility);
    }
    public override bool RightClick(int i, int j)
    {
        if (!VoyageUtilityPlayer.InReach(Main.LocalPlayer, i, j)) return false;
        Main.mouseRightRelease = false;
        switch (Utility)
        {
            case 0: VoyageUtilityUI.Open(0, i, j); break;
            case 3:
                VoyageUtilityPlayer.RequestFurniture(1, i, j);
                SoundEngine.PlaySound(SoundID.Item4, new Vector2(i * 16, j * 16));
                break;
            case 7: VoyageUtilityUI.Open(7, i, j); break;
            case 8:
                VoyageUtilityPlayer.RequestFurniture(2, i, j);
                SoundEngine.PlaySound(SoundID.MenuTick);
                break;
            default: return false;
        }
        return true;
    }
    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
    {
        if (Utility == 5) { r = .3f; g = .75f; b = .48f; }
        else if (Utility == 8) { r = .35f; g = .26f; b = .14f; }
        else { r = .10f; g = .22f; b = .28f; }
    }
    public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile tile = Main.tile[i, j];
        if (!Terraria.GameContent.Drawing.TileDrawing.IsVisible(tile) || tile.TileFrameX % 54 != 0 || tile.TileFrameY % 54 != 0) return false;
        Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        Vector2 center = new Vector2(i * 16 + 24, j * 16 + 24) - Main.screenPosition + offset;
        Color lighting = Lighting.GetColor(i + 1, j + 1);
        if (tile.TileColor != 0) lighting = lighting.MultiplyRGB(WorldGen.paintColor(tile.TileColor));
        VoyageFurnitureDrawing.DrawUtility(spriteBatch, center, Utility, 1f, lighting, tile.TileFrameX / 54 % 3);
        return false;
    }
}
public sealed class VoyageCargoTerminalTile : VoyageUtilityTile { public override int Utility => 0; }
public sealed class VoyageRepairStationTile : VoyageUtilityTile { public override int Utility => 3; }
public sealed class VoyageEcologyLampTile : VoyageUtilityTile { public override int Utility => 5; }
public sealed class VoyageStarChartTile : VoyageUtilityTile { public override int Utility => 7; }
public sealed class VoyageArkDisplayTile : VoyageUtilityTile { public override int Utility => 8; }

public abstract class VoyageDecorTile : ModTile
{
    public abstract int Kind { get; }
    public override string Texture => VoyageCatalog.Root + "Material0";
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileLavaDeath[Type] = false;
        Main.tileLighted[Type] = Kind == 1;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.StyleWrapLimit = VoyageCatalog.Count;
        TileObjectData.newTile.LavaDeath = false;
        if (Kind == 0) { TileObjectData.newTile.AnchorBottom = default; TileObjectData.newTile.AnchorWall = true; }
        TileObjectData.addTile(Type);
        for (int theme = 0; theme < VoyageCatalog.Count; theme++)
            AddMapEntry(VoyageCatalog.Colors[theme], Language.GetText("Mods.StarfallThrone.Items.VoyageFurniture" + (theme * 5 + Kind) + ".DisplayName"));
        DustType = DustID.Electric; HitSound = SoundID.Tink;
        if (Kind == 1) AddToArray(ref TileID.Sets.RoomNeeds.CountsAsTorch);
    }
    public override ushort GetMapOption(int i, int j) => (ushort)Math.Clamp(Main.tile[i, j].TileFrameX / 54, 0, 16);
    public override IEnumerable<Item> GetItemDrops(int i, int j)
    {
        int theme = Math.Clamp(Main.tile[i, j].TileFrameX / 54, 0, 16);
        yield return new Item(VoyageCatalog.Item("VoyageFurniture" + (theme * 5 + Kind)));
    }
    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
    {
        if (Kind != 1) return;
        Color c = VoyageCatalog.Colors[Math.Clamp(Main.tile[i, j].TileFrameX / 54, 0, 16)];
        r = c.R / 255f * .72f; g = c.G / 255f * .72f; b = c.B / 255f * .72f;
    }
    public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile tile = Main.tile[i, j];
        if (!Terraria.GameContent.Drawing.TileDrawing.IsVisible(tile) || tile.TileFrameX % 54 != 0 || tile.TileFrameY % 54 != 0) return false;
        Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        Vector2 center = new Vector2(i * 16 + 24, j * 16 + 24) - Main.screenPosition + offset;
        Color lighting = Lighting.GetColor(i + 1, j + 1);
        if (tile.TileColor != 0) lighting = lighting.MultiplyRGB(WorldGen.paintColor(tile.TileColor));
        VoyageFurnitureDrawing.Draw(spriteBatch, center, Math.Clamp(tile.TileFrameX / 54, 0, 16), Kind, 1f, lighting);
        return false;
    }
}
public sealed class VoyagePlaqueTile : VoyageDecorTile { public override int Kind => 0; }
public sealed class VoyageLampTile : VoyageDecorTile { public override int Kind => 1; }
public sealed class VoyageStatueTile : VoyageDecorTile { public override int Kind => 2; }

public abstract class VoyageBlockTile : ModTile
{
    public abstract int Theme { get; }
    public override string Texture => "Terraria/Images/Tiles_1";
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true; Main.tileBlockLight[Type] = true; Main.tileBrick[Type] = true;
        Main.tileMergeDirt[Type] = true;
        DustType = DustID.Stone; HitSound = SoundID.Tink;
        AddMapEntry(VoyageCatalog.Colors[Theme], Language.GetText("Mods.StarfallThrone.Items.VoyageFurniture" + (Theme * 5 + 4) + ".DisplayName"));
    }
    public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref TileDrawInfo drawData)
        => drawData.tileLight = drawData.tileLight.MultiplyRGB(Color.Lerp(VoyageCatalog.Colors[Theme], Color.White, .25f));
    public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile tile = Main.tile[i, j];
        if (!Terraria.GameContent.Drawing.TileDrawing.IsVisible(tile) || tile.Slope != SlopeType.Solid || tile.IsHalfBlock) return;
        Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        Vector2 center = new Vector2(i * 16 + 8, j * 16 + 8) - Main.screenPosition + offset;
        VoyageFurnitureDrawing.BlockMark(spriteBatch, center, Theme, 1, Lighting.GetColor(i, j));
    }
}

public abstract class VoyagePlatformTile : ModTile
{
    public abstract int Theme { get; }
    public override string Texture => "Terraria/Images/Tiles_19";
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true; Main.tileSolidTop[Type] = true;
        Main.tileSolid[Type] = true; Main.tileNoAttach[Type] = true; Main.tileTable[Type] = true;
        Main.tileLavaDeath[Type] = false;
        TileID.Sets.Platforms[Type] = true; TileID.Sets.DisableSmartCursor[Type] = true;
        AddToArray(ref TileID.Sets.RoomNeeds.CountsAsDoor);
        AdjTiles = new int[] { TileID.Platforms };
        DustType = DustID.Stone; HitSound = SoundID.Tink;
        AddMapEntry(VoyageCatalog.Colors[Theme], Language.GetText("Mods.StarfallThrone.Items.VoyageFurniture" + (Theme * 5 + 3) + ".DisplayName"));
        TileObjectData.newTile.CoordinateHeights = new[] { 16 };
        TileObjectData.newTile.CoordinateWidth = 16; TileObjectData.newTile.CoordinatePadding = 2;
        TileObjectData.newTile.StyleHorizontal = true; TileObjectData.newTile.StyleMultiplier = 27;
        TileObjectData.newTile.StyleWrapLimit = 27; TileObjectData.newTile.UsesCustomCanPlace = false;
        TileObjectData.newTile.LavaDeath = false;
        TileObjectData.addTile(Type);
    }
    public override void PostSetDefaults() => Main.tileNoSunLight[Type] = false;
    public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref TileDrawInfo drawData)
        => drawData.tileLight = drawData.tileLight.MultiplyRGB(Color.Lerp(VoyageCatalog.Colors[Theme], Color.White, .15f));
}
