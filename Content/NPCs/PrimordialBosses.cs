using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.BossBars;
using StarfallThrone.Content.Items;
using StarfallThrone.Content.Projectiles;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.NPCs;

public abstract class PrimordialBossNPC : ReforgedBossNPC
{
    public abstract int BossIndex { get; }
    public override int EncounterId => BossIndex;

    public override string Texture => $"StarfallThrone/Content/Assets/Bosses/Primordial/{PrimordialBossData.Key(BossIndex)}";

    public override string BossHeadTexture => $"StarfallThrone/Content/Assets/BossHeads/Primordial/{PrimordialBossData.Key(BossIndex)}_Head_Boss";


    public override void SetStaticDefaults() => Main.npcFrameCount[Type] = 1;

    public override void SetDefaults()
    {
        NPC.width = 92 + BossIndex * 5;
        NPC.height = 86 + BossIndex * 4;
        NPC.damage = PrimordialBossData.Damage[BossIndex];
        NPC.defense = PrimordialBossData.Defense[BossIndex];
        NPC.lifeMax = PrimordialBossData.Life[BossIndex];
        NPC.knockBackResist = 0f;
        NPC.value = Item.buyPrice(gold: 2 + BossIndex * 2);
        NPC.boss = true;
        NPC.npcSlots = 10f;
        NPC.aiStyle = -1;
        NPC.noGravity = true;
        NPC.noTileCollide = true;
        NPC.HitSound = SoundID.NPCHit4;
        NPC.DeathSound = SoundID.NPCDeath14;
        NPC.BossBar = ModContent.GetInstance<StarfallBossBar>();
        Music = MusicID.Boss1;
        ConfigureBody();
    }

    public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
    {
        NPC.lifeMax = (int)(NPC.lifeMax * balance);
        NPC.life = NPC.lifeMax;
        NPC.damage = (int)(NPC.damage * bossAdjustment);
    }

    public override void ModifyNPCLoot(NPCLoot npcLoot)
    {
        npcLoot.Add(ItemDropRule.BossBag(PrimordialDropCatalog.BagType(BossIndex)));
        npcLoot.Add(new DropBasedOnExpertMode(
            ItemDropRule.Common(PrimordialDropCatalog.CoreType(BossIndex), 1, 1, 2),
            ItemDropRule.DropNothing()));
        npcLoot.Add(new DropBasedOnExpertMode(
            ItemDropRule.Common(PrimordialDropCatalog.WeaponOneType(BossIndex), 4),
            ItemDropRule.DropNothing()));
        npcLoot.Add(new DropBasedOnExpertMode(
            ItemDropRule.Common(PrimordialDropCatalog.WeaponTwoType(BossIndex), 4),
            ItemDropRule.DropNothing()));
        if (BossIndex == 9)
            npcLoot.Add(new DropBasedOnExpertMode(
                ItemDropRule.Common(PrimordialDropCatalog.WeaponThreeType(BossIndex), 4),
                ItemDropRule.DropNothing()));
        npcLoot.Add(ItemDropRule.MasterModeCommonDrop(PrimordialDropCatalog.RelicType(BossIndex)));
        npcLoot.Add(ItemDropRule.Common(PrimordialDropCatalog.TrophyType(BossIndex), 10));
    }

    public override void OnKill()
    {
        ClearAttacks();
        if (Main.netMode != NetmodeID.MultiplayerClient)
            PrimordialWorld.SetDowned(BossIndex);
    }

    public override void HitEffect(NPC.HitInfo hit)
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        int count = NPC.life <= 0 ? 35 : 4;
        for (int i = 0; i < count; i++)
            Dust.NewDustPerfect(NPC.Center, DustID.GoldFlame, Main.rand.NextVector2Circular(5f, 5f), 0, PrimordialBossData.Color(BossIndex), 1f);
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        Texture2D texture = TextureAssets.Npc[Type].Value;
        Vector2 position = NPC.Center - screenPos;
        Vector2 origin = texture.Size() * 0.5f;
        float scale = Math.Max(0.55f, NPC.width / (float)texture.Width) * 0.9f;
        int phase = Math.Clamp((int)NPC.ai[3], 0, 2);
        float pulse = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * (4.5f + phase) + BossIndex) * 0.018f;
        Color aura = PrimordialBossData.Color(BossIndex);

        for (int i = 1; i >= 1; i--)
        {
            Vector2 offset = (Main.GlobalTimeWrappedHourly * (i % 2 == 0 ? 1f : -1f)).ToRotationVector2() * i * 2.2f;
            spriteBatch.Draw(texture, position + offset, null, Color.FromNonPremultiplied(aura.R, aura.G, aura.B, 22 + phase * 10),
                NPC.rotation, origin, scale * (1f + i * 0.06f) * pulse, SpriteEffects.None, 0f);
        }
        spriteBatch.Draw(texture, position, null, drawColor, NPC.rotation, origin, scale * pulse, SpriteEffects.None, 0f);
        DrawCombatTelegraph(spriteBatch, screenPos);
        return false;
    }

    public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position)
    {
        scale = 1.15f;
        position = NPC.Top - Vector2.UnitY * 16f;
        return true;
    }
}

[AutoloadBossHead]
public sealed class ResinWhelpNPC : PrimordialBossNPC { public override int BossIndex => 0; }
[AutoloadBossHead]
public sealed class CopperveinScarabNPC : PrimordialBossNPC { public override int BossIndex => 1; }
[AutoloadBossHead]
public sealed class BellscorpionMatriarchNPC : PrimordialBossNPC { public override int BossIndex => 2; }
[AutoloadBossHead]
public sealed class FrosthornStalkerNPC : PrimordialBossNPC { public override int BossIndex => 3; }
[AutoloadBossHead]
public sealed class LanternMiretoadNPC : PrimordialBossNPC { public override int BossIndex => 4; }
[AutoloadBossHead]
public sealed class WaxenArbiterNPC : PrimordialBossNPC { public override int BossIndex => 5; }
[AutoloadBossHead]
public sealed class RotrootWeaverNPC : PrimordialBossNPC { public override int BossIndex => 6; }
[AutoloadBossHead]
public sealed class GravebellKeybearerNPC : PrimordialBossNPC { public override int BossIndex => 7; }
[AutoloadBossHead]
public sealed class MeteorMawNPC : PrimordialBossNPC { public override int BossIndex => 8; }
[AutoloadBossHead]
public sealed class DawnshardScionNPC : PrimordialBossNPC { public override int BossIndex => 9; }
