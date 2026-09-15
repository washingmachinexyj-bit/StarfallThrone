using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.Systems;
using StarfallThrone.Content.Voyage;

namespace StarfallThrone.Content.Mining;

/// <summary>Stable saved indices. Never sort by the internal order of miniboss IDs.</summary>
public static class MiningCatalog
{
    public const int OreCount = 13, StationCount = 12;
    public const string Root = "StarfallThrone/Content/Assets/Mining/";
    public static readonly string[] Keys = { "Moss", "Coppermarrow", "Frostrune", "Dawn", "Imperial", "Doomfire", "Starmagnet", "Silentmoon", "Ringwreck", "Prismphase", "Everlife", "Coordinate", "Genesis" };
    public static readonly string[] OreNames = { "苔纹矿", "铜髓矿", "霜纹晶矿", "晨辉矿", "帝凝晶矿", "劫火矿", "吞星磁矿", "寂月矿", "环骸矿", "璨相晶矿", "恒生矿", "坐标晶矿", "创星源矿" };
    public static readonly string[] BarNames = { "苔纹锭", "铜髓锭", "霜纹晶锭", "晨辉锭", "帝凝晶锭", "劫火锭", "吞星磁锭", "寂月锭", "环骸锭", "璨相晶锭", "恒生锭", "坐标晶锭", "创星源核" };
    public static readonly string[] ComponentNames = { "苔纹纤维束", "铜髓铆件", "霜纹透镜", "晨辉聚光片", "帝凝缓冲组件", "劫火动力组件", "磁轨组件", "寂月稳相组件", "舰体骨架组件", "折光组件", "恒生组织组件", "坐标组件", "创生组件" };
    public static readonly string[] StationNames = { "露壳手作台", "根火工坊", "霜纹工坊", "星落熔铸台", "永劫聚变炉", "星界铸造台", "航行工作台", "环带重工台", "晶环折光台", "恒生反应器", "群星织构台", "天穹创生中枢" };
    public static readonly string[] ToolNames = { "露壳手镐", "铜脉凿镐", "霜角探矿镐", "初辉勘探镐", "帝凝破岩镐", "永劫熔穿钻", "吞星磁轨镐", "寂月开拓镐", "环带工程钻", "璨星分光镐", "恒生根钻", "群星坐标切割器", "方舟创星钻", "方舟创星钻·工程改装" };
    public static readonly string[] Purposes = { "采集饰品、苔光路标与培晶附件", "早期装备骨架、精密工具与矿脉探针", "精密法器、探矿灯与初辉勘探镐", "初辉装备、采集徽记与永久照明", "月后初段装备、护盾框架与永劫熔穿钻", "热能装备、聚变炉与吞星磁轨镐", "机械装备、磁力采集器与寂月开拓镐", "星际入门装备、勘探仪与环带工程钻", "舰体装甲、重工设施与璨星分光镐", "光学装备、光谱镜与恒生根钻", "生态与召唤装备、培晶舱与坐标切割器", "最终战前装备、导航仪与方舟创星钻", "方舟毕业装备、创生设施与工程改装" };
    public static readonly string[] BossNames = { "苔团滚滚", "铜脉甲虫", "霜角猎兽", "初辉王胚", "史莱姆皇帝", "永劫肉墙", "钢铁吞星者", "月神终焉", "星骸环带吞噬者", "晶环女皇·璨星", "星际植群·世界树之种", "引航圣者·群星坐标", "星海主宰·天穹方舟" };
    public static readonly int[] PickRequirements = { 35, 40, 45, 50, 230, 260, 290, 320, 340, 380, 430, 480, 550 };
    public static readonly int[] ToolPower = { 35, 40, 45, 50, 240, 270, 300, 330, 350, 390, 440, 490, 560, 600 };
    public static readonly Color[] Colors = { new(116,183,93),new(197,130,76),new(123,196,235),new(244,217,124),new(176,112,233),new(221,76,56),new(95,146,217),new(192,206,238),new(219,155,81),new(190,137,238),new(107,216,159),new(127,139,246),new(241,217,239) };
    private static readonly int[] Smelters = { 0,1,2,2,3,4,5,5,7,8,9,10,11 };
    private static readonly int[] ToolStations = { 0,1,1,2,3,3,4,5,6,7,8,9,10,11 };
    private static readonly int[] VoyageBosses = { 2,7,11,15,16 };
    public static int ItemType(string name) => ModContent.Find<ModItem>("StarfallThrone", name).Type;
    public static int OreType(int i) => ItemType("MiningOre" + i);
    public static int BarType(int i) => ItemType("MiningBar" + i);
    public static int ComponentType(int i) => ItemType("MiningComponent" + i);
    public static int OreTileType(int i) => ModContent.Find<ModTile>("StarfallThrone", "MiningOreTile" + i).Type;
    public static int StationTileType(int i) => i == 6 ? VoyageCatalog.Workbench : ModContent.Find<ModTile>("StarfallThrone", "MiningStationTile" + i).Type;
    public static int StationItemType(int i) => ItemType(i == 6 ? "VoyageWorkbench" : "MiningStationItem" + i);
    public static int ToolType(int i) => ItemType(i == 8 ? "VoyageUtility2" : "MiningTool" + i);
    public static int SmeltStation(int i) => Smelters[i];
    public static int ToolStation(int i) => ToolStations[i];
    public static int OreFromTile(int type) => type >= TileID.Count && TileLoader.GetTile(type) is MiningOreTile ore ? ore.Index : -1;
    public static bool Unlocked(int i) => i switch
    {
        0 => MiniBossWorld.Downed[0],
        1 => PrimordialWorld.IsDowned(1), 2 => PrimordialWorld.IsDowned(3), 3 => PrimordialWorld.IsDowned(9),
        4 => StarfallWorld.IsDowned(0), 5 => StarfallWorld.IsDowned(6), 6 => StarfallWorld.IsDowned(10), 7 => StarfallWorld.IsDowned(16),
        >= 8 and <= 12 => VoyageWorld.IsDowned(VoyageBosses[i - 8]),
        _ => false
    };
    public static bool StationUnlocked(int i) => i switch
    {
        0 => MiniBossWorld.Downed[6], 1 => PrimordialWorld.IsDowned(0), 2 => Unlocked(2),
        3 => NPC.downedMoonlord, 4 => Unlocked(5), 5 => Unlocked(6), 6 => Unlocked(7),
        >= 7 and <= 11 => Unlocked(i + 1), _ => false
    };
    public static int BossMaterial(int i) => i switch
    {
        0 => MiniBossData.MaterialType(0), 1 => ItemType("CopperveinCore"), 2 => ItemType("FrosthornCore"), 3 => ItemType("DawnshardCore"),
        4 => ItemType("ImperialGel"), 5 => ItemType("EternalFlesh"), 6 => ItemType("StarEaterMagnet"), 7 => ItemType("MoonGodCore"),
        >= 8 and <= 12 => VoyageCatalog.Material(VoyageBosses[i - 8]), _ => ItemID.StoneBlock
    };
    public static int BossCore(int i) => i >= 8 && i < OreCount ? VoyageCatalog.Core(VoyageBosses[i - 8]) : BossMaterial(i);
}
