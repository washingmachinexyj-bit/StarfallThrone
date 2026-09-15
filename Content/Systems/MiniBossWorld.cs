using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace StarfallThrone.Content.Systems;

public sealed class MiniBossWorld : ModSystem
{
    public static bool[] Downed = new bool[MiniBossData.Count];
    public override void ClearWorld() => Downed = new bool[MiniBossData.Count];
    public static void MarkDowned(int index)
    {
        if (Downed[index]) return;
        Downed[index] = true;
        if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.WorldData);
    }
    public override void SaveWorldData(TagCompound tag)
    {
        for (int i = 0; i < Downed.Length; i++) if (Downed[i]) tag["MiniDowned" + MiniBossData.Keys[i]] = true;
    }
    public override void LoadWorldData(TagCompound tag)
    {
        Downed = new bool[MiniBossData.Count];
        for (int i = 0; i < Downed.Length; i++) Downed[i] = tag.GetBool("MiniDowned" + MiniBossData.Keys[i]);
    }
    public override void NetSend(BinaryWriter writer) { foreach (bool flag in Downed) writer.Write(flag); }
    public override void NetReceive(BinaryReader reader) { for (int i = 0; i < Downed.Length; i++) Downed[i] = reader.ReadBoolean(); }
}
