using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Systems;

public static class CombatProfiles
{
    public const int Count = 27;
    public static bool Early(int id) => id < 10;
    public static bool Grounded(int id) => id is 0 or 1 or 2 or 3 or 4 or 5 or 6 or 10 or 22;
    public static Color Color(int id) => Early(id) ? PrimordialBossData.Color(id) : BossData.Color(id - 10);
    public static string Key(int id) => Early(id) ? PrimordialBossData.Key(id) : BossData.Key(id - 10);
    public static int NPCType(int id) => ModContent.Find<ModNPC>("StarfallThrone", Key(id) + "NPC").Type;
    public static int Windup(int id) => Early(id) ? 60 - id / 3 * 4 : 60 - (id - 10) / 5 * 4;
    public static int ActiveTime(int id, int move) => move == 2 ? 130 : id is 11 or 16 or 22 or 24 or 26 ? 120 : 96;
    public static int Recovery(int id) => Early(id) ? 66 - id * 2 : 50;
    public static int Phase(NPC npc, int id) => npc.life <= npc.lifeMax * (Early(id) ? .24f : .34f) ? 2 : npc.life <= npc.lifeMax * (Early(id) ? .55f : .68f) ? 1 : 0;
    public static float Speed(int id, int phase) => Early(id) ? 4.2f + id * .22f + phase * .35f : 10f + (id - 10) * .2f + phase;
}
