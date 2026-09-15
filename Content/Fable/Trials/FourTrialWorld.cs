using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using StarfallThrone.Content.Fable.Equipment;

namespace StarfallThrone.Content.Fable.Trials;

public sealed class FourTrialWorld : ModSystem
{
    public static bool[] Downed = new bool[FourTrialCatalog.Count];

    public static bool AnyActive() => Main.npc.Any(n => n.active && n.ModNPC is FourTrialBossNPC);

    public override void ClearWorld() => Downed = new bool[FourTrialCatalog.Count];

    public override void SaveWorldData(TagCompound tag)
    {
        int mask = 0;
        for (int i = 0; i < Downed.Length; i++)
            if (Downed[i]) mask |= 1 << i;
        tag["Victories"] = mask;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        int mask = tag.GetInt("Victories");
        Downed = new bool[FourTrialCatalog.Count];
        for (int i = 0; i < Downed.Length; i++) Downed[i] = (mask & (1 << i)) != 0;
    }

    public override void NetSend(BinaryWriter writer)
    {
        int mask = 0;
        for (int i = 0; i < Downed.Length; i++)
            if (Downed[i]) mask |= 1 << i;
        writer.Write(mask);
    }

    public override void NetReceive(BinaryReader reader)
    {
        int mask = reader.ReadInt32();
        Downed = new bool[FourTrialCatalog.Count];
        for (int i = 0; i < Downed.Length; i++) Downed[i] = (mask & (1 << i)) != 0;
    }

    public static void Sync()
    {
        if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.WorldData);
    }

    public static bool TrySummon(Player player, int index)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !FourTrialCatalog.Valid(index) ||
            !player.active || player.dead || player.ghost || !FableWorld.HasBook(player)) return false;

        if (FableWorld.AnyEncounter || Main.invasionType > 0 || Main.pumpkinMoon || Main.snowMoon ||
            Terraria.GameContent.Events.DD2Event.Ongoing)
        {
            Say(player, "FourTrialBusy");
            return false;
        }

        Vector2 spawn = player.Center + new Vector2(player.direction == 0 ? 1 : player.direction, -110);
        if (Collision.SolidCollision(spawn - new Vector2(16, 16), 32, 32))
            spawn = player.Center + new Vector2(-(player.direction == 0 ? 1 : player.direction) * 120, -110);

        int slot = NPC.NewNPC(player.GetSource_Misc("FourTrial"), (int)spawn.X, (int)spawn.Y,
            FourTrialCatalog.BossType(index), Target: player.whoAmI);
        if (slot < 0 || slot >= Main.maxNPCs || Main.npc[slot].ModNPC is not FourTrialBossNPC boss)
            return false;

        Main.npc[slot].target = player.whoAmI;
        Main.npc[slot].netUpdate = true;
        if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: slot);
        return true;
    }

    public static void RecordVictory(int index)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !FourTrialCatalog.Valid(index)) return;
        bool first = !Downed[index];
        Downed[index] = true;
        Sync();
        if (!first) return;
        string key = "FourTrialVictory" + index;
        if (Main.netMode == NetmodeID.SinglePlayer) Main.NewText(FourTrialCatalog.Text(key), FourTrialCatalog.Colors[index]);
        else ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Mods.StarfallThrone.Fable." + key), FourTrialCatalog.Colors[index]);
    }

    public static void ReceivePacket(BinaryReader reader, int sender)
    {
        byte action = reader.ReadByte();
        int index = reader.ReadByte();
        if (Main.netMode != NetmodeID.Server || sender < 0 || sender >= Main.maxPlayers || action is not (1 or 2) ||
            !FourTrialCatalog.Valid(index)) return;

        Player player = Main.player[sender];
        FablePlayer state = player.GetModPlayer<FablePlayer>();
        if (!player.active || player.dead || player.ghost || state.RequestCooldown > 0) return;
        state.RequestCooldown = 20;
        TrySummon(player, index);
    }

    private static void Say(Player player, string key)
    {
        if (Main.netMode == NetmodeID.SinglePlayer) Main.NewText(FourTrialCatalog.Text(key), Color.Wheat);
        else if (Main.netMode == NetmodeID.Server)
            ChatHelper.SendChatMessageToClient(NetworkText.FromKey("Mods.StarfallThrone.Fable." + key), Color.Wheat, player.whoAmI);
    }
}
