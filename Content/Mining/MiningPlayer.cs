using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace StarfallThrone.Content.Mining;

public sealed class MiningPlayer : ModPlayer
{
    public byte[] Modes = new byte[14];
    public int SelectedOre;
    public byte GatherTier;
    public bool AccessoryMagnet, ToolMagnet, Spectrum;
    public Rectangle Selection;
    public bool SelectionActive;
    public Point? SelectionFirst;
    public Point? SavedWaypoint;
    public readonly List<Point> SurveyResults = new();
    public int SurveyKind = -1;
    private int localCooldown, serverCooldown, interactionCooldown, modeCooldown, surveyCooldown;
    private bool rightLatch;
    internal Point MiningIntent = new(-1, -1);
    internal ulong IntentTick;
    internal static int ExtractionOwner = -1;

    public override void ResetEffects()
    {
        GatherTier = 0; AccessoryMagnet = ToolMagnet = Spectrum = false;
    }
    public override void PostUpdateEquips()
    {
        if (GatherTier == 0) return;
        Player.pickSpeed *= GatherTier switch { 1 => .95f, 2 => .92f, _ => .85f };
        Player.blockRange += GatherTier == 3 ? 3 : 1;
        if (GatherTier == 3) AccessoryMagnet = true;
    }
    public override void SaveData(TagCompound tag)
    {
        tag["MiningModes"] = Modes;
        tag["MiningSurveyOre"] = SelectedOre;
        // World coordinates are never restored into a different world.
        tag["MiningSelectionWorld"] = Main.worldID;
        tag["MiningSelection"] = new[] { Selection.X, Selection.Y, Selection.Width, Selection.Height };
        tag["MiningSelectionEnabled"] = SelectionActive;
        if (SavedWaypoint is Point p) tag["MiningWaypoint"] = new[] { p.X, p.Y };
    }
    public override void LoadData(TagCompound tag)
    {
        Modes = new byte[14];
        if (tag.ContainsKey("MiningModes"))
        {
            byte[] saved = tag.GetByteArray("MiningModes");
            for (int i = 0; i < Math.Min(saved.Length, Modes.Length); i++)
                Modes[i] = (byte)Math.Clamp((int)saved[i], 0, MiningTool.ModeCount(i) - 1);
        }
        SelectedOre = Math.Clamp(tag.GetInt("MiningSurveyOre"), 0, 12);
        savedWorld = tag.GetInt("MiningSelectionWorld");
        var box = tag.GetIntArray("MiningSelection");
        if (box.Length == 4 && box[2] is > 0 and <= 200 && box[3] is > 0 and <= 200)
            Selection = new Rectangle(box[0], box[1], box[2], box[3]);
        SelectionActive = tag.GetBool("MiningSelectionEnabled") && Selection.Width > 0;
        var waypoint = tag.GetIntArray("MiningWaypoint");
        SavedWaypoint = waypoint.Length == 2 ? new Point(waypoint[0], waypoint[1]) : null;
    }
    private int savedWorld;
    public override void OnEnterWorld()
    {
        if (savedWorld != Main.worldID) { SelectionActive = false; Selection = default; SavedWaypoint = null; }
        SurveyResults.Clear(); SelectionFirst = null; rightLatch = false;
        if (Player.whoAmI == Main.myPlayer && Main.netMode == NetmodeID.MultiplayerClient) SendState();
    }
    public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
    {
        if (Main.netMode != NetmodeID.Server) return;
        ModPacket packet = Packet(8); packet.Write((byte)Player.whoAmI); WriteState(packet); packet.Send(toWho, fromWho);
    }
    internal void WriteState(BinaryWriter writer)
    {
        writer.Write(Modes); writer.Write((byte)SelectedOre); writer.Write(SelectionActive);
        writer.Write((short)Selection.X); writer.Write((short)Selection.Y);
        writer.Write((short)Selection.Width); writer.Write((short)Selection.Height);
    }
    internal bool ReadState(BinaryReader reader)
    {
        if (reader.BaseStream.Length - reader.BaseStream.Position < 24) return false;
        byte[] modes = reader.ReadBytes(14);
        int selected = reader.ReadByte(); bool enabled = reader.ReadBoolean();
        int x = reader.ReadInt16(), y = reader.ReadInt16(), width = reader.ReadInt16(), height = reader.ReadInt16();
        for (int i = 0; i < 14; i++) Modes[i] = (byte)Math.Clamp((int)modes[i], 0, MiningTool.ModeCount(i) - 1);
        SelectedOre = Math.Clamp(selected, 0, 12);
        SelectionActive = enabled && width is > 0 and <= 200 && height is > 0 and <= 200
            && WorldGen.InWorld(x, y, 2) && WorldGen.InWorld(x + width - 1, y + height - 1, 2);
        Selection = SelectionActive ? new Rectangle(x, y, width, height) : default;
        return true;
    }
    private static ModPacket Packet(byte operation)
    {
        ModPacket packet = ModContent.GetInstance<global::StarfallThrone.StarfallThrone>().GetPacket();
        packet.Write((byte)31); packet.Write(operation); return packet;
    }
    private void SendState()
    {
        if (Main.netMode != NetmodeID.MultiplayerClient) return;
        ModPacket packet = Packet(0); WriteState(packet); packet.Send();
    }
    public void CycleMode(int tool)
    {
        if (rightLatch || modeCooldown > 0) return;
        rightLatch = true; modeCooldown = 15;
        Modes[tool] = (byte)((Modes[tool] + 1) % MiningTool.ModeCount(tool));
        Main.NewText(MiningText.Get("CurrentMode", MiningText.Mode(tool, Modes[tool])), Color.LightCyan);
        SendState();
    }
    public void SelectNextOre(int maximum)
    {
        if (rightLatch || modeCooldown > 0) return;
        rightLatch = true; modeCooldown = 15;
        for (int step = 1; step <= maximum + 1; step++)
        {
            int candidate = (SelectedOre + step) % (maximum + 1);
            if (!MiningCatalog.Unlocked(candidate)) continue;
            SelectedOre = candidate; break;
        }
        SurveyResults.Clear(); surveyCooldown = 0;
        Main.NewText(MiningText.Get("SurveySelected", new Item(MiningCatalog.OreType(SelectedOre)).Name), MiningCatalog.Colors[SelectedOre]);
        SendState();
    }
    public void MarkConstruction(Point at, bool clear)
    {
        if (modeCooldown > 0) return;
        modeCooldown = 20;
        if (clear) { SelectionActive = false; SelectionFirst = null; Selection = default; SendState(); return; }
        if (!WorldGen.InWorld(at.X, at.Y, 2) || Vector2.DistanceSquared(Player.Center, at.ToWorldCoordinates()) > 1200 * 1200) return;
        if (SelectionFirst is not Point first)
        {
            SelectionFirst = at;
            Main.NewText(MiningText.Get("MarkerFirst"), Color.LightCyan);
            return;
        }
        int x = Math.Min(first.X, at.X), y = Math.Min(first.Y, at.Y);
        int w = Math.Abs(first.X - at.X) + 1, h = Math.Abs(first.Y - at.Y) + 1;
        if (w > 200 || h > 200) { Main.NewText(MiningText.Get("MarkerTooLarge"), Color.Orange); return; }
        Selection = new Rectangle(x, y, w, h); SelectionFirst = null; SelectionActive = true;
        Main.NewText(MiningText.Get("MarkerReady", w, h), Color.LightGreen); SendState();
    }
    public override void PreUpdateMovement()
    {
        // Set fallStart before vanilla resolves the landing, not one frame afterward.
        if (Player.velocity.Y < 0 || Player.controlDown) return;
        int left = (int)Player.Left.X / 16, right = (int)Player.Right.X / 16;
        int top = (int)Player.Bottom.Y / 16;
        int bottom = (int)(Player.Bottom.Y + Math.Min(Player.velocity.Y, 48) + 2) / 16;
        for (int x = left; x <= right; x++)
        for (int y = top; y <= bottom; y++)
            if (WorldGen.InWorld(x, y, 2) && Main.tile[x, y].HasTile
                && Main.tile[x, y].TileType == ModContent.TileType<MiningUtilityTile13>())
            { Player.fallStart = (int)(Player.position.Y / 16); return; }
    }
    public override void PostUpdate()
    {
        if (localCooldown > 0) localCooldown--;
        if (serverCooldown > 0) serverCooldown--;
        if (interactionCooldown > 0) interactionCooldown--;
        if (modeCooldown > 0) modeCooldown--;
        if (surveyCooldown > 0) surveyCooldown--;
        if (Main.dedServ || Player.whoAmI != Main.myPlayer) return;
        if (!Main.mouseRight) rightLatch = false;
        if (GatherTier >= 2) Lighting.AddLight(Player.Center, .18f, .16f, .09f);
        if (Player.dead || Player.noItems || Player.CCed || Main.gameMenu) return;
        int kind = Player.HeldItem.ModItem is MiningSurvey survey && survey.Index < 3 ? survey.Index : -1;
        if (kind >= 0 && surveyCooldown == 0) Scan(kind);
        if (kind < 0) SurveyKind = -1;
    }
    internal bool BeginNativeStrike(int x, int y)
    {
        NoteIntent(x, y);
        if (Player.whoAmI != Main.myPlayer || localCooldown > 0 || Player.HeldItem.pick <= 0
            || !InMiningReach(Player, x, y) || Player.altFunctionUse == 2 || Main.gameMenu) return false;
        localCooldown = MiningInterval(Player);
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            // Sent before the native tile-break message, so the server can attribute its drop.
            ModPacket packet = Packet(1); packet.Write((short)x); packet.Write((short)y); packet.Send();
        }
        return Main.netMode == NetmodeID.SinglePlayer;
    }
    private void NoteIntent(int x, int y) { MiningIntent = new Point(x, y); IntentTick = Main.GameUpdateCount; }
    public static bool InMiningReach(Player player, int x, int y)
        => WorldGen.InWorld(x, y, 3) && player.active && !player.dead && !player.noItems && !player.CCed
        && Math.Abs(x - player.Center.X / 16f) <= Player.tileRangeX + player.HeldItem.tileBoost + player.blockRange + 1
        && Math.Abs(y - player.Center.Y / 16f) <= Player.tileRangeY + player.HeldItem.tileBoost + player.blockRange + 1;
    public static int MiningInterval(Player player)
        => Math.Max(8, (int)Math.Ceiling(Math.Max(1, player.HeldItem.useTime) * Math.Max(.35f, player.pickSpeed)));
    public static bool SafeExtra(Player player, int x, int y)
    {
        if (!InMiningReach(player, x, y) || !MiningWorld.CanAlter(x, y)) return false;
        MiningPlayer state = player.GetModPlayer<MiningPlayer>();
        if (state.SelectionActive && !state.Selection.Contains(x, y)) return false;
        Tile tile = Main.tile[x, y];
        if (!tile.HasTile || tile.IsActuated || Main.tileFrameImportant[tile.TileType] || Main.tileSolidTop[tile.TileType]
            || !Main.tileSolid[tile.TileType]) return false;
        // Never remove a support immediately beside furniture or a chest.
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            Tile neighbor = Main.tile[x + dx, y + dy];
            if (neighbor.HasTile && Main.tileFrameImportant[neighbor.TileType]) return false;
        }
        if (TileLoader.GetTile(tile.TileType) is ModTile modTile && player.HeldItem.pick < modTile.MinPick) return false;
        return true;
    }
    public static IEnumerable<Point> ShapeTargets(int tool, int mode, int x, int y)
    {
        if (mode <= 0) yield break;
        int rx = 0, ry = 0;
        if (tool is 7 or 9) { rx = mode == 1 ? 1 : 0; ry = mode == 2 ? 1 : 0; }
        else if (tool == 11 || (tool == 13 && mode == 3)) rx = ry = 1;
        else if (tool is 12 or 13) { rx = mode == 2 ? 1 : 2; ry = mode == 2 ? 2 : 1; }
        for (int dx = -rx; dx <= rx; dx++)
        for (int dy = -ry; dy <= ry; dy++)
            if (dx != 0 || dy != 0) yield return new Point(x + dx, y + dy);
    }
    internal void MineExtra(int x, int y)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || Player.HeldItem.ModItem is not MiningTool tool) return;
        int mode = Modes[tool.Index];
        // Copper tool's extra tap only helps its own mineral; vanilla resistance still applies.
        if (tool.Index == 1 && Main.tile[x, y].HasTile && Main.tile[x, y].TileType == MiningCatalog.OreTileType(1))
            PickOne(x, y);
        if (mode == 0) return;
        if (tool.Index == 10)
        {
            int type = Main.tile[x, y].HasTile ? Main.tile[x, y].TileType : -1;
            if (type < 0)
                foreach (Point adjacent in ShapeTargets(11, 1, x, y))
                    if (Main.tile[adjacent.X, adjacent.Y].HasTile && IsOre(Main.tile[adjacent.X, adjacent.Y].TileType))
                    { type = Main.tile[adjacent.X, adjacent.Y].TileType; break; }
            if (!IsOre(type)) return;
            Queue<Point> queue = new(); HashSet<Point> seen = new();
            queue.Enqueue(new Point(x, y)); seen.Add(new Point(x, y)); int count = 0;
            while (queue.Count > 0 && count < 6 && seen.Count < 40)
            {
                Point p = queue.Dequeue();
                foreach (Point next in new[] { new Point(p.X + 1, p.Y), new Point(p.X - 1, p.Y), new Point(p.X, p.Y + 1), new Point(p.X, p.Y - 1) })
                {
                    if (!seen.Add(next) || !WorldGen.InWorld(next.X, next.Y, 3)) continue;
                    Tile tile = Main.tile[next.X, next.Y];
                    if (!tile.HasTile || tile.TileType != type || !SafeExtra(Player, next.X, next.Y)) continue;
                    queue.Enqueue(next); PickOne(next.X, next.Y); if (++count >= 6) break;
                }
            }
        }
        else foreach (Point point in ShapeTargets(tool.Index, mode, x, y)) PickOne(point.X, point.Y);
    }
    public static bool IsOre(int type) => type >= 0 && type < TileLoader.TileCount
        && (TileID.Sets.Ore[type] || MiningCatalog.OreFromTile(type) >= 0);
    private void PickOne(int x, int y)
    {
        if (!SafeExtra(Player, x, y)) return;
        ExtractionOwner = Player.whoAmI;
        try
        {
            // Use vanilla's accumulated pick damage and TileLoader checks, never KillTile.
            Player.PickTile(x, y, Player.HeldItem.pick);
            if (Main.netMode == NetmodeID.Server) NetMessage.SendTileSquare(-1, x, y, 1);
        }
        finally { ExtractionOwner = -1; }
    }
    private void Scan(int kind)
    {
        surveyCooldown = 90; SurveyKind = kind; SurveyResults.Clear();
        if (!MiningCatalog.Unlocked(SelectedOre) || (kind == 0 && SelectedOre > 3)) return;
        int radius = kind switch { 0 => 45, 1 => 100, _ => 160 };
        int cx = (int)Player.Center.X / 16, cy = (int)Player.Center.Y / 16;
        int type = MiningCatalog.OreTileType(SelectedOre);
        List<Point> candidates = new();
        for (int x = Math.Max(2, cx - radius); x <= Math.Min(Main.maxTilesX - 3, cx + radius); x++)
        for (int y = Math.Max(2, cy - radius); y <= Math.Min(Main.maxTilesY - 3, cy + radius); y++)
        {
            if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > radius * radius) continue;
            Tile tile = Main.tile[x, y];
            if (tile.HasTile && tile.TileType == type) candidates.Add(new Point(x, y));
        }
        candidates.Sort((a, b) => Vector2.DistanceSquared(a.ToVector2(), new Vector2(cx, cy)).CompareTo(Vector2.DistanceSquared(b.ToVector2(), new Vector2(cx, cy))));
        foreach (Point candidate in candidates)
        {
            bool sameVein = SurveyResults.Exists(p => Vector2.DistanceSquared(p.ToVector2(), candidate.ToVector2()) < 20 * 20);
            if (sameVein) continue;
            SurveyResults.Add(candidate);
            if (SurveyResults.Count >= (kind == 2 ? 3 : 1)) break;
        }
    }
    public void RememberNearest()
    {
        if (SurveyResults.Count == 0) return;
        SavedWaypoint = SurveyResults[0]; Main.NewText(MiningText.Get("WaypointSaved"), Color.LightCyan);
    }
    public static bool InFurnitureReach(Player player, int x, int y)
        => player.active && !player.dead && WorldGen.InWorld(x, y, 3)
        && Vector2.DistanceSquared(player.Center, new Vector2(x * 16 + 8, y * 16 + 8)) <= 160 * 160;
    public static void RequestFurniture(int x, int y)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        { ModPacket packet = Packet(2); packet.Write((short)x); packet.Write((short)y); packet.Send(); }
        else MiningUtilityTile.Interact(Main.LocalPlayer, x, y);
    }
    public static void Tell(Player player, string key, params object[] args)
    {
        if (Main.netMode == NetmodeID.Server)
            Terraria.Chat.ChatHelper.SendChatMessageToClient(NetworkText.FromKey("Mods.StarfallThrone.MiningToolsUI." + key, args), Color.LightCyan, player.whoAmI);
        else Main.NewText(MiningText.Get(key, args), Color.LightCyan);
    }
    public static void ReceivePacket(BinaryReader reader, int sender)
    {
        if (reader.BaseStream.Length - reader.BaseStream.Position < 1) return;
        byte op = reader.ReadByte();
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            if (op != 8 || reader.BaseStream.Length - reader.BaseStream.Position < 25) return;
            int id = reader.ReadByte(); if (id >= Main.maxPlayers) return;
            // These are personal preferences. A joining server's default snapshot must not
            // erase this character's saved modes before OnEnterWorld uploads them.
            if (id == Main.myPlayer) return;
            Main.player[id].GetModPlayer<MiningPlayer>().ReadState(reader); return;
        }
        if (Main.netMode != NetmodeID.Server || sender < 0 || sender >= Main.maxPlayers) return;
        Player player = Main.player[sender];
        if (!player.active || player.dead || player.noItems || player.CCed) return;
        MiningPlayer state = player.GetModPlayer<MiningPlayer>();
        if (op == 0)
        {
            if (state.interactionCooldown > 0) return;
            state.interactionCooldown = 6;
            if (state.ReadState(reader)) state.SyncPlayer(-1, sender, false);
        }
        else if (op is 1 or 2)
        {
            if (reader.BaseStream.Length - reader.BaseStream.Position < 4) return;
            int x = reader.ReadInt16(), y = reader.ReadInt16();
            if (op == 1)
            {
                if (state.serverCooldown > 0 || player.HeldItem.pick <= 0 || !InMiningReach(player, x, y)) return;
                // Item state and mining frequency come from the server-side inventory, never the packet.
                if (!player.controlUseItem || player.altFunctionUse == 2) return;
                state.serverCooldown = MiningInterval(player); state.NoteIntent(x, y); state.MineExtra(x, y);
            }
            else
            {
                if (state.interactionCooldown > 0 || !InFurnitureReach(player, x, y)) return;
                state.interactionCooldown = 12; MiningUtilityTile.Interact(player, x, y);
            }
        }
    }
}

/// <summary>Wraps actual native pick strikes, including vanilla channeled drills, not mouse animation guesses.</summary>
public sealed class MiningNativeHooks : ModSystem
{
    public override void Load() => On_Player.PickTile += OnPick;
    public override void Unload() => On_Player.PickTile -= OnPick;
    private static void OnPick(On_Player.orig_PickTile orig, Player player, int x, int y, int pickPower)
    {
        if (MiningPlayer.ExtractionOwner >= 0) { orig(player, x, y, pickPower); return; }
        MiningPlayer state = player.GetModPlayer<MiningPlayer>();
        bool extras = state.BeginNativeStrike(x, y);
        int previous = MiningPlayer.ExtractionOwner;
        MiningPlayer.ExtractionOwner = player.whoAmI;
        try { orig(player, x, y, pickPower); }
        finally { MiningPlayer.ExtractionOwner = previous; }
        if (extras) state.MineExtra(x, y);
    }
}

/// <summary>Only real tile-break drops receive a short-lived mining ownership tag.</summary>
public sealed class MiningExtractedItem : GlobalItem
{
    public override bool InstancePerEntity => true;
    public int Owner = -1;
    public ulong Born;
    public override void OnSpawn(Item item, IEntitySource source)
    {
        Owner = -1; Born = Main.GameUpdateCount;
        if (source is not EntitySource_TileBreak tileSource || item.createTile < 0 || item.createWall >= 0) return;
        if (MiningPlayer.ExtractionOwner >= 0) { Owner = MiningPlayer.ExtractionOwner; return; }
        int candidate = -1;
        for (int i = 0; i < Main.maxPlayers; i++)
        {
            Player player = Main.player[i];
            if (!player.active || player.dead || player.HeldItem.pick <= 0) continue;
            MiningPlayer state = player.GetModPlayer<MiningPlayer>();
            if (Main.GameUpdateCount - state.IntentTick > 20 || state.MiningIntent.X != tileSource.TileCoords.X || state.MiningIntent.Y != tileSource.TileCoords.Y) continue;
            if (candidate >= 0) return; // Ambiguous simultaneous miners: do not steal either player's drop.
            candidate = i;
        }
        Owner = candidate;
    }
    public override void NetSend(Item item, BinaryWriter writer) => writer.Write((short)Owner);
    public override void NetReceive(Item item, BinaryReader reader)
    {
        int proposed = reader.ReadInt16();
        // Never accept ownership labels authored by a client on the server.
        if (Main.netMode != NetmodeID.Server) { Owner = proposed is >= 0 and < 255 ? proposed : -1; Born = Main.GameUpdateCount; }
    }
    private bool Eligible(Player player) => Owner == player.whoAmI && Main.GameUpdateCount - Born <= 900
        && (player.GetModPlayer<MiningPlayer>().AccessoryMagnet || player.GetModPlayer<MiningPlayer>().ToolMagnet);
    public override void GrabRange(Item item, Player player, ref int grabRange)
    { if (Eligible(player)) grabRange = Math.Max(grabRange, 240); }
    public override bool GrabStyle(Item item, Player player)
    {
        if (!Eligible(player)) return false;
        Vector2 delta = player.Center - item.Center;
        if (delta.LengthSquared() > 260 * 260) return false;
        item.velocity = Vector2.Lerp(item.velocity, delta.SafeNormalize(Vector2.Zero) * 9, .18f); return true;
    }
    public override bool CanStackInWorld(Item destination, Item source)
        => destination.GetGlobalItem<MiningExtractedItem>().LiveOwner == source.GetGlobalItem<MiningExtractedItem>().LiveOwner;
    private int LiveOwner => Main.GameUpdateCount - Born <= 900 ? Owner : -1;
}
