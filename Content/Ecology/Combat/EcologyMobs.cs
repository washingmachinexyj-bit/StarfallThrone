using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Ecology.Combat;

public abstract class EcologyMobNPC : ModNPC
{
    public abstract int Index { get; }
    public int Biome => Index / 4;
    public int Slot => Index % 4;
    public bool Elite => Slot == 3;
    public int Serial;
    public Vector2 Aim;
    public bool Authority => Main.netMode != NetmodeID.MultiplayerClient;
    public int Timer => (int)NPC.ai[0];
    public bool Flying => Index is 1 or 5 or 9 or 13 or 17 or 21 or 30 or 32 or 33 or 34 or 35 or 37 or 38 or 39;
    public int BaseLife => new[] { 75, 130, 165, 230, 460, 850, 1150, 1450, 1800, 2400 }[Biome] * (Elite ? 3 : 1);
    public int BaseDamage => new[] { 15, 22, 26, 31, 44, 57, 69, 80, 89, 100 }[Biome] + (Elite ? 8 : 0);
    public override string Texture => EcologyCatalog.Root + "Mob" + Index;
    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 1; NPCID.Sets.MPAllowedEnemies[Type] = true;
        SpawnModBiomes = new[] { ModContent.Find<ModBiome>("StarfallThrone", "EcologyBiome" + Biome).Type };
        if (Elite) NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
    }
    public override void SetDefaults()
    {
        NPC.width = Elite ? 66 : 38; NPC.height = Elite ? 58 : 36; NPC.aiStyle = -1;
        NPC.noGravity = NPC.noTileCollide = Flying; NPC.lifeMax = BaseLife; NPC.damage = BaseDamage;
        NPC.defense = Biome * 3 + (Elite ? 8 : 2); NPC.knockBackResist = Elite ? .15f : .5f;
        NPC.npcSlots = Elite ? 2 : 1; NPC.value = Item.buyPrice(silver: 2 + Biome * 2);
        NPC.HitSound = Index is 0 or 7 or 16 or 20 or 28 or 31 or 36 ? SoundID.NPCHit4 : SoundID.NPCHit1;
        NPC.DeathSound = SoundID.NPCDeath1; Banner = Type; BannerItem = EcologyCatalog.Item("EcologyBanner" + Index);
        Serial = 0; Aim = Vector2.Zero;
    }
    public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
    { NPC.lifeMax = (int)(BaseLife * (Main.masterMode ? 1.8f : Main.expertMode ? 1.4f : 1)); NPC.damage = (int)(BaseDamage * EcologyBossNPC.DifficultyDamage); }
    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry entry)
    {
        EcologyBestiary.Attach(entry, Biome);
        entry.Info.Add(new FlavorTextBestiaryInfoElement("Mods.StarfallThrone.EcologyCombat.MobBestiary" + Index));
    }
    public override float SpawnChance(NPCSpawnInfo info)
    {
        if (info.PlayerInTown || !EcologyCatalog.VanillaUnlocked(Biome) || EcologyWorld.ActiveBiome(info.Player) != Biome) return 0;
        EcologyRegion r = EcologyWorld.RegionAt(new Point(info.SpawnTileX, info.SpawnTileY));
        if (r == null || !r.Ecology || r.Biome != Biome || r.TileCount < 300) return 0;
        foreach (NPC n in Main.ActiveNPCs) if (n.boss || Elite && n.type == Type) return 0;
        return Elite ? .015f : .18f;
    }
    public override void ModifyNPCLoot(NPCLoot loot)
    {
        loot.Add(ItemDropRule.Common(EcologyCatalog.Material(Biome, Slot), 1, Elite ? 2 : 1, Elite ? 4 : 3));
        if (Elite) loot.Add(ItemDropRule.Common(EcologyCatalog.Material(Biome, 0), 1, 2, 5));
    }
    public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
    {
        if (Index is 0 or 8 or 16 or 28 or 36 && modifiers.HitDirection == -NPC.direction) modifiers.FinalDamage *= .5f;
        if (Index is 5 or 7 && Timer < 60) modifiers.FinalDamage *= .55f;
    }
    public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone) => Anger();
    public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone) => Anger();
    private void Anger() { if (Authority && Index == 4) { NPC.ai[2] = 90; NPC.netUpdate = true; } }
    public override bool CanHitPlayer(Player target, ref int cooldownSlot)
        => Serial != 0 && !(Index is 2 or 5 or 10 or 25 or 31 && Timer % 180 < 60);
    public override void AI()
    {
        NPC.TargetClosest(); if (!NPC.HasValidTarget) { NPC.velocity.X *= .95f; return; }
        Player player = Main.player[NPC.target];
        if (Serial == 0)
        { if (!Authority) return; Serial = Main.rand.Next(1, int.MaxValue); Aim = player.Center; NPC.netUpdate = true; }
        NPC.ai[0]++; NPC.ai[2] = Math.Max(0, NPC.ai[2] - 1);
        if (Timer % 180 == 1 && Authority) { Aim = player.Center; NPC.netUpdate = true; }
        int t = Timer % 180;
        switch (Index)
        {
            case 0: Walk(player, 1.25f); break; // Frontal shell, deliberately easy to flank.
            case 1: OrbitDive(player, t, 100, 4, 8); break;
            case 2: Ambush(player, t, 9, false); break;
            case 3: DoubleCharge(player, t, 8); break;
            case 4: Walk(player, NPC.ai[2] > 0 ? 4.2f : 2); break;
            case 5: if (t < 60) Hover(player.Center - new Vector2(0, 100), 1); else OrbitDive(player, t, 80, 3, 7); break;
            case 6: Caster(player, t, 1, 3.5f, EcologyShape.Bubble, .7f); break;
            case 7: Walk(player, .7f); if (t is 65 or 100 or 135) Volley(Aim, 2, 4, EcologyShape.Shard, .5f); break;
            case 8: if (t < 45) { NPC.velocity.X *= .9f; if (t == 1) NPC.ai[2] = 30; } else Walk(player, 3); break;
            case 9: Hover(player.Center + new Vector2(-NPC.direction * 130, -95), 4); if (t == 70) Volley(Aim, 1, 7, EcologyShape.Shard); break;
            case 10: Ambush(player, t, 8, true); break;
            case 11: Walk(player, .65f); if (t is 70 or 110) Volley(Aim, 3, 4, EcologyShape.Bubble, 1.2f); break;
            case 12: Walk(player, 2); if (t == 60) Shoot(EcologyShape.Wave, NPC.Center + new Vector2(0, 12), new Vector2(NPC.direction * 4, 0), 35, 100, 12); break;
            case 13: OrbitDive(player, t, 90, 3, 9); break;
            case 14: Walk(player, t < 70 ? .9f : 0); if (t == 65) Shoot(EcologyShape.Ring, NPC.Center, Vector2.Zero, 45, 85, 10, NPC.Center + new Vector2(220, 0)); break;
            case 15: Walk(player, 1); if (t is 60 or 95 or 130) Volley(Aim, 3, 5, EcologyShape.Shard, .55f); break;
            case 16: Walk(player, 2.5f); break;
            case 17: Hover(player.Center + new Vector2(-140, 50), 4); if (t % 60 == 20) Shoot(EcologyShape.Shard, NPC.Center, new Vector2(0, -6), 30, 100, 14); break;
            case 18: Walk(player, t < 55 ? 1.5f : 0); if (t == 60) Shoot(EcologyShape.Beam, NPC.Center, Vector2.Zero, 45, 20, 14, NPC.Center + (Aim - NPC.Center).SafeNormalize(Vector2.UnitX) * 190); break;
            case 19: DoubleCharge(player, t, 11); break;
            case 20: Walk(player, t < 60 ? 4 : .2f); if (t == 75) Volley(Aim, 1, 7, EcologyShape.Shard); break;
            case 21: OrbitDive(player, t, 150, 4, 11); break;
            case 22: Caster(player, t, 3, 5, EcologyShape.Wave, .5f); break;
            case 23: Walk(player, t < 45 ? 1.5f : 0); if (t is 70 or 100) Volley(Aim, 4, 7, EcologyShape.Shard, .6f); break;
            case 24: Walk(player, 0); if (t == 60) Volley(Aim, 4, 5, EcologyShape.Bubble, .9f); break;
            case 25: Ambush(player, t, 11, true); break;
            case 26: Walk(player, 1.8f); if (t == 70) Shoot(EcologyShape.Bubble, NPC.Center, (Aim - NPC.Center).SafeNormalize(Vector2.UnitX) * 2.5f, 45, 90, 24); break;
            case 27: Walk(player, 2); if (t is 60 or 120) Shoot(EcologyShape.Pool, Aim + new Vector2(0, 25), Vector2.Zero, 55, 90, 55); break;
            case 28: DoubleCharge(player, t, 10); break;
            case 29: Walk(player, t < 45 ? 2 : 0); if (t == 70) Shoot(EcologyShape.Beam, NPC.Center, Vector2.Zero, 50, 25, 14, NPC.Center + (Aim - NPC.Center).SafeNormalize(Vector2.UnitX) * 340); break;
            case 30: OrbitDive(player, t, 170, 5, 12); break;
            case 31: Walk(player, .7f); if (t is 65 or 120) Volley(Aim + new Vector2(t == 65 ? -70 : 70, 0), 4, 7, EcologyShape.Shard, .9f); break;
            case 32: OrbitDive(player, t, 110, 4, 11); break;
            case 33: Hover(player.Center - new Vector2(0, 170), 2.5f); if (t == 70) Volley(Aim, 5, 2.5f, EcologyShape.Bubble, 2.2f); break;
            case 34: Hover(Aim + new Vector2(MathF.Cos(t * .035f) * 150, MathF.Sin(t * .035f) * 65 - 70), 6); if (t == 90) Shoot(EcologyShape.Beam, NPC.Center, Vector2.Zero, 50, 25, 12, Aim); break;
            case 35: Hover(player.Center + new Vector2(-180, -80), 4); if (t is 50 or 120) Volley(Aim, 5, 3, EcologyShape.Bubble, 1.5f); break;
            case 36: Walk(player, 3); if (t == 70 && NPC.collideY) NPC.velocity.Y = -7; break;
            case 37: Hover(Aim - new Vector2(0, 120), 2); if (t == 55 && Authority) { NPC.Center = EcologyBossNPC.WorldPoint(Aim + new Vector2(NPC.direction * 160, -100)); NPC.netUpdate = true; } if (t == 60) Shoot(EcologyShape.Beam, NPC.Center, Vector2.Zero, 65, 22, 15, Aim); break;
            case 38: OrbitDive(player, t, 190, 6, 14); break;
            case 39: Hover(Aim + new Vector2(0, -210), 5); if (t == 60) Shoot(EcologyShape.Beam, NPC.Center, Vector2.Zero, 60, 24, 18, Aim + new Vector2(0, 120)); if (t == 120) Charge(Aim, 14); break;
        }
        if (Flying) NPC.rotation = Math.Clamp(NPC.velocity.X * .025f, -.3f, .3f);
        NPC.spriteDirection = NPC.direction;
    }
    private void Walk(Player player, float speed)
    {
        NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, Math.Sign(player.Center.X - NPC.Center.X) * speed, .08f);
        if (NPC.collideX && NPC.collideY && speed > 0) NPC.velocity.Y = -6;
    }
    private void Hover(Vector2 to, float speed)
    { Vector2 delta = to - NPC.Center; NPC.velocity = Vector2.Lerp(NPC.velocity, delta.SafeNormalize(Vector2.Zero) * Math.Min(speed, delta.Length() * .1f), .08f); }
    private void Charge(Vector2 to, float speed)
    { if (Authority) { NPC.velocity = (to - NPC.Center).SafeNormalize(Vector2.UnitX) * speed; if (!Flying) NPC.velocity.Y = Math.Clamp(NPC.velocity.Y, -5, 2); NPC.netUpdate = true; } }
    private void OrbitDive(Player player, int t, float radius, float drift, float speed)
    {
        if (t < 95) Hover(Aim + new Vector2(MathF.Cos(t * .035f) * radius, MathF.Sin(t * .035f) * radius * .5f - 60), drift);
        else if (t == 95) Charge(Aim, speed);
        else if (t > 125) NPC.velocity *= .93f;
    }
    private void Ambush(Player player, int t, float speed, bool twice)
    {
        if (t < 60 || t > 130) NPC.velocity.X *= .88f;
        if (t == 60 || twice && t == 110) Charge(Aim, speed);
    }
    private void DoubleCharge(Player player, int t, float speed)
    {
        if (t is 55 or 120) Charge(Aim, speed);
        else if (t < 55 || t > 85 && t < 120 || t > 150) NPC.velocity.X *= .9f;
    }
    private void Caster(Player player, int t, int count, float speed, EcologyShape shape, float spread)
    { Walk(player, t < 50 ? 1.4f : .1f); if (t == 70) Volley(Aim, count, speed, shape, spread); }
    private void Volley(Vector2 to, int count, float speed, EcologyShape shape, float spread = 0)
    {
        for (int i = 0; i < count; i++) Shoot(shape, NPC.Center, (to - NPC.Center).SafeNormalize(Vector2.UnitX).RotatedBy(count == 1 ? 0 : (i / (float)(count - 1) - .5f) * spread) * speed, 35, 100, shape == EcologyShape.Bubble ? 18 : 10);
    }
    public EcologyHazard Shoot(EcologyShape shape, Vector2 from, Vector2 velocity, int delay, int duration, float width, Vector2? end = null)
    {
        if (!Authority) return null;
        int count = 0; foreach (Projectile p in Main.ActiveProjectiles) if (p.ModProjectile is EcologyHazard h && h.OwnerSlot == NPC.whoAmI && h.Serial == Serial) count++;
        if (count >= 12) return null;
        int damage = Math.Max(1, (int)(BaseDamage * EcologyBossNPC.DifficultyDamage) / (Main.masterMode ? 6 : Main.expertMode ? 4 : 2));
        int slot = Projectile.NewProjectile(NPC.GetSource_FromAI(), EcologyBossNPC.WorldPoint(from), velocity, ModContent.ProjectileType<EcologyHazard>(), damage, 0, Main.myPlayer, NPC.whoAmI + 1, 0, (int)shape);
        if (slot >= Main.maxProjectiles || slot < 0 || Main.projectile[slot].ModProjectile is not EcologyHazard hazard) return null;
        hazard.Setup(NPC, Serial, Biome, delay, duration, width, end ?? from + new Vector2(240, 0), 0); return hazard;
    }
    public override void SendExtraAI(BinaryWriter w) { w.Write(Serial); EcologyBossNPC.Write(w, Aim); }
    public override void ReceiveExtraAI(BinaryReader r) { Serial = r.ReadInt32(); Aim = EcologyBossNPC.Read(r); }
    public override bool PreDraw(SpriteBatch batch, Vector2 screenPos, Color drawColor)
    {
        Texture2D texture = TextureAssets.Npc[Type].Value; Vector2 at = NPC.Center - screenPos;
        float scale = (Elite ? 88f : 55f) / Math.Max(texture.Width, texture.Height);
        bool disguise = Index is 2 or 5 or 10 or 25 && Timer % 180 < 60;
        if (Index == 2 && Timer % 180 < 60) batch.Draw(texture, at + new Vector2(60, 0), null, drawColor * .25f, 0, texture.Size() / 2, scale, SpriteEffects.None, 0);
        batch.Draw(texture, at, null, drawColor * (disguise ? .65f : 1), NPC.rotation, texture.Size() / 2, scale * (1 + MathF.Sin(Timer * .08f) * .025f), NPC.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);
        int t = Timer % 180;
        if (t >= 20 && t < 60 && Serial != 0) CombatDrawing.Line(batch, at, Aim - screenPos, EcologyCatalog.Colors[Biome] * .5f, Elite ? 2 : 1);
        return false;
    }
}
