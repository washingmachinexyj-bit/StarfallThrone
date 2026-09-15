using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Divine;

public static class DivineCatalog
{
    public const int Count = 3;
    public const string Root = "StarfallThrone/Content/Assets/Divine/";
    public static readonly string[] Keys = { "Miroen", "Velsa", "NoDay" };
    public static readonly string[] Names = { "原露之神·弥露恩", "烬誓之神·维尔萨", "天命之神·无昼" };
    public static readonly Color[] Colors = { new(162,232,164), new(255,154,67), new(237,219,161) };
    public static readonly int[] Defense = { 12,20,45 };
    private static readonly int[,] Lives = { {4800,7200,9200}, {36000,54000,69000}, {240000,360000,460000} };
    public static int Life(int index, int difficulty) => Lives[index, System.Math.Clamp(difficulty,0,2)];
    public static int Difficulty => Main.masterMode ? 2 : Main.expertMode ? 1 : 0;
    public static int Item(string name) => ModContent.Find<ModItem>("StarfallThrone",name).Type;
    public static int NPCType(int index) => ModContent.Find<ModNPC>("StarfallThrone","DivineBoss"+index).Type;
    public static int Material(int index) => Item("DivineMaterial"+index);
    public static int Summon(int index) => Item("DivineSummon"+index);
    public static int Bag(int index) => Item("DivineBag"+index);
    public static int Relic(int index) => Item("DivineRelic"+index);
    public static int Trophy(int index) => Item("DivineTrophy"+index);
    public static int Mask(int index) => Item("DivineMask"+index);
    public static int Cache(int index) => Item("DivineCache"+index);
    public static int Weapon(int index) => Item("DivineWeapon"+index);
    public static int Expert(int index) => Item("DivineExpert"+index);
    public static int Armor(int index) => Item("DivineArmor"+index);
    public static int Pet(int index) => Item("DivinePetItem"+index);
    public static int Halo(int index) => Item("DivineHalo"+index);
    public static int CraftStation(int index) => index switch { 0 => TileID.WorkBenches,1 => TileID.Anvils,_ => TileID.LunarCraftingStation };
    public static float Progression(int index) => index switch {0 => .97f,1 => 6.95f,_ => 17.95f};
}
