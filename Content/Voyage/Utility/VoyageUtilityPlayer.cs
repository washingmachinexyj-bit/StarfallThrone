using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace StarfallThrone.Content.Voyage.Utility;

/// <summary>
/// Cargo is character-owned, just like vanilla personal banks, never a world chest.
/// There is deliberately no packet that can replace cargo contents or another player's items.
/// The only custom requests are bounded, validated interactions with world utility furniture.
/// </summary>
public sealed class VoyageUtilityPlayer : ModPlayer
{
    public Item[] Cargo = Array.Empty<Item>();
    public bool Builder;
    public int RecallTicks { get; private set; }
    private Vector2 recallOrigin;
    private int requestCooldown;

    public override void Initialize() => Cargo = Enumerable.Range(0, 40).Select(_ => new Item()).ToArray();
    public override void ResetEffects() => Builder = false;
    public override void SaveData(TagCompound tag) => tag["VoyageCargoV1"] = Cargo.Select(ItemIO.Save).ToList();
    public override void LoadData(TagCompound tag)
    {
        Initialize();
        if (!tag.ContainsKey("VoyageCargoV1")) return;
        var items = tag.GetList<TagCompound>("VoyageCargoV1");
        for (int i = 0; i < Math.Min(40, items.Count); i++) Cargo[i] = ItemIO.Load(items[i]);
    }
    public override void OnEnterWorld()
    {
        RecallTicks = 0;
        requestCooldown = 0;
        if (Player.whoAmI == Main.myPlayer) VoyageUtilityUI.Close();
    }
    public override void UpdateDead()
    {
        RecallTicks = 0;
        if (Player.whoAmI == Main.myPlayer) VoyageUtilityUI.Close();
    }
    public override void PostHurt(Player.HurtInfo info)
    {
        if (RecallTicks == 0) return;
        RecallTicks = 0;
        if (Player.whoAmI == Main.myPlayer && Main.netMode == NetmodeID.MultiplayerClient)
        {
            ModPacket packet = Mod.GetPacket(); packet.Write((byte)22); packet.Write((byte)3); packet.Send();
        }
    }
    public override void PostUpdate()
    {
        if (requestCooldown > 0) requestCooldown--;
        if (RecallTicks == 0) return;
        bool interrupted = !Player.active || Player.dead || Player.HeldItem.type != ModContent.ItemType<VoyageUtility4>()
            || VoyageCatalog.AnyEncounter() || Vector2.DistanceSquared(Player.position, recallOrigin) > 4
            || Player.controlJump || Player.controlLeft || Player.controlRight || Player.controlUp || Player.controlDown
            || (RecallTicks > 12 && !Player.channel);
        if (interrupted)
        {
            RecallTicks = 0;
            return;
        }
        RecallTicks++;
        if (!Main.dedServ && RecallTicks % 5 == 0)
        {
            Vector2 spot = Player.Center + (RecallTicks * .18f).ToRotationVector2() * 28;
            int dust = Dust.NewDust(spot, 1, 1, DustID.Electric, 0, -1, 100, VoyageCatalog.Colors[6], .65f);
            Main.dust[dust].noGravity = true;
        }
        if (RecallTicks < 121) return;
        RecallTicks = 0;
        if (Main.netMode == NetmodeID.MultiplayerClient || VoyageCatalog.AnyEncounter()) return;
        if (!TrySpawnDestination(Player, out Vector2 destination)) return;
        bool immune = Player.immune; int immuneTime = Player.immuneTime;
        Player.Teleport(destination, 1);
        Player.velocity = Vector2.Zero;
        Player.immune = immune; Player.immuneTime = immuneTime;
        Player.channel = false;
        Player.fallStart = (int)(Player.position.Y / 16f);
        // Use the ordinary authoritative teleport replication; no client-supplied position.
        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.TeleportEntity, -1, -1, null, 0, Player.whoAmI, destination.X, destination.Y, 1);
        else SoundEngine.PlaySound(SoundID.Item6, Player.Center);
    }

    public void RequestRecall()
    {
        if (VoyageCatalog.AnyEncounter() || RecallTicks != 0 || Player.dead) return;
        BeginRecall();
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            ModPacket packet = Mod.GetPacket(); packet.Write((byte)22); packet.Write((byte)0); packet.Send();
        }
    }
    private void BeginRecall()
    {
        recallOrigin = Player.position;
        RecallTicks = 1;
    }

    public static bool TrySpawnDestination(Player player, out Vector2 destination)
    {
        int x = player.SpawnX, y = player.SpawnY;
        if (x < 0 || y < 0 || !Player.CheckSpawn(x, y)) { x = Main.spawnTileX; y = Main.spawnTileY; }
        // Bed/world spawn can have changed since the character last used it. Never put the
        // player inside solid tiles or lava and never alter blocks to make space.
        for (int radius = 0; radius <= 12; radius++)
        for (int dy = 0; dy <= 12; dy++)
        for (int side = radius == 0 ? 1 : -1; side <= 1; side += 2)
        {
            int tx = x + radius * side, ty = y - dy;
            if (!WorldGen.InWorld(tx, ty, 10)) continue;
            Vector2 spot = new(tx * 16 + 8 - player.width / 2, ty * 16 - player.height);
            if (Collision.SolidCollision(spot, player.width, player.height)) continue;
            bool lava = false;
            for (int a = (int)spot.X / 16; a <= (int)(spot.X + player.width - 1) / 16; a++)
            for (int b = (int)spot.Y / 16; b <= (int)(spot.Y + player.height - 1) / 16; b++)
            {
                Tile tile = Main.tile[a, b];
                lava |= tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Lava;
            }
            if (lava) continue;
            destination = spot;
            return true;
        }
        destination = player.position;
        return false;
    }

    public static bool InReach(Player player, int i, int j)
        => player.active && !player.dead && WorldGen.InWorld(i, j, 10)
        && Vector2.DistanceSquared(player.Center, new Vector2(i * 16 + 8, j * 16 + 8)) <= 160 * 160;

    public static void RequestFurniture(byte operation, int i, int j)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            ModPacket packet = ModContent.GetInstance<global::StarfallThrone.StarfallThrone>().GetPacket();
            packet.Write((byte)22); packet.Write(operation); packet.Write((short)i); packet.Write((short)j); packet.Send();
        }
        else ActOnFurniture(Main.LocalPlayer, operation, i, j);
    }

    private static void ActOnFurniture(Player player, byte operation, int i, int j)
    {
        if (!InReach(player, i, j)) return;
        Tile tile = Main.tile[i, j];
        if (!tile.HasTile) return;
        if (operation == 1 && tile.TileType == ModContent.TileType<VoyageRepairStationTile>())
        {
            player.AddBuff(ModContent.BuffType<VoyageRepairBuff>(), 36000);
        }
        else if (operation == 2 && tile.TileType == ModContent.TileType<VoyageArkDisplayTile>())
        {
            int left = i - tile.TileFrameX % 54 / 18, top = j - tile.TileFrameY % 54 / 18;
            int next = (tile.TileFrameX / 54 + 1) % 3;
            for (int dx = 0; dx < 3; dx++)
            for (int dy = 0; dy < 3; dy++)
            {
                Tile part = Main.tile[left + dx, top + dy];
                if (!part.HasTile || part.TileType != tile.TileType) return;
            }
            for (int dx = 0; dx < 3; dx++)
            for (int dy = 0; dy < 3; dy++)
                Main.tile[left + dx, top + dy].TileFrameX = (short)(next * 54 + dx * 18);
            if (Main.netMode == NetmodeID.Server) NetMessage.SendTileSquare(-1, left, top, 3, 3);
        }
    }

    public static void ReceivePacket(BinaryReader reader, int sender)
    {
        if (reader.BaseStream.Length - reader.BaseStream.Position < 1) return;
        byte operation = reader.ReadByte();
        int i = 0, j = 0;
        if (operation is 1 or 2)
        {
            if (reader.BaseStream.Length - reader.BaseStream.Position < 4) return;
            i = reader.ReadInt16(); j = reader.ReadInt16();
        }
        if (Main.netMode != NetmodeID.Server || sender < 0 || sender >= Main.maxPlayers || operation > 3) return;
        Player player = Main.player[sender];
        if (!player.active || player.dead) return;
        VoyageUtilityPlayer state = player.GetModPlayer<VoyageUtilityPlayer>();
        if (operation == 3) { state.RecallTicks = 0; return; }
        if (state.requestCooldown > 0) return;
        state.requestCooldown = 12;
        if (operation == 0)
        {
            if (player.HeldItem.type == ModContent.ItemType<VoyageUtility4>() && state.RecallTicks == 0 && !VoyageCatalog.AnyEncounter())
                state.BeginRecall();
        }
        else ActOnFurniture(player, operation, i, j);
    }

    /// <summary>Explicit quick-stack only: never auto-imports items from the inventory.</summary>
    public void QuickStackCargo()
    {
        for (int slot = 10; slot < 50; slot++)
        {
            Item source = Player.inventory[slot];
            if (source.IsAir || source.favorited) continue;
            foreach (Item destination in Cargo)
                if (!destination.IsAir && source.type == destination.type && destination.stack < destination.maxStack)
                    ItemLoader.TryStackItems(destination, source, out _);
            if (source.stack <= 0) source.TurnToAir();
        }
        SyncInventory();
        Recipe.FindRecipes();
    }

    public void Withdraw(int cargoSlot)
    {
        if (cargoSlot < 0 || cargoSlot >= Cargo.Length || Cargo[cargoSlot].IsAir) return;
        Item source = Cargo[cargoSlot];
        for (int slot = 0; slot < 50 && !source.IsAir; slot++)
        {
            Item target = Player.inventory[slot];
            if (!target.IsAir && target.type == source.type && target.stack < target.maxStack)
                ItemLoader.TryStackItems(target, source, out _);
        }
        if (source.stack <= 0) source.TurnToAir();
        if (!source.IsAir)
            for (int slot = 0; slot < 50; slot++)
                if (Player.inventory[slot].IsAir)
                {
                    // Transfer ownership of the actual instance; do not clone-and-forget mod data.
                    Player.inventory[slot] = source;
                    Cargo[cargoSlot] = new Item();
                    break;
                }
        SyncInventory();
        Recipe.FindRecipes();
    }

    private void SyncInventory()
    {
        if (Main.netMode != NetmodeID.MultiplayerClient || Player.whoAmI != Main.myPlayer) return;
        for (int slot = 0; slot < 50; slot++)
            NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, Player.whoAmI, slot, Player.inventory[slot].prefix);
    }
}
