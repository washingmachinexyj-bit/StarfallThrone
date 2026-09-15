using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
namespace StarfallThrone.Content.Pantheon;

public static class PantheonCatalog
{
    public const int Count=17;
    public const string Root="StarfallThrone/Content/Assets/Pantheon/";
    public static readonly string[] Keys={"Aelion","Ophir","Uroth","Velmora","Melith","Neroth","Acheron","Ilythia","EclipseDyad","Valther","Khaldran","Ephyra","Oranth","Thalor","Iriselle","Agnostos","Asterion"};
    public static readonly string[] Names={"太初星神·艾利昂","洞见星神·欧菲尔","吞界古神·乌洛斯","绯梦神祇·维尔莫拉","丰穰星后·梅莉丝","渡魂冥神·奈洛斯","两界门神·阿刻隆","晶律女神·伊莉希娅","日蚀双神·索利斯与诺克丝","四臂律神·瓦尔瑟","铸世龙神·卡尔德兰","万生母神·艾菲菈","擎天古神·欧兰瑟","风海暴神·萨洛尔","七曜星后·伊莉赛尔","无名司仪·阿格诺斯","众神之源·阿斯特里昂"};
    public static readonly int[] Life={12000000,13800000,15900000,18400000,21300000,24700000,28700000,33400000,39000000,45600000,53500000,63000000,74500000,88000000,104000000,123000000,145000000};
    public static readonly int[] Damage={800,840,890,940,1000,1060,1130,1200,1280,1370,1470,1580,1700,1840,2000,2190,2400};
    public static readonly Color[] Colors={new(112,241,212),new(238,215,152),new(160,131,232),new(239,144,198),new(240,189,88),new(139,227,236),new(196,191,222),new(150,212,255),new(255,185,111),new(208,189,153),new(242,151,92),new(155,227,167),new(159,177,204),new(103,210,237),new(245,172,232),new(212,205,240),new(236,226,193)};
    public static int Item(string name)=>ModContent.Find<ModItem>("StarfallThrone",name).Type;
    public static int BossType(int i)=>ModContent.Find<ModNPC>("StarfallThrone",Keys[i]+"NPC").Type;
    public static int Material(int i)=>Item("PantheonMaterial"+i);
    public static int Core(int i)=>Item("PantheonCore"+i);
    public static int Summon(int i)=>Item("PantheonSummon"+i);
    public static int Bag(int i)=>Item("PantheonBag"+i);
    public static int Relic(int i)=>Item("PantheonRelic"+i);
    public static int Trophy(int i)=>Item("PantheonTrophy"+i);
    public static int Mask(int i)=>Item("PantheonMask"+i);
    public static int Pet(int i)=>Item("PantheonPetItem"+i);
    public static int Weapon(int i)=>Item("PantheonWeapon"+i);
    public static int Armor(int i)=>Item("PantheonArmor"+i);
    public static int Expert(int i)=>Item("PantheonExpert"+i);
    public static int Essence(int i)=>Item("PantheonEssence"+i);
    public static int Station(int i)=>Item("PantheonStation"+i);
    public static int StationTile(int i)=>ModContent.Find<ModTile>("StarfallThrone","PantheonStationTile"+i).Type;
    public static int Tier(int boss)=>boss<2?0:boss<5?1:boss<8?2:boss<11?3:boss<14?4:5;
    public static int CraftStation(int tier)=>tier==0?global::StarfallThrone.Content.Mining.MiningCatalog.StationTileType(11):StationTile(tier-1);
    public static float Progression(int i)=>134f+i;
    public static bool AnyEncounter()=>global::StarfallThrone.Content.Voyage.VoyageCatalog.AnyEncounter();
}
