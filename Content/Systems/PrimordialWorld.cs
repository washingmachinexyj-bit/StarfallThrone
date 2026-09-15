using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace StarfallThrone.Content.Systems;

public sealed class PrimordialWorld : ModSystem
{
    public static bool[] Downed = new bool[PrimordialBossData.Count];

    public static bool IsDowned(int index) => index >= 0 && index < Downed.Length && Downed[index];

    public static bool CanSummon(int index) => index == 0 || IsDowned(index - 1);

    public static void SetDowned(int index)
    {
        if (index < 0 || index >= Downed.Length || Downed[index])
            return;

        Downed[index] = true;
        if (Main.netMode == Terraria.ID.NetmodeID.Server)
            NetMessage.SendData(Terraria.ID.MessageID.WorldData);
    }

    public override void OnWorldLoad() => Downed = new bool[PrimordialBossData.Count];
    public override void OnWorldUnload() => Downed = new bool[PrimordialBossData.Count];

    public override void SaveWorldData(TagCompound tag)
    {
        for (int i = 0; i < Downed.Length; i++)
            if (Downed[i])
                tag[$"PrimordialDowned{i}"] = true;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        Downed = new bool[PrimordialBossData.Count];
        for (int i = 0; i < Downed.Length; i++)
            Downed[i] = tag.ContainsKey($"PrimordialDowned{i}") && tag.GetBool($"PrimordialDowned{i}");
    }

    public override void NetSend(BinaryWriter writer)
    {
        for (int i = 0; i < Downed.Length; i++)
            writer.Write(Downed[i]);
    }

    public override void NetReceive(BinaryReader reader)
    {
        Downed = new bool[PrimordialBossData.Count];
        for (int i = 0; i < Downed.Length; i++)
            Downed[i] = reader.ReadBoolean();
    }
}
