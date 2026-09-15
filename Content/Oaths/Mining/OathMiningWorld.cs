using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using StarfallThrone.Content.Mining;

namespace StarfallThrone.Content.Oaths.Mining;

/// <summary>Three independent finite jobs. All generation routes share the old protected-tile API.</summary>
public sealed class OathMiningWorld : ModSystem
{
    private static bool[] started = new bool[3], done = new bool[3];
    private static int[] cursor = new int[3], placed = new int[3];
    private static uint[] random = new uint[3];
    private static TagCompound future;
    public const int Target = 1200, AttemptLimit = 6000, AttemptsPerTick = 4;
    public static bool Active(int i) => i >= 0 && i < 3 && started[i] && !done[i] && future == null;
    public static int PlacedCount(int i) => i >= 0 && i < 3 ? placed[i] : 0;
    public static int Attempts(int i) => i >= 0 && i < 3 ? cursor[i] : 0;
    private static bool Authority => Main.netMode != NetmodeID.MultiplayerClient;
    public override void ClearWorld() { started = new bool[3]; done = new bool[3]; cursor = new int[3]; placed = new int[3]; random = new uint[3]; future = null; }
    public override void OnWorldUnload() => ClearWorld();
    public static void Unlock(int i)
    {
        if (!Authority || i < 0 || i > 2 || future != null || started[i] || !OathWorld.SupremeWon[i]) return;
        started[i] = true;
        random[i] = unchecked((uint)Main.worldID * 747796405u + (uint)(i + 1) * 2891336453u) | 1;
        // Respect the existing opt-out: cultivation still unlocks, no automatic terrain edits.
        done[i] = !ModContent.GetInstance<MiningWorldConfig>().GenerateOnFutureFirstKills;
        Announce(done[i] ? "Cultivation" : "Queued", i); Sync();
    }
    private static void Sync() { if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.WorldData); }
    private static void Announce(string state, int i)
    {
        string key = "Mods.StarfallThrone.OathsMining." + state + i;
        if (Main.netMode == NetmodeID.Server) ChatHelper.BroadcastChatMessage(NetworkText.FromKey(key), OathMiningCatalog.Colors[i]);
        else if (!Main.gameMenu) Main.NewText(Language.GetTextValue(key), OathMiningCatalog.Colors[i]);
    }
    public override void PostUpdateWorld()
    {
        if (!Authority || Main.gameMenu || future != null) return;
        for (int i = 0; i < 3; i++)
        {
            if (!Active(i)) continue;
            if (!OathWorld.SupremeWon[i]) continue;
            if (!ModContent.GetInstance<MiningWorldConfig>().GenerateOnFutureFirstKills) { Finish(i); continue; }
            for (int n = 0; n < AttemptsPerTick && Active(i); n++) Step(i);
        }
    }
    private static uint Next(ref uint value) { value ^= value << 13; value ^= value >> 17; value ^= value << 5; return value; }
    internal static void Step(int i)
    {
        if (!Authority || !Active(i) || !OathWorld.SupremeWon[i]) return;
        int top = Math.Max(60, (int)Main.rockLayer + 20), bottom = Main.maxTilesY - 220;
        if (Main.maxTilesX < 160 || bottom <= top) { Finish(i); return; }
        cursor[i]++;
        int x = 60 + (int)(Next(ref random[i]) % (uint)(Main.maxTilesX - 120));
        int y = top + (int)(Next(ref random[i]) % (uint)(bottom - top));
        if (NeighborhoodSafe(x, y))
        {
            bool changed = false;
            for (int dx = -2; dx <= 2; dx++) for (int dy = -2; dy <= 2; dy++)
            {
                if (placed[i] >= Target || dx * dx + dy * dy > 5 || !MiningWorld.CanGenerateAt(x + dx, y + dy)) continue;
                Tile cell = Main.tile[x + dx, y + dy];
                cell.TileType = (ushort)OathMiningCatalog.Tile("OathOreTile" + i);
                cell.TileFrameX = cell.TileFrameY = 0; placed[i]++; changed = true;
            }
            if (changed)
            {
                for (int dx = -3; dx <= 3; dx++) for (int dy = -3; dy <= 3; dy++) WorldGen.SquareTileFrame(x + dx, y + dy);
                if (Main.netMode == NetmodeID.Server) NetMessage.SendTileSquare(-1, x, y, 9);
            }
        }
        if (placed[i] >= Target || cursor[i] >= AttemptLimit) Finish(i);
    }
    internal static bool NeighborhoodSafe(int x, int y)
    {
        MiningWorld protection = ModContent.GetInstance<MiningWorld>();
        for (int dx = -6; dx <= 6; dx++) for (int dy = -6; dy <= 6; dy++)
        {
            int a = x + dx, b = y + dy;
            if (!WorldGen.InWorld(a, b, 30) || protection.IsProtected(a, b)) return false;
            Tile t = Main.tile[a, b];
            if (t.RedWire || t.BlueWire || t.GreenWire || t.YellowWire || t.HasActuator || t.IsActuated || t.TileColor != 0 || t.WallColor != 0 ||
                t.IsTileInvisible || t.IsWallInvisible || t.IsTileFullbright || t.IsWallFullbright) return false;
            if (t.WallType != 0 && (t.WallType >= WallID.Count || Main.wallHouse[t.WallType] || Main.wallDungeon[t.WallType] || t.WallType is WallID.LihzahrdBrick or WallID.LihzahrdBrickUnsafe)) return false;
            if (t.HasTile && (Main.tileFrameImportant[t.TileType] || Main.tileDungeon[t.TileType] || t.TileType is TileID.LihzahrdBrick or TileID.WoodBlock or TileID.GrayBrick or TileID.RedBrick or TileID.Glass)) return false;
        }
        return true;
    }
    private static void Finish(int i) { done[i] = true; Announce(placed[i] > 0 ? "Finished" : "Cultivation", i); Sync(); }
    public override void SaveWorldData(TagCompound tag)
    {
        if (future != null) { tag["OathMining"] = future; return; }
        TagCompound data = new() { ["Version"] = 1 };
        for (int i = 0; i < 3; i++) data["Ore" + i] = new TagCompound { ["Started"] = started[i], ["Done"] = done[i], ["Cursor"] = cursor[i], ["Placed"] = placed[i], ["Random"] = unchecked((int)random[i]) };
        tag["OathMining"] = data;
    }
    public override void LoadWorldData(TagCompound tag)
    {
        ClearWorld(); if (!tag.ContainsKey("OathMining")) return;
        TagCompound data = tag.GetCompound("OathMining");
        if (data.GetInt("Version") > 1) { future = data; return; }
        for (int i = 0; i < 3; i++)
        {
            TagCompound e = data.GetCompound("Ore" + i); started[i] = e.GetBool("Started"); done[i] = e.GetBool("Done");
            cursor[i] = Math.Clamp(e.GetInt("Cursor"), 0, AttemptLimit); placed[i] = Math.Clamp(e.GetInt("Placed"), 0, Target);
            random[i] = unchecked((uint)e.GetInt("Random"));
            if (random[i] == 0) random[i] = (uint)(i + 1);
            if (cursor[i] >= AttemptLimit || placed[i] >= Target) done[i] = true;
            if (done[i] || cursor[i] > 0 || placed[i] > 0) started[i] = true;
        }
    }
    public override void NetSend(BinaryWriter writer)
    {
        for (int i = 0; i < 3; i++) { writer.Write(started[i]); writer.Write(done[i]); writer.Write(cursor[i]); writer.Write(placed[i]); }
    }
    public override void NetReceive(BinaryReader reader)
    {
        bool[] a = new bool[3], b = new bool[3]; int[] c = new int[3], d = new int[3];
        for (int i = 0; i < 3; i++) { a[i] = reader.ReadBoolean(); b[i] = reader.ReadBoolean(); c[i] = Math.Clamp(reader.ReadInt32(), 0, AttemptLimit); d[i] = Math.Clamp(reader.ReadInt32(), 0, Target); }
        started = a; done = b; cursor = c; placed = d;
    }
}
