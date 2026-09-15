using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;
using StarfallThrone.Content.Mining;

namespace StarfallThrone.Content.Oaths.Mining;

/// <summary>Opt-in native fixtures; main owns invocation after recipes and native tile buffers are ready.</summary>
public static class OathMiningValidation
{
    private static int checks;
    private static void Check(bool ok, string why) { if (!ok) throw new InvalidOperationException("Oaths mining: " + why); checks++; }
    private static void Guard() => Check(Main.gameMenu && Terraria.Program.LaunchParameters.ContainsKey("-starfall-art-smoke") || Main.dedServ && Terraria.Program.LaunchParameters.ContainsKey("-testservermodloading"), "isolated fixture only");
    public static void Data()
    {
        Guard();
        Check(MiningCatalog.ToolPower[13] >= 600, "pre-supreme engineering pick already reaches all ore gates");
        for (int i = 0; i < 3; i++)
        {
            int ore = OathMiningCatalog.Item("OathOre" + i), bar = OathCatalog.Bar(i), station = OathMiningCatalog.Item("OathStation" + i), pick = OathMiningCatalog.Item("OathPick" + i);
            var tile = (OathOreTileBase)TileLoader.GetTile(OathMiningCatalog.Tile("OathOreTile" + i));
            Check(tile.MinPick == 600 && !tile.CanExplode(0, 0) && !tile.CanReplace(0, 0, TileID.Stone), "exact gate and no explosives/replacement bypass");
            Check(new Item(pick).pick == 610 + i * 20, "independent bootstrap pick");
            Recipe RecipeFor(int type) => Main.recipe.Take(Recipe.numRecipes).Single(r => r.createItem.type == type);
            var p = RecipeFor(pick); var s = RecipeFor(station); var b = RecipeFor(bar); var o = RecipeFor(ore);
            Check(p.requiredTile.SequenceEqual(new[] { OathCatalog.BaseStation }) && p.requiredItem.Count == 1 && p.requiredItem[0].type == OathCatalog.Material(i) && p.requiredItem[0].stack == 10, "bootstrap tool has no ore dependency");
            Check(s.requiredTile.SequenceEqual(new[] { OathCatalog.BaseStation }) && s.requiredItem.All(x => x.type != ore && x.type != bar) && s.requiredItem.Any(x => x.type == OathCatalog.Material(i) && x.stack == 20), "station builds before mining");
            Check(b.requiredTile.SequenceEqual(new[] { OathCatalog.Station(i) }) && b.requiredItem.Count == 1 && b.requiredItem[0].type == ore && b.requiredItem[0].stack == 4, "own station smelts 4 to 1");
            Check(o.createItem.stack == 8 && o.requiredTile.Contains(OathCatalog.Station(i)) && o.requiredItem.Any(x => x.type == OathCatalog.Material(i) && x.stack == 1), "renewable ore avoids finite-world lock");
            var furniture = TileObjectData.GetTileData(OathCatalog.Station(i), 0);
            Check(furniture.Width == 3 && furniture.Height == 3 && furniture.CoordinatePadding == 2 && furniture.CoordinateWidth == 16, "native 18px furniture geometry");
            Check(!TileLoader.GetTile(OathCatalog.Station(i)).AdjTiles.Any(t => Enumerable.Range(0, 3).Any(j => j != i && t == OathCatalog.Station(j))), "stations do not unlock one another");
        }
        ModContent.GetInstance<OathMiningWorld>().Mod.Logger.Info("OATHS_MINING_DATA_PASS checks=" + checks);
    }
    private readonly record struct Cell(TileTypeData Type, WallTypeData Wall, TileWallWireStateData State, LiquidData Liquid, TileWallBrightnessInvisibilityData Light)
    {
        public Cell(Tile t) : this(t.Get<TileTypeData>(), t.Get<WallTypeData>(), t.Get<TileWallWireStateData>(), t.Get<LiquidData>(), t.Get<TileWallBrightnessInvisibilityData>()) { }
        public void Restore(Tile t) { t.Get<TileTypeData>() = Type; t.Get<WallTypeData>() = Wall; t.Get<TileWallWireStateData>() = State; t.Get<LiquidData>() = Liquid; t.Get<TileWallBrightnessInvisibilityData>() = Light; }
    }
    public static void Run()
    {
        Guard(); const int w = 1024, h = 640;
        Check(Main.tile.Width >= w && Main.tile.Height >= h, "fixture requires allocated 1024x640 native tile arena");
        var world = ModContent.GetInstance<OathMiningWorld>(); TagCompound saved = new(); world.SaveWorldData(saved);
        var oldMining = new Dictionary<FieldInfo, object>();
        foreach (FieldInfo f in typeof(MiningWorld).GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static))
        {
            if (f.IsLiteral) continue; object value = f.GetValue(null);
            if (f.IsInitOnly) { if (value is Array a) value = a.Clone(); else if (value is HashSet<int> set) value = new HashSet<int>(set); else if (value is IList list) value = list.Cast<object>().ToArray(); }
            oldMining[f] = value;
        }
        bool[] wins = OathWorld.SupremeWon; bool menu = Main.gameMenu, gen = WorldGen.gen, actions = WorldGen.noTileActions;
        int net = Main.netMode, width = Main.maxTilesX, height = Main.maxTilesY, sx = Main.spawnTileX, sy = Main.spawnTileY, my = Main.myPlayer;
        double rock = Main.rockLayer, surface = Main.worldSurface;
        var players = (Player[])Main.player.Clone(); var npcs = (NPC[])Main.npc.Clone(); var items = (Item[])Main.item.Clone(); var chests = Main.chest;
        var config = ModContent.GetInstance<MiningWorldConfig>(); bool generate = config.GenerateOnFutureFirstKills;
        Cell[] cells = new Cell[w * h]; for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) cells[x * h + y] = new Cell(Main.tile[x, y]);
        try
        {
            Main.netMode = NetmodeID.SinglePlayer; Main.maxTilesX = w; Main.maxTilesY = h; Main.spawnTileX = Main.spawnTileY = 1; Main.myPlayer = 0;
            Main.rockLayer = 150; Main.worldSurface = 60; WorldGen.gen = WorldGen.noTileActions = false; config.GenerateOnFutureFirstKills = true;
            for (int i = 0; i < Main.player.Length; i++) Main.player[i] = new Player { whoAmI = i, active = false };
            for (int i = 0; i < Main.npc.Length; i++) Main.npc[i] = new NPC { whoAmI = i, active = false };
            for (int i = 0; i < Main.item.Length; i++) Main.item[i] = new Item(); Main.chest = new Chest[chests.Length];
            ModContent.GetInstance<MiningWorld>().OnWorldLoad(); world.ClearWorld(); OathWorld.SupremeWon = new bool[3];
            for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) { Tile t = Main.tile[x, y]; new Cell().Restore(t); t.HasTile = true; t.TileType = TileID.Stone; }
            OathMiningWorld.Unlock(0); Check(!OathMiningWorld.Active(0), "locked world cannot queue via public API");
            Main.tile[600, 260].TileType = TileID.Containers; Check(!OathMiningWorld.NeighborhoodSafe(600, 260), "container neighborhood protected");
            Main.tile[650, 260].WallType = WallID.Wood; Check(!OathMiningWorld.NeighborhoodSafe(650, 260), "housing wall protected");
            Tile wire = Main.tile[700, 260]; wire.RedWire = true; Check(!OathMiningWorld.NeighborhoodSafe(700, 260), "wire protected");
            Tile paint = Main.tile[750, 260]; paint.TileColor = 1; Check(!OathMiningWorld.NeighborhoodSafe(750, 260), "paint protected");
            Main.tile[800, 260].TileType = TileID.LihzahrdBrick; Check(!OathMiningWorld.NeighborhoodSafe(800, 260), "temple protected");
            Main.gameMenu = false; MiningWorld.RecordLocalPlacement(850, 260, false); Check(!OathMiningWorld.NeighborhoodSafe(850, 260), "recorded player placement protected"); Main.gameMenu = menu;
            for (int i = 0; i < 3; i++)
            {
                OathWorld.SupremeWon[i] = true; OathMiningWorld.Unlock(i);
                Check(OathMiningWorld.Active(i), "own victory independently queues ore " + i);
                for (int n = 0; n < 8; n++) OathMiningWorld.Step(i);
                int prior = OathMiningWorld.Attempts(i); TagCompound resume = new(); world.SaveWorldData(resume); world.ClearWorld(); world.LoadWorldData(resume);
                Check(OathMiningWorld.Attempts(i) == prior && OathMiningWorld.Active(i), "saved job cursor resumes");
                TagCompound again = new(); world.SaveWorldData(again);
                TagCompound a = resume.GetCompound("OathMining").GetCompound("Ore" + i), b = again.GetCompound("OathMining").GetCompound("Ore" + i);
                Check(a.GetInt("Random") == b.GetInt("Random") && a.GetInt("Placed") == b.GetInt("Placed"), "resume preserves RNG and placed total, not just cursor");
                OathMiningWorld.Unlock(i); Check(OathMiningWorld.Attempts(i) == prior, "repeat kill cannot reset job");
                int safety = 0; while (OathMiningWorld.Active(i) && safety++ <= OathMiningWorld.AttemptLimit) OathMiningWorld.Step(i);
                Check(!OathMiningWorld.Active(i) && OathMiningWorld.PlacedCount(i) > 0 && OathMiningWorld.PlacedCount(i) <= OathMiningWorld.Target && OathMiningWorld.Attempts(i) <= OathMiningWorld.AttemptLimit, "bounded real stone-to-ore generation " + i);
                int count = 0; for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) if (Main.tile[x, y].HasTile && Main.tile[x, y].TileType == OathMiningCatalog.Tile("OathOreTile" + i)) count++;
                Check(count == OathMiningWorld.PlacedCount(i), "diagnostics match actual native ore tiles");
                OathMiningWorld.Unlock(i); Check(!OathMiningWorld.Active(i), "completed generation never repeats");
            }
            Check(Main.tile[600, 260].TileType == TileID.Containers && Main.tile[650, 260].WallType == WallID.Wood && Main.tile[700, 260].RedWire && Main.tile[750, 260].TileColor == 1 && Main.tile[800, 260].TileType == TileID.LihzahrdBrick && Main.tile[850, 260].TileType == TileID.Stone, "all protection sentinels unchanged after generation");
            Player player = new() { whoAmI = 0, active = true, position = new Vector2(740 * 16, 350 * 16) }; Main.player[0] = player; player.ResetEffects(); player.inventory[0] = new Item(OathMiningCatalog.Item("OathPick0")); player.selectedItem = 0;
            Main.gameMenu = false;
            for (int i = 0; i < 3; i++)
            {
                Tile cell = Main.tile[740 + i, 355]; cell.HasTile = true; cell.TileType = (ushort)OathMiningCatalog.Tile("OathOreTile" + i);
                for (int n = 0; n < 40; n++) player.PickTile(740 + i, 355, 599);
                Check(cell.HasTile, "native 599% pick blocked " + i);
                for (int n = 0; n < 80 && cell.HasTile; n++) player.PickTile(740 + i, 355, 600);
                Check(!cell.HasTile, "native 600% pick succeeds " + i);
                int x = 770 + i * 10, y = 360, type = OathCatalog.Station(i); var data = TileObjectData.GetTileData(type, 0);
                for (int dx = 0; dx < 3; dx++) { for (int dy = 0; dy < 3; dy++) { Tile space = Main.tile[x + dx, y + dy]; space.HasTile = false; } Tile floor = Main.tile[x + dx, y + 3]; floor.HasTile = true; floor.TileType = TileID.Stone; }
                WorldGen.PlaceObject(x + data.Origin.X, y + data.Origin.Y, type);
                for (int dx = 0; dx < 3; dx++) for (int dy = 0; dy < 3; dy++) Check(Main.tile[x + dx, y + dy].HasTile && Main.tile[x + dx, y + dy].TileType == type && Main.tile[x + dx, y + dy].TileFrameX == dx * 18 && Main.tile[x + dx, y + dy].TileFrameY == dy * 18, "native placed furniture frame");
                Check(TileLoader.GetTile(type).GetItemDrops(x, y).Single().type == OathMiningCatalog.Item("OathStation" + i), "one recoverable station drop");
            }
            Main.gameMenu = menu; world.ClearWorld(); Main.netMode = NetmodeID.MultiplayerClient; OathMiningWorld.Unlock(0); OathMiningWorld.Step(0); Check(!OathMiningWorld.Active(0), "client cannot generate");
            Main.netMode = NetmodeID.SinglePlayer; config.GenerateOnFutureFirstKills = false; OathMiningWorld.Unlock(0); Check(!OathMiningWorld.Active(0) && OathMiningWorld.PlacedCount(0) == 0, "existing config opt-out retains cultivation only");
            TagCompound end = new(); world.SaveWorldData(end); world.ClearWorld(); world.LoadWorldData(end); config.GenerateOnFutureFirstKills = true; OathMiningWorld.Unlock(0); Check(!OathMiningWorld.Active(0), "cultivation-only decision persists and cannot regenerate");
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream); world.NetSend(writer); byte[] bytes = stream.ToArray();
            bool threw = false; try { world.NetReceive(new BinaryReader(new MemoryStream(bytes.Take(bytes.Length - 1).ToArray()))); } catch (EndOfStreamException) { threw = true; }
            Check(threw && !OathMiningWorld.Active(0), "truncated sync is staged without partial mutation");
            TagCompound exhausted = new(); world.SaveWorldData(exhausted);
            TagCompound job = exhausted.GetCompound("OathMining").GetCompound("Ore1"); job["Started"] = true; job["Done"] = false; job["Cursor"] = OathMiningWorld.AttemptLimit - 1; job["Placed"] = 0;
            world.LoadWorldData(exhausted);
            for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) Main.tile[x, y].TileType = TileID.GrayBrick;
            OathMiningWorld.Step(1); Check(!OathMiningWorld.Active(1) && OathMiningWorld.Attempts(1) == OathMiningWorld.AttemptLimit && OathMiningWorld.PlacedCount(1) == 0, "fully protected terrain terminates at finite attempt limit");
        }
        finally
        {
            for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) cells[x * h + y].Restore(Main.tile[x, y]);
            foreach (var pair in oldMining)
            {
                if (!pair.Key.IsInitOnly) { pair.Key.SetValue(null, pair.Value); continue; }
                object target = pair.Key.GetValue(null);
                if (target is Array a && pair.Value is Array copy) Array.Copy(copy, a, copy.Length);
                else if (target is HashSet<int> set && pair.Value is HashSet<int> savedSet) { set.Clear(); set.UnionWith(savedSet); }
                else if (target is IList list && pair.Value is object[] values) { list.Clear(); foreach (object value in values) list.Add(value); }
            }
            world.LoadWorldData(saved); OathWorld.SupremeWon = wins; Main.gameMenu = menu; Main.netMode = net; Main.maxTilesX = width; Main.maxTilesY = height; Main.spawnTileX = sx; Main.spawnTileY = sy; Main.myPlayer = my; Main.rockLayer = rock; Main.worldSurface = surface; WorldGen.gen = gen; WorldGen.noTileActions = actions;
            for (int i = 0; i < players.Length; i++) Main.player[i] = players[i]; for (int i = 0; i < npcs.Length; i++) Main.npc[i] = npcs[i]; for (int i = 0; i < items.Length; i++) Main.item[i] = items[i]; Main.chest = chests; config.GenerateOnFutureFirstKills = generate;
        }
        world.Mod.Logger.Info("OATHS_MINING_RUN_PASS checks=" + checks + " generation=3 protected=true resumable=true pick599blocked600allowed=true furniture=3");
    }
}
