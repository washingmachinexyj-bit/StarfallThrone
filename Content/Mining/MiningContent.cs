using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace StarfallThrone.Content.Mining;

public abstract class MiningMaterial : ModItem
{
    public abstract int Index { get; }
    public abstract int Kind { get; }
    public override string Texture => MiningCatalog.Root + (Kind == 0 ? "Ore" : Kind == 1 ? "Bar" : "Component") + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = Kind == 0 ? 100 : 25;
    public override void SetDefaults()
    {
        if (Kind == 0) Item.DefaultToPlaceableTile(MiningCatalog.OreTileType(Index));
        Item.width = 32; Item.height = Kind == 1 ? 24 : 32;
        Item.maxStack = 9999; Item.material = true;
        Item.rare = Index <= 3 ? ItemRarityID.White : Index < 8 ? ItemRarityID.Cyan : ItemRarityID.Red;
        Item.value = Item.sellPrice(copper: (Index < 4 ? 8 + Index * 6 : 150 + Index * 80) * (Kind == 0 ? 1 : Kind == 1 ? 3 : 6));
    }
}

public sealed class ShellPatternFragment : ModItem
{
    public override string Texture => MiningCatalog.Root + "ShellPatternFragment";
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 25;
    public override void SetDefaults() { Item.width = Item.height = 24; Item.maxStack = 9999; Item.material = true; Item.value = 10; }
}

public abstract class MiningOreTile : ModTile
{
    public abstract int Index { get; }
    public override string Texture => MiningCatalog.Root + "OreTile" + Index;
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;
        Main.tileBlockLight[Type] = true;
        Main.tileLighted[Type] = true;
        Main.tileSpelunker[Type] = true;
        Main.tileOreFinderPriority[Type] = (short)(310 + Index * 5);
        TileID.Sets.Ore[Type] = true;
        TileID.Sets.CanBeClearedDuringGeneration[Type] = false;
        MinPick = MiningCatalog.PickRequirements[Index];
        MineResist = Index < 4 ? 1.05f + Index * .12f : 2.6f + (Index - 4) * .38f;
        DustType = DustID.Stone;
        HitSound = SoundID.Tink;
        AddMapEntry(MiningCatalog.Colors[Index], Language.GetText("Mods.StarfallThrone.Items.MiningOre" + Index + ".DisplayName"));
        RegisterItemDrop(MiningCatalog.OreType(Index));
    }
    public override bool CanExplode(int i, int j) => false;
    public override bool CanReplace(int i, int j, int tileTypeBeingPlaced) => false;
    public override bool Slope(int i, int j) => false;
    public override bool TileFrame(int i, int j, ref bool resetFrame, ref bool noBreak)
    {
        Main.tile[i, j].TileFrameX = Main.tile[i, j].TileFrameY = 0;
        return false;
    }
    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
    {
        float brightness = Index < 4 ? .025f : .055f;
        Vector3 color = MiningCatalog.Colors[Index].ToVector3() * brightness;
        r = color.X; g = color.Y; b = color.Z;
    }
}

public abstract class MiningStationItem : ModItem
{
    public abstract int Index { get; }
    public override string Texture => MiningCatalog.Root + "Station" + Index;
    public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(MiningCatalog.StationTileType(Index));
        Item.width = Item.height = 32; Item.maxStack = 99;
        Item.rare = Index <= 2 ? ItemRarityID.Blue : Index < 6 ? ItemRarityID.Cyan : ItemRarityID.Red;
        Item.value = Item.sellPrice(silver: Index <= 2 ? 5 + Index * 8 : 120 + Index * 40);
    }
}

public abstract class MiningStationTile : ModTile
{
    public abstract int Index { get; }
    public override string Texture => MiningCatalog.Root + "StationTile" + Index;
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true; Main.tileNoAttach[Type] = true;
        Main.tileTable[Type] = true; Main.tileLavaDeath[Type] = false; Main.tileLighted[Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
        TileObjectData.newTile.LavaDeath = false;
        TileObjectData.addTile(Type);
        AddToArray(ref TileID.Sets.RoomNeeds.CountsAsTable);
        AddMapEntry(MiningCatalog.Colors[System.Math.Min(12, Index + 1)], Language.GetText("Mods.StarfallThrone.Items.MiningStationItem" + Index + ".DisplayName"));
        DustType = Index < 3 ? DustID.WoodFurniture : DustID.Electric;
        HitSound = SoundID.Tink;
        // Native furniture drops once through GetItemDrops, never additionally in KillMultiTile.
    }
    public override IEnumerable<Item> GetItemDrops(int i, int j) { yield return new Item(MiningCatalog.StationItemType(Index)); }
    public override void MouseOver(int i, int j)
    {
        Main.LocalPlayer.noThrow = 2;
        Main.LocalPlayer.cursorItemIconEnabled = true;
        Main.LocalPlayer.cursorItemIconID = MiningCatalog.StationItemType(Index);
    }
    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
    {
        Color c = MiningCatalog.Colors[System.Math.Min(12, Index + 1)];
        float intensity = Index <= 2 ? .07f : .12f;
        r = c.R / 255f * intensity; g = c.G / 255f * intensity; b = c.B / 255f * intensity;
    }
}

public sealed class MiningAdjacencySystem : ModSystem
{
    public override void PostSetupContent()
    {
        for (int i = 0; i < MiningCatalog.StationCount; i++)
        {
            ModTile tile = TileLoader.GetTile(MiningCatalog.StationTileType(i));
            List<int> previous = new() { TileID.WorkBenches };
            // Early stations remain a separate optional ladder. Late stations inherit the
            // earlier late-game mod stations, never the vanilla anvils/altars by accident.
            int first = i < 3 ? 0 : 3;
            for (int j = first; j < i; j++) previous.Add(MiningCatalog.StationTileType(j));
            tile.AdjTiles = previous.ToArray();
        }
    }
}
