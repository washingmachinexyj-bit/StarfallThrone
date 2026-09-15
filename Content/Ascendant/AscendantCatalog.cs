using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Ascendant;

public static class AscendantCatalog
{
    public const int Count = 3;
    public const string Root = "StarfallThrone/Content/Assets/Ascendant/";
    public static readonly string[] Keys = { "TrueMiroen", "TrueVelsa", "TrueNoDay" };
    public static readonly string[] Names = { "万生初誓·弥露恩", "四境焚律·维尔萨", "终命裁决·无昼" };
    public static readonly Color[] Colors = { new(162,232,164), new(255,154,67), new(237,219,161) };
    public static readonly int[] Defense = { 18,30,60 };
    private static readonly int[,] Lives = { {12000,18000,22800}, {90000,135000,171000}, {720000,1080000,1368000} };
    public static int Life(int index, int difficulty) => Lives[index, System.Math.Clamp(difficulty,0,2)];
    public static int Difficulty => Main.masterMode ? 2 : Main.expertMode ? 1 : 0;
    public static int Item(string name) => ModContent.Find<ModItem>("StarfallThrone",name).Type;
    public static int NPCType(int index) => ModContent.Find<ModNPC>("StarfallThrone","AscendantBoss"+index).Type;
    public static int Material(int index) => Item("AscendantMaterial"+index);
    public static int Summon(int index) => Item("AscendantSummon"+index);
    public static int Bag(int index) => Item("AscendantBag"+index);
    public static int Relic(int index) => Item("AscendantRelic"+index);
    public static int Trophy(int index) => Item("AscendantTrophy"+index);
    public static int Mask(int index) => Item("AscendantMask"+index);
    public static int Cache(int index) => Item("AscendantCache"+index);
    public static int Weapon(int index) => Item("AscendantWeapon"+index);
    public static int Expert(int index) => Item("AscendantExpert"+index);
    public static int Armor(int index) => Item("AscendantArmor"+index);
    public static int Pet(int index) => Item("AscendantPetItem"+index);
    public static int Halo(int index) => Item("AscendantHalo"+index);
    public static int CraftStation(int index) => index switch { 0 => TileID.WorkBenches,1 => TileID.Anvils,_ => TileID.LunarCraftingStation };
    public static float Progression(int index) => index switch {0 => .99f,1 => 6.99f,_ => 17.99f};
}
