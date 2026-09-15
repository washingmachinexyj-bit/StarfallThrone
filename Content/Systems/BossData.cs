using Microsoft.Xna.Framework;

namespace StarfallThrone.Content.Systems;

public static class BossData
{
    public static readonly string[] Keys =
    {
        "SlimeEmperor", "TerminalEye", "InfiniteDevourer", "CrimsonMind", "BeeMatriarch",
        "SkeletalJudge", "EternalWall", "DynastyQueen", "TwinCalamity", "PrimeVerdict",
        "StarEater", "WastelandFlower", "TempleCore", "AbyssalDuke", "CoronaEmpress",
        "AstralPontiff", "EndMoon"
    };

    public static readonly string[] DisplayNames =
    {
        "史莱姆皇帝", "终焉魔眼", "无尽吞噬者", "猩红心魔", "蜂后圣母",
        "骸骨审判官", "永劫肉墙", "史莱姆女皇·王朝", "双生天灾", "机械终审",
        "钢铁吞星者", "荒原花神", "石巨人·神殿核心", "深渊公爵", "光耀女皇·日冕",
        "星界教皇", "月神终焉"
    };

    public static readonly int[] LifeMultiplierPercent = { 105, 110, 118, 128, 145, 165, 190, 220, 260, 310, 370, 440, 520, 610, 720, 850, 1200 };
    public static readonly int[] DamageMultiplierPercent = { 105, 110, 115, 120, 130, 145, 160, 180, 210, 240, 280, 330, 390, 460, 550, 670, 750 };
    public static readonly Color[] Colors =
    {
        new(65, 220, 255), new(255, 185, 75), new(125, 245, 105), new(240, 95, 225), new(255, 205, 65),
        new(220, 230, 255), new(255, 90, 70), new(110, 230, 255), new(255, 80, 90), new(170, 190, 255),
        new(90, 235, 130), new(190, 100, 255), new(235, 180, 90), new(75, 210, 255), new(255, 180, 105),
        new(180, 105, 255), new(235, 245, 255)
    };

    public static float Progression(int index) => 100f + index;
    public static string Key(int index) => Keys[index];
    public static string DisplayName(int index) => DisplayNames[index];
    public static Color Color(int index) => Colors[index];
}
