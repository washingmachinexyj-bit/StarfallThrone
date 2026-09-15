using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Ecology.Combat;

public enum EcologyStage { Approach, Warning, Attack, Recovery, Transition }

public abstract partial class EcologyBossNPC : ModNPC
{
    public abstract int Index { get; }
    public override string Texture => EcologyCatalog.Root + "Boss" + Index;
    public override string BossHeadTexture => Texture + "_Head_Boss";
    public EcologyStage Stage => (EcologyStage)(int)NPC.ai[0];
    public int Timer => (int)NPC.ai[1];
    public int Move => (int)NPC.ai[2] % 4;
    public int Phase => (int)NPC.ai[3];
    public bool Authority => Main.netMode != NetmodeID.MultiplayerClient;
    public bool Ready, Cancelled;
    public int Serial, RegionId = -1, AbandonTicks, Exposed, PartsBroken, Shell;
    public Vector2 Arena, Aim, Origin, Mark;
    public Rectangle ArenaBounds;
    public Color Tint => EcologyCatalog.Colors[Index];
    public static float DifficultyLife => Main.masterMode ? 1.9f : Main.expertMode ? 1.5f : 1f;
    public static float DifficultyDamage => Main.masterMode ? 1.8f : Main.expertMode ? 1.4f : 1f;
    public int ContactDamage => (int)(EcologyCatalog.Damage[Index] * DifficultyDamage);
    public int ShotDamage(bool heavy = false) => (int)((heavy ? EcologyCatalog.HeavyDamage[Index] : EcologyCatalog.Damage[Index]) * DifficultyDamage);
    public int EngineDamage(bool heavy = false) => Math.Max(1, ShotDamage(heavy) / (Main.masterMode ? 6 : Main.expertMode ? 4 : 2));
    public int AttackLength => Index switch { 0 => 160, 1 => 180, 2 => 190, 3 => 200, 4 => 180, 5 => 220, 6 => 210, 7 => 190, 8 => 230, _ => 220 };
    public bool ContactActive => Stage == EcologyStage.Attack && NPC.velocity.LengthSquared() > 36 && Exposed == 0 &&
        (Index == 0 && Move == 0 || Index == 1 && Move == 2 || Index == 2 && Move == 3 || Index == 4 && Move == 0 || Index == 5 && Move == 0 || Index == 7 && Move == 1 || Index == 8 && Move == 0 || Index == 9 && Move == 3);
    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 1; NPCID.Sets.MPAllowedEnemies[Type] = true;
        NPCID.Sets.BossBestiaryPriority.Add(Type); NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
        SpawnModBiomes = new[] { ModContent.Find<ModBiome>("StarfallThrone", "EcologyBiome" + Index).Type };
    }
    public override void SetDefaults()
    {
        NPC.width = Index < 4 ? 112 : 144; NPC.height = Index is 1 or 3 ? 140 : 108;
        NPC.aiStyle = -1; NPC.boss = true; NPC.noGravity = NPC.noTileCollide = true; NPC.knockBackResist = 0; NPC.npcSlots = 12;
        NPC.lifeMax = EcologyCatalog.Lives[Index]; NPC.defense = EcologyCatalog.Defenses[Index]; NPC.damage = ContactDamage;
        NPC.value = Item.buyPrice(gold: 5 + Index * 5); NPC.HitSound = Index is 1 or 5 or 7 ? SoundID.NPCHit4 : SoundID.NPCHit1; NPC.DeathSound = SoundID.NPCDeath14;
        NPC.BossBar = ModContent.GetInstance<EcologyBossBar>();
        if (!Main.dedServ) Music = Index < 4 ? MusicID.Boss1 : Index < 9 ? MusicID.Boss3 : MusicID.LunarBoss;
        Ready = Cancelled = false; Serial = AbandonTicks = Exposed = PartsBroken = Shell = 0; RegionId = -1;
        Arena = Aim = Origin = Mark = Vector2.Zero; ArenaBounds = Rectangle.Empty;
    }
    public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
    { NPC.lifeMax = Math.Max(1, (int)(EcologyCatalog.Lives[Index] * DifficultyLife * balance)); NPC.life = NPC.lifeMax; NPC.damage = ContactDamage; }
    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry entry)
    {
        EcologyBestiary.Attach(entry, Index);
        entry.Info.Add(new FlavorTextBestiaryInfoElement("Mods.StarfallThrone.EcologyCombat.Bestiary" + Index));
    }
    public override void BossLoot(ref int potionType) => potionType = EcologyLoot.HealingPotion(Index);
    public override void ModifyNPCLoot(NPCLoot npcLoot) => EcologyLoot.Configure(npcLoot, Index);
    public override bool CheckActive() => false;
    public override bool PreKill() => !Cancelled;
    public override void OnKill() { Cleanup(); if (Authority && !Cancelled) EcologyWorld.Defeat(Index); }
    public override bool CanHitPlayer(Player target, ref int cooldownSlot) => Ready && !Cancelled && ContactActive;
    public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
    {
        if (Exposed > 0 || Stage == EcologyStage.Recovery) { modifiers.Defense *= .55f; modifiers.FinalDamage *= 1.18f; }
        else if (Shell > 0) modifiers.FinalDamage *= Index == 1 ? .45f : .75f;
        else if (Index == 4 && Move is 0 or 3) modifiers.FinalDamage *= .72f;
    }
    public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone) => HitShell(damageDone);
    public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone) => HitShell(damageDone);
    private void HitShell(int damage)
    {
        if (!Authority || Shell <= 0) return;
        Shell = Math.Max(0, Shell - damage); NPC.netUpdate = true;
        if (Shell == 0) Expose(120);
    }
    public static Vector2 WorldPoint(Vector2 p)
        => new(Math.Clamp(float.IsFinite(p.X) ? p.X : 128, 128, Math.Max(129, Main.maxTilesX * 16 - 128)), Math.Clamp(float.IsFinite(p.Y) ? p.Y : 128, 128, Math.Max(129, Main.maxTilesY * 16 - 128)));
    public Vector2 Bound(Vector2 p) => ArenaBounds.Width > 0 ? EcologySummonBase.ClampToRegion(WorldPoint(p), ArenaBounds) : WorldPoint(p);
    public override void AI()
    {
        if (Cancelled) return;
        NPC.TargetClosest(false);
        if (!NPC.HasValidTarget || NPC.Distance(Main.player[NPC.target].Center) > 3000) { Cancel(); return; }
        Player player = Main.player[NPC.target];
        if (!Ready)
        {
            if (!Authority) return;
            EcologyRegion region = EcologyWorld.RegionAt(player.Center.ToTileCoordinates());
            if (region == null || !region.Ecology || region.Biome != Index) { Cancel(); return; }
            RegionId = region.Id; ArenaBounds = region.Bounds; Arena = Bound(player.Center); Aim = player.Center; Origin = NPC.Center;
            Serial = Main.rand.Next(1, int.MaxValue); Ready = true;
            Shell = Index == 1 ? NPC.lifeMax / 8 : 0; NPC.netUpdate = true;
        }
        if (Authority)
        {
            if (RegionId >= 0)
            {
                EcologyRegion region = EcologyWorld.RegionAt(Arena.ToTileCoordinates());
                if (region == null || region.Id != RegionId || !region.Ecology || region.Biome != Index) { Cancel(); return; }
            }
            Rectangle safe = new(ArenaBounds.X * 16 - 240, ArenaBounds.Y * 16 - 240, ArenaBounds.Width * 16 + 480, ArenaBounds.Height * 16 + 480);
            if (!safe.Contains(player.Center.ToPoint())) AbandonTicks++; else AbandonTicks = 0;
            if (AbandonTicks > 180) { Cancel(); return; }
            if (Phase == 0 && NPC.life < NPC.lifeMax * .5f)
            { NPC.ai[3] = 1; Cleanup(); Shell = 0; Enter(EcologyStage.Transition); }
        }
        NPC.timeLeft = 600;
        if (Exposed > 0) Exposed--;
        switch (Stage)
        {
            case EcologyStage.Approach:
                Fly(Bound(player.Center + new Vector2((Move % 2 == 0 ? -1 : 1) * 270, -140)), Index < 4 ? 7 : 11);
                if (Timer >= 60 && Authority) { Aim = Bound(player.Center); Origin = NPC.Center; Mark = Aim; Enter(EcologyStage.Warning); }
                break;
            case EcologyStage.Warning:
                NPC.velocity *= .87f;
                if (Timer >= (Phase == 0 ? 55 : 42)) Enter(EcologyStage.Attack);
                break;
            case EcologyStage.Attack:
                RunSignature(player);
                if (Timer >= AttackLength) Enter(EcologyStage.Recovery);
                break;
            case EcologyStage.Recovery:
                NPC.velocity *= .88f;
                if (Timer >= (Index is 1 or 7 ? 85 : 60) && Authority) { NPC.ai[2]++; Enter(EcologyStage.Approach); }
                break;
            case EcologyStage.Transition:
                NPC.velocity *= .86f;
                if (Timer >= 85) Enter(EcologyStage.Approach);
                break;
        }
        NPC.ai[1]++; NPC.spriteDirection = NPC.direction = NPC.velocity.X >= 0 ? 1 : -1;
        NPC.rotation = Math.Clamp(NPC.velocity.X * .008f, -.16f, .16f);
        NPC.velocity = Bound(NPC.Center + NPC.velocity) - NPC.Center;
    }
    public void Enter(EcologyStage stage)
    {
        if (!Authority) return;
        NPC.ai[0] = (int)stage; NPC.ai[1] = 0; NPC.netUpdate = true;
        if (stage == EcologyStage.Attack) { PartsBroken = 0; SoundEngine.PlaySound(Index == 3 ? SoundID.Item35 : SoundID.Roar, NPC.Center); }
    }
    public void Fly(Vector2 point, float speed, float inertia = .09f)
    { Vector2 delta = point - NPC.Center; NPC.velocity = Vector2.Lerp(NPC.velocity, delta.SafeNormalize(Vector2.Zero) * Math.Min(speed, delta.Length() * .1f), inertia); }
    public void Dash(Vector2 toward, float speed) { if (Authority) { NPC.velocity = (toward - NPC.Center).SafeNormalize(Vector2.UnitX) * speed; NPC.netUpdate = true; } }
    public void Expose(int ticks)
    {
        if (!Authority) return;
        if (Exposed == 0 || ticks > Exposed + 10) NPC.netUpdate = true;
        Exposed = Math.Max(Exposed, ticks);
    }
    public void PartBroken(int kind)
    {
        if (!Authority) return;
        PartsBroken++; if (kind is 0 or 1 or 2 or 3 or 4) Expose(kind == 2 ? 150 : 110);
        if (Index == 4 && kind == 1)
        {
            // Breaking the cold-state radiator advances overheating, with a new safe tell.
            Cleanup(); NPC.ai[2] = ((int)NPC.ai[2] / 4) * 4 + 1; Enter(EcologyStage.Warning);
        }
    }
    public void Cleanup()
    {
        if (!Authority) return;
        foreach (Projectile p in Main.ActiveProjectiles) if (p.ModProjectile is EcologyHazard h && h.OwnedBy(this)) p.Kill();
        foreach (NPC n in Main.ActiveNPCs) if (n.ModNPC is EcologyPart part && part.OwnedBy(this))
        { n.active = false; if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: n.whoAmI); }
    }
    public void Cancel()
    {
        if (!Authority || Cancelled) return;
        Cancelled = true; Cleanup(); NPC.active = false;
        if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
    }
    public override void SendExtraAI(BinaryWriter w)
    {
        w.Write(Ready); w.Write(Cancelled); w.Write(Serial); w.Write(RegionId); w.Write(Exposed); w.Write(PartsBroken); w.Write(Shell);
        Write(w, Arena); Write(w, Aim); Write(w, Origin); Write(w, Mark);
        w.Write(ArenaBounds.X); w.Write(ArenaBounds.Y); w.Write(ArenaBounds.Width); w.Write(ArenaBounds.Height);
    }
    public override void ReceiveExtraAI(BinaryReader r)
    {
        Ready = r.ReadBoolean(); Cancelled = r.ReadBoolean(); Serial = r.ReadInt32(); RegionId = r.ReadInt32();
        Exposed = Math.Clamp(r.ReadInt32(), 0, 300); PartsBroken = Math.Clamp(r.ReadInt32(), 0, 100); Shell = Math.Clamp(r.ReadInt32(), 0, NPC.lifeMax);
        Arena = Read(r); Aim = Read(r); Origin = Read(r); Mark = Read(r);
        ArenaBounds = new Rectangle(r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
    }
    internal static void Write(BinaryWriter w, Vector2 p) { w.Write(p.X); w.Write(p.Y); }
    internal static Vector2 Read(BinaryReader r) => WorldPoint(new Vector2(r.ReadSingle(), r.ReadSingle()));
    public override bool PreDraw(SpriteBatch batch, Vector2 screenPos, Color drawColor)
    {
        Texture2D texture = TextureAssets.Npc[Type].Value; Vector2 at = NPC.Center - screenPos;
        float breath = MathF.Sin(Main.GlobalTimeWrappedHourly * (Index == 8 ? 3.5f : 2.5f)) * .025f;
        Vector2 scale = new(NPC.width * 1.55f / texture.Width * (1 + breath), NPC.height * 1.55f / texture.Height * (1 - breath));
        if (Index == 0 && Move is 0 or 3 && Stage is EcologyStage.Warning or EcologyStage.Attack)
            for (int i = 0; i < 3; i++) batch.Draw(texture, at + new Vector2((i - 1) * 110, -65), null, Tint * .18f, NPC.rotation, texture.Size() / 2, scale * .9f, SpriteEffects.None, 0);
        if (Index == 9 || Index == 8 && Move == 1) CombatDrawing.Circle(batch, Arena - screenPos, Index == 9 ? 180 : 130, Tint * .35f, 2);
        batch.Draw(texture, at, null, Color.Lerp(drawColor, Color.White, .3f), NPC.rotation, texture.Size() / 2, scale, NPC.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);
        if (Stage == EcologyStage.Warning)
        {
            CombatDrawing.Line(batch, at, Aim - screenPos, Tint * .65f, 2);
            CombatDrawing.Circle(batch, Aim - screenPos, 24, Tint, 2);
            CombatDrawing.Line(batch, at - new Vector2(40, NPC.height), at - new Vector2(40, NPC.height) + new Vector2(80 * Math.Min(1, Timer / (Phase == 0 ? 55f : 42f)), 0), Tint, 4);
        }
        if (Exposed > 0 || Stage == EcologyStage.Recovery) CombatDrawing.Circle(batch, at, NPC.width * .55f, Color.White * .7f, 2, -MathHelper.PiOver2, .35f);
        if (Index == 1 && Shell > 0) CombatDrawing.Circle(batch, at, NPC.width * .7f, Tint * .55f, 4);
        return false;
    }
}
