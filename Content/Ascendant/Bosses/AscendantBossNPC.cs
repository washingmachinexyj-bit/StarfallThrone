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

namespace StarfallThrone.Content.Ascendant.Bosses;

public enum AscendantStage { Approach, Warning, Attack, Recovery, Transition, Choice }

/// <summary>Independent encounters: authoritative spawns, slot+serial ownership and committed attack geometry.</summary>
public abstract partial class AscendantBossNPC : ModNPC
{
    public abstract int Index { get; }
    public override string Texture => AscendantCatalog.Root + "Boss" + Index;
    public override string BossHeadTexture => Texture + "_Head_Boss";
    public bool[] Participants = new bool[Main.maxPlayers], Damaged = new bool[Main.maxPlayers];
    public Vector2 Arena, Aim, Origin;
    public Vector2[] Trace = new Vector2[31];
    public int Serial, VulnerableTicks, TraceCount, BrokenMask, Suppressed = -1, SelectedLaw = -1;
    public bool Ready, Cancelled, FinalUsed, FinalActive, KnotBroken;
    public float Vulnerability = 1;
    public AscendantStage Stage => (AscendantStage)(int)NPC.ai[0];
    public int Timer => (int)NPC.ai[1];
    public int Round => (int)NPC.ai[2];
    public int Phase => (int)NPC.ai[3];
    public int Move => Round % (Index == 2 ? 5 : 4);
    public bool Authority => Main.netMode != NetmodeID.MultiplayerClient;
    public Color Tint => AscendantCatalog.Colors[Index];
    public static int Difficulty => Main.masterMode ? 2 : Main.expertMode ? 1 : 0;
    public int ContactDamage => (int)((Index == 0 ? 32 : Index == 1 ? 90 : 180) * DifficultyFactor);
    private static float DifficultyFactor => Difficulty == 2 ? 1.8f : Difficulty == 1 ? 1.4f : 1;
    public int ShotDamage(bool heavy = false) => (int)((Index == 0 ? heavy ? 60 : 44 : Index == 1 ? heavy ? 155 : 120 : heavy ? 300 : 230) * DifficultyFactor);
    // Terraria applies 2/4/6 to hostile projectile damage; contact values are already difficulty-adjusted.
    public int EngineProjectileDamage(bool heavy = false) => Math.Max(1, ShotDamage(heavy) / (Difficulty == 2 ? 6 : Difficulty == 1 ? 4 : 2));
    public int ExpectedPhase => NPC.life < NPC.lifeMax * (Index == 1 ? .30f : .35f) ? 2 : NPC.life < NPC.lifeMax * (Index == 1 ? .65f : .70f) ? 1 : 0;
    public int AttackLength => FinalActive ? Index == 0 ? 975 : 940 : Index == 0 ? 440 : Index == 1 ? Phase == 2 ? 680 : 550 : Phase == 0 ? 450 : 660;
    public int RecoveryLength => FinalActive ? 360 : Index == 1 && Phase == 2 ? 300 : Index == 0 ? 80 : 100;
    public bool ChoiceResolved => SelectedLaw >= 0;
    public int ChoiceLeft => Move;
    public int ChoiceRight => (Move + 2) % 5;

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 1;
        NPCID.Sets.MPAllowedEnemies[Type] = true;
        NPCID.Sets.BossBestiaryPriority.Add(Type);
        NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
    }
    public override void SetDefaults()
    {
        NPC.width = Index == 0 ? 100 : 142; NPC.height = Index == 0 ? 118 : 172;
        NPC.aiStyle = -1; NPC.boss = true; NPC.noGravity = NPC.noTileCollide = true;
        NPC.knockBackResist = 0; NPC.npcSlots = 15; NPC.lifeMax = AscendantCatalog.Life(Index, 0);
        NPC.damage = ContactDamage; NPC.defense = AscendantCatalog.Defense[Index];
        NPC.value = Item.buyPrice(gold: Index == 0 ? 12 : Index == 1 ? 35 : 120);
        NPC.HitSound = Index == 0 ? SoundID.NPCHit1 : SoundID.NPCHit4; NPC.DeathSound = SoundID.NPCDeath14;
        NPC.BossBar = ModContent.GetInstance<AscendantBossBar>();
        if (!Main.dedServ) Music = Index == 0 ? MusicID.Boss1 : Index == 1 ? MusicID.Boss2 : MusicID.LunarBoss;
        Participants = new bool[Main.maxPlayers]; Damaged = new bool[Main.maxPlayers]; Trace = new Vector2[31];
        Ready = Cancelled = FinalUsed = FinalActive = KnotBroken = false;
        Serial = VulnerableTicks = TraceCount = BrokenMask = 0; Suppressed = SelectedLaw = -1; Vulnerability = 1;
        Arena = Aim = Origin = Vector2.Zero;
    }
    public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
    { NPC.lifeMax = Math.Max(1, (int)(AscendantCatalog.Life(Index, Difficulty) * balance)); NPC.life = NPC.lifeMax; NPC.damage = ContactDamage; }
    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry entry)
        => entry.Info.Add(new FlavorTextBestiaryInfoElement("Mods.StarfallThrone.AscendantCombat.Bestiary" + Index));
    public override void BossLoot(ref int potionType) => potionType = Index == 0 ? ItemID.LesserHealingPotion : Index == 1 ? ItemID.HealingPotion : ItemID.GreaterHealingPotion;
    public override void ModifyNPCLoot(NPCLoot npcLoot) => AscendantLoot.Configure(npcLoot, Index);
    public override bool CheckActive() => false;
    public override bool CheckDead() { if (!Cancelled && AscendantWorld.Open(Index) && AscendantWorld.Prerequisites(Index)) return true; Cancel(); return false; }
    public override bool PreKill() { if (!Cancelled && AscendantWorld.Open(Index) && AscendantWorld.Prerequisites(Index)) return true; Cancel(); return false; }
    public override void OnKill()
    {
        Cleanup();
        if (Authority && !Cancelled && AscendantWorld.Open(Index) && AscendantWorld.Prerequisites(Index)) AscendantWorld.RecordVictory(NPC, Index, Participants, Damaged);
    }
    // A large decorative flying body is harmless outside the two clearly drawn committed dew-crown passes.
    public override bool CanHitPlayer(Player target, ref int cooldownSlot)
        => Ready && !Cancelled && Index == 0 && !FinalActive && Phase == 2 && Stage == AscendantStage.Attack
        && (Timer >= 250 && Timer < 290 || Timer >= 350 && Timer < 390) && NPC.velocity.LengthSquared() > 16;
    public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo)
    { if (target.whoAmI >= 0 && target.whoAmI < Damaged.Length) Damaged[target.whoAmI] = true; }
    public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
    {
        if (VulnerableTicks > 0) modifiers.FinalDamage *= Vulnerability;
        else if (Stage == AscendantStage.Recovery && FinalActive) modifiers.FinalDamage *= 1.25f;
    }
    public static Vector2 WorldPoint(Vector2 point, float inset = 80)
    {
        float xmax = Math.Max(inset + 1, Main.maxTilesX * 16f - inset), ymax = Math.Max(inset + 1, Main.maxTilesY * 16f - inset);
        return new Vector2(Math.Clamp(float.IsFinite(point.X) ? point.X : inset, inset, xmax), Math.Clamp(float.IsFinite(point.Y) ? point.Y : inset, inset, ymax));
    }
    public override void AI()
    {
        // Time of day is only a summon requirement; dawn/dusk never silently ends a trial.
        if (Authority && (!AscendantWorld.Open(Index) || !AscendantWorld.Prerequisites(Index))) { Cancel(); return; }
        if (Cancelled) return;
        if (Authority)
            foreach (NPC other in Main.ActiveNPCs)
                if (other.boss && other.whoAmI != NPC.whoAmI) { Cancel("Interrupted"); return; }
        NPC.TargetClosest(false);
        if (!NPC.HasValidTarget || NPC.Distance(Main.player[NPC.target].Center) > 3600)
        { if (Authority) Cancel(); return; }
        Player player = Main.player[NPC.target];
        if (!Ready)
        {
            if (!Authority) return;
            Serial = Main.rand.Next(1, int.MaxValue); Arena = Aim = WorldPoint(player.Center); Origin = NPC.Center;
            Array.Fill(Trace, Aim); Ready = true;
            for (int i = 0; i < Main.maxPlayers; i++)
                if (Eligible(Main.player[i])) Participants[i] = true;
            NPC.netUpdate = true;
        }
        NPC.timeLeft = 600;
        if (Authority)
            for (int i = 0; i < Main.maxPlayers; i++)
                if (!Participants[i] && Eligible(Main.player[i])) { Participants[i] = true; Damaged[i] = true; }
        if (VulnerableTicks > 0) VulnerableTicks--;
        if (Authority && ExpectedPhase > Phase)
        { NPC.ai[3] = ExpectedPhase; Cleanup(); Enter(AscendantStage.Transition); }
        if (Authority && !FinalUsed && (Index == 0 || Index == 2) && NPC.life <= NPC.lifeMax * (Index == 0 ? .12f : .10f))
        { FinalUsed = FinalActive = true; Cleanup(); Enter(AscendantStage.Transition); }
        switch (Stage)
        {
            case AscendantStage.Approach:
                Fly(WorldPoint(player.Center + new Vector2(Index == 0 ? (Round % 2 == 0 ? -260 : 260) : 0, Index == 0 ? -150 : -280)), Index == 0 ? 5 : 10);
                if (Timer >= 60 && Authority)
                {
                    Arena = Aim = WorldPoint(player.Center); Origin = NPC.Center;
                    BrokenMask = 0; Suppressed = SelectedLaw = -1; KnotBroken = false; TraceCount = 0;
                    CleanupNodes();
                    if (Index == 0 && Phase > 0 && !FinalActive) for (int i = 0; i < 3; i++) SpawnNode(AscendantNodeKind.Dew, i);
                    if (Index == 1) for (int i = 0; i < 4; i++) SpawnNode(AscendantNodeKind.Furnace, i);
                    Enter(Index == 2 && Phase == 2 && !FinalActive ? AscendantStage.Choice : AscendantStage.Warning);
                }
                break;
            case AscendantStage.Choice:
                NPC.velocity *= .9f;
                if (Authority && Timer >= 45)
                {
                    Vector2 left = Arena + new Vector2(-150, 0), right = Arena + new Vector2(150, 0);
                    if (Vector2.Distance(player.Center, left) < 85) ResolveChoice(false);
                    else if (Vector2.Distance(player.Center, right) < 85) ResolveChoice(true);
                    else if (Timer >= 180) ResolveChoice(Vector2.DistanceSquared(player.Center, right) < Vector2.DistanceSquared(player.Center, left));
                }
                break;
            case AscendantStage.Warning:
                NPC.velocity *= .88f;
                if (Timer >= (FinalActive ? 120 : 75)) Enter(AscendantStage.Attack);
                break;
            case AscendantStage.Attack:
                if (Index == 0) Miroen(player); else if (Index == 1) Velsa(player); else NoDay(player);
                if (Timer >= AttackLength && Authority) { ClearHazards(); CleanupNodes(); Enter(AscendantStage.Recovery); }
                break;
            case AscendantStage.Recovery:
                NPC.velocity *= .9f;
                if (Timer >= RecoveryLength && Authority) { FinalActive = false; NPC.ai[2]++; Enter(AscendantStage.Approach); }
                break;
            case AscendantStage.Transition:
                NPC.velocity *= .9f;
                if (Timer >= 90) Enter(AscendantStage.Approach);
                break;
        }
        NPC.ai[1]++;
        if (Authority && Timer % 60 == 0) NPC.netUpdate = true;
        NPC.rotation = Math.Clamp(NPC.velocity.X * .012f, -.12f, .12f);
        NPC.velocity = WorldPoint(NPC.Center + NPC.velocity) - NPC.Center;
    }
    private bool Eligible(Player player) => player.active && !player.dead && Vector2.Distance(player.Center, Arena) < (Index == 0 ? 1200 : 1900);
    private void Fly(Vector2 to, float speed)
    { Vector2 desired = (to - NPC.Center).SafeNormalize(Vector2.Zero) * Math.Min(speed, NPC.Distance(to) * .075f); NPC.velocity = Vector2.Lerp(NPC.velocity, desired, .08f); }
    public void Enter(AscendantStage stage)
    { if (!Authority) return; NPC.ai[0] = (int)stage; NPC.ai[1] = 0; NPC.netUpdate = true; }
    public void ResolveChoice(bool right)
    {
        if (!Authority || Stage != AscendantStage.Choice || ChoiceResolved) return;
        SelectedLaw = right ? ChoiceRight : ChoiceLeft; Enter(AscendantStage.Warning);
    }
    public void Expose(float multiplier, int ticks)
    { if (!Authority) return; Vulnerability = multiplier; VulnerableTicks = ticks; NPC.netUpdate = true; }
    public void Cancel(string reason = "Abandoned")
    {
        if (!Authority || Cancelled) return;
        Cancelled = true; Cleanup(); NPC.life = Math.Max(1, NPC.life); NPC.active = false;
        string key = "Mods.StarfallThrone.AscendantCombat." + (AscendantWorld.Open(Index) ? reason : "Expired");
        if (Main.netMode == NetmodeID.SinglePlayer && !Main.gameMenu) Main.NewText(Language.GetTextValue(key, AscendantCatalog.Names[Index]), Tint);
        else if (Main.netMode == NetmodeID.Server) Terraria.Chat.ChatHelper.BroadcastChatMessage(NetworkText.FromKey(key, AscendantCatalog.Names[Index]), Tint);
        if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
    }
    public void ClearHazards(int mark = -1)
    {
        if (!Authority) return;
        foreach (Projectile shot in Main.ActiveProjectiles)
            if (shot.ModProjectile is AscendantHazard h && h.OwnedBy(this) && (mark < 0 || h.Mark == mark)) shot.Kill();
    }
    public void CleanupNodes()
    {
        if (!Authority) return;
        foreach (NPC node in Main.ActiveNPCs)
            if (node.ModNPC is AscendantNode n && n.OwnedBy(this))
            { node.active = false; if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: node.whoAmI); }
    }
    public void Cleanup() { ClearHazards(); CleanupNodes(); }
    public override void SendExtraAI(BinaryWriter w)
    {
        w.Write(Ready); w.Write(Cancelled); w.Write(Serial); w.Write(NPC.lifeMax); w.Write(VulnerableTicks); w.Write(Vulnerability);
        w.Write(FinalUsed); w.Write(FinalActive); w.Write(KnotBroken); w.Write(BrokenMask); w.Write(Suppressed); w.Write(SelectedLaw);
        WriteVector(w, Arena); WriteVector(w, Aim); WriteVector(w, Origin); w.Write(TraceCount);
        for (int i = 0; i < Trace.Length; i++) WriteVector(w, Trace[i]);
    }
    public override void ReceiveExtraAI(BinaryReader r)
    {
        Ready = r.ReadBoolean(); Cancelled = r.ReadBoolean(); Serial = r.ReadInt32(); NPC.lifeMax = Math.Max(1, r.ReadInt32());
        VulnerableTicks = Math.Clamp(r.ReadInt32(), 0, 600); Vulnerability = Math.Clamp(r.ReadSingle(), 1, 1.25f);
        FinalUsed = r.ReadBoolean(); FinalActive = r.ReadBoolean(); KnotBroken = r.ReadBoolean(); BrokenMask = r.ReadInt32() & 7;
        Suppressed = Math.Clamp(r.ReadInt32(), -1, 3); SelectedLaw = Math.Clamp(r.ReadInt32(), -1, 4);
        Arena = WorldPoint(ReadVector(r)); Aim = WorldPoint(ReadVector(r)); Origin = WorldPoint(ReadVector(r)); TraceCount = Math.Clamp(r.ReadInt32(), 0, 31);
        for (int i = 0; i < Trace.Length; i++) Trace[i] = WorldPoint(ReadVector(r));
    }
    internal static void WriteVector(BinaryWriter writer, Vector2 v) { writer.Write(v.X); writer.Write(v.Y); }
    internal static Vector2 ReadVector(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle());
    public override bool? DrawHealthBar(byte position, ref float scale, ref Vector2 at) => false;
    public override bool PreDraw(SpriteBatch batch, Vector2 screenPos, Color drawColor)
    {
        if (!Ready) return true;
        Vector2 at = NPC.Center - screenPos; float t = Main.GlobalTimeWrappedHourly;
        int count = Index == 0 ? 8 : Index == 1 ? 4 : 5;
        float radius = Index == 0 ? 92 : Index == 1 ? 126 : 158;
        for (int i = 0; i < count; i++)
        {
            float angle = t * (Index == 2 ? -.16f : .2f) + i * MathHelper.TwoPi / count;
            Vector2 sigil = at + angle.ToRotationVector2() * radius * new Vector2(1, .72f);
            AscendantGlyph.Draw(batch, sigil, i, Index == 0 ? 11 : 17, Tint * .75f);
            if (FinalActive) CombatDrawing.Line(batch, at, sigil, Tint * .15f, 2);
        }
        Texture2D tex = TextureAssets.Npc[Type].Value;
        Vector2 scale = new(1 + .018f * MathF.Sin(t * 3), 1 - .018f * MathF.Sin(t * 3));
        batch.Draw(tex, at, null, Color.Lerp(drawColor, Color.White, .4f), NPC.rotation, tex.Size() / 2, scale, SpriteEffects.None, 0);
        if (VulnerableTicks > 0 || Stage == AscendantStage.Recovery && FinalActive)
        { CombatDrawing.Circle(batch, at, radius + 12, Color.White * .8f, 3); Caption(batch, at - new Vector2(0, tex.Height / 2f + 32), "Exposed"); }
        if (Stage == AscendantStage.Warning)
        {
            Vector2 bar = at - new Vector2(55, tex.Height / 2f + 16);
            CombatDrawing.Line(batch, bar, bar + new Vector2(110, 0), Color.Black, 7);
            CombatDrawing.Line(batch, bar, bar + new Vector2(110 * Math.Min(1, Timer / (FinalActive ? 120f : 75f)), 0), Tint, 4);
            Caption(batch, at - new Vector2(0, tex.Height / 2f + 42), FinalActive ? "Final" + Index : "Round" + Index);
            if (FinalActive) for (int i = 0; i < count; i++)
            { Vector2 sigil = at + new Vector2((i - (count - 1) / 2f) * 34, 125); AscendantGlyph.Draw(batch, sigil, i, 10, Tint); Utils.DrawBorderString(batch, (i + 1).ToString(), sigil + new Vector2(-4, 15), Color.White, .65f); }
        }
        if (Stage == AscendantStage.Choice)
        {
            DrawChoice(batch, Arena + new Vector2(-150, 0) - screenPos, ChoiceLeft);
            DrawChoice(batch, Arena + new Vector2(150, 0) - screenPos, ChoiceRight);
            Caption(batch, Arena - screenPos - new Vector2(0, 140), "Choose");
        }
        if (Index == 2 && TraceCount > 1 && Stage == AscendantStage.Attack && Timer <= 252)
        {
            for (int i = 1; i < TraceCount; i++) CombatDrawing.Line(batch, Trace[i - 1] - screenPos, Trace[i] - screenPos, Tint * .65f, 3);
            Caption(batch, NPC.Center - screenPos + new Vector2(0, 118), Timer <= 180 ? "Recording" : "Replay");
        }
        if (Index == 0 && Phase == 2 && !FinalActive && Stage == AscendantStage.Attack
            && (Timer >= 190 && Timer < 250 || Timer >= 290 && Timer < 350))
        {
            Vector2 end = Origin + (Aim - Origin).SafeNormalize(Vector2.UnitX) * 320;
            CombatDrawing.Line(batch, Origin - screenPos, end - screenPos, Tint * .12f, NPC.width);
            CombatDrawing.Line(batch, Origin - screenPos, end - screenPos, Tint * .8f, 3);
            CombatDrawing.Circle(batch, end - screenPos, NPC.width / 2, Tint * .7f, 2);
        }
        return false;
    }
    private void DrawChoice(SpriteBatch batch, Vector2 at, int law)
    {
        CombatDrawing.Circle(batch, at, 70, Tint * .75f, 3);
        AscendantGlyph.Draw(batch, at, law, 23, Tint);
        Caption(batch, at + new Vector2(0, 85), "Law" + law);
        Caption(batch, at + new Vector2(0, 105), "Law" + ((law + 1) % 5));
    }
    private void Caption(SpriteBatch batch, Vector2 at, string key)
        => Utils.DrawBorderString(batch, Language.GetTextValue("Mods.StarfallThrone.AscendantCombat." + key), at, Color.White, .72f, .5f);
}

[AutoloadBossHead] public sealed class AscendantBoss0 : AscendantBossNPC { public override int Index => 0; }
[AutoloadBossHead] public sealed class AscendantBoss1 : AscendantBossNPC { public override int Index => 1; }
[AutoloadBossHead] public sealed class AscendantBoss2 : AscendantBossNPC { public override int Index => 2; }
