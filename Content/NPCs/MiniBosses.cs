using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using StarfallThrone.Content.Projectiles;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.NPCs;

public abstract partial class MiniBossNPC : ModNPC
{
    public abstract int Index { get; }
    public int State => (int)NPC.ai[0];
    public int Timer => (int)NPC.ai[1];
    public bool Dormant => Index is 0 or 6 && NPC.ai[3] < 0;
    private Vector2 Locked => new(NPC.ai[2], NPC.ai[3]);
    private bool Angry => NPC.life < NPC.lifeMax * (Index is 0 or 6 ? 0.4f : Index is 2 or 4 or 7 ? 0.45f : 0.5f);
    private bool Flying => Index is 2 or 3;
    public override string Texture => "StarfallThrone/Content/Assets/Minis/" + MiniBossData.Keys[Index];
    public override string BossHeadTexture => Texture + "_Head_Boss";

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 1;
        NPCID.Sets.MPAllowedEnemies[Type] = true;
        NPCID.Sets.DontDoHardmodeScaling[Type] = true;
    }
    public override void SetDefaults()
    {
        NPC.width = new[] { 42, 54, 40, 54, 54, 46, 40, 34 }[Index];
        NPC.height = new[] { 32, 36, 54, 40, 36, 58, 28, 34 }[Index];
        NPC.lifeMax = MiniBossData.Life[Index];
        NPC.damage = MiniBossData.Contact[Index];
        NPC.defense = MiniBossData.Defense[Index];
        NPC.knockBackResist = 0f;
        NPC.aiStyle = -1;
        NPC.boss = false;
        NPC.noGravity = Flying;
        NPC.noTileCollide = false;
        NPC.npcSlots = 4;
        NPC.value = Item.buyPrice(silver: Index == 6 ? 4 : Index == 7 ? 6 : 8 + Index * 4);
        NPC.HitSound = Index is 0 or 5 ? SoundID.NPCHit7 : SoundID.NPCHit1;
        NPC.DeathSound = SoundID.NPCDeath1;
    }
    public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
    {
        NPC.lifeMax = (int)(NPC.lifeMax * balance);
    }
    public override float SpawnChance(NPCSpawnInfo spawnInfo)
    {
        if (Index is not (0 or 6) || !Main.dayTime || MiniBossWorld.Downed[Index] || NPC.AnyNPCs(Type)
            || !MiniBossData.Habitat(spawnInfo.Player, Index) || MiniBossData.AnyActive()) return 0;
        return Index == 6 ? 0.004f : 0.006f;
    }
    public override void OnSpawn(IEntitySource source)
    {
        if (Index is 0 or 6 && source is EntitySource_SpawnNPC)
        {
            NPC.ai[3] = -1; NPC.friendly = true; NPC.dontTakeDamage = true; NPC.npcSlots = 1;
        }
    }
    public override bool CanChat() => Dormant;
    public override string GetChat() => Language.GetTextValue("Mods.StarfallThrone.MiniMessages." + (Index == 6 ? "SleepingDew" : "SleepingMoss"));
    public override void SetChatButtons(ref string button, ref string button2)
    {
        if (Dormant) button = Language.GetTextValue("Mods.StarfallThrone.MiniMessages." + (Index == 6 ? "WakeDew" : "WakeMoss"));
    }
    public override void OnChatButtonClicked(bool firstButton, ref string shopName)
    {
        if (!firstButton || !Dormant) return;
        Main.npcChatText = ""; Main.LocalPlayer.SetTalkNPC(-1);
        if (Main.netMode == NetmodeID.SinglePlayer) TryWake(Main.LocalPlayer, NPC.whoAmI);
        else
        {
            ModPacket packet = Mod.GetPacket(); packet.Write((byte)2); packet.Write((short)NPC.whoAmI); packet.Send();
        }
    }
    public static void TryWake(Player player, int npcIndex)
    {
        if (!player.active || player.dead || npcIndex < 0 || npcIndex >= Main.maxNPCs || MiniBossData.AnyActive()) return;
        NPC npc = Main.npc[npcIndex];
        if (!npc.active || npc.ModNPC is not MiniBossNPC { Dormant: true } || npc.Distance(player.Center) > 160) return;
        npc.ai[3] = 0; npc.ai[0] = npc.ai[1] = 0; npc.friendly = false; npc.dontTakeDamage = false;
        npc.npcSlots = 4; npc.target = player.whoAmI; npc.netUpdate = true;
        if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npcIndex);
    }
    public override void BossHeadSlot(ref int index) { if (Dormant) index = -1; }
    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry entry)
    {
        entry.Info.Add(BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface);
        entry.Info.Add(new FlavorTextBestiaryInfoElement("Mods.StarfallThrone.MiniBestiary." + MiniBossData.Keys[Index]));
    }
    public override bool CanHitPlayer(Player target, ref int cooldownSlot) => ContactActive;
    public override bool? CanFallThroughPlatforms() => NPC.HasValidTarget && Main.player[NPC.target].Top.Y > NPC.Bottom.Y + 48;
    private bool ContactActive => Index switch
    {
        0 => State == 2,
        1 => State is 2 or 4,
        2 => State == 2,
        3 => State == 4,
        4 => State is 3 or 5,
        6 => State == 4,
        7 => State is 1 or 3 or 5,
        _ => State == 2
    };
    public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
    {
        if (Index == 1 && State == 5) modifiers.FinalDamage *= 0.85f;
    }

    // Ground probes never mutate tiles. Requiring clear headroom prevents emerging inside a roof.
    public static bool FindGround(Vector2 near, int width, int height, out Vector2 bottom)
    {
        int tx = (int)(near.X / 16f), startY = (int)(near.Y / 16f) - 6;
        for (int y = Math.Max(10, startY); y < Math.Min(Main.maxTilesY - 10, startY + 32); y++)
        {
            if (!WorldGen.InWorld(tx, y, 10) || !WorldGen.SolidTile(tx, y)) continue;
            Vector2 b = new(tx * 16 + 8, y * 16);
            if (Collision.SolidCollision(b - new Vector2(width / 2f, height), width, height)) continue;
            bottom = b; return true;
        }
        bottom = near; return false;
    }
    private void Lock(Vector2 position) { NPC.ai[2] = position.X; NPC.ai[3] = position.Y; NPC.netUpdate = true; }
    private void Change(int state) { NPC.ai[0] = state; NPC.ai[1] = 0; NPC.netUpdate = true; }
    private int Facing(float x) => x >= NPC.Center.X ? 1 : -1;
    private void Walk(float x, float speed)
    {
        NPC.direction = Facing(x); NPC.spriteDirection = NPC.direction;
        float desired = Math.Abs(x - NPC.Center.X) < 24 ? 0 : NPC.direction * speed;
        NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, desired, 0.12f);
        if (NPC.collideX && NPC.collideY) NPC.velocity.Y = -4.6f;
    }
    private void Hover(Vector2 position, float speed = 2f)
    {
        Vector2 desired = (position - NPC.Center).SafeNormalize(Vector2.Zero) * Math.Min(speed, NPC.Distance(position) / 12f);
        NPC.velocity = Vector2.Lerp(NPC.velocity, desired, 0.09f);
        if (NPC.collideX) NPC.velocity.Y = -2f;
        NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;
    }
    private void Rest() { NPC.velocity.X *= 0.84f; if (Flying) NPC.velocity.Y *= 0.84f; }
    private void Shot(int kind, Vector2 position, Vector2 velocity, float mode = 0)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient) return;
        int desired = MiniBossData.Attack[Index];
        // Hostile projectile damage uses the engine's 2x/4x/6x player-hurt multiplier.
        int multiplier = Main.masterMode ? 6 : Main.expertMode ? 4 : 2;
        float difficulty = Main.masterMode ? 3f : Main.expertMode ? 2f : 1f;
        int raw = Math.Max(1, (int)MathF.Round(desired * difficulty / multiplier));
        Projectile.NewProjectile(NPC.GetSource_FromAI(), position, velocity,
            ModContent.ProjectileType<MiniHostileProjectile>(), raw, 0f, Main.myPlayer, kind, mode, NPC.whoAmI + 1);
    }
    private void Arc(int kind, int count, float speed, float upward)
    {
        int direction = NPC.direction;
        for (int j = 0; j < count; j++) Shot(kind, NPC.Center, new Vector2(direction * (speed + j * 0.8f), -upward + j * 0.55f));
    }
    private void WarnGround(Vector2 position, int kind)
    {
        Shot(kind, position, Vector2.Zero, 1);
    }
    public override void AI()
    {
        if (Dormant)
        {
            NPC.friendly = true; NPC.dontTakeDamage = true; NPC.velocity.X *= 0.85f;
            return;
        }
        NPC.friendly = false;
        NPC.TargetClosest(false);
        if (!NPC.HasValidTarget || NPC.Distance(Main.player[NPC.target].Center) > 2400f)
        {
            NPC.velocity.X *= 0.96f;
            if (Flying) NPC.velocity.Y -= 0.08f;
            NPC.timeLeft = Math.Min(NPC.timeLeft, 60);
            return;
        }
        NPC.timeLeft = 300;
        NPC.ai[1]++;
        Player p = Main.player[NPC.target];
        NPC.noTileCollide = false; NPC.noGravity = Flying; NPC.dontTakeDamage = false; NPC.alpha = 0;
        switch (Index)
        {
            case 0: Moss(p); break;
            case 1: Crab(p); break;
            case 2: Parasol(p); break;
            case 3: Moth(p); break;
            case 4: Burrower(p); break;
            case 5: Sprout(p); break;
            case 6: Dew(p); break;
            case 7: Puff(p); break;
        }
        if (NPC.velocity.X != 0 && !Flying) NPC.spriteDirection = NPC.velocity.X > 0 ? 1 : -1;
    }
    private void Moss(Player p)
    {
        switch (State)
        {
            case 0: Walk(p.Center.X, 0.9f); if (Timer >= 60) { Lock(p.Center); Change(1); } break;
            case 1: Rest(); if (Timer >= 54) { NPC.velocity.X = Facing(Locked.X) * 4.8f; Change(2); } break;
            case 2:
                NPC.rotation += NPC.velocity.X * 0.045f;
                if (NPC.collideX || Timer >= (Angry ? 42 : 34)) { NPC.velocity.X *= 0.2f; Change(3); }
                break;
            case 3: Rest(); NPC.rotation = MathHelper.Lerp(NPC.rotation, 0, 0.12f); if (Timer >= 72) Change(0); break;
        }
    }
    private void Crab(Player p)
    {
        switch (State)
        {
            case 0: Walk(p.Center.X + (NPC.Center.X > p.Center.X ? 90 : -90), Angry ? 1.6f : 1.2f); if (Timer >= 65) { Lock(p.Center); Change(1); } break;
            case 1: Rest(); if (Timer >= 42) { NPC.direction = Facing(Locked.X); NPC.velocity.X = NPC.direction * 3.6f; Change(2); } break;
            case 2: if (NPC.collideX || Timer >= 18) Change(3); break;
            case 3: Rest(); if (Timer >= 22) Change(4); break;
            case 4: Rest(); if (Timer == 1) Shot(0, NPC.Center + new Vector2(NPC.direction * 26, 0), new Vector2(NPC.direction * 2.1f, 0), 2); if (Timer >= 15) Change(5); break;
            case 5: Rest(); if (Timer >= 48) { Arc(0, 3, 2f, 4f); Change(6); } break;
            case 6: Rest(); if (Timer >= (Angry ? 48 : 65)) Change(0); break;
        }
    }
    private void Parasol(Player p)
    {
        switch (State)
        {
            case 0: Hover(p.Center + new Vector2(MathF.Sin(Timer * 0.025f) * 100, -110), Angry ? 2.4f : 2f); if (Timer >= 90) { Lock(p.Center); Change(1); } break;
            case 1: Rest(); if (Timer >= 48) { NPC.velocity = (Locked - NPC.Center).SafeNormalize(Vector2.UnitY) * 5f; Change(2); } break;
            case 2: if (NPC.collideX || NPC.collideY || Timer >= 26) Change(3); break;
            case 3: Rest(); if (Timer >= 60) { Lock(p.Center + new Vector2(NPC.Center.X < p.Center.X ? -110 : 110, -60)); Change(4); } break;
            case 4: Hover(Locked, 2.5f); if (Timer >= 40) { Shot(1, NPC.Center, (p.Center - NPC.Center).SafeNormalize(Vector2.UnitX) * 2.5f); Change(5); } break;
            case 5: Rest(); if (Timer >= 50) Change(0); break;
        }
    }
    private void Moth(Player p)
    {
        switch (State)
        {
            case 0: Hover(p.Center + new Vector2(100 * MathF.Sin(Timer * 0.025f), -120), 2.3f); if (Timer >= 70) Change(1); break;
            case 1:
                Rest();
                if (Timer == 32) { Shot(2, NPC.Center, new Vector2(-1.2f, 1f)); Shot(2, NPC.Center, new Vector2(1.2f, 1f)); }
                if (Timer >= 65) Change(2); break;
            case 2:
                Hover(p.Center + new Vector2(0, -110));
                if (Timer == 1)
                {
                    // Keep a >= 5-tile corridor between the two 40px hazards.
                    if (FindGround(p.Bottom + new Vector2(Angry ? -68 : 0, 0), 40, 32, out Vector2 first)) WarnGround(first, 3);
                    if (Angry && FindGround(p.Bottom + new Vector2(68, 0), 40, 32, out Vector2 second)) WarnGround(second, 3);
                }
                if (Timer >= 195) { Lock(p.Center + new Vector2(p.Center.X > NPC.Center.X ? 150 : -150, -42)); Change(3); } break;
            case 3: Rest(); if (Timer >= 42) { NPC.velocity = (Locked - NPC.Center).SafeNormalize(Vector2.UnitX) * 3.8f; Change(4); } break;
            case 4: if (NPC.collideX || NPC.collideY || Timer >= 45) Change(5); break;
            case 5: Rest(); if (Timer >= 65) Change(0); break;
        }
    }
    private void Burrower(Player p)
    {
        switch (State)
        {
            case 0:
                Walk(p.Center.X, 1.4f);
                if (Timer >= 80)
                {
                    if (FindGround(p.Bottom + new Vector2(p.direction * 48, 0), NPC.width, NPC.height, out Vector2 bottom)) { Lock(bottom); Change(1); }
                    else { Lock(p.Center); Change(4); }
                }
                break;
            case 1: Rest(); if (Timer >= 24) Change(2); break;
            case 2:
                NPC.noGravity = true; NPC.noTileCollide = true; NPC.dontTakeDamage = true; NPC.alpha = 155;
                NPC.velocity = (Locked - new Vector2(0, NPC.height / 2f) - NPC.Center) / Math.Max(1, 55 - Timer);
                if (Timer >= 54)
                {
                    NPC.Bottom = Locked; NPC.velocity = new Vector2(0, -3.4f); Change(3);
                }
                break;
            case 3:
                if (Timer == 1) { NPC.direction = Facing(p.Center.X); Arc(4, 3, 2.2f, 3.8f); }
                NPC.velocity.X *= 0.85f;
                if (Timer >= 120) { if (Angry) { Lock(p.Center); Change(4); } else Change(0); }
                break;
            case 4: Rest(); if (Timer >= 42) { NPC.velocity.X = Facing(Locked.X) * 4.1f; Change(5); } break;
            case 5: if (NPC.collideX || Timer >= 28) Change(6); break;
            case 6: Rest(); if (Timer >= 72) Change(0); break;
        }
    }
    private void Sprout(Player p)
    {
        switch (State)
        {
            case 0: Walk(p.Center.X, Angry ? 1.54f : 1.4f); if (Timer >= 68) { Lock(p.Center); Change(1); } break;
            case 1: Rest(); if (Timer >= 42) { NPC.direction = Facing(Locked.X); NPC.velocity.X = NPC.direction * 3.1f; Change(2); } break;
            case 2: if (Timer == 1) Shot(5, NPC.Center + new Vector2(NPC.direction * 24, 0), new Vector2(NPC.direction * 2.4f, 0), 2); if (Timer >= 16) Change(3); break;
            case 3: Rest(); if (Timer >= (Angry ? 30 : 72)) { NPC.direction = Facing(p.Center.X); Arc(5, 3, 2.5f, 5f); Change(4); } break;
            case 4:
                Rest();
                // Every seed expires before a root is allowed to become dangerous.
                if (Timer >= 110)
                {
                    if (FindGround(p.Bottom + new Vector2(-64, 0), 30, 38, out Vector2 a)) WarnGround(a, 5);
                    if (FindGround(p.Bottom + new Vector2(64, 0), 30, 38, out Vector2 b)) Shot(5, b, Vector2.Zero, 3);
                    Change(5);
                }
                break;
            case 5: Rest(); if (Timer >= 150) Change(0); break;
        }
    }
    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        Texture2D texture = TextureAssets.Npc[Type].Value;
        if (Index == 6 && Dormant) texture = ModContent.Request<Texture2D>("StarfallThrone/Content/Assets/Minis/SleepingLeaf").Value;
        bool warning = Index == 7 ? State == 4 : State == 1 || (Index == 4 && State is 2 or 4) || (Index is 3 or 6 && State == 3);
        Color tint = warning ? Color.Lerp(drawColor, MiniBossData.Colors[Index], 0.35f + 0.15f * MathF.Sin(Timer * 0.25f)) : drawColor;
        float size = Math.Max(NPC.width, NPC.height) + 16;
        Vector2 position = NPC.Bottom - screenPos - new Vector2(0, size / 2f - 4);
        float rollScale = Dormant ? 0.82f : Index == 0 && State == 1 ? 0.91f : 1f;
        Main.EntitySpriteDraw(texture, position, null, tint * NPC.Opacity, NPC.rotation, texture.Size() / 2f,
            size / texture.Width * rollScale, NPC.spriteDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
        DrawStarterTelegraph(spriteBatch, screenPos);
        if (Index == 4 && State == 2)
        {
            Vector2 at = Locked - screenPos;
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)at.X - 28, (int)at.Y - 4, 56, 4), MiniBossData.Colors[Index]);
            for (int k = -1; k <= 1; k++) spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)at.X + k * 14 - 2, (int)at.Y - 10, 4, 8), Color.Orange);
        }
        return false;
    }
    public override void HitEffect(NPC.HitInfo hit)
    {
        if (Main.dedServ) return;
        for (int i = 0; i < (NPC.life <= 0 ? 22 : 4); i++)
            Dust.NewDust(NPC.position, NPC.width, NPC.height, Index is 2 or 3 ? DustID.MagicMirror : DustID.Dirt, hit.HitDirection, -1f, 120, MiniBossData.Colors[Index], 0.85f);
    }
    public override void OnKill()
    {
        MiniBossWorld.MarkDowned(Index);
        if (Main.netMode != NetmodeID.MultiplayerClient)
            foreach (Projectile shot in Main.ActiveProjectiles)
                if (shot.ModProjectile is MiniHostileProjectile && (int)shot.ai[2] == NPC.whoAmI + 1) shot.Kill();
    }
    public override void ModifyNPCLoot(NPCLoot loot)
    {
        loot.Add(ItemDropRule.Common(MiniBossData.MaterialType(Index), 1, Index >= 6 ? 4 : 5, Index >= 6 ? 6 : 8));
        loot.Add(ItemDropRule.Common(MiniBossData.RewardType(Index), 3));
        loot.Add(ItemDropRule.Common(MiniBossData.TrophyType(Index), 8));
        string weapon = Index switch { 1 => "ClawFork", 2 => "FoldedUmbrella", 3 => "WickWand", 4 => "FlintHammer", 5 => "ThornBow", 7 => "DandelionWand", _ => "" };
        if (weapon.Length > 0) loot.Add(ItemDropRule.Common(MiniBossData.ItemType(weapon), 3));
        if (Index == 3) loot.Add(ItemDropRule.Common(MiniBossData.ItemType("MothLantern"), 3));
    }
}

[AutoloadBossHead] public sealed class MossTumblerNPC : MiniBossNPC { public override int Index => 0; }
[AutoloadBossHead] public sealed class PotHermitNPC : MiniBossNPC { public override int Index => 1; }
[AutoloadBossHead] public sealed class TatteredParasolNPC : MiniBossNPC { public override int Index => 2; }
[AutoloadBossHead] public sealed class WickMothNPC : MiniBossNPC { public override int Index => 3; }
[AutoloadBossHead] public sealed class FlintBurrowerNPC : MiniBossNPC { public override int Index => 4; }
[AutoloadBossHead] public sealed class BriarSproutNPC : MiniBossNPC { public override int Index => 5; }
