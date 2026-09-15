using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.BossBars;

namespace StarfallThrone.Content.Fable.Trials;

public abstract class FourTrialBossNPC : ModNPC
{
    public abstract int Index { get; }
    public override string Texture => FourTrialCatalog.BossTexture(Index);
    // These trials use a compact sprite for both the Checklist icon and the boss head.
    public override string BossHeadTexture => Texture;

    private int DifficultyDamage(int value) => (int)MathF.Ceiling(value * (Main.masterMode ? 1.4f : Main.expertMode ? 1.2f : 1f));

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 1;
        NPCID.Sets.MPAllowedEnemies[Type] = true;
        NPCID.Sets.BossBestiaryPriority.Add(Type);
    }

    public override void SetDefaults()
    {
        int size = 16 + Index * 3;
        NPC.width = NPC.height = size;
        NPC.aiStyle = -1;
        NPC.lifeMax = FourTrialCatalog.Life[Index];
        NPC.defense = FourTrialCatalog.Defense[Index];
        NPC.damage = FourTrialCatalog.Contact[Index];
        NPC.boss = true;
        NPC.BossBar = ModContent.GetInstance<StarfallBossBar>();
        NPC.noGravity = true;
        NPC.noTileCollide = true;
        NPC.knockBackResist = .7f;
        NPC.npcSlots = 1;
        NPC.value = 0;
        NPC.timeLeft = 36000;
        NPC.lavaImmune = true;
        NPC.netAlways = true;
        NPC.HitSound = SoundID.NPCHit1;
        NPC.DeathSound = SoundID.NPCDeath1;
        if (!Main.dedServ) Music = MusicID.Boss1;
    }

    public override void ApplyDifficultyAndPlayerScaling(int players, float balance, float bossAdjustment)
    {
        float multiplier = Main.masterMode ? 1.5f : Main.expertMode ? 1.25f : 1f;
        NPC.lifeMax = (int)MathF.Ceiling(FourTrialCatalog.Life[Index] * multiplier * (1f + .3f * Math.Max(0, players - 1)));
        NPC.damage = DifficultyDamage(FourTrialCatalog.Contact[Index]);
        NPC.defense = FourTrialCatalog.Defense[Index];
    }

    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry entry)
    {
        entry.Info.Add(BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface);
        entry.Info.Add(new FlavorTextBestiaryInfoElement("Mods.StarfallThrone.Fable.FourTrialHint" + Index));
    }

    public override void BossLoot(ref int potionType) => potionType = ItemID.LesserHealingPotion;

    public override bool CheckActive() => false;

    public override void AI()
    {
        NPC.TargetClosest(false);
        if (!NPC.HasValidTarget || Main.player[NPC.target].dead || Main.player[NPC.target].ghost)
        {
            NPC.damage = 0;
            NPC.timeLeft = 10;
            return;
        }

        Player player = Main.player[NPC.target];
        NPC.timeLeft = 36000;
        int timer = (int)NPC.ai[0]++;
        int t = timer % 180;
        NPC.damage = 0;

        float bob = MathF.Sin(timer * (.025f + Index * .004f)) * (8 + Index * 4);
        Vector2 destination = player.Center + new Vector2(MathF.Sin(timer * .018f) * (24 + Index * 13), -58 - Index * 7 + bob);
        switch (Index)
        {
            case 0:
                // Copper is a ceremonial one-hit nod: it only hovers and never hurts the player.
                destination = player.Center + new Vector2(MathF.Sin(timer * .02f) * 18, -42 + bob * .35f);
                break;
            case 1:
                if (t >= 72 && t < 94)
                {
                    destination = player.Center;
                    NPC.damage = DifficultyDamage(FourTrialCatalog.Contact[Index]);
                }
                break;
            case 2:
                if (t >= 68 && t < 94)
                {
                    destination = player.Center + new Vector2(MathF.Sign(NPC.Center.X - player.Center.X) * 36, -22);
                    NPC.damage = DifficultyDamage(FourTrialCatalog.Contact[Index]);
                }
                if (t == 116) Shoot((player.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 2.1f);
                break;
            case 3:
                if (t >= 54 && t < 82)
                {
                    destination = player.Center + new Vector2(MathF.Sign(NPC.Center.X - player.Center.X) * 48, -16);
                    NPC.damage = DifficultyDamage(FourTrialCatalog.Contact[Index]);
                }
                if (t == 102)
                {
                    Vector2 velocity = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 2.2f;
                    Shoot(velocity);
                    Shoot(velocity.RotatedBy(.36f));
                    Shoot(velocity.RotatedBy(-.36f));
                }
                if (t == 145 && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    NPC.Center = player.Center + new Vector2(MathF.Sign(player.Center.X - NPC.Center.X) * -90, -72);
                    NPC.netUpdate = true;
                }
                break;
        }

        Vector2 desired = (destination - NPC.Center) * (Index == 0 ? .06f : .085f);
        NPC.velocity = Vector2.Lerp(NPC.velocity, desired, .18f);
        if (NPC.velocity.LengthSquared() > 8 * 8) NPC.velocity = NPC.velocity.SafeNormalize(Vector2.Zero) * 8;
        NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
        NPC.rotation = MathHelper.Clamp(NPC.velocity.X * .025f, -.22f, .22f);
        if (t is 50 or 68 or 102) NPC.netUpdate = true;
    }

    private void Shoot(Vector2 velocity)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || FourTrialCatalog.Shot[Index] <= 0) return;
        int id = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, velocity,
            ModContent.ProjectileType<FourTrialHazard>(), FourTrialCatalog.Shot[Index], 0, Main.myPlayer, Index);
        if (id >= 0 && id < Main.maxProjectiles) Main.projectile[id].netUpdate = true;
    }

    public override bool CanHitPlayer(Player target, ref int cooldownSlot) => NPC.damage > 0;

    public override void ModifyNPCLoot(NPCLoot loot)
    {
        loot.Add(ItemDropRule.BossBag(FourTrialCatalog.Bag(Index)));
        loot.Add(ItemDropRule.Common(FourTrialCatalog.Trophy(Index), 10));
        loot.Add(ItemDropRule.MasterModeCommonDrop(FourTrialCatalog.Relic(Index)));
        var normal = new LeadingConditionRule(new Conditions.NotExpert());
        normal.OnSuccess(ItemDropRule.Common(FourTrialCatalog.Weapon(Index)));
        normal.OnSuccess(ItemDropRule.Common(FourTrialCatalog.Memento(Index)));
        normal.OnSuccess(ItemDropRule.Common(FourTrialCatalog.Mask(Index), 50));
        loot.Add(normal);
    }

    public override void OnKill() => FourTrialWorld.RecordVictory(Index);

    public override void HitEffect(NPC.HitInfo hit)
    {
        if (Main.dedServ) return;
        for (int i = 0; i < 2 + Index; i++)
        {
            int dust = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GemDiamond, 0, 0, 100,
                FourTrialCatalog.Colors[Index], .6f);
            Main.dust[dust].noGravity = true;
        }
    }

    public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position)
    {
        scale = .68f + Index * .04f;
        return true;
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        Texture2D texture = TextureAssets.Npc[Type].Value;
        Vector2 at = NPC.Center - screenPos;
        float pulse = 1f + MathF.Sin(NPC.ai[0] * .08f) * .025f;
        spriteBatch.Draw(texture, at, null, FourTrialCatalog.Colors[Index] * .22f, NPC.rotation,
            texture.Size() / 2, pulse * 1.24f, SpriteEffects.None, 0);
        spriteBatch.Draw(texture, at, null, Color.White, NPC.rotation, texture.Size() / 2,
            pulse, NPC.spriteDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
        return false;
    }
}

[AutoloadBossHead] public sealed class FourTrialBoss0 : FourTrialBossNPC { public override int Index => 0; }
[AutoloadBossHead] public sealed class FourTrialBoss1 : FourTrialBossNPC { public override int Index => 1; }
[AutoloadBossHead] public sealed class FourTrialBoss2 : FourTrialBossNPC { public override int Index => 2; }
[AutoloadBossHead] public sealed class FourTrialBoss3 : FourTrialBossNPC { public override int Index => 3; }
