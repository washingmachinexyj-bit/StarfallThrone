#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using StarfallThrone.Content.Systems;
using StarfallThrone.Content.Voyage;

namespace StarfallThrone.Content.Mining;

/// <summary>
/// Thirteen independent, bounded, resumable ore jobs. Existing boss flags keep their old IDs/schema.
/// No world-file IO is performed here: tML owns atomic world saves and invokes Save/LoadWorldData.
/// </summary>
public sealed class MiningWorld : ModSystem
{
    public const string Locale = "Mods.StarfallThrone.MiningWorld.";
    public const int SchemaVersion = 1;
    public const byte PacketId = 30;
    public const int Count = 13;
    private const int ChunkSize = 16, MaxRegions = 64, MaxTarget = 20000;
    private static readonly string[] Keys = { "Moss", "Coppermarrow", "Frostrune", "Dawn", "Imperial", "Doomfire", "Starmagnet", "Silentmoon", "Ringwreck", "Prismphase", "Everlife", "Coordinate", "Genesis" };
    private static readonly int[] ToolIndices = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
    public static bool[] Generated = new bool[Count];
    public static bool[] PendingLegacy = new bool[Count];
    private static bool[] observed = new bool[Count];
    private static bool[] cultivatedOnly = new bool[Count];
    private static OreJob?[] jobs = new OreJob?[Count];
    private static HashSet<int> editedChunks = new();
    private static readonly List<Rectangle> protectedRegions = new();
    private static readonly HashSet<int> structureChunks = new();
    private static readonly HashSet<int> clientReportedChunks = new();
    private static readonly bool[] greeted = new bool[256];
    private static readonly ulong[] lastPlacementPacketTick = new ulong[256];
    private static ulong structureCacheTick;
    private static bool structureCacheValid;
    private static bool initialized, hasSchema, worldReady, schemaTooNew, pauseGeneration;
    private static int generationDepth;
    public static bool GenerationActive => jobs.Any(j => j != null);
    public static IReadOnlyList<Rectangle> ProtectedRegions => protectedRegions.AsReadOnly();
    public static bool IsCultivationOnly(int ore) => ValidOre(ore) && cultivatedOnly[ore];
    public static int PlacedCount(int ore) => ValidOre(ore) ? placedTotals[ore] : 0;
    private static int[] placedTotals = new int[Count];

    private static bool ValidOre(int ore) => ore >= 0 && ore < Count;
    private static bool Authority => Main.netMode != NetmodeID.MultiplayerClient;
    private static MiningWorldConfig Config => ModContent.GetInstance<MiningWorldConfig>();
    private static MiningWorld Instance => ModContent.GetInstance<MiningWorld>();
    private static bool Flag(bool[] flags, int i) => i >= 0 && i < flags.Length && flags[i];

    // Kept separate from Catalog.Unlocked to avoid recursion and registration/load-order dependencies.
    public static bool BossUnlocked(int ore) => ore switch
    {
        0 => Flag(MiniBossWorld.Downed, 0),
        1 => Flag(PrimordialWorld.Downed, 1),
        2 => Flag(PrimordialWorld.Downed, 3),
        3 => Flag(PrimordialWorld.Downed, 9),
        4 => Flag(StarfallWorld.Downed, 0),
        5 => Flag(StarfallWorld.Downed, 6),
        6 => Flag(StarfallWorld.Downed, 10),
        7 => Flag(StarfallWorld.Downed, 16),
        8 => Flag(VoyageWorld.Downed, 2),
        9 => Flag(VoyageWorld.Downed, 7),
        10 => Flag(VoyageWorld.Downed, 11),
        11 => Flag(VoyageWorld.Downed, 15),
        12 => Flag(VoyageWorld.Downed, 16),
        _ => false
    };

    public override void ClearWorld() => Reset();
    public override void OnWorldLoad() { Reset(); worldReady = true; }
    public override void OnWorldUnload() => Reset();
    public override void PostWorldGen() { hasSchema = true; worldReady = true; }

    private static void Reset()
    {
        Generated = new bool[Count]; PendingLegacy = new bool[Count]; observed = new bool[Count];
        cultivatedOnly = new bool[Count]; jobs = new OreJob?[Count]; placedTotals = new int[Count];
        editedChunks = new HashSet<int>(); protectedRegions.Clear(); structureChunks.Clear(); clientReportedChunks.Clear();
        Array.Clear(greeted); Array.Clear(lastPlacementPacketTick);
        structureCacheTick = 0; structureCacheValid = false; initialized = hasSchema = worldReady = schemaTooNew = pauseGeneration = false;
        generationDepth = 0; preservedNewerData = null;
    }

    public override void PostWorldLoad() => InitializeProgress();

    private static void InitializeProgress()
    {
        if (initialized || !Authority || !worldReady) return;
        // Runs only after every ModSystem has loaded its existing downed flags.
        for (int i = 0; i < Count; i++)
        {
            bool downed = BossUnlocked(i);
            if (!hasSchema)
            {
                observed[i] = downed;
                PendingLegacy[i] = downed;
            }
            else if (!observed[i] && downed && jobs[i] == null && !Generated[i] && !cultivatedOnly[i])
            {
                // A missing entry from a future expanded schema is a legacy choice, never an auto-retrofill.
                observed[i] = true;
                PendingLegacy[i] = true;
            }
        }
        initialized = hasSchema = true;
    }

    public override void PostUpdateWorld()
    {
        if (!Authority || Main.gameMenu || !worldReady) return;
        InitializeProgress();
        NotifyLegacyPlayers();
        if (schemaTooNew) return;
        for (int i = 0; i < Count; i++)
        {
            if (observed[i] || !BossUnlocked(i)) continue;
            observed[i] = true;
            if (Generated[i] || PendingLegacy[i] || cultivatedOnly[i] || jobs[i] != null) continue;
            if (Config.GenerateOnFutureFirstKills) Queue(i);
            else { cultivatedOnly[i] = true; Announce("CultivationUnlocked", OreText(i)); Sync(); }
        }
        if (pauseGeneration) return;
        int ore = Array.FindIndex(jobs, j => j != null);
        if (ore < 0) return;
        OreJob job = jobs[ore]!;
        var elapsed = Stopwatch.StartNew();
        for (int n = 0; n < Math.Clamp(Config.AttemptsPerTick, 1, 24); n++)
        {
            if (job.Complete) { Finish(ore, job); break; }
            // Every attempted candidate consumes a persisted cursor, including rejected/protected sites.
            AttemptVein(ore, job, job.Cursor++);
            if (job.Complete) { Finish(ore, job); break; }
            if (elapsed.ElapsedMilliseconds >= 2) break;
        }
    }

    private static void Queue(int ore)
    {
        if (!Authority || !ValidOre(ore) || Generated[ore] || jobs[ore] != null || !BossUnlocked(ore)) return;
        PendingLegacy[ore] = cultivatedOnly[ore] = false;
        int basis = ore == 0 ? 750 : ore < 4 ? 1050 : ore < 8 ? 1900 : 2200;
        double area = Main.maxTilesX * (double)Main.maxTilesY / (4200.0 * 1200.0);
        int target = Math.Clamp((int)Math.Round(basis * area), basis / 2, MaxTarget);
        jobs[ore] = new OreJob(Mix(unchecked((uint)Main.worldID) ^ (uint)(ore + 1) * 0x9E3779B9u), target);
        Announce("Awakening" + ore);
        Announce("Queued", OreText(ore), MiningCatalog.PickRequirements[ore], ToolText(ToolIndices[ore]));
        Sync();
    }

    private static void Finish(int ore, OreJob job)
    {
        Generated[ore] = true; placedTotals[ore] = job.Placed; jobs[ore] = null;
        Announce(job.Placed < job.Target / 2 ? "CompletedSparse" : "Completed", OreText(ore), job.Placed,
            MiningCatalog.PickRequirements[ore], Language.GetText(Locale + "Layer" + ore).Value,
            ToolText(ToolIndices[ore]));
        Instance.Mod.Logger.Info($"MINING_GENERATED {Keys[ore]} placed={job.Placed}/{job.Target} attempts={job.Cursor}/{job.Limit} seed={job.Seed}");
        Sync();
    }

    private static NetworkText OreText(int ore) => NetworkText.FromKey("Mods.StarfallThrone.Items.MiningOre" + ore + ".DisplayName");
    private static NetworkText ToolText(int tool) => NetworkText.FromKey("Mods.StarfallThrone.Items." + (tool == 8 ? "VoyageUtility2" : "MiningTool" + tool) + ".DisplayName");
    private static void Announce(string key, params object[] args)
    {
        if (Main.gameMenu) return;
        NetworkText message = NetworkText.FromKey(Locale + key, args);
        if (Main.netMode == NetmodeID.Server) ChatHelper.BroadcastChatMessage(message, new Color(153, 222, 202));
        else Main.NewText(message.ToString(), new Color(153, 222, 202));
    }
    private static void Sync() { if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.WorldData); }

    private static void NotifyLegacyPlayers()
    {
        bool pending = PendingLegacy.Any(x => x);
        for (int p = 0; p < Main.maxPlayers && p < greeted.Length; p++)
        {
            if (!Main.player[p].active) { greeted[p] = false; continue; }
            if (greeted[p]) continue;
            greeted[p] = true;
            if (!pending && !schemaTooNew) continue;
            NetworkText message = NetworkText.FromKey(Locale + (schemaTooNew ? "NewerSchema" : "LegacyNotice"));
            if (Main.netMode == NetmodeID.Server) ChatHelper.SendChatMessageToClient(message, Color.Gold, p);
            else Main.NewText(message.ToString(), Color.Gold);
        }
    }

    private static uint Mix(uint value)
    {
        value ^= value >> 16; value *= 0x7FEB352Du; value ^= value >> 15; value *= 0x846CA68Bu; return value ^ (value >> 16);
    }
    private static uint Next(ref uint state) { state = Mix(state + 0x9E3779B9u); return state; }

    internal static Point Candidate(uint seed, int cursor, int ore, int width, int height, double surface, double rock)
    {
        uint random = Mix(seed ^ unchecked((uint)cursor * 0x85EBCA6Bu));
        int top = ore <= 2 ? (int)surface + 24 : ore == 3 ? (int)surface + 35 : (int)rock + 30;
        int bottom = ore <= 2 ? (int)rock + 70 : ore == 3 ? (int)rock + 150 : height - 210;
        if (ore is 5 or 6 or 11 or 12) top = Math.Max(top, (int)(height * .55));
        top = Math.Clamp(top, 40, Math.Max(40, height - 60));
        bottom = Math.Clamp(bottom, top + 1, Math.Max(top + 1, height - 40));
        return new Point(40 + (int)(Next(ref random) % (uint)Math.Max(1, width - 80)), top + (int)(Next(ref random) % (uint)(bottom - top)));
    }

    private static void AttemptVein(int ore, OreJob job, int cursor)
    {
        Point center = Candidate(job.Seed, cursor, ore, Main.maxTilesX, Main.maxTilesY, Main.worldSurface, Main.rockLayer);
        if (!WorldGen.InWorld(center.X, center.Y, 30) || Instance.IsProtected(center.X, center.Y)) return;
        uint random = Mix(job.Seed ^ unchecked((uint)cursor * 0xC2B2AE35u));
        int rx = ore == 0 ? 2 : 3, ry = ore < 4 ? 2 : 3;
        Rectangle footprint = new(center.X - rx, center.Y - ry, rx * 2 + 1, ry * 2 + 1);
        if (!NeighborhoodSafe(footprint)) return;
        // Prefer a matching biome on two of every three candidate attempts, but retain a global fallback.
        if (cursor % 3 != 0 && ore is 0 or 1 or 2 or 9 or 10 && !PreferredBiome(ore, center)) return;
        int placed = 0;
        generationDepth++;
        try
        {
            for (int y = footprint.Top; y < footprint.Bottom && job.Placed < job.Target; y++)
            for (int x = footprint.Left; x < footprint.Right && job.Placed < job.Target; x++)
            {
                double ellipse = (x - center.X) * (x - center.X) / (double)(rx * rx) + (y - center.Y) * (y - center.Y) / (double)(ry * ry);
                if (ellipse > 1.05 || (Next(ref random) & 7) == 0 || !CanGenerateAt(x, y)) continue;
                Tile tile = Main.tile[x, y];
                tile.TileType = checked((ushort)MiningCatalog.OreTileType(ore));
                tile.TileFrameX = tile.TileFrameY = 0;
                WorldGen.SquareTileFrame(x, y, false);
                job.Placed++; placed++;
            }
        }
        finally { generationDepth--; }
        if (placed > 0 && Main.netMode == NetmodeID.Server)
            NetMessage.SendTileSquare(-1, footprint.X - 1, footprint.Y - 1, footprint.Width + 2, footprint.Height + 2);
    }

    private static bool PreferredBiome(int ore, Point p)
    {
        int snow = 0, hallow = 0, jungle = 0, evil = 0;
        for (int dy = -24; dy <= 24; dy += 8)
        for (int dx = -24; dx <= 24; dx += 8)
        {
            Tile t = Main.tile[p.X + dx, p.Y + dy];
            if (!t.HasTile) continue;
            if (t.TileType is TileID.SnowBlock or TileID.IceBlock) snow++;
            if (t.TileType is TileID.Pearlstone or TileID.HallowedGrass or TileID.HallowedIce) hallow++;
            if (t.TileType is TileID.Mud or TileID.JungleGrass) jungle++;
            if (t.TileType is TileID.Ebonstone or TileID.Crimstone) evil++;
        }
        return ore switch { 2 => snow > 2, 9 => hallow > 2, 10 => jungle > 2, _ => snow + hallow + jungle + evil < 4 };
    }

    internal static bool EligibleNaturalTile(bool hasTile, int type, bool protectedCell, bool liquid, bool wired, bool alteredShape, bool painted, bool unsafeWall)
        => hasTile && IsNaturalStone(type) && !protectedCell && !liquid && !wired && !alteredShape && !painted && !unsafeWall;

    private static bool IsNaturalStone(int type) => type is TileID.Stone or TileID.Ebonstone or TileID.Crimstone or TileID.Pearlstone;
    private static bool BuiltWall(int type) => type != 0 &&
        (type >= WallID.Count || type < Main.wallHouse.Length && Main.wallHouse[type] || type < Main.wallDungeon.Length && Main.wallDungeon[type] || type is WallID.LihzahrdBrickUnsafe or WallID.LihzahrdBrick);
    private static bool Wired(Tile tile) => tile.RedWire || tile.BlueWire || tile.GreenWire || tile.YellowWire || tile.HasActuator || tile.IsActuated;
    private static bool Decorated(Tile tile) => tile.TileColor != 0 || tile.WallColor != 0 || tile.IsTileInvisible || tile.IsWallInvisible || tile.IsTileFullbright || tile.IsWallFullbright;

    public static bool CanGenerateAt(int x, int y)
    {
        if (!WorldGen.InWorld(x, y, 30)) return false;
        Tile tile = Main.tile[x, y];
        return EligibleNaturalTile(tile.HasTile, tile.TileType, Instance.IsProtected(x, y), tile.LiquidAmount != 0,
            Wired(tile), tile.IsHalfBlock || tile.Slope != SlopeType.Solid, Decorated(tile), BuiltWall(tile.WallType));
    }

    private static bool NeighborhoodSafe(Rectangle footprint)
    {
        int padding = Math.Clamp(Config.StructurePadding, 4, 20);
        footprint.Inflate(padding, padding);
        for (int y = footprint.Top; y < footprint.Bottom; y++)
        for (int x = footprint.Left; x < footprint.Right; x++)
        {
            if (!WorldGen.InWorld(x, y, 10) || Instance.IsProtected(x, y)) return false;
            Tile t = Main.tile[x, y];
            if (BuiltWall(t.WallType) || Wired(t) || Decorated(t)) return false;
            if (t.HasTile && (Main.tileFrameImportant[t.TileType] || Main.tileDungeon[t.TileType] || t.TileType is TileID.LihzahrdBrick or TileID.WoodBlock or TileID.GrayBrick or TileID.RedBrick or TileID.Glass)) return false;
        }
        return true;
    }

    /// <summary>Conservative area-tool check. Does not prevent ordinary player mining or modify background walls.</summary>
    public static bool CanAlter(int x, int y)
    {
        if (!WorldGen.InWorld(x, y, 10) || Instance.IsProtectedCore(x, y, ignorePlayers: true, ignoreExcavations: true)) return false;
        Tile t = Main.tile[x, y];
        if (!t.HasTile || Main.tileFrameImportant[t.TileType] || Main.tileDungeon[t.TileType] || t.TileType == TileID.LihzahrdBrick ||
            BuiltWall(t.WallType) || Wired(t) || Decorated(t)) return false;
        return true;
    }

    /// <summary>Custom rectangles, edited 16x16 chunks, spawn area, players/beds, chests, tile entities and NPC homes.</summary>
    public bool IsProtected(int x, int y) => IsProtectedCore(x, y, ignorePlayers: false, ignoreExcavations: false);

    internal static bool ProtectionApplies(bool persistent, bool nearPlayer, bool ignorePlayers) => persistent || (!ignorePlayers && nearPlayer);
    internal static bool ProvenanceBlocks(bool editedChunk, bool ignoreExcavations) => editedChunk && !ignoreExcavations;

    private bool IsProtectedCore(int x, int y, bool ignorePlayers, bool ignoreExcavations)
    {
        if (!WorldGen.InWorld(x, y, 10)) return true;
        int radius = Math.Clamp(Config.SpawnProtectionRadius, 80, 400);
        if (Math.Abs(x - Main.spawnTileX) <= radius && Math.Abs(y - Main.spawnTileY) <= radius) return true;
        // Digging the first block records its chunk for future ore generation, but must not disable the
        // remaining blocks of this very same area-tool stroke. Explicit regions, beds, spawn and
        // real structures still block area tools; edited chunks are generation-only provenance.
        if (protectedRegions.Any(r => r.Contains(x, y)) || ProvenanceBlocks(editedChunks.Contains(ChunkKey(x, y)), ignoreExcavations)) return true;
        RefreshStructureCache();
        if (structureChunks.Contains(ChunkKey(x, y))) return true;
        foreach (Player player in Main.ActivePlayers)
        {
            if (player.SpawnX > 0 && Math.Abs(x - player.SpawnX) < 50 && Math.Abs(y - player.SpawnY) < 50) return true;
            bool nearby = Math.Abs(x - player.Center.X / 16f) < 16 && Math.Abs(y - player.Center.Y / 16f) < 16;
            if (ProtectionApplies(false, nearby, ignorePlayers)) return true;
        }
        return false;
    }

    private static int ChunkKey(int x, int y) => (x / ChunkSize) | ((y / ChunkSize) << 16);
    private static void AddProtectedChunks(HashSet<int> set, int x, int y, int padding)
    {
        for (int cy = Math.Max(0, y - padding) / ChunkSize; cy <= Math.Min(Main.maxTilesY - 1, y + padding) / ChunkSize; cy++)
        for (int cx = Math.Max(0, x - padding) / ChunkSize; cx <= Math.Min(Main.maxTilesX - 1, x + padding) / ChunkSize; cx++)
            set.Add(cx | (cy << 16));
    }

    private static void RefreshStructureCache()
    {
        if (structureCacheValid && Main.GameUpdateCount >= structureCacheTick && Main.GameUpdateCount - structureCacheTick < 60) return;
        structureCacheTick = Main.GameUpdateCount; structureCacheValid = true;
        structureChunks.Clear();
        foreach (Chest? chest in Main.chest) if (chest != null) AddProtectedChunks(structureChunks, chest.x, chest.y, 20);
        foreach (Point16 point in TileEntity.ByPosition.Keys) AddProtectedChunks(structureChunks, point.X, point.Y, 16);
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!npc.townNPC) continue;
            AddProtectedChunks(structureChunks, (int)(npc.Center.X / 16), (int)(npc.Center.Y / 16), 36);
            if (!npc.homeless) AddProtectedChunks(structureChunks, npc.homeTileX, npc.homeTileY, 70);
        }
    }

    public static void RecordServerEdit(int x, int y)
    {
        if (!Authority || !worldReady || Main.gameMenu || WorldGen.gen || generationDepth != 0 || !WorldGen.InWorld(x, y, 1)) return;
        editedChunks.Add(ChunkKey(x, y));
        structureCacheValid = false;
    }

    public static void RecordLocalPlacement(int x, int y, bool wall)
    {
        if (!worldReady || Main.gameMenu || WorldGen.gen || generationDepth != 0 || !WorldGen.InWorld(x, y, 1)) return;
        if (Authority) { RecordServerEdit(x, y); return; }
        int chunk = ChunkKey(x, y);
        editedChunks.Add(chunk);
        if (!clientReportedChunks.Add(chunk)) return;
        ModPacket packet = Instance.Mod.GetPacket();
        packet.Write(PacketId); packet.Write((byte)0); packet.Write((short)x); packet.Write((short)y); packet.Write(wall);
        packet.Send();
    }

    // Packet 30 accepts placement provenance only. No packet requests generation or changes region authority.
    public static void ReceivePacket(BinaryReader reader, int whoAmI)
    {
        if (Main.netMode != NetmodeID.Server || whoAmI < 0 || whoAmI >= Main.maxPlayers || whoAmI >= lastPlacementPacketTick.Length) return;
        try
        {
            byte action = reader.ReadByte();
            if (action != 0) return;
            int x = reader.ReadInt16(), y = reader.ReadInt16(); bool wall = reader.ReadBoolean();
            Player player = Main.player[whoAmI];
            if (!player.active || player.dead || !WorldGen.InWorld(x, y, 1) ||
                Math.Abs(x - player.Center.X / 16f) > 64 || Math.Abs(y - player.Center.Y / 16f) > 64) return;
            Item held = player.HeldItem;
            if (held == null || held.IsAir || (wall ? held.createWall < 0 : held.createTile < 0)) return;
            ulong tick = Main.GameUpdateCount;
            if (lastPlacementPacketTick[whoAmI] == tick) return;
            lastPlacementPacketTick[whoAmI] = tick;
            RecordServerEdit(x, y);
        }
        catch (EndOfStreamException) { /* A truncated or forged packet cannot change progression. */ }
        catch (IOException) { }
    }

    public static bool IsHost(int player) => Main.netMode == NetmodeID.SinglePlayer ||
        Main.netMode == NetmodeID.Server && player >= 0 && player < Main.maxPlayers && Main.player[player].active && NetMessage.DoesPlayerSlotCountAsAHost(player);

    internal static bool ChooseLegacy(bool generate)
    {
        if (!Authority || schemaTooNew) return false;
        InitializeProgress();
        bool changed = false;
        for (int i = 0; i < Count; i++)
        {
            if (!PendingLegacy[i]) continue;
            PendingLegacy[i] = false; changed = true;
            if (generate && BossUnlocked(i)) Queue(i);
            else cultivatedOnly[i] = true;
        }
        if (changed) Sync();
        return changed;
    }

    internal static bool AddRegion(Rectangle region)
    {
        if (!Authority || schemaTooNew || protectedRegions.Count >= MaxRegions || region.Width < 1 || region.Height < 1 ||
            region.X < 0 || region.Y < 0 || (long)region.X + region.Width > Main.maxTilesX || (long)region.Y + region.Height > Main.maxTilesY) return false;
        protectedRegions.Add(region); Sync(); return true;
    }
    internal static bool RemoveRegion(int index)
    {
        if (!Authority || schemaTooNew || index < 0 || index >= protectedRegions.Count) return false;
        protectedRegions.RemoveAt(index); Sync(); return true;
    }

    public override void SaveWorldData(TagCompound tag)
    {
        // Unknown newer data is kept verbatim; a downgrade must not erase its generation progress.
        if (schemaTooNew && preservedNewerData != null) { tag["Mining"] = preservedNewerData; return; }
        var data = new TagCompound { ["Version"] = SchemaVersion, ["Paused"] = pauseGeneration };
        var ores = new List<TagCompound>();
        for (int i = 0; i < Count; i++)
        {
            var entry = new TagCompound { ["Key"] = Keys[i], ["Observed"] = observed[i], ["Generated"] = Generated[i],
                ["Legacy"] = PendingLegacy[i], ["Cultivation"] = cultivatedOnly[i], ["Placed"] = placedTotals[i] };
            if (jobs[i] != null) entry["Job"] = jobs[i]!.Save();
            ores.Add(entry);
        }
        data["Ores"] = ores;
        data["EditedChunks"] = editedChunks.OrderBy(x => x).ToArray();
        data["Regions"] = protectedRegions.Select(r => new TagCompound { ["X"] = r.X, ["Y"] = r.Y, ["W"] = r.Width, ["H"] = r.Height }).ToList();
        tag["Mining"] = data;
    }
    private static TagCompound? preservedNewerData;

    public override void LoadWorldData(TagCompound tag)
    {
        Reset(); worldReady = true; preservedNewerData = null;
        if (!tag.ContainsKey("Mining")) return;
        TagCompound data = tag.GetCompound("Mining");
        int version = data.GetInt("Version");
        hasSchema = version >= 1; schemaTooNew = version > SchemaVersion;
        if (schemaTooNew) preservedNewerData = data;
        pauseGeneration = data.GetBool("Paused");
        foreach (TagCompound entry in data.GetList<TagCompound>("Ores"))
        {
            int i = Array.IndexOf(Keys, entry.GetString("Key")); if (i < 0) continue;
            observed[i] = entry.GetBool("Observed"); Generated[i] = entry.GetBool("Generated");
            PendingLegacy[i] = entry.GetBool("Legacy"); cultivatedOnly[i] = entry.GetBool("Cultivation");
            placedTotals[i] = Math.Clamp(entry.GetInt("Placed"), 0, MaxTarget);
            if (entry.ContainsKey("Job") && !Generated[i] && !PendingLegacy[i] && !cultivatedOnly[i] && !schemaTooNew)
                jobs[i] = OreJob.Load(entry.GetCompound("Job"));
        }
        foreach (int chunk in data.GetIntArray("EditedChunks"))
        {
            int cx = chunk & 65535, cy = (int)((uint)chunk >> 16);
            if (cx < (Main.maxTilesX + 15) / 16 && cy < (Main.maxTilesY + 15) / 16) editedChunks.Add(chunk);
        }
        foreach (TagCompound r in data.GetList<TagCompound>("Regions"))
        {
            if (protectedRegions.Count >= MaxRegions) break;
            int x = r.GetInt("X"), y = r.GetInt("Y"), w = r.GetInt("W"), h = r.GetInt("H");
            if (x >= 0 && y >= 0 && w > 0 && h > 0 && (long)x + w <= Main.maxTilesX && (long)y + h <= Main.maxTilesY)
                protectedRegions.Add(new Rectangle(x, y, w, h));
        }
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write((byte)SchemaVersion); writer.Write((byte)Count); writer.Write(pauseGeneration); writer.Write(schemaTooNew);
        for (int i = 0; i < Count; i++)
        {
            byte flags = (byte)((Generated[i] ? 1 : 0) | (PendingLegacy[i] ? 2 : 0) | (cultivatedOnly[i] ? 4 : 0) | (jobs[i] != null ? 8 : 0));
            writer.Write(flags); writer.Write(jobs[i]?.Placed ?? placedTotals[i]);
        }
        writer.Write((byte)protectedRegions.Count);
        foreach (Rectangle r in protectedRegions) { writer.Write(r.X); writer.Write(r.Y); writer.Write(r.Width); writer.Write(r.Height); }
    }

    public override void NetReceive(BinaryReader reader)
    {
        // ModSystem world data is server->client only. Clients never send their own generation flags.
        int version = reader.ReadByte(), count = reader.ReadByte();
        pauseGeneration = reader.ReadBoolean(); schemaTooNew = reader.ReadBoolean() || version > SchemaVersion;
        for (int i = 0; i < count; i++)
        {
            int flags = reader.ReadByte(), placed = reader.ReadInt32(); if (i >= Count) continue;
            Generated[i] = (flags & 1) != 0; PendingLegacy[i] = (flags & 2) != 0; cultivatedOnly[i] = (flags & 4) != 0;
            placedTotals[i] = Math.Clamp(placed, 0, MaxTarget);
        }
        protectedRegions.Clear(); int regions = reader.ReadByte();
        for (int i = 0; i < regions; i++)
        {
            int x = reader.ReadInt32(), y = reader.ReadInt32(), w = reader.ReadInt32(), h = reader.ReadInt32();
            if (i < MaxRegions && x >= 0 && y >= 0 && w > 0 && h > 0 && (long)x + w <= Main.maxTilesX && (long)y + h <= Main.maxTilesY)
                protectedRegions.Add(new Rectangle(x, y, w, h));
        }
        worldReady = true;
    }

    internal sealed class OreJob
    {
        public uint Seed;
        public int Cursor, Placed, Target, Limit;
        public bool Complete => Placed >= Target || Cursor >= Limit;
        public OreJob(uint seed, int target) { Seed = seed; Target = Math.Clamp(target, 1, MaxTarget); Limit = Target * 12; }
        public TagCompound Save() => new() { ["Seed"] = unchecked((int)Seed), ["Cursor"] = Cursor, ["Placed"] = Placed, ["Target"] = Target, ["Limit"] = Limit };
        public static OreJob Load(TagCompound tag)
        {
            int target = Math.Clamp(tag.GetInt("Target"), 1, MaxTarget);
            var job = new OreJob(unchecked((uint)tag.GetInt("Seed")), target);
            job.Limit = Math.Clamp(tag.GetInt("Limit"), 1, MaxTarget * 12);
            job.Cursor = Math.Clamp(tag.GetInt("Cursor"), 0, job.Limit);
            job.Placed = Math.Clamp(tag.GetInt("Placed"), 0, target);
            return job;
        }
    }

    /// <summary>Pure state/eligibility tests: no world load, tile writes, save-file IO, NPCs or RNG mutation.</summary>
    public static string SelfTest()
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException("MiningWorld: " + message); checks++; }
        Check(Keys.Distinct().Count() == Count, "stable unique ore keys");
        Check(!ProtectionApplies(false, true, true), "area mining allows natural stone near player outside persistent protection");
        Check(ProtectionApplies(false, true, false), "world generation avoids the same nearby player");
        Check(ProtectionApplies(true, true, true), "persistent protection is not bypassed by area-tool player exemption");
        Check(!ProvenanceBlocks(true, true), "mined center chunk cannot disable the following area-tool blocks");
        Check(ProvenanceBlocks(true, false), "mined center chunk remains protected from later ore generation");
        for (int ore = 0; ore < Count; ore++)
        {
            var original = new OreJob(Mix((uint)(ore + 19)), 900) { Cursor = 137, Placed = 29 };
            OreJob resumed = OreJob.Load(original.Save());
            Check(resumed.Seed == original.Seed && resumed.Cursor == 137 && resumed.Placed == 29 && resumed.Limit == original.Limit, "job roundtrip " + ore);
            for (int i = 0; i < 128; i++)
            {
                Point a = Candidate(original.Seed, original.Cursor + i, ore, 4200, 1200, 350, 600);
                Point b = Candidate(resumed.Seed, resumed.Cursor + i, ore, 4200, 1200, 350, 600);
                Check(a == b && a.X >= 40 && a.X < 4160 && a.Y >= 40 && a.Y < 1160, "deterministic bounded candidate " + ore);
            }
            resumed.Cursor = resumed.Limit; Check(resumed.Complete, "finite sparse-world termination " + ore);
            resumed.Cursor = 0; resumed.Placed = resumed.Target; Check(resumed.Complete, "target completion " + ore);
        }
        Check(EligibleNaturalTile(true, TileID.Stone, false, false, false, false, false, false), "natural stone eligible");
        foreach (int type in new[] { TileID.Containers, TileID.LihzahrdBrick, TileID.BlueDungeonBrick, TileID.Gold, TileID.WoodBlock, TileID.WorkBenches })
            Check(!EligibleNaturalTile(true, type, false, false, false, false, false, false), "preserve existing tile " + type);
        for (int flag = 0; flag < 7; flag++)
            Check(!EligibleNaturalTile(flag != 0, TileID.Stone, flag == 1, flag == 2, flag == 3, flag == 4, flag == 5, flag == 6), "preservation rejection " + flag);
        var malformed = new TagCompound { ["Target"] = int.MaxValue, ["Limit"] = int.MaxValue, ["Cursor"] = int.MaxValue, ["Placed"] = int.MaxValue };
        OreJob clamped = OreJob.Load(malformed);
        Check(clamped.Target == MaxTarget && clamped.Limit == MaxTarget * 12 && clamped.Complete, "clamp corrupt job lengths");
        return $"MINING_WORLD_SELFTEST_PASS: {checks} checks; 13 deterministic resumable jobs, bounds, finite termination, preservation predicates; no world modified.";
    }

    internal static void RunCommand(CommandCaller caller, string[] args)
    {
        string action = args.Length == 0 ? "help" : args[0];
        void Reply(string key, params object[] values) => caller.Reply(Language.GetTextValue(Locale + key, values), new Color(153, 222, 202));
        if (action == "help") { Reply("Help"); return; }
        InitializeProgress();
        if (action == "status")
        {
            Reply("Status", Generated.Count(x => x), PendingLegacy.Count(x => x), jobs.Count(x => x != null), cultivatedOnly.Count(x => x), protectedRegions.Count);
            for (int i = 0; i < Count; i++)
            {
                string state = Generated[i] ? "StateDone" : PendingLegacy[i] ? "StatePending" : jobs[i] != null ? "StateRunning" : cultivatedOnly[i] ? "StateCultivation" : "StateLocked";
                Reply("StatusOre", OreText(i).ToString(), Language.GetTextValue(Locale + state), jobs[i]?.Placed ?? placedTotals[i]);
            }
            if (pauseGeneration) Reply("Paused");
            return;
        }
        if (action == "regions")
        {
            for (int i = 0; i < protectedRegions.Count; i++)
            {
                Rectangle r = protectedRegions[i]; Reply("Region", i + 1, r.X, r.Y, r.Right - 1, r.Bottom - 1);
            }
            if (protectedRegions.Count == 0) Reply("NoRegions");
            return;
        }
        bool host = Main.netMode == NetmodeID.SinglePlayer || caller.CommandType == CommandType.Console || caller.Player != null && IsHost(caller.Player.whoAmI);
        if (!host || !Authority) { Reply("HostOnly"); return; }
        if (schemaTooNew) { Reply("NewerSchema"); return; }
        if (action is "generate" or "cultivation")
        {
            if (args.Length != 2 || args[1] != "confirm") { Reply(action == "generate" ? "ConfirmGenerate" : "ConfirmCultivation"); return; }
            Reply(ChooseLegacy(action == "generate") ? action == "generate" ? "LegacyQueued" : "LegacyCultivation" : "NoPending");
        }
        else if (action is "pause" or "resume")
        {
            pauseGeneration = action == "pause"; Sync(); Reply(pauseGeneration ? "Paused" : "Resumed");
        }
        else if (action == "protect" && args.Length == 5 && int.TryParse(args[1], out int x1) && int.TryParse(args[2], out int y1) && int.TryParse(args[3], out int x2) && int.TryParse(args[4], out int y2))
        {
            if (x1 < 0 || x2 < 0 || y1 < 0 || y2 < 0 || x1 >= Main.maxTilesX || x2 >= Main.maxTilesX || y1 >= Main.maxTilesY || y2 >= Main.maxTilesY) { Reply("InvalidRegion"); return; }
            var r = new Rectangle(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1) + 1, Math.Abs(y2 - y1) + 1);
            Reply(AddRegion(r) ? "RegionAdded" : "InvalidRegion");
        }
        else if (action == "unprotect" && args.Length == 2 && int.TryParse(args[1], out int index))
            Reply(RemoveRegion(index - 1) ? "RegionRemoved" : "InvalidRegion");
        else if (action == "selftest") caller.Reply(SelfTest(), Color.LightGreen);
        else Reply("Help");
    }
}

public sealed class StarfallMiningCommand : ModCommand
{
    public override CommandType Type => CommandType.World | CommandType.Console;
    public override string Command => "starfallmining";
    public override string Usage => "/starfallmining help";
    public override string Description => Language.GetTextValue(MiningWorld.Locale + "Description");
    public override void Action(CommandCaller caller, string input, string[] args) => MiningWorld.RunCommand(caller, args);
}
