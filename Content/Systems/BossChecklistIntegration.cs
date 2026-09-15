using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using StarfallThrone.Content.Items;
using StarfallThrone.Content.NPCs;

namespace StarfallThrone.Content.Systems;

public sealed class BossChecklistIntegration : ModSystem
{
    public override void PostSetupContent()
    {
        if (!ModLoader.TryGetMod("BossChecklist", out Mod bossChecklist))
            return;

        for (int i = 0; i < MiniBossData.Count; i++)
        {
            int miniIndex = i;
            object result = bossChecklist.Call("LogMiniBoss", Mod, MiniBossData.Keys[i], MiniBossData.Progression(i),
                (Func<bool>)(() => MiniBossWorld.Downed[miniIndex]), MiniBossData.NPCType(i),
                new Dictionary<string, object>
                {
                    ["spawnItems"] = MiniBossData.SummonType(i),
                    ["collectibles"] = new List<int> { MiniBossData.TrophyType(i) },
                    ["spawnInfo"] = Language.GetText($"Mods.StarfallThrone.MiniChecklist.{MiniBossData.Keys[i]}.SpawnInfo"),
                    ["despawnMessage"] = Language.GetText($"Mods.StarfallThrone.MiniChecklist.{MiniBossData.Keys[i]}.DespawnMessage")
                });
            if (!Equals(result, "Success")) Mod.Logger.Warn($"Mini Boss Checklist registration failed: {MiniBossData.Keys[i]}");
        }

        for (int i = 0; i < PrimordialBossData.Count; i++)
        {
            int bossIndex = i;
            try
            {
                bossChecklist.Call(
                    "LogBoss",
                    Mod,
                    PrimordialBossData.Key(bossIndex),
                    PrimordialBossData.Progression(bossIndex),
                    (Func<bool>)(() => PrimordialWorld.IsDowned(bossIndex)),
                    PrimordialBossChecklistNPCType(bossIndex),
                    new Dictionary<string, object>
                    {
                        ["spawnItems"] = PrimordialSummonType(bossIndex),
                        ["collectibles"] = new List<int> { PrimordialDropCatalog.RelicType(bossIndex), PrimordialDropCatalog.TrophyType(bossIndex) },
                        ["spawnInfo"] = Language.GetText($"Mods.StarfallThrone.BossChecklist.{PrimordialBossData.Key(bossIndex)}.SpawnInfo"),
                        ["despawnMessage"] = Language.GetText($"Mods.StarfallThrone.BossChecklist.{PrimordialBossData.Key(bossIndex)}.DespawnMessage")
                    });
            }
            catch (Exception exception)
            {
                Mod.Logger.Warn($"Primordial Boss Checklist registration failed for {PrimordialBossData.Key(bossIndex)}: {exception.Message}");
            }
        }

        for (int i = 0; i < StarfallWorld.BossCount; i++)
        {
            int bossIndex = i;
            try
            {
                bossChecklist.Call(
                    "LogBoss",
                    Mod,
                    BossData.Key(bossIndex),
                    BossData.Progression(bossIndex),
                    (Func<bool>)(() => StarfallWorld.IsDowned(bossIndex)),
                    BossNPCType(bossIndex),
                    new Dictionary<string, object>
                    {
                        ["spawnItems"] = SummonType(bossIndex),
                        ["collectibles"] = new List<int> { BossDropCatalog.RelicType(bossIndex), BossDropCatalog.TrophyType(bossIndex) },
                        ["spawnInfo"] = Language.GetText($"Mods.StarfallThrone.BossChecklist.{BossData.Key(bossIndex)}.SpawnInfo"),
                        ["despawnMessage"] = Language.GetText($"Mods.StarfallThrone.BossChecklist.{BossData.Key(bossIndex)}.DespawnMessage")
                    });
            }
            catch (Exception exception)
            {
                Mod.Logger.Warn($"Boss Checklist registration failed for {BossData.Key(bossIndex)}: {exception.Message}");
            }
        }
    }

    private static int PrimordialBossChecklistNPCType(int index) => PrimordialDropCatalog.NPCType(index);

    private static int PrimordialSummonType(int index) => index switch
    {
        0 => ModContent.ItemType<ResinAcornSummon>(), 1 => ModContent.ItemType<CopperCarapaceSummon>(), 2 => ModContent.ItemType<SandBellSummon>(),
        3 => ModContent.ItemType<FrosthornFluteSummon>(), 4 => ModContent.ItemType<MireLanternSummon>(), 5 => ModContent.ItemType<WaxSealSummon>(),
        6 => ModContent.ItemType<EvilRootTotemSummon>(), 7 => ModContent.ItemType<GravebellKeySummon>(), 8 => ModContent.ItemType<MeteorGizzardSummon>(),
        _ => ModContent.ItemType<DawnshardSigilSummon>()
    };

    private static int BossNPCType(int index) => index switch
    {
        0 => ModContent.NPCType<SlimeEmperorNPC>(), 1 => ModContent.NPCType<TerminalEyeNPC>(), 2 => ModContent.NPCType<InfiniteDevourerNPC>(),
        3 => ModContent.NPCType<CrimsonMindNPC>(), 4 => ModContent.NPCType<BeeMatriarchNPC>(), 5 => ModContent.NPCType<SkeletalJudgeNPC>(),
        6 => ModContent.NPCType<EternalWallNPC>(), 7 => ModContent.NPCType<DynastyQueenNPC>(), 8 => ModContent.NPCType<TwinCalamityNPC>(),
        9 => ModContent.NPCType<PrimeVerdictNPC>(), 10 => ModContent.NPCType<StarEaterNPC>(), 11 => ModContent.NPCType<WastelandFlowerNPC>(),
        12 => ModContent.NPCType<TempleCoreNPC>(), 13 => ModContent.NPCType<AbyssalDukeNPC>(), 14 => ModContent.NPCType<CoronaEmpressNPC>(),
        15 => ModContent.NPCType<AstralPontiffNPC>(), _ => ModContent.NPCType<EndMoonNPC>()
    };

    private static int SummonType(int index) => index switch
    {
        0 => ModContent.ItemType<EmperorCrown>(), 1 => ModContent.ItemType<TerminalEye>(), 2 => ModContent.ItemType<InfiniteBait>(),
        3 => ModContent.ItemType<CrimsonHeartSummon>(), 4 => ModContent.ItemType<RoyalBeeSigil>(), 5 => ModContent.ItemType<JudgmentBoneflute>(),
        6 => ModContent.ItemType<EternalVoodooDoll>(), 7 => ModContent.ItemType<DynastyPrismSummon>(), 8 => ModContent.ItemType<TwinOmen>(),
        9 => ModContent.ItemType<PrimeVerdictSummon>(), 10 => ModContent.ItemType<StarEaterWorm>(), 11 => ModContent.ItemType<WastelandBudSummon>(),
        12 => ModContent.ItemType<TempleStarCoreSummon>(), 13 => ModContent.ItemType<AbyssLure>(), 14 => ModContent.ItemType<CoronaButterfly>(),
        15 => ModContent.ItemType<AstralScripture>(), _ => ModContent.ItemType<EndMoonRite>()
    };
}
