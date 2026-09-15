using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Fable;

public static class FableCatalog
{
    public const int Count = 18;
    public const string Root = "StarfallThrone/Content/Assets/Fable/";
    public static readonly int[] Life = {45,65,90,120,155,195,240,290,345,405,470,535,600,665,730,800,880,960};
    public static readonly int[] Defense = {0,0,0,0,0,0,0,0,0,1,1,1,1,1,1,1,2,2};
    public static readonly int[] Contact = {2,3,3,4,4,4,5,5,5,6,6,6,7,7,7,8,8,9};
    public static readonly int[] Shot = {3,4,4,5,5,6,6,6,7,7,8,8,9,9,10,10,11,11};
    public static readonly Color[] Colors = {new(129,201,233),new(222,162,165),new(172,141,205),new(226,166,192),new(235,201,110),new(214,209,189),new(221,152,153),new(197,166,235),new(182,206,169),new(177,186,189),new(171,181,199),new(206,162,172),new(194,159,112),new(138,205,202),new(218,190,226),new(153,178,219),new(166,200,175),new(232,223,195)};
    public static bool Valid(int i) => i >= 0 && i < Count;
    public static int BossType(int i) => ModContent.Find<ModNPC>("StarfallThrone", "FableBoss" + i).Type;
    public static int Weapon(int i) => ModContent.Find<ModItem>("StarfallThrone", "FableWeapon" + i).Type;
    public static int Item(string name) => ModContent.Find<ModItem>("StarfallThrone", name).Type;
    public static string Text(string key, params object[] args) => Language.GetTextValue("Mods.StarfallThrone.Fable." + key, args);
    public static string Name(int i) => Language.GetTextValue("Mods.StarfallThrone.NPCs.FableBoss" + i + ".DisplayName");
    public static float Progression(int i) => -1f + i * .04f;
    public static bool StarterWeapon(Item item) => item.type is ItemID.CopperShortsword or ItemID.CopperPickaxe or ItemID.CopperAxe or ItemID.TinShortsword or ItemID.TinPickaxe or ItemID.TinAxe
        || item.ModItem is Equipment.PracticeSword or Equipment.FableWeaponBase or global::StarfallThrone.Content.Oaths.Seeds.SeedWeaponBase
        or global::StarfallThrone.Content.Fable.Trials.FourTrialWeaponBase;
}
