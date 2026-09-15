using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Systems;

public static class MiniBossData
{
    public const int Count = 8;
    // Append identifiers: saved kills, item names and placed furniture styles retain their old meaning.
    public static readonly string[] Keys = { "MossTumbler", "PotHermit", "TatteredParasol", "WickMoth", "FlintBurrower", "BriarSprout", "DewShell", "DandelionPuff" };
    public static readonly int[] Order = { 6, 7, 0, 1, 2, 3, 4, 5 };
    public static readonly int[] Life = { 200, 310, 435, 580, 735, 880, 90, 145 };
    public static readonly int[] Defense = { 0, 1, 1, 2, 2, 3, 0, 0 };
    public static readonly int[] Contact = { 5, 6, 8, 9, 10, 10, 3, 4 };
    public static readonly int[] Attack = { 7, 8, 9, 10, 11, 12, 5, 6 };
    public static readonly Color[] Colors = { new(132, 198, 88), new(220, 144, 99), new(145, 190, 245), new(250, 201, 85), new(181, 165, 139), new(181, 217, 100), new(130, 216, 247), new(241, 233, 190) };
    public static int Prerequisite(int i) => i is >= 2 and <= 5 ? i - 1 : -1;
    public static bool Unlocked(int i) => i >= 0 && i < Count && (Prerequisite(i) < 0 || MiniBossWorld.Downed[Prerequisite(i)]);
    public static float Progression(int i) => i == 6 ? 0.001f : i == 7 ? 0.007f : 0.015f + i * 0.017f;
    public static int NPCType(int i) => ModContent.Find<ModNPC>("StarfallThrone", Keys[i] + "NPC").Type;
    public static int ItemType(string name) => ModContent.Find<ModItem>("StarfallThrone", name).Type;
    public static int SummonType(int i) => ItemType("MiniSummon" + i);
    public static int MaterialType(int i) => ItemType("MiniMaterial" + i);
    public static int TrophyType(int i) => ItemType("MiniTrophy" + i);
    public static int RewardType(int i) => ItemType(new[] { "MossInsoles", "PotBuckle", "UmbrellaVanity", "EmberPendant", "DiggingClaws", "SproutBadge", "DewBrooch", "FluffAnklet" }[i]);
    public static bool AnyActive()
    {
        foreach (NPC npc in Main.ActiveNPCs)
            if (npc.boss || npc.ModNPC is NPCs.MiniBossNPC { Dormant: false }) return true;
        return false;
    }
    public static bool Habitat(Player p, int i)
    {
        bool ordinary = !p.ZoneDungeon && !p.ZoneJungle && !p.ZoneSnow && !p.ZoneDesert && !p.ZoneBeach
            && !p.ZoneCorrupt && !p.ZoneCrimson && !p.ZoneHallow;
        bool surface = p.ZoneOverworldHeight;
        bool shallow = p.position.Y / 16f >= Main.worldSurface && p.position.Y / 16f < Main.rockLayer;
        if (!ordinary || !(surface || ((i == 1 || i == 4) && shallow))) return false;
        return i is 2 or 3 ? !Main.dayTime : i is 5 or 6 ? Main.dayTime : true;
    }
}
