using Microsoft.Xna.Framework;

namespace StarfallThrone.Content.Systems;

public static class PrimordialBossData
{
    public const int Count = 10;

    public static readonly string[] Keys =
    {
        "ResinWhelp", "CopperveinScarab", "BellscorpionMatriarch", "FrosthornStalker", "LanternMiretoad",
        "WaxenArbiter", "RotrootWeaver", "GravebellKeybearer", "MeteorMaw", "DawnshardScion"
    };

    public static readonly string[] DisplayNames =
    {
        "树脂幼兽", "铜脉甲虫", "沙鸣蝎后", "霜角猎兽", "沼灯吞蛙",
        "蜂蜡巡裁者", "腐根织母", "墓钟执钥者", "陨铁噬星兽", "初辉王胚"
    };

    public static readonly int[] Life = { 900, 1000, 1110, 1220, 1340, 1460, 1580, 1690, 1790, 1880 };
    public static readonly int[] Defense = { 3, 5, 7, 8, 10, 11, 13, 15, 17, 18 };
    public static readonly int[] Damage = { 10, 14, 17, 20, 22, 25, 27, 30, 33, 36 };
    public static readonly int[] AttackDamage = { 12, 16, 20, 22, 25, 28, 31, 34, 37, 40 };
    public static readonly Color[] Colors =
    {
        new(238, 166, 62), new(200, 113, 46), new(236, 190, 64), new(152, 222, 255), new(112, 232, 156),
        new(255, 201, 70), new(178, 72, 214), new(112, 132, 190), new(234, 92, 47), new(196, 228, 255)
    };

    public static string Key(int index) => Keys[index];
    public static string DisplayName(int index) => DisplayNames[index];
    public static Color Color(int index) => Colors[index];
    public static float Progression(int index) => 0.12f + index * 0.075f;
}
