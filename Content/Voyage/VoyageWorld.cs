using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace StarfallThrone.Content.Voyage;

/// <summary>Independent world progression; never changes the three earlier encounter ladders.</summary>
public sealed class VoyageWorld : ModSystem
{
    public static bool[] Downed = new bool[VoyageCatalog.Count];
    public static bool IsDowned(int index) => index >= 0 && index < Downed.Length && Downed[index];
    public static bool CanSummon(int index) => index >= 0 && index < VoyageCatalog.Count &&
        (index == 0 ? global::StarfallThrone.Content.Systems.StarfallWorld.IsDowned(16) : IsDowned(index - 1));
    public static bool TrySummon(int index, int playerIndex)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !CanSummon(index) || VoyageCatalog.AnyEncounter() ||
            playerIndex < 0 || playerIndex >= Main.maxPlayers || !Main.player[playerIndex].active || Main.player[playerIndex].dead) return false;
        NPC.SpawnOnPlayer(playerIndex, VoyageCatalog.BossType(index));
        return true;
    }
    public static void SetDowned(int index)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || index < 0 || index >= Downed.Length || Downed[index]) return;
        Downed[index] = true;
        if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.WorldData);
    }
    public override void OnWorldLoad() => Downed = new bool[VoyageCatalog.Count];
    public override void OnWorldUnload() => Downed = new bool[VoyageCatalog.Count];
    public override void SaveWorldData(TagCompound tag)
    {
        for (int i = 0; i < Downed.Length; i++) if (Downed[i]) tag["VoyageDowned" + i] = true;
    }
    public override void LoadWorldData(TagCompound tag)
    {
        Downed = new bool[VoyageCatalog.Count];
        for (int i = 0; i < Downed.Length; i++) Downed[i] = tag.GetBool("VoyageDowned" + i);
    }
    public override void NetSend(BinaryWriter writer)
    {
        for (int i = 0; i < Downed.Length; i++) writer.Write(Downed[i]);
    }
    public override void NetReceive(BinaryReader reader)
    {
        for (int i = 0; i < Downed.Length; i++) Downed[i] = reader.ReadBoolean();
    }
}
