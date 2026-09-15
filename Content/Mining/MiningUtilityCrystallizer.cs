using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace StarfallThrone.Content.Mining;

/// <summary>Two finite paid jobs at most. World mutations and inventory consumption are server-owned.</summary>
public sealed class MiningCrystallizerEntity : ModTileEntity
{
    public const int WorkRequired = 7200; // Four units/tick = 30 seconds; growth chamber uses five.
    private readonly List<int> pending = new();
    private int work;
    private int capacity = 1, speed = 4, attachmentClock;
    public int PendingCount => pending.Count;
    public float ProgressFraction => Math.Clamp(work / (float)WorkRequired, 0, 1);
    public int Capacity => capacity;
    public int CurrentOre => pending.Count == 0 ? -1 : pending[0];
    public override bool IsTileValidForEntity(int x, int y)
        => WorldGen.InWorld(x, y, 4) && Main.tile[x, y].HasTile
        && Main.tile[x, y].TileType == ModContent.TileType<MiningUtilityTile12>()
        && Main.tile[x, y].TileFrameX % 54 == 0 && Main.tile[x, y].TileFrameY % 54 == 0;

    public void Interact(Player player)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !MiningPlayer.InFurnitureReach(player, Position.X + 1, Position.Y + 1)
            || !IsTileValidForEntity(Position.X, Position.Y)) return;
        RefreshAttachments();
        if (player.HeldItem.ModItem is not MiningSeed seed)
        {
            MiningPlayer.Tell(player, "VatStatus", PendingCount, capacity, (int)(ProgressFraction * 100)); return;
        }
        if (!MiningCatalog.Unlocked(seed.Index)) { MiningPlayer.Tell(player, "VatLocked"); return; }
        if (pending.Count >= capacity) { MiningPlayer.Tell(player, "VatFull"); return; }
        int slot = player.selectedItem;
        if (slot is < 0 or >= 58 || player.inventory[slot].type != seed.Type || player.inventory[slot].stack <= 0) return;
        int ore = seed.Index;
        player.inventory[slot].stack--;
        if (player.inventory[slot].stack == 0) player.inventory[slot].TurnToAir();
        pending.Add(ore);
        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, player.whoAmI, slot, player.inventory[slot].prefix);
        Sync();
        MiningPlayer.Tell(player, "VatLoaded", new Item(MiningCatalog.OreType(ore)).Name, MiningCatalog.PickRequirements[ore]);
    }
    public void RefreshAttachments()
    {
        capacity = 1; speed = 4;
        for (int x = Math.Max(4, Position.X - 7); x <= Math.Min(Main.maxTilesX - 5, Position.X + 9); x++)
        for (int y = Math.Max(4, Position.Y - 7); y <= Math.Min(Main.maxTilesY - 5, Position.Y + 9); y++)
        {
            Tile tile = Main.tile[x, y];
            if (!tile.HasTile || TileLoader.GetTile(tile.TileType) is not MiningUtilityTile utility) continue;
            if (utility.Index is 7 or 8 or 9 or 10) capacity = 2;
            if (utility.Index is 8 or 10) speed = 5;
        }
    }
    public override void Update()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient) return;
        if (++attachmentClock >= 120) { attachmentClock = 0; RefreshAttachments(); }
        if (pending.Count == 0) { work = 0; return; }
        // Imported seeds remain inert in worlds where their boss has not been defeated.
        if (!MiningCatalog.Unlocked(pending[0])) return;
        work = Math.Min(WorkRequired, work + speed);
        if (work >= WorkRequired) TryCommit();
        if (attachmentClock % 60 == 0) Sync();
    }
    public bool OutputClear()
    {
        for (int dx = 0; dx < 3; dx++)
        for (int dy = -2; dy <= -1; dy++)
        {
            int x = Position.X + dx, y = Position.Y + dy;
            if (!WorldGen.InWorld(x, y, 4) || Main.tile[x, y].HasTile || Main.tile[x, y].LiquidAmount > 0) return false;
            Rectangle cell = new(x * 16, y * 16, 16, 16);
            for (int p = 0; p < Main.maxPlayers; p++)
                if (Main.player[p].active && !Main.player[p].dead && Main.player[p].Hitbox.Intersects(cell)) return false;
        }
        return true;
    }
    public bool TryCommit()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || work < WorkRequired || pending.Count == 0
            || !IsTileValidForEntity(Position.X, Position.Y) || !MiningCatalog.Unlocked(pending[0]) || !OutputClear()) return false;
        int ore = pending[0]; int type = MiningCrystalTile.TypeFor(ore);
        // Atomic on Terraria's world thread: no asynchronous gap between consuming the job
        // and committing its six empty, checked cells. No external placement hooks execute.
        pending.RemoveAt(0); work = 0;
        for (int dx = 0; dx < 3; dx++)
        for (int dy = -2; dy <= -1; dy++)
        {
            Tile tile = Main.tile[Position.X + dx, Position.Y + dy];
            tile.HasTile = true; tile.TileType = (ushort)type;
            tile.TileFrameX = tile.TileFrameY = 0; tile.Slope = SlopeType.Solid; tile.IsHalfBlock = false;
        }
        if (Main.netMode == NetmodeID.Server) NetMessage.SendTileSquare(-1, Position.X, Position.Y - 2, 3, 2);
        Sync(); return true;
    }
    public override void OnKill()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient) return;
        // Remove ownership before spawning refunds. Produced crystals are never a pending job.
        int[] refunds = pending.ToArray(); pending.Clear(); work = 0;
        foreach (int ore in refunds)
            Item.NewItem(new EntitySource_TileBreak(Position.X, Position.Y), Position.X * 16, Position.Y * 16, 48, 48,
                MiningCatalog.ItemType("MiningSeed" + ore));
    }
    public override void SaveData(TagCompound tag)
    { tag["JobsV1"] = pending.ToArray(); tag["WorkV1"] = work; }
    public override void LoadData(TagCompound tag)
    {
        pending.Clear();
        foreach (int ore in tag.GetIntArray("JobsV1"))
            if (ore is >= 0 and < 13 && pending.Count < 2) pending.Add(ore);
        work = pending.Count == 0 ? 0 : Math.Clamp(tag.GetInt("WorkV1"), 0, WorkRequired);
        capacity = 1; speed = 4;
    }
    public override void NetSend(BinaryWriter writer)
    {
        writer.Write((byte)pending.Count);
        foreach (int ore in pending) writer.Write((byte)ore);
        writer.Write(work); writer.Write((byte)capacity); writer.Write((byte)speed);
    }
    public override void NetReceive(BinaryReader reader)
    {
        int count = reader.ReadByte();
        if (count > 2 || reader.BaseStream.Length - reader.BaseStream.Position < count + 6) return;
        int[] incoming = new int[count]; for (int i = 0; i < count; i++) incoming[i] = reader.ReadByte();
        int incomingWork = reader.ReadInt32(), incomingCapacity = reader.ReadByte(), incomingSpeed = reader.ReadByte();
        if (Main.netMode == NetmodeID.Server) return;
        pending.Clear(); foreach (int ore in incoming) if (ore < 13) pending.Add(ore);
        work = pending.Count == 0 ? 0 : Math.Clamp(incomingWork, 0, WorkRequired);
        capacity = Math.Clamp(incomingCapacity, 1, 2); speed = Math.Clamp(incomingSpeed, 4, 5);
    }
    private void Sync()
    {
        if (Main.netMode == NetmodeID.Server && ID >= 0)
            NetMessage.SendData(MessageID.TileEntitySharing, -1, -1, null, ID, Position.X, Position.Y);
    }
}
