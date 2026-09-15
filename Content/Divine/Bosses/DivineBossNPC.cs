using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Divine.Bosses;

public enum DivineStage { Approach, Warning, Attack, Recovery, Transition }

/// <summary>Server-owned encounter identity. Timers/committed aims, never the client's cursor, drive hazards.</summary>
public abstract partial class DivineBossNPC : ModNPC
{
    public abstract int Index { get; }
    public override string Texture => DivineCatalog.Root + "Boss" + Index;
    public override string BossHeadTexture => Texture + "_Head_Boss";
    public bool[] Participants = new bool[Main.maxPlayers], Damaged = new bool[Main.maxPlayers];
    public Vector2 Arena, Aim, Origin;
    public Vector2[] Trace = new Vector2[25];
    public int Serial, Stagger, TraceCount;
    public bool Ready, Cancelled;
    public DivineStage Stage => (DivineStage)(int)NPC.ai[0];
    public int Timer => (int)NPC.ai[1];
    public int Move => (int)NPC.ai[2] % (Index == 2 ? 4 : 3);
    public int Phase => (int)NPC.ai[3];
    public bool Authority => Main.netMode != NetmodeID.MultiplayerClient;
    public Color Tint => DivineCatalog.Colors[Index];
    public static int Difficulty => Main.masterMode ? 2 : Main.expertMode ? 1 : 0;
    public int ContactDamage => Index switch { 0 => new[] { 38, 54, 68 }[Difficulty], 1 => new[] { 64, 90, 115 }[Difficulty], _ => new[] { 135, 189, 243 }[Difficulty] };
    public int ShotDamage(bool heavy = false) => Index switch
    {
        0 => (heavy ? new[] { 52, 72, 90 } : new[] { 32, 45, 57 })[Difficulty],
        1 => (heavy ? new[] { 86, 120, 154 } : new[] { 56, 78, 101 })[Difficulty],
        _ => (heavy ? new[] { 185, 259, 333 } : new[] { 116, 162, 209 })[Difficulty]
    };
    // Terraria multiplies hostile projectile.damage by 2/4/6 before defense. NPC.damage is already final
    // after ApplyDifficultyAndPlayerScaling resets the vanilla contact multiplier.
    public int EngineProjectileDamage(bool heavy = false) => Math.Max(1, ShotDamage(heavy) / (Difficulty == 2 ? 6 : Difficulty == 1 ? 4 : 2));
    public int ExpectedPhase => Index == 2 ? NPC.life < NPC.lifeMax * .15f ? 3 : NPC.life < NPC.lifeMax * .4f ? 2 : NPC.life < NPC.lifeMax * .75f ? 1 : 0
        : NPC.life < NPC.lifeMax * (Index == 0 ? .35f : .3f) ? 2 : NPC.life < NPC.lifeMax * .7f ? 1 : 0;
    public int AttackLength => Index switch { 0 => Move == 1 ? 330 : Move == 2 && Phase == 2 ? 285 : 250, 1 => 300, _ => Move is 2 or 3 ? 330 : 310 };
    public int RecoveryLength => Index == 0 && Phase == 2 && Move == 2 ? 180 : Index == 2 && Move == 3 ? 240 : Main.expertMode ? 55 : 70;
    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 1;
        NPCID.Sets.MPAllowedEnemies[Type] = true;
        NPCID.Sets.BossBestiaryPriority.Add(Type);
        NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
    }
    public override void SetDefaults()
    {
        NPC.width = Index == 0 ? 92 : 130; NPC.height = Index == 0 ? 104 : 164;
        NPC.aiStyle = -1; NPC.boss = true; NPC.noGravity = true; NPC.noTileCollide = true;
        NPC.knockBackResist = 0; NPC.npcSlots = 15;
        NPC.lifeMax = DivineCatalog.Life(Index, 0); NPC.damage = ContactDamage; NPC.defense = DivineCatalog.Defense[Index];
        NPC.value = Item.buyPrice(gold: Index == 0 ? 5 : Index == 1 ? 20 : 80);
        NPC.HitSound = Index == 0 ? SoundID.NPCHit1 : SoundID.NPCHit4; NPC.DeathSound = SoundID.NPCDeath14;
        NPC.BossBar = ModContent.GetInstance<DivineBossBar>();
        if (!Main.dedServ) Music = Index == 0 ? MusicID.Boss1 : Index == 1 ? MusicID.Boss2 : MusicID.LunarBoss;
        Participants = new bool[Main.maxPlayers]; Damaged = new bool[Main.maxPlayers]; Trace = new Vector2[25];
        Ready = Cancelled = false; Serial = Stagger = TraceCount = 0; Arena = Aim = Origin = Vector2.Zero;
    }
    public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
    {
        NPC.lifeMax = Math.Max(1, (int)(DivineCatalog.Life(Index, Difficulty) * balance));
        NPC.life = NPC.lifeMax; NPC.damage = ContactDamage;
    }
    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry entry)
    {
        entry.Info.Add(new FlavorTextBestiaryInfoElement("Mods.StarfallThrone.DivineCombat.Bestiary" + Index));
    }
    public override void BossLoot(ref int potionType) => potionType = Index == 0 ? ItemID.LesserHealingPotion : Index == 1 ? ItemID.HealingPotion : ItemID.GreaterHealingPotion;
    public override void ModifyNPCLoot(NPCLoot npcLoot) => DivineLoot.Configure(npcLoot, Index);
    public override bool CheckActive() => false;
    public override bool CheckDead()
    {
        if (!Cancelled && DivineWorld.Open(Index)) return true;
        Cancel(); return false;
    }
    public override bool PreKill()
    {
        if (!Cancelled && DivineWorld.Open(Index)) return true;
        Cancel(); return false;
    }
    public override void OnKill()
    {
        Cleanup();
        if (Authority && !Cancelled && DivineWorld.Open(Index)) DivineWorld.RecordVictory(NPC, Index, Participants, Damaged);
    }
    public override bool CanHitPlayer(Player target, ref int cooldownSlot)
        => !Cancelled && Ready && Stagger == 0 && Stage == DivineStage.Attack && Index == 0 && Move == 2 && Phase == 2 && (Timer >= 175 && Timer < 190 || Timer >= 230 && Timer < 245) && NPC.velocity.LengthSquared() > 16;
    public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
    {
        if (Stagger > 0) { modifiers.Defense *= 0; modifiers.FinalDamage *= 1.3f; }
        else if (Stage == DivineStage.Recovery && (Index == 0 && Phase == 2 && Move == 2 || Index == 2 && Move == 3)) modifiers.FinalDamage *= 1.2f;
    }
    public static Vector2 WorldPoint(Vector2 p, float inset = 96)
    {
        float maxX = Math.Max(inset + 1, Main.maxTilesX * 16f - inset), maxY = Math.Max(inset + 1, Main.maxTilesY * 16f - inset);
        return new Vector2(Math.Clamp(float.IsFinite(p.X) ? p.X : inset, inset, maxX), Math.Clamp(float.IsFinite(p.Y) ? p.Y : inset, inset, maxY));
    }
    public Vector2 ArenaPoint(Vector2 p)
    {
        // Horizontal room must not push a surface/Underworld arena hundreds of pixels vertically.
        Vector2 result = WorldPoint(p);
        float margin = Index == 0 ? 400 : 850;
        result.X = Math.Clamp(result.X, margin, Math.Max(margin + 1, Main.maxTilesX * 16f - margin));
        return result;
    }
    public override void AI()
    {
        // Day/night is a summon condition only. A dawn transition never enrages or cancels a running trial.
        if (Authority && !DivineWorld.Open(Index)) { Cancel(); return; }
        if (Cancelled) return;
        if (Authority)
            foreach (NPC other in Main.ActiveNPCs)
                if (other.boss && other.whoAmI != NPC.whoAmI && other.ModNPC is not DivineBossNPC)
                { Cancel("Interrupted"); return; }
        NPC.TargetClosest(false);
        if (!NPC.HasValidTarget || NPC.Distance(Main.player[NPC.target].Center) > 3600)
        { if (Authority) Cancel(); return; }
        Player p = Main.player[NPC.target];
        if (!Ready)
        {
            if (!Authority) return;
            Serial = Main.rand.Next(1, int.MaxValue); Arena = ArenaPoint(p.Center);
            Aim = p.Center; Origin = NPC.Center; Ready = true;
            for (int i = 0; i < Main.maxPlayers; i++)
                if (Main.player[i].active && !Main.player[i].dead && Vector2.Distance(Main.player[i].Center, Arena) < (Index == 0 ? 1000 : 1800)) Participants[i] = true;
            Array.Fill(Trace, p.Center);
            if (Index == 1) { SpawnBrazier(0); SpawnBrazier(1); }
            NPC.netUpdate = true;
        }
        NPC.timeLeft = 600;
        if (Authority)
            for (int i = 0; i < Main.maxPlayers; i++)
                if (!Participants[i] && Main.player[i].active && !Main.player[i].dead && Vector2.Distance(Main.player[i].Center, Arena) < (Index == 0 ? 1000 : 1800))
                { Participants[i] = true; Damaged[i] = true; } // Late arrivals may earn loot, never an unearned whole-fight no-hit halo.
        if (Authority && ExpectedPhase > Phase)
        {
            NPC.ai[3] = ExpectedPhase; ClearHazards(); TraceCount = 0; Enter(DivineStage.Transition);
        }
        if (Stagger > 0) { Stagger--; NPC.velocity *= .88f; if (Stagger == 0 && Authority) { ResetBraziers(); Enter(DivineStage.Approach); } return; }
        switch (Stage)
        {
            case DivineStage.Approach:
                Vector2 offset = Index switch { 0 => new Vector2(((int)NPC.ai[2] % 2 == 0 ? -1 : 1) * 260, -150), 1 => new Vector2(0, -250), _ => new Vector2(0, -300) };
                Fly(WorldPoint(p.Center + offset), Index == 0 ? 5 : 11);
                if (Timer >= 65 && Authority)
                {
                    Aim = WorldPoint(p.Center); Origin = NPC.Center;
                    if (Index != 2 || Move == 2) TraceCount = 0;
                    // Arena stays fixed while an attack resolves. Never move it beneath an active warning.
                    if (Index != 1) Arena = ArenaPoint(Vector2.Lerp(Arena, p.Center, .3f));
                    Enter(DivineStage.Warning);
                }
                break;
            case DivineStage.Warning:
                NPC.velocity *= .88f;
                if (Timer >= (Index == 0 ? 65 : 75)) Enter(DivineStage.Attack);
                break;
            case DivineStage.Attack:
                if (Index == 0) Miroen(p); else if (Index == 1) Velsa(p); else NoDay(p);
                if (Timer >= AttackLength) Enter(DivineStage.Recovery);
                break;
            case DivineStage.Recovery:
                NPC.velocity *= .86f;
                if (Timer >= RecoveryLength && Authority) { NPC.ai[2]++; Enter(DivineStage.Approach); }
                break;
            case DivineStage.Transition:
                NPC.velocity *= .9f;
                if (Timer >= 90) Enter(DivineStage.Approach);
                break;
        }
        NPC.ai[1]++;
        NPC.rotation = Math.Clamp(NPC.velocity.X * .012f, -.15f, .15f);
        Vector2 next = WorldPoint(NPC.Center + NPC.velocity);
        NPC.velocity = next - NPC.Center;
    }
    public void Enter(DivineStage stage)
    { if (!Authority) return; NPC.ai[0] = (int)stage; NPC.ai[1] = 0; NPC.netUpdate = true; }
    private void Fly(Vector2 to, float maxSpeed)
    { Vector2 desired = (to - NPC.Center).SafeNormalize(Vector2.Zero) * Math.Min(maxSpeed, NPC.Distance(to) * .08f); NPC.velocity = Vector2.Lerp(NPC.velocity, desired, .07f); }
    public void Cancel(string reason = "Abandoned")
    {
        if (!Authority || Cancelled) return;
        Cancelled = true; Cleanup(); NPC.life = Math.Max(1, NPC.life); NPC.active = false;
        string key = "Mods.StarfallThrone.DivineCombat." + (DivineWorld.Open(Index) ? reason : "Expired");
        if (Main.netMode == NetmodeID.SinglePlayer && !Main.gameMenu) Main.NewText(Language.GetTextValue(key, DivineCatalog.Names[Index]), Tint);
        else if (Main.netMode == NetmodeID.Server) Terraria.Chat.ChatHelper.BroadcastChatMessage(NetworkText.FromKey(key, DivineCatalog.Names[Index]), Tint);
        if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
    }
    public void ClearHazards()
    {
        if (!Authority) return;
        foreach (Projectile p in Main.ActiveProjectiles) if (p.ModProjectile is DivineHazard h && h.OwnedBy(this)) p.Kill();
    }
    public void Cleanup()
    {
        if (!Authority) return;
        ClearHazards();
        foreach (NPC n in Main.ActiveNPCs) if (n.ModNPC is DivineBrazier b && b.OwnedBy(this))
        { n.active = false; if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: n.whoAmI); }
    }
    public override void SendExtraAI(BinaryWriter w)
    {
        w.Write(Ready); w.Write(Cancelled); w.Write(Serial); w.Write(Stagger); w.Write(NPC.lifeMax);
        WritePoint(w, Arena); WritePoint(w, Aim); WritePoint(w, Origin); w.Write(TraceCount);
        for (int i = 0; i < 25; i++) WritePoint(w, Trace[i]);
    }
    public override void ReceiveExtraAI(BinaryReader r)
    {
        Ready = r.ReadBoolean(); Cancelled = r.ReadBoolean(); Serial = r.ReadInt32(); Stagger = Math.Clamp(r.ReadInt32(), 0, 240); NPC.lifeMax = Math.Max(1, r.ReadInt32());
        Arena = ReadPoint(r); Aim = ReadPoint(r); Origin = ReadPoint(r); TraceCount = Math.Clamp(r.ReadInt32(), 0, 25);
        for (int i = 0; i < 25; i++) Trace[i] = ReadPoint(r);
    }
    internal static void WritePoint(BinaryWriter w, Vector2 p) { w.Write(p.X); w.Write(p.Y); }
    internal static Vector2 ReadPoint(BinaryReader r) => WorldPoint(new Vector2(r.ReadSingle(), r.ReadSingle()));
    public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false;
    public override bool PreDraw(SpriteBatch batch, Vector2 screenPos, Color drawColor)
    {
        Texture2D tex = TextureAssets.Npc[Type].Value;
        float time = Main.GlobalTimeWrappedHourly;
        Vector2 scale = new(Index == 0 ? 1 : .95f);
        float rotation = NPC.rotation;
        Vector2 at = NPC.Center - screenPos;
        if (Index == 0)
        {
            float breath = MathF.Sin(time * (Phase == 2 ? 4.5f : 2.7f)) * .035f;
            float squash = Stage == DivineStage.Warning && Move == 2 ? .12f * Math.Min(1, Timer / 65f) : 0;
            scale *= new Vector2(1 + breath + squash, 1 - breath - squash);
            at.Y += MathF.Sin(time * 2) * 5;
            // Three fluttering crown slivers are cut from the generated body's crown, never painted stock icons.
            Rectangle crown = new(tex.Width / 3, 0, tex.Width / 3, Math.Max(1, tex.Height / 4));
            for (int i = 0; i < 3; i++)
            {
                float a = time * .4f + i * MathHelper.TwoPi / 3;
                Vector2 petal = at + new Vector2(MathF.Cos(a) * (Phase == 2 ? 86 : 64), -54 + MathF.Sin(a) * 15);
                batch.Draw(tex, petal, crown, Tint * .45f, MathF.Sin(time * 3 + i) * .18f, crown.Size() / 2, .65f, SpriteEffects.None, 0);
            }
        }
        else if (Index == 1)
        {
            rotation += MathF.Sin(time * (Stagger > 0 ? 11 : 2.1f)) * (Stagger > 0 ? .07f : .025f + Phase * .008f);
            scale.Y *= 1 + MathF.Sin(time * 3.5f) * .015f;
            CombatDrawing.Disc(batch, at + new Vector2(0, 12), 32 + MathF.Sin(time * 6) * 3, Tint * (Stagger > 0 ? .45f : .18f));
        }
        else
        {
            float angle = Phase == 3 ? -MathHelper.PiOver2 : time * (.15f + Phase * .07f);
            for (int ring = 0; ring < 3; ring++)
            {
                float a = angle * (ring % 2 == 0 ? 1 : -1) + ring * .7f;
                CombatDrawing.Circle(batch, at, 105 + ring * 17, Tint * (.36f - ring * .07f), 2, a, .5f);
                for (int tick = 0; tick < 12; tick++)
                {
                    float bearing = a + tick * MathHelper.TwoPi / 12;
                    CombatDrawing.Line(batch, at + bearing.ToRotationVector2() * (103 + ring * 17), at + bearing.ToRotationVector2() * (109 + ring * 17), Tint * .45f, 2);
                }
            }
            rotation = Phase == 3 ? 0 : rotation * .4f;
            at.Y += MathF.Sin(time * 1.5f) * (Phase == 3 ? 1 : 4);
        }
        batch.Draw(tex, at, null, Color.Lerp(drawColor, Color.White, .4f), rotation, tex.Size() / 2, scale, SpriteEffects.None, 0);
        if (Stage == DivineStage.Warning)
        {
            CombatDrawing.Circle(batch, Aim - screenPos, 26, Tint * .7f, 2);
            if (Index == 0 && Move == 2) CombatDrawing.Line(batch, Origin - screenPos, Aim - screenPos, Tint * .5f, 2);
            Vector2 bar = at - new Vector2(45, tex.Height / 2f + 10);
            CombatDrawing.Line(batch, bar, bar + new Vector2(90, 0), Color.Black, 6);
            CombatDrawing.Line(batch, bar, bar + new Vector2(90 * Math.Min(1, Timer / 75f), 0), Tint, 3);
        }
        if (Index == 2 && Move == 2 && Stage == DivineStage.Attack && Timer <= 120)
            for (int i = 1; i < TraceCount; i++)
                CombatDrawing.Line(batch, Trace[i - 1] - screenPos, Trace[i] - screenPos, Tint * .6f, 2);
        if (Index == 0 && Phase == 2 && Move == 2 && Stage == DivineStage.Attack && (Timer >= 145 && Timer < 175 || Timer >= 200 && Timer < 230))
            CombatDrawing.Line(batch, Origin - screenPos, Origin + (Aim - Origin).SafeNormalize(Vector2.UnitX) * 210 - screenPos, Tint * .8f, 3);
        if (Stagger > 0 || Stage == DivineStage.Recovery && (Index == 0 && Phase == 2 && Move == 2 || Index == 2 && Move == 3))
            CombatDrawing.Circle(batch, at, Index == 0 ? 80 : 125, Color.White * .8f, 3);
        return false;
    }
}

[AutoloadBossHead] public sealed class DivineBoss0 : DivineBossNPC { public override int Index => 0; }
[AutoloadBossHead] public sealed class DivineBoss1 : DivineBossNPC { public override int Index => 1; }
[AutoloadBossHead] public sealed class DivineBoss2 : DivineBossNPC { public override int Index => 2; }
