using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Voyage;

public static class VoyageCatalog
{
    public const int Count = 17;
    public const string Root = "StarfallThrone/Content/Assets/Voyage/";
    public static readonly string[] Keys = { "StarportSovereign", "Farwatcher", "BeltDevourer", "ConsensusBrain", "HiveCarrier", "OrbitalAnchor", "WarpBulwark", "CrystalOrbitQueen", "BinaryHunters", "FourArmTribunal", "SunpiercerFleet", "Worldseed", "PrecursorGuardian", "AetherLeviathan", "SolarSailEmpress", "StarNavigator", "CelestialArk" };
    public static readonly string[] Names = { "星港凝胶君主", "巡天之眼·远望者", "星骸环带吞噬者", "万脑中枢·共识", "蜂巢母舰·金翅王庭", "轨道骸将·执锚者", "跃迁壁垒·方舟封锁线", "晶环女皇·璨星", "双星猎杀者·赤移与蓝移", "轨道执刑机·四臂天衡", "列星歼灭舰·贯日者", "星际植群·世界树之种", "先驱守卫·巨构之心", "以太巡洋兽·利维坦", "光帆圣后·万色航迹", "引航圣者·群星坐标", "星海主宰·天穹方舟" };
    public static readonly int[] Life = { 1900000, 2100000, 2350000, 2600000, 2900000, 3200000, 3550000, 3950000, 4400000, 4900000, 5450000, 6050000, 6700000, 7400000, 8150000, 9000000, 10000000 };
    public static readonly Color[] Colors = { new(65,215,190),new(160,220,250),new(155,110,235),new(225,100,185),new(245,190,50),new(130,170,200),new(240,115,90),new(210,150,255),new(115,155,255),new(220,220,235),new(100,205,190),new(140,205,95),new(225,190,105),new(75,195,255),new(255,180,215),new(140,155,245),new(245,220,155) };
    public static int Item(string key) => ModContent.Find<ModItem>("StarfallThrone", key).Type;
    public static int BossType(int i) => ModContent.Find<ModNPC>("StarfallThrone", Keys[i] + "NPC").Type;
    public static int Material(int i) => Item("VoyageMaterial" + i);
    public static int Core(int i) => Item("VoyageCore" + i);
    public static int Weapon(int i) => Item("VoyageWeapon" + i);
    public static int Bag(int i) => Item("VoyageBag" + i);
    public static int Summon(int i) => Item("VoyageSummon" + i);
    public static int Expert(int i) => Item("VoyageExpert" + i);
    public static int Relic(int i) => Item("VoyageRelic" + i);
    public static int Trophy(int i) => Item("VoyageTrophy" + i);
    public static int Mask(int i) => Item("VoyageMask" + i);
    public static int Alloy => Item("VoyageAlloy");
    public static int Plate(int i) => Item("VoyagePlate" + i);
    public static int Workbench => ModContent.Find<ModTile>("StarfallThrone", "VoyageWorkbenchTile").Type;
    public static float Progression(int i) => 117 + i;
    public static bool AnyEncounter()
    {
        foreach (NPC n in Main.ActiveNPCs)
            if (n.boss || n.ModNPC is global::StarfallThrone.Content.NPCs.MiniBossNPC) return true;
        return Main.invasionType > 0 || Main.pumpkinMoon || Main.snowMoon;
    }
}
