#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using StarfallThrone.Content.Systems;
using StarfallThrone.Content.Voyage;

namespace StarfallThrone.Content.Mining;

/// <summary>Isolated menu-only integration tests. Never loads/saves world files; tile simulations use a restored menu arena.</summary>
public static class MiningWorldValidation
{
    /// <summary>
    /// Real generation on a bounded, snapshotted main-menu tile arena. Main.tile is never replaced or
    /// resized (Tilemap uses shared native buffers); every native tile data field touched is restored.
    /// </summary>
    public static string RunGenerationTests()
    {
        if (!Main.gameMenu || !Terraria.Program.LaunchParameters.ContainsKey("-starfall-art-smoke"))
            throw new InvalidOperationException("Generation validation requires the isolated main-menu smoke client.");
        const int width = 1024, height = 640;
        if (Main.tile.Width < width || Main.tile.Height < height)
            throw new InvalidOperationException("The isolated tile arena must provide at least 1024x640 cells.");
        int checks = 0;
        void Check(bool ok, string why) { if (!ok) throw new InvalidOperationException("Mining generation: " + why); checks++; }
        Dictionary<FieldInfo, object?> original = CaptureMiningState();
        bool[] mini = MiniBossWorld.Downed, early = PrimordialWorld.Downed, old = StarfallWorld.Downed, voyage = VoyageWorld.Downed;
        Player[] players = (Player[])Main.player.Clone(); NPC[] npcs = (NPC[])Main.npc.Clone(); Item[] items = (Item[])Main.item.Clone();
        Chest[] chests = Main.chest;
        int net = Main.netMode, myPlayer = Main.myPlayer, sx = Main.spawnTileX, sy = Main.spawnTileY, worldId = Main.worldID;
        int worldWidth = Main.maxTilesX, worldHeight = Main.maxTilesY;
        double surface = Main.worldSurface, rock = Main.rockLayer;
        bool gen = WorldGen.gen, noActions = WorldGen.noTileActions;
        MiningWorldConfig config = ModContent.GetInstance<MiningWorldConfig>();
        bool enabled = config.GenerateOnFutureFirstKills; int attempts = config.AttemptsPerTick, padding = config.StructurePadding, spawn = config.SpawnProtectionRadius;
        NativeCell[] cells = CaptureCells(width, height);
        var world = new MiningWorld();
        try
        {
            Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0;
            Main.maxTilesX = width; Main.maxTilesY = height; Main.worldSurface = 60; Main.rockLayer = 150;
            Main.spawnTileX = Main.spawnTileY = 16; Main.worldID = 7031337;
            WorldGen.gen = WorldGen.noTileActions = false;
            config.GenerateOnFutureFirstKills = true; config.AttemptsPerTick = 1; config.StructurePadding = 8; config.SpawnProtectionRadius = 80;
            for (int i = 0; i < Main.player.Length; i++) Main.player[i] = new Player { whoAmI = i, active = false };
            for (int i = 0; i < Main.npc.Length; i++) Main.npc[i] = new NPC { whoAmI = i, active = false };
            for (int i = 0; i < Main.item.Length; i++) Main.item[i] = new Item();
            Main.chest = new Chest[chests.Length];
            MiniBossWorld.Downed = new bool[8]; PrimordialWorld.Downed = new bool[10];
            StarfallWorld.Downed = new bool[17]; VoyageWorld.Downed = new bool[17];
            for (int x = 0; x < width; x++) for (int y = 0; y < height; y++)
            {
                Tile t = Main.tile[x, y];
                t.Get<TileTypeData>() = default; t.Get<WallTypeData>() = default; t.Get<TileWallWireStateData>() = default;
                t.Get<LiquidData>() = default; t.Get<TileWallBrightnessInvisibilityData>() = default;
                t.HasTile = true; t.TileType = TileID.Stone;
            }
            world.LoadWorldData(new TagCompound()); world.PostWorldLoad();
            Check(!MiningWorld.GenerationActive && !MiningWorld.PendingLegacy.Any(x => x), "unprogressed world does not generate");
            Check(MiningWorld.AddRegion(new Rectangle(780, 200, 60, 120)), "explicit protected test region");
            Main.chest[0] = new Chest { x = 460, y = 260 };
            Main.tile[460, 260].TileType = TileID.Containers;
            Main.tile[520, 260].TileType = TileID.Gold;
            Main.tile[580, 260].TileType = TileID.LihzahrdBrick;
            Main.tile[640, 260].WallType = WallID.BlueDungeonUnsafe;
            Main.tile[660, 260].TileType = TileID.GrayBrick;
            Main.tile[700, 260].WallType = WallID.Wood;
            Tile wiredSentinel = Main.tile[720, 260]; wiredSentinel.RedWire = true;
            Tile paintedSentinel = Main.tile[740, 260]; paintedSentinel.TileColor = 1;
            Main.gameMenu = false;
            MiningWorld.RecordLocalPlacement(850, 260, false);

            Player p = new() { whoAmI = 0, active = true, position = new Vector2(938 * 16, 319 * 16) };
            p.ResetEffects(); p.inventory[0] = new Item(MiningCatalog.ToolType(13)); p.selectedItem = 0; Main.player[0] = p;
            Check(MiningWorld.CanAlter(940, 320) && !MiningWorld.CanGenerateAt(940, 320), "near-player stone permits area mining but rejects generation");
            for (int hit = 0; hit < 20 && Main.tile[940, 320].HasTile; hit++) p.PickTile(940, 320, 600);
            Check(!Main.tile[940, 320].HasTile, "native primary tile actually broke");
            Check(MiningWorld.CanAlter(941, 320), "extra block still allowed after native primary break recorded its chunk");
            Check(!MiningWorld.CanGenerateAt(941, 320), "same edited chunk remains generation-protected");

            SetAllBossFlags(true);
            world.PostUpdateWorld();
            TagCompound firstKill = new(); world.SaveWorldData(firstKill);
            var entries = firstKill.GetCompound("Mining").GetList<TagCompound>("Ores");
            Check(entries.All(e => e.GetBool("Observed") && e.ContainsKey("Job")), "all 13 real first-kill transitions queue exactly once");
            Check(!MiningWorld.PendingLegacy.Any(x => x), "new first kills are not misclassified as legacy");
            // Shorten only the test jobs after observing the real first-kill path. Production targets are unchanged.
            foreach (TagCompound e in entries)
            {
                TagCompound job = e.GetCompound("Job"); job["Target"] = 32; job["Limit"] = 4096;
            }
            world.LoadWorldData(firstKill); world.PostWorldLoad(); config.AttemptsPerTick = 24;
            NativeCell[] resumeCells = CaptureCells(width, height);
            TagCompound resumeState = new(); world.SaveWorldData(resumeState);
            void CompleteJobs()
            {
                int ticks = 0;
                while (MiningWorld.GenerationActive && ticks++ < 60000) world.PostUpdateWorld();
                Check(!MiningWorld.GenerationActive && MiningWorld.Generated.All(x => x), "all generation jobs terminate");
            }
            CompleteJobs();
            int[] counts = CountGeneratedOres(width, height);
            for (int ore = 0; ore < 13; ore++) Check(counts[ore] == 32 && MiningWorld.PlacedCount(ore) == 32, "real generated ore count " + ore);
            Check(Main.tile[460, 260].TileType == TileID.Containers && ReferenceEquals(Main.chest[0], Main.chest.First(c => c != null)), "chest retained");
            Check(Main.tile[520, 260].TileType == TileID.Gold, "existing vanilla ore retained");
            Check(Main.tile[580, 260].TileType == TileID.LihzahrdBrick && Main.tile[640, 260].WallType == WallID.BlueDungeonUnsafe, "temple and dungeon retained");
            Check(Main.tile[660, 260].TileType == TileID.GrayBrick && Main.tile[700, 260].WallType == WallID.Wood, "construction and placed wall retained");
            Check(Main.tile[720, 260].RedWire && Main.tile[740, 260].TileColor == 1, "wires and paint retained");
            for (int x = 780; x < 840; x++) for (int y = 200; y < 320; y++)
                if (Main.tile[x, y].TileType != TileID.Stone) throw new InvalidOperationException("Protected rectangle was altered");
            Check(Main.tile[850, 260].TileType == TileID.Stone, "recorded placed-stone chunk retained");
            ulong uninterruptedHash = TerrainHash(width, height);
            RestoreCells(resumeCells, width, height); world.LoadWorldData(resumeState); world.PostWorldLoad();
            CompleteJobs();
            Check(TerrainHash(width, height) == uninterruptedHash, "interrupted/resumed real generation produces identical tile types");
            for (int tick = 0; tick < 40; tick++) world.PostUpdateWorld();
            Check(TerrainHash(width, height) == uninterruptedHash && !MiningWorld.GenerationActive, "already observed kills never regenerate ore");
            return $"MINING_WORLD_GENERATION_PASS: {checks} checks; 13 actual first kills and 416 ore tiles, deterministic resume, native primary/extra-block regression, protected structures; isolated arena restored.";
        }
        finally
        {
            Main.gameMenu = true;
            RestoreCells(cells, width, height);
            Main.maxTilesX = worldWidth; Main.maxTilesY = worldHeight; Main.worldSurface = surface; Main.rockLayer = rock;
            Main.spawnTileX = sx; Main.spawnTileY = sy; Main.worldID = worldId; Main.netMode = net; Main.myPlayer = myPlayer;
            WorldGen.gen = gen; WorldGen.noTileActions = noActions;
            for (int i = 0; i < Main.player.Length; i++) Main.player[i] = players[i];
            for (int i = 0; i < Main.npc.Length; i++) Main.npc[i] = npcs[i];
            for (int i = 0; i < Main.item.Length; i++) Main.item[i] = items[i];
            Main.chest = chests;
            MiniBossWorld.Downed = mini; PrimordialWorld.Downed = early; StarfallWorld.Downed = old; VoyageWorld.Downed = voyage;
            config.GenerateOnFutureFirstKills = enabled; config.AttemptsPerTick = attempts; config.StructurePadding = padding; config.SpawnProtectionRadius = spawn;
            RestoreMiningState(original);
        }
    }

    private readonly record struct NativeCell(TileTypeData Type, WallTypeData Wall, TileWallWireStateData State, LiquidData Liquid, TileWallBrightnessInvisibilityData Light)
    {
        public NativeCell(Tile t) : this(t.Get<TileTypeData>(), t.Get<WallTypeData>(), t.Get<TileWallWireStateData>(), t.Get<LiquidData>(), t.Get<TileWallBrightnessInvisibilityData>()) { }
        public void Restore(Tile t)
        {
            t.Get<TileTypeData>() = Type; t.Get<WallTypeData>() = Wall; t.Get<TileWallWireStateData>() = State;
            t.Get<LiquidData>() = Liquid; t.Get<TileWallBrightnessInvisibilityData>() = Light;
        }
    }
    private static NativeCell[] CaptureCells(int width, int height)
    {
        var cells = new NativeCell[width * height];
        for (int x = 0; x < width; x++) for (int y = 0; y < height; y++) cells[x * height + y] = new NativeCell(Main.tile[x, y]);
        return cells;
    }
    private static void RestoreCells(NativeCell[] cells, int width, int height)
    {
        for (int x = 0; x < width; x++) for (int y = 0; y < height; y++) cells[x * height + y].Restore(Main.tile[x, y]);
    }
    private static int[] CountGeneratedOres(int width, int height)
    {
        var counts = new int[13]; var lookup = Enumerable.Range(0, 13).ToDictionary(MiningCatalog.OreTileType, i => i);
        for (int x = 0; x < width; x++) for (int y = 0; y < height; y++)
        {
            Tile t = Main.tile[x, y]; if (t.HasTile && lookup.TryGetValue(t.TileType, out int ore)) counts[ore]++;
        }
        return counts;
    }
    private static ulong TerrainHash(int width, int height)
    {
        ulong value = 14695981039346656037ul;
        for (int x = 0; x < width; x++) for (int y = 0; y < height; y++)
        {
            Tile t = Main.tile[x, y]; value = unchecked((value ^ (uint)(t.HasTile ? t.TileType + 1 : 0)) * 1099511628211ul);
        }
        return value;
    }

    public static string RunStateTests()
    {
        if (!Main.gameMenu) throw new InvalidOperationException("Mining world state tests require an isolated main-menu test session.");
        int checks = 0;
        void Check(bool ok, string what) { if (!ok) throw new InvalidOperationException("Mining world state: " + what); checks++; }
        var world = new MiningWorld();
        Dictionary<FieldInfo, object?> original = CaptureMiningState();
        bool[] mini = MiniBossWorld.Downed, early = PrimordialWorld.Downed, old = StarfallWorld.Downed, voyage = VoyageWorld.Downed;
        int netMode = Main.netMode;
        bool menu = Main.gameMenu;
        try
        {
            Main.netMode = NetmodeID.SinglePlayer;
            MiniBossWorld.Downed = new bool[8]; PrimordialWorld.Downed = new bool[10];
            StarfallWorld.Downed = new bool[17]; VoyageWorld.Downed = new bool[17];
            world.OnWorldLoad(); world.LoadWorldData(new TagCompound());
            Check(!MiningWorld.PendingLegacy.Any(x => x), "LoadWorldData does not observe another system before it loads");
            SetAllBossFlags(true);
            world.PostWorldLoad();
            Check(MiningWorld.PendingLegacy.All(x => x), "all 13 legacy milestones classified after all systems load");
            Check(!MiningWorld.Generated.Any(x => x) && !MiningWorld.GenerationActive, "legacy loading never queues or generates tiles");

            Check(MiningWorld.ChooseLegacy(false), "explicit cultivation choice accepted");
            Check(!MiningWorld.PendingLegacy.Any(x => x) && !MiningWorld.GenerationActive, "cultivation choice queues no generation");
            Check(Enumerable.Range(0, 13).All(MiningWorld.IsCultivationOnly), "cultivation choice covers all pending stages");
            TagCompound cultivationSave = new(); world.SaveWorldData(cultivationSave);
            world.OnWorldUnload(); world.LoadWorldData(cultivationSave); world.PostWorldLoad();
            Check(Enumerable.Range(0, 13).All(MiningWorld.IsCultivationOnly) && !MiningWorld.GenerationActive, "cultivation choice persists across load");
            Check(!MiningWorld.ChooseLegacy(true), "repeated command cannot silently reverse cultivation-only choice");

            world.LoadWorldData(new TagCompound()); world.PostWorldLoad();
            Check(MiningWorld.ChooseLegacy(true), "explicit host-path opt-in queues legacy stages");
            Check(MiningWorld.GenerationActive && !MiningWorld.PendingLegacy.Any(x => x), "opt-in queues jobs without marking completion");
            Check(!MiningWorld.Generated.Any(x => x), "queue creation itself does not generate blocks");
            TagCompound interrupted = new(); world.SaveWorldData(interrupted);
            IList<TagCompound> entries = interrupted.GetCompound("Mining").GetList<TagCompound>("Ores");
            Check(entries.Count == 13 && entries.All(e => e.ContainsKey("Job")), "13 independently saved jobs");
            TagCompound firstJob = entries[0].GetCompound("Job");
            firstJob["Cursor"] = 37; firstJob["Placed"] = 11;
            int seed = firstJob.GetInt("Seed"), target = firstJob.GetInt("Target"), limit = firstJob.GetInt("Limit");
            world.OnWorldUnload(); world.LoadWorldData(interrupted); world.PostWorldLoad();
            TagCompound resumed = new(); world.SaveWorldData(resumed);
            TagCompound continued = resumed.GetCompound("Mining").GetList<TagCompound>("Ores")[0].GetCompound("Job");
            Check(continued.GetInt("Cursor") == 37 && continued.GetInt("Placed") == 11 && continued.GetInt("Seed") == seed &&
                continued.GetInt("Target") == target && continued.GetInt("Limit") == limit, "world save resumes identical finite job without reseeding");
            Check(!MiningWorld.ChooseLegacy(true), "repeat opt-in cannot duplicate queued jobs");

            // Mix completed, pending and cultivation flags and include an active job; every field has an independent meaning.
            for (int i = 0; i < 13; i++)
            {
                MiningWorld.Generated[i] = i % 3 == 0;
                MiningWorld.PendingLegacy[i] = i % 3 == 1;
            }
            bool testRegion = Main.maxTilesX > 100 && Main.maxTilesY > 100;
            if (testRegion) Check(MiningWorld.AddRegion(new Rectangle(20, 30, 40, 50)), "custom region accepted for persistence test");
            TagCompound mixed = new(); world.SaveWorldData(mixed);
            bool[] generated = (bool[])MiningWorld.Generated.Clone(), pending = (bool[])MiningWorld.PendingLegacy.Clone();
            world.LoadWorldData(mixed);
            Check(MiningWorld.Generated.SequenceEqual(generated) && MiningWorld.PendingLegacy.SequenceEqual(pending), "full world flag save/load roundtrip");
            if (testRegion) Check(MiningWorld.ProtectedRegions.Count == 1 && MiningWorld.ProtectedRegions[0] == new Rectangle(20, 30, 40, 50), "protected rectangle save/load roundtrip");
            using (var stream = new MemoryStream())
            {
                using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
                world.NetSend(writer); writer.Flush(); stream.Position = 0;
                world.ClearWorld(); Main.netMode = NetmodeID.MultiplayerClient;
                world.NetReceive(new BinaryReader(stream, Encoding.UTF8, true));
                Check(MiningWorld.Generated.SequenceEqual(generated) && MiningWorld.PendingLegacy.SequenceEqual(pending), "server/client progression roundtrip");
                if (testRegion) Check(MiningWorld.ProtectedRegions.Count == 1 && MiningWorld.ProtectedRegions[0] == new Rectangle(20, 30, 40, 50), "server/client protected rectangle roundtrip");
                Check(stream.Position == stream.Length, "network reader consumes exactly its own schema");
                Check(!MiningWorld.ChooseLegacy(true), "client cannot invoke generation even by calling the API directly");
            }
            Main.netMode = NetmodeID.SinglePlayer;

            // Unknown future schemas are deliberately fail-closed and losslessly round-trip through this older version.
            var futureData = new TagCompound { ["Version"] = MiningWorld.SchemaVersion + 1, ["FutureSentinel"] = "keep this unknown field" };
            world.LoadWorldData(new TagCompound { ["Mining"] = futureData }); world.PostWorldLoad();
            Check(!MiningWorld.ChooseLegacy(true) && !MiningWorld.GenerationActive, "newer save schema blocks generation");
            TagCompound futureSave = new(); world.SaveWorldData(futureSave);
            Check(ReferenceEquals(futureSave.GetCompound("Mining"), futureData) && futureSave.GetCompound("Mining").GetString("FutureSentinel") == "keep this unknown field", "unknown schema preserved verbatim");

            SetAllBossFlags(false);
            world.LoadWorldData(new TagCompound()); world.PostWorldLoad();
            Check(!MiningWorld.PendingLegacy.Any(x => x) && !MiningWorld.GenerationActive, "new/unprogressed world starts with no retrofill queue");
            Check(!MiningWorld.BossUnlocked(-1) && !MiningWorld.BossUnlocked(13), "invalid progression indices rejected");
            MiniBossWorld.Downed[6] = true;
            Check(!MiningWorld.BossUnlocked(0), "snail does not incorrectly unlock moss ore");
            MiniBossWorld.Downed[0] = true;
            Check(MiningWorld.BossUnlocked(0), "MossTumbler saved ID 0 unlocks moss ore");

            // Exercise the real GlobalTile hook after the primary dig, without calling WorldGen.KillTile
            // or changing any tile. This is the provenance event area-tool extra blocks will observe.
            if (Main.maxTilesX > 100 && Main.maxTilesY > 100)
            {
                Main.gameMenu = false;
                bool fail = false, effectOnly = false, noItem = false;
                new MiningGlobalTile().KillTile(64, 64, TileID.Stone, ref fail, ref effectOnly, ref noItem);
                Main.gameMenu = true;
                TagCompound primaryDig = new(); world.SaveWorldData(primaryDig);
                int key = (64 / 16) | ((64 / 16) << 16);
                bool recorded = primaryDig.GetCompound("Mining").GetIntArray("EditedChunks").Contains(key);
                Check(recorded, "actual primary-block GlobalTile hook records edited chunk");
                Check(!MiningWorld.ProvenanceBlocks(recorded, true), "subsequent area-tool blocks ignore primary-dig provenance");
                Check(MiningWorld.ProvenanceBlocks(recorded, false), "later world generation still respects primary-dig provenance");
            }
            return $"MINING_WORLD_STATE_PASS: {checks} checks; delayed legacy classification, explicit choices, persisted jobs/flags, network, authority, future-schema preservation; no tiles or world files touched.";
        }
        finally
        {
            MiniBossWorld.Downed = mini; PrimordialWorld.Downed = early; StarfallWorld.Downed = old; VoyageWorld.Downed = voyage;
            Main.netMode = netMode;
            Main.gameMenu = menu;
            RestoreMiningState(original);
        }
    }

    private static void SetAllBossFlags(bool value)
    {
        Array.Fill(MiniBossWorld.Downed, value); Array.Fill(PrimordialWorld.Downed, value);
        Array.Fill(StarfallWorld.Downed, value); Array.Fill(VoyageWorld.Downed, value);
    }

    // Preserve exact original references for non-readonly fields; Reset reassigns those arrays/sets.
    // Readonly collections are cleared in Reset and therefore need their contents snapshotted too.
    private static Dictionary<FieldInfo, object?> CaptureMiningState()
    {
        var snapshot = new Dictionary<FieldInfo, object?>();
        foreach (FieldInfo field in typeof(MiningWorld).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (field.IsLiteral) continue;
            object? value = field.GetValue(null);
            if (field.IsInitOnly)
            {
                if (value is Array a) value = a.Clone();
                else if (value is HashSet<int> set) value = new HashSet<int>(set);
                else if (value is IList list) value = list.Cast<object>().ToArray();
            }
            snapshot[field] = value;
        }
        return snapshot;
    }

    private static void RestoreMiningState(Dictionary<FieldInfo, object?> snapshot)
    {
        foreach ((FieldInfo field, object? value) in snapshot)
        {
            if (!field.IsInitOnly) { field.SetValue(null, value); continue; }
            object? target = field.GetValue(null);
            if (target is Array array && value is Array copy) Array.Copy(copy, array, copy.Length);
            else if (target is HashSet<int> set && value is HashSet<int> setCopy) { set.Clear(); set.UnionWith(setCopy); }
            else if (target is IList list && value is object[] items) { list.Clear(); foreach (object item in items) list.Add(item); }
        }
    }
}
