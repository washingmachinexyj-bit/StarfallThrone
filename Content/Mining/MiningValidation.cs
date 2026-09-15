using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.GameContent.ItemDropRules;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Mining;

/// <summary>Runs only inside the explicitly launched isolated client, never user saves.</summary>
public static class MiningValidation
{
    private static void Check(bool result, string detail)
    { if (!result) throw new InvalidOperationException("Mining validation: " + detail); }
    private static void Log(string message) => ModContent.GetInstance<MiningAdjacencySystem>().Mod.Logger.Info(message);
    public static void Data()
    {
        Check(Terraria.Program.LaunchParameters.ContainsKey("-starfall-art-smoke"), "explicit test guard");
        for (int i = 0; i < MiningCatalog.OreCount; i++)
        {
            ModTile tile = TileLoader.GetTile(MiningCatalog.OreTileType(i));
            Check(tile.MinPick == MiningCatalog.PickRequirements[i] && tile.MineResist > 1f, "ore mining definition " + i);
            Check(!tile.CanExplode(0, 0) && !tile.CanReplace(0, 0, TileID.Dirt), "bomb/swap bypass " + i);
            Check(new Item(MiningCatalog.OreType(i)).createTile == tile.Type, "placeable ore " + i);
            Check(TileLoader.GetItemDropFromTypeAndStyle(tile.Type) == MiningCatalog.OreType(i), "ore drop " + i);
            Check(Main.tileSpelunker[tile.Type] && TileID.Sets.Ore[tile.Type], "ore discovery " + i);
            foreach (int material in new[] { MiningCatalog.BarType(i), MiningCatalog.ComponentType(i) })
                Check(Main.recipe.Take(Recipe.numRecipes).Any(r => !r.Disabled && r.requiredItem.Any(x => x.type == material)), "unused refined material " + material);
            Texture2D texture = ModContent.Request<Texture2D>(tile.Texture, AssetRequestMode.ImmediateLoad).Value;
            Check(texture.Width >= 16 && texture.Height >= 16, "ore texture geometry " + i);
        }
        for (int i = 0; i < 14; i++)
        {
            Item tool = new(MiningCatalog.ToolType(i));
            Check(tool.pick == MiningCatalog.ToolPower[i], "tool pick " + i);
            Check(Main.recipe.Take(Recipe.numRecipes).Any(r => !r.Disabled && r.createItem.type == tool.type), "tool recipe " + i);
            if (i > 0) Check(tool.pick >= MiningCatalog.PickRequirements[Math.Min(i, 12)], "tool cannot open own stage " + i);
        }
        for (int i = 0; i < MiningCatalog.StationCount; i++)
        {
            Item item = new(MiningCatalog.StationItemType(i));
            int tileType = MiningCatalog.StationTileType(i);
            Check(item.createTile == tileType, "station placeable " + i);
            if (i != 0 && i != 3)
                Check(TileLoader.GetTile(tileType).AdjTiles.Contains(MiningCatalog.StationTileType(i - 1)), "station inheritance " + i);
            Check(!TileLoader.GetTile(tileType).AdjTiles.Contains(TileID.LihzahrdAltar), "vanilla gate adjacency " + i);
        }
        var shellDrops = new List<DropRateInfo>();
        foreach (var rule in Main.ItemDropsDB.GetRulesForNPCID(MiniBossData.NPCType(6), false))
            rule.ReportDroprates(shellDrops, new DropRateInfoChainFeed(1f));
        Check(shellDrops.Any(d => d.itemId == ModContent.ItemType<ShellPatternFragment>() && d.stackMin == 6 && d.stackMax == 6 && d.dropRate >= .999f), "six guaranteed extra shell fragments");
        Log("MINING_DATA_PASS ores=13 bars=13 components=13 stations=12 tools=14 shellDrop=6 minPickAndBlastSwapGuards=13");
        MiningRecipeValidation.Validate();
        Log(MiningWorld.SelfTest());
        Log(MiningWorldValidation.RunStateTests());
        MiningToolsValidation.Data();
    }

    public static void Run()
    {
        Check(Main.gameMenu && Terraria.Program.LaunchParameters.ContainsKey("-starfall-art-smoke"), "isolated menu only");
        NativeMiningThresholds();
        RenderAndBreakStations();
        MiningToolsValidation.Run();
        Log(MiningWorldValidation.RunGenerationTests());
    }

    private static void NativeMiningThresholds()
    {
        Player previousPlayer = Main.player[0];
        Item[] previousItems = (Item[])Main.item.Clone();
        int previousNet = Main.netMode, previousId = Main.myPlayer;
        bool previousActions = WorldGen.noTileActions, previousGen = WorldGen.gen;
        const int x = 740, y = 120;
        Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0;
        WorldGen.noTileActions = WorldGen.gen = false;
        for (int i = 0; i < Main.item.Length; i++) Main.item[i] = new Item();
        try
        {
            for (int ore = 0; ore < MiningCatalog.OreCount; ore++)
            {
                Player player = new() { active = true, whoAmI = 0, position = new Vector2((x-2)*16, (y-1)*16) };
                player.ResetEffects(); Main.player[0] = player;
                player.inventory[0] = new Item(MiningCatalog.ToolType(ore)); player.selectedItem = 0;
                for (int dx = -2; dx <= 2; dx++) for (int dy = -2; dy <= 2; dy++) Main.tile[x+dx,y+dy].ClearEverything();
                Tile tile = Main.tile[x,y]; tile.HasTile = true; tile.TileType = (ushort)MiningCatalog.OreTileType(ore);
                for (int hit = 0; hit < 20; hit++) player.PickTile(x,y,MiningCatalog.PickRequirements[ore]-1);
                Check(Main.tile[x,y].HasTile, "native lower pick must not mine ore " + ore);
                for (int hit = 0; hit < 40 && Main.tile[x,y].HasTile; hit++) player.PickTile(x,y,MiningCatalog.PickRequirements[ore]);
                Check(!Main.tile[x,y].HasTile, "native matching pick must mine ore " + ore);
                int dropped = Main.item.Where(it => it.active && it.type == MiningCatalog.OreType(ore)).Sum(it => it.stack);
                Check(dropped == 1, "one ore per native tile break " + ore + " got " + dropped);
            }
            Log("MINING_PICK_RUNTIME_PASS ores=13 underpoweredReject=13 thresholdMine=13 singleOreDrops=13");
        }
        finally
        {
            Main.player[0] = previousPlayer; Main.netMode = previousNet; Main.myPlayer = previousId;
            WorldGen.noTileActions = previousActions; WorldGen.gen = previousGen;
            for (int i = 0; i < Main.item.Length; i++) Main.item[i] = previousItems[i];
        }
    }

    private static void RenderAndBreakStations()
    {
        const int left = 780, top = 65;
        var device = Main.instance.GraphicsDevice;
        var targets = device.GetRenderTargets();
        using var atlas = new RenderTarget2D(device, 800, 440);
        var previousItems = (Item[])Main.item.Clone();
        Vector2 screen = Main.screenPosition; bool draw = Main.drawToScreen;
        int previousNet = Main.netMode;
        Main.netMode = NetmodeID.SinglePlayer;
        Main.drawToScreen = true;
        try
        {
            device.SetRenderTarget(atlas); device.Clear(new Color(24,29,39));
            int placed = 0;
            for (int index = 0; index < MiningCatalog.StationCount; index++)
            {
                for (int x = left - 2; x < left + 8; x++) for (int y = top - 2; y < top + 8; y++)
                { Tile ground = Main.tile[x,y]; ground.ClearEverything(); if (y >= top + 4) { ground.HasTile = true; ground.TileType = TileID.Dirt; } }
                for (int j = 0; j < Main.item.Length; j++) Main.item[j] = new Item();
                int tileType = MiningCatalog.StationTileType(index);
                TileObjectData data = TileObjectData.GetTileData(tileType, 0);
                int originX = left + data.Origin.X, originY = top + 4 - data.Height + data.Origin.Y;
                WorldGen.PlaceObject(originX, originY, tileType);
                int placedTop = top + 4 - data.Height;
                Check(Main.tile[left, placedTop].HasTile && Main.tile[left, placedTop].TileType == tileType, "native place station " + index);
                Vector2 cell = new(index % 6 * 128 + 32, index / 6 * 150 + 24);
                Main.screenPosition = new Vector2(left * 16, placedTop * 16) - cell;
                Texture2D texture = ModContent.Request<Texture2D>(TileLoader.GetTile(tileType).Texture, AssetRequestMode.ImmediateLoad).Value;
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                for (int x = 0; x < data.Width; x++) for (int y = 0; y < data.Height; y++)
                {
                    Tile t = Main.tile[left+x, placedTop+y];
                    Rectangle source = new(t.TileFrameX, t.TileFrameY, 16, data.CoordinateHeights[y]);
                    Check(source.Right <= texture.Width && source.Bottom <= texture.Height, "station frame out of texture " + index);
                    Main.spriteBatch.Draw(texture, cell + new Vector2(x * 16, y * 16), source, Color.White);
                }
                Main.spriteBatch.End();
                WorldGen.KillTile(left, placedTop);
                WorldGen.SquareTileFrame(left, placedTop, true);
                int amount = Main.item.Where(it => it.active && it.type == MiningCatalog.StationItemType(index)).Sum(it => it.stack);
                Check(amount == 1, "station must break into exactly one item " + index + " actual=" + amount);
                placed++;
            }
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            for (int i = 0; i < MiningCatalog.OreCount; i++)
            {
                var ore = ModContent.Request<Texture2D>(MiningCatalog.Root + "OreTile" + i).Value;
                for (int x = 0; x < 3; x++) for (int y = 0; y < 2; y++)
                    Main.spriteBatch.Draw(ore, new Vector2(i * 59 + x * 16 + 8, 330 + y * 16), Color.White);
                var bar = ModContent.Request<Texture2D>(MiningCatalog.Root + "Bar" + i).Value;
                Main.spriteBatch.Draw(bar, new Vector2(i * 59 + 15, 380), Color.White);
            }
            Main.spriteBatch.End();
            device.SetRenderTargets(targets);
            string path = Path.GetFullPath(Path.Combine(Main.SavePath, "..", "..", "outputs", "mining-world-runtime.png"));
            using (FileStream stream = File.Create(path)) atlas.SaveAsPng(stream, atlas.Width, atlas.Height);
            Log("MINING_RENDER_PASS nativePlaced=" + placed + " singleDrops=" + placed + " oreTextures=13 ingots=13");
        }
        finally
        {
            device.SetRenderTargets(targets); Main.screenPosition = screen; Main.drawToScreen = draw; Main.netMode = previousNet;
            for (int i = 0; i < Main.item.Length; i++) Main.item[i] = previousItems[i];
        }
    }
}
