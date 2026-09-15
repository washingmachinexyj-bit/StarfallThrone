using System;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace StarfallThrone.Content.Systems;

public sealed class StarfallWorld : ModSystem
{
    public const int BossCount = 17;
    public static bool[] Downed = new bool[BossCount];

    public static bool IsDowned(int bossIndex) => bossIndex >= 0 && bossIndex < BossCount && Downed[bossIndex];

    public static bool CanSummon(int bossIndex)
    {
        if (bossIndex == 0)
            return NPC.downedMoonlord;

        return IsDowned(bossIndex - 1);
    }

    public static void SetDowned(int bossIndex)
    {
        if (bossIndex < 0 || bossIndex >= BossCount || Downed[bossIndex])
            return;

        Downed[bossIndex] = true;
        if (Main.netMode == Terraria.ID.NetmodeID.Server)
            NetMessage.SendData(Terraria.ID.MessageID.WorldData);
    }

    public override void OnWorldLoad() => Downed = new bool[BossCount];

    public override void OnWorldUnload() => Downed = new bool[BossCount];

    public override void SaveWorldData(TagCompound tag)
    {
        for (int i = 0; i < BossCount; i++)
            if (Downed[i])
                tag[$"Downed{i}"] = true;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        Downed = new bool[BossCount];
        for (int i = 0; i < BossCount; i++)
            Downed[i] = tag.ContainsKey($"Downed{i}") && tag.GetBool($"Downed{i}");
    }

    public override void NetSend(BinaryWriter writer)
    {
        for (int i = 0; i < BossCount; i++)
            writer.Write(Downed[i]);
    }

    public override void NetReceive(BinaryReader reader)
    {
        Downed = new bool[BossCount];
        for (int i = 0; i < BossCount; i++)
            Downed[i] = reader.ReadBoolean();
    }
}
