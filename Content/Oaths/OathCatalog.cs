using Microsoft.Xna.Framework;
using Terraria.Localization;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Oaths;
public static class OathCatalog
{
    public const string Root="StarfallThrone/Content/Assets/Oaths/";
    public static readonly int[] Life={240000000,330000000,460000000},Defense={230,260,300},Contact={2800,3200,3600};
    public static readonly Color[] Colors={new(119,240,193),new(255,156,73),new(222,214,255)};
    public static bool Valid(int i)=>i>=0&&i<3;
    public static int Boss(int i)=>ModContent.Find<ModNPC>("StarfallThrone","SupremeBoss"+i).Type;
    public static int Seed(int i)=>ModContent.Find<ModNPC>("StarfallThrone","SeedBoss"+i).Type;
    public static int Item(string name)=>ModContent.Find<ModItem>("StarfallThrone",name).Type;
    public static int Material(int i)=>Item("SupremeMaterial"+i);
    public static int Bar(int i)=>Item("OathBar"+i);
    public static int Station(int i)=>ModContent.Find<ModTile>("StarfallThrone","OathStationTile"+i).Type;
    public static int BaseStation=>ModContent.Find<ModTile>("StarfallThrone","TriuneAltarTile").Type;
    public static int Weapon(int i)=>Item("SupremeWeapon"+i);
    public static string Text(string key,params object[] args)=>Language.GetTextValue("Mods.StarfallThrone.Oaths."+key,args);
}
