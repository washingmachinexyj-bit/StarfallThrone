using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.GameContent.ItemDropRules;
using StarfallThrone.Content.BossBars;
using StarfallThrone.Content.Items;
using StarfallThrone.Content.Projectiles;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.NPCs;

public abstract class AscendantBossNPC : ReforgedBossNPC
{
    public abstract int BossIndex { get; }
    public override int EncounterId => 10 + BossIndex;
    protected virtual int BaseLife => 145000;
    protected virtual int BaseDamage => 220;

    public override string Texture => $"StarfallThrone/Content/Assets/Bosses/{BossData.Key(BossIndex)}";

    public override string BossHeadTexture => $"StarfallThrone/Content/Assets/BossHeads/{BossData.Key(BossIndex)}_Head_Boss";


    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 1;
    }

    public override void SetDefaults()
    {
        NPC.width = 180 + BossIndex * 4;
        NPC.height = 150 + BossIndex * 4;
        NPC.scale = 1f;
        NPC.damage = (int)(BaseDamage * BossData.DamageMultiplierPercent[BossIndex] / 100f);
        NPC.defense = 90 + BossIndex * 8;
        NPC.lifeMax = (int)(BaseLife * BossData.LifeMultiplierPercent[BossIndex] / 100f);
        NPC.knockBackResist = 0f;
        NPC.value = Item.buyPrice(platinum: 2 + BossIndex);
        NPC.boss = true;
        NPC.npcSlots = 20f;
        NPC.aiStyle = -1;
        NPC.noGravity = true;
        NPC.noTileCollide = true;
        NPC.HitSound = SoundID.NPCHit4;
        NPC.DeathSound = SoundID.NPCDeath14;
        NPC.BossBar = ModContent.GetInstance<StarfallBossBar>();
        Music = MusicID.Boss3;
        ConfigureBody();
    }

    public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
    {
        NPC.lifeMax = (int)(NPC.lifeMax * balance);
        NPC.life = NPC.lifeMax;
        NPC.damage = (int)(NPC.damage * bossAdjustment);
    }

    public override void OnKill()
    {
        ClearAttacks();
        if (Main.netMode != NetmodeID.MultiplayerClient)
            StarfallWorld.SetDowned(BossIndex);
    }

    public override void HitEffect(NPC.HitInfo hit)
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        int dustCount = NPC.life <= 0 ? 45 : 5;
        for (int i = 0; i < dustCount; i++)
            Dust.NewDustPerfect(NPC.Center, DustID.ShimmerSpark, Main.rand.NextVector2Circular(7f, 7f), 0, BossData.Color(BossIndex), 1.2f);
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        Texture2D texture = TextureAssets.Npc[Type].Value;
        Vector2 drawPosition = NPC.Center - screenPos;
        Vector2 origin = texture.Size() * 0.5f;
        float drawScale = Math.Max(0.45f, NPC.width / (float)texture.Width) * 0.9f;
        int phase = Math.Clamp((int)NPC.ai[3], 0, 2);
        float time = Main.GlobalTimeWrappedHourly * (5.5f + phase * 1.4f) + BossIndex;
        float pulse = 1f + (float)Math.Sin(time) * (0.014f + phase * 0.006f);
        Color auraColor = BossData.Color(BossIndex);
        Color brightAura = Color.Lerp(auraColor, Color.White, 0.18f + phase * 0.12f);

        // Restrained phase glow keeps the new detailed silhouette legible.
        for (int layer = 1; layer >= 1; layer--)
        {
            float layerPulse = pulse + layer * (0.045f + phase * 0.01f);
            float angle = time * 0.12f * layer * (BossIndex % 2 == 0 ? 1f : -1f);
            Vector2 offset = angle.ToRotationVector2() * (layer * (2.5f + phase * 1.5f));
            Color layerColor = Color.FromNonPremultiplied(brightAura.R, brightAura.G, brightAura.B, 12 + phase * 10);
            spriteBatch.Draw(texture, drawPosition + offset, null, layerColor, NPC.rotation + angle * 0.03f, origin,
                drawScale * (1.06f + layer * 0.055f) * layerPulse, SpriteEffects.None, 0f);
        }

        spriteBatch.Draw(texture, drawPosition, null, Color.FromNonPremultiplied(brightAura.R, brightAura.G, brightAura.B, 30 + phase * 12),
            NPC.rotation, origin, drawScale * 1.045f * pulse, SpriteEffects.None, 0f);
        Color coreColor = phase == 2 ? Color.Lerp(drawColor, Color.White, 0.24f) : drawColor;
        spriteBatch.Draw(texture, drawPosition, null, coreColor, NPC.rotation, origin, drawScale * pulse, SpriteEffects.None, 0f);
        DrawCombatTelegraph(spriteBatch, screenPos);
        return false;
    }

    public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position)
    {
        scale = 1.35f;
        position = NPC.Top - Vector2.UnitY * 24f;
        return true;
    }

    public override void ModifyNPCLoot(NPCLoot npcLoot)
    {
        // Classic mode keeps the normal drops on the boss. Expert and Master move them into the personal treasure bag.
        npcLoot.Add(ItemDropRule.BossBag(BossDropCatalog.BagType(BossIndex)));
        npcLoot.Add(new DropBasedOnExpertMode(
            ItemDropRule.Common(BossDropCatalog.CoreType(BossIndex), 1, 15 + BossIndex, 25 + BossIndex * 2),
            ItemDropRule.DropNothing()));
        npcLoot.Add(new DropBasedOnExpertMode(
            ItemDropRule.Common(ModContent.ItemType<StarfallResidue>(), 1, 20, 45),
            ItemDropRule.DropNothing()));
        npcLoot.Add(new DropBasedOnExpertMode(
            ItemDropRule.Common(BossDropCatalog.WeaponType(BossIndex), 1),
            ItemDropRule.DropNothing()));
        npcLoot.Add(ItemDropRule.MasterModeCommonDrop(BossDropCatalog.RelicType(BossIndex)));
        npcLoot.Add(ItemDropRule.Common(BossDropCatalog.TrophyType(BossIndex), 10));
    }
}

[AutoloadBossHead]
public sealed class SlimeEmperorNPC : AscendantBossNPC { public override int BossIndex => 0; }
[AutoloadBossHead]
public sealed class TerminalEyeNPC : AscendantBossNPC { public override int BossIndex => 1; }
[AutoloadBossHead]
public sealed class InfiniteDevourerNPC : AscendantBossNPC { public override int BossIndex => 2; }
[AutoloadBossHead]
public sealed class CrimsonMindNPC : AscendantBossNPC { public override int BossIndex => 3; }
[AutoloadBossHead]
public sealed class BeeMatriarchNPC : AscendantBossNPC { public override int BossIndex => 4; }
[AutoloadBossHead]
public sealed class SkeletalJudgeNPC : AscendantBossNPC { public override int BossIndex => 5; }
[AutoloadBossHead]
public sealed class EternalWallNPC : AscendantBossNPC { public override int BossIndex => 6; }
[AutoloadBossHead]
public sealed class DynastyQueenNPC : AscendantBossNPC { public override int BossIndex => 7; }
[AutoloadBossHead]
public sealed class TwinCalamityNPC : AscendantBossNPC { public override int BossIndex => 8; }
[AutoloadBossHead]
public sealed class PrimeVerdictNPC : AscendantBossNPC { public override int BossIndex => 9; }
[AutoloadBossHead]
public sealed class StarEaterNPC : AscendantBossNPC { public override int BossIndex => 10; }
[AutoloadBossHead]
public sealed class WastelandFlowerNPC : AscendantBossNPC { public override int BossIndex => 11; }
[AutoloadBossHead]
public sealed class TempleCoreNPC : AscendantBossNPC { public override int BossIndex => 12; }
[AutoloadBossHead]
public sealed class AbyssalDukeNPC : AscendantBossNPC { public override int BossIndex => 13; }
[AutoloadBossHead]
public sealed class CoronaEmpressNPC : AscendantBossNPC { public override int BossIndex => 14; }
[AutoloadBossHead]
public sealed class AstralPontiffNPC : AscendantBossNPC { public override int BossIndex => 15; }
[AutoloadBossHead]
public sealed class EndMoonNPC : AscendantBossNPC { public override int BossIndex => 16; }

public static class BossDropCatalog
{
    public static int CoreType(int index) => index switch
    {
        0 => ModContent.ItemType<ImperialGel>(), 1 => ModContent.ItemType<PhaseIris>(), 2 => ModContent.ItemType<VoidSegmentCrystal>(),
        3 => ModContent.ItemType<CrimsonNeuralCore>(), 4 => ModContent.ItemType<BeeGodHoney>(), 5 => ModContent.ItemType<JudgmentMarrow>(),
        6 => ModContent.ItemType<EternalFlesh>(), 7 => ModContent.ItemType<PrismCrown>(), 8 => ModContent.ItemType<TwinEyeCore>(),
        9 => ModContent.ItemType<PrimeGear>(), 10 => ModContent.ItemType<StarEaterMagnet>(), 11 => ModContent.ItemType<WastelandFlowerCore>(),
        12 => ModContent.ItemType<TempleStarCore>(), 13 => ModContent.ItemType<AbyssScaleHeart>(), 14 => ModContent.ItemType<CoronaPrism>(),
        15 => ModContent.ItemType<AstralDoctrine>(), _ => ModContent.ItemType<MoonGodCore>()
    };

    public static int WeaponType(int index) => index switch
    {
        0 => ModContent.ItemType<ImperialScepter>(), 1 => ModContent.ItemType<PhaseBreaker>(), 2 => ModContent.ItemType<DevourerLance>(),
        3 => ModContent.ItemType<MindCircuit>(), 4 => ModContent.ItemType<RoyalStinger>(), 5 => ModContent.ItemType<JudgmentCodex>(),
        6 => ModContent.ItemType<EternalTendril>(), 7 => ModContent.ItemType<DynastyPrism>(), 8 => ModContent.ItemType<TwinRayCannon>(),
        9 => ModContent.ItemType<PrimeSaw>(), 10 => ModContent.ItemType<StarChainDrill>(), 11 => ModContent.ItemType<FlowerThornCrown>(),
        12 => ModContent.ItemType<TempleRiftCannon>(), 13 => ModContent.ItemType<AbyssalHarpoon>(), 14 => ModContent.ItemType<CoronaPrismWeapon>(),
        15 => ModContent.ItemType<PontiffStarbook>(), _ => ModContent.ItemType<EndMoonWeapon>()
    };

    public static int RelicType(int index) => index switch
    {
        0 => ModContent.ItemType<SlimeEmperorRelic>(), 1 => ModContent.ItemType<TerminalEyeRelic>(), 2 => ModContent.ItemType<InfiniteDevourerRelic>(),
        3 => ModContent.ItemType<CrimsonMindRelic>(), 4 => ModContent.ItemType<BeeMatriarchRelic>(), 5 => ModContent.ItemType<SkeletalJudgeRelic>(),
        6 => ModContent.ItemType<EternalWallRelic>(), 7 => ModContent.ItemType<DynastyQueenRelic>(), 8 => ModContent.ItemType<TwinCalamityRelic>(),
        9 => ModContent.ItemType<PrimeVerdictRelic>(), 10 => ModContent.ItemType<StarEaterRelic>(), 11 => ModContent.ItemType<WastelandFlowerRelic>(),
        12 => ModContent.ItemType<TempleCoreRelic>(), 13 => ModContent.ItemType<AbyssalDukeRelic>(), 14 => ModContent.ItemType<CoronaEmpressRelic>(),
        15 => ModContent.ItemType<AstralPontiffRelic>(), _ => ModContent.ItemType<EndMoonRelic>()
    };

    public static int TrophyType(int index) => index switch
    {
        0 => ModContent.ItemType<SlimeEmperorTrophy>(), 1 => ModContent.ItemType<TerminalEyeTrophy>(), 2 => ModContent.ItemType<InfiniteDevourerTrophy>(),
        3 => ModContent.ItemType<CrimsonMindTrophy>(), 4 => ModContent.ItemType<BeeMatriarchTrophy>(), 5 => ModContent.ItemType<SkeletalJudgeTrophy>(),
        6 => ModContent.ItemType<EternalWallTrophy>(), 7 => ModContent.ItemType<DynastyQueenTrophy>(), 8 => ModContent.ItemType<TwinCalamityTrophy>(),
        9 => ModContent.ItemType<PrimeVerdictTrophy>(), 10 => ModContent.ItemType<StarEaterTrophy>(), 11 => ModContent.ItemType<WastelandFlowerTrophy>(),
        12 => ModContent.ItemType<TempleCoreTrophy>(), 13 => ModContent.ItemType<AbyssalDukeTrophy>(), 14 => ModContent.ItemType<CoronaEmpressTrophy>(),
        15 => ModContent.ItemType<AstralPontiffTrophy>(), _ => ModContent.ItemType<EndMoonTrophy>()
    };

    public static int BagType(int index) => index switch
    {
        0 => ModContent.ItemType<SlimeEmperorBag>(), 1 => ModContent.ItemType<TerminalEyeBag>(), 2 => ModContent.ItemType<InfiniteDevourerBag>(),
        3 => ModContent.ItemType<CrimsonMindBag>(), 4 => ModContent.ItemType<BeeMatriarchBag>(), 5 => ModContent.ItemType<SkeletalJudgeBag>(),
        6 => ModContent.ItemType<EternalWallBag>(), 7 => ModContent.ItemType<DynastyQueenBag>(), 8 => ModContent.ItemType<TwinCalamityBag>(),
        9 => ModContent.ItemType<PrimeVerdictBag>(), 10 => ModContent.ItemType<StarEaterBag>(), 11 => ModContent.ItemType<WastelandFlowerBag>(),
        12 => ModContent.ItemType<TempleCoreBag>(), 13 => ModContent.ItemType<AbyssalDukeBag>(), 14 => ModContent.ItemType<CoronaEmpressBag>(),
        15 => ModContent.ItemType<AstralPontiffBag>(), _ => ModContent.ItemType<EndMoonBag>()
    };

    public static int AccessoryType(int index) => index switch
    {
        0 => ModContent.ItemType<ImperialGelShield>(), 1 => ModContent.ItemType<PhaseEyeCore>(), 2 => ModContent.ItemType<DevourerMagnet>(),
        3 => ModContent.ItemType<IllusionHeart>(), 4 => ModContent.ItemType<HiveHeart>(), 5 => ModContent.ItemType<JudgeMask>(),
        6 => ModContent.ItemType<SoulHinge>(), 7 => ModContent.ItemType<PrismWing>(), 8 => ModContent.ItemType<PolarEngine>(),
        9 => ModContent.ItemType<FourArmProtocol>(), 10 => ModContent.ItemType<MagneticCoil>(), 11 => ModContent.ItemType<WorldRoot>(),
        12 => ModContent.ItemType<TitanPrism>(), 13 => ModContent.ItemType<TideEngine>(), 14 => ModContent.ItemType<CoronaCrown>(),
        15 => ModContent.ItemType<FourPillarEdict>(), _ => ModContent.ItemType<CosmicHeart>()
    };
}
