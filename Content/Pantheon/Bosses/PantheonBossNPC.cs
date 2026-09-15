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

namespace StarfallThrone.Content.Pantheon.Bosses;

public enum PantheonStage { Approach, Attack, Recovery, Transition }

/// <summary>Each deity owns one health pool. Helpers are bound to slot AND encounter serial.</summary>
public abstract partial class PantheonBossNPC : ModNPC
{
    public abstract int Index { get; }
    public override string Texture => PantheonCatalog.Root + "Boss" + Index;
    public override string BossHeadTexture => Texture + "_Head_Boss";
    public bool Ready, Cancelled, VictoryRecorded, FinaleUsed, Finale;
    public int Serial, Age, Broken, Exposed, Chosen = -1, DashStart = -1;
    public Vector2 Arena, Aim, Origin, DashFrom, DashTo;
    public Vector2[] Spine = new Vector2[60];
    private int[] shotStamp = new int[Main.maxProjectiles];
    private int[] shotIdentity = new int[Main.maxProjectiles], shotOwner = new int[Main.maxProjectiles];
    private int[] meleeStamp = new int[Main.maxPlayers];
    public PantheonStage Stage => (PantheonStage)(int)NPC.ai[0];
    public int Timer => (int)NPC.ai[1];
    public int Round => (int)NPC.ai[2];
    public int Phase => (int)NPC.ai[3];
    public int Move => Round % 3;
    public bool Authority => Main.netMode != NetmodeID.MultiplayerClient;
    public Color Tint => PantheonCatalog.Colors[Index];
    public static int Difficulty => Main.masterMode ? 2 : Main.expertMode ? 1 : 0;
    public static float DamageFactor => Difficulty == 2 ? 1.65f : Difficulty == 1 ? 1.35f : 1;
    public static int LifeFor(int index, int difficulty) => (int)Math.Round(PantheonCatalog.Life[index] * (difficulty == 2 ? 1.9d : difficulty == 1 ? 1.5d : 1d));
    public int ContactDamage => (int)(PantheonCatalog.Damage[Index] * DamageFactor);
    public int ShotDamage(float factor = 1) => (int)(PantheonCatalog.Damage[Index] * DamageFactor * factor);
    public int EngineDamage(float factor = 1) => Math.Max(1, ShotDamage(factor) / (Difficulty == 2 ? 6 : Difficulty == 1 ? 4 : 2));
    public int ExpectedPhase => NPC.life < NPC.lifeMax * .3f ? 2 : NPC.life < NPC.lifeMax * .7f ? 1 : 0;
    public int AttackLength => Finale ? 900 : Index == 14 && Move == 2 ? 850 : Index == 16 ? 540 : 420;
    public bool Prerequisite => Index == 0 ? global::StarfallThrone.Content.Voyage.VoyageWorld.IsDowned(16) : PantheonWorld.IsDowned(Index - 1);

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 1;
        NPCID.Sets.MPAllowedEnemies[Type] = true;
        NPCID.Sets.BossBestiaryPriority.Add(Type);
        NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
    }
    public override void SetDefaults()
    {
        NPC.width = Index is 2 or 10 ? 104 : Index == 16 ? 150 : 112;
        NPC.height = Index is 2 or 10 ? 92 : Index == 6 ? 180 : Index == 16 ? 190 : 136;
        NPC.aiStyle = -1; NPC.boss = true; NPC.noGravity = NPC.noTileCollide = true;
        NPC.chaseable = Index != 8;
        NPC.knockBackResist = 0; NPC.npcSlots = 20; NPC.lifeMax = PantheonCatalog.Life[Index];
        NPC.damage = ContactDamage; NPC.defense = 180 + Index * 18;
        NPC.value = Item.buyPrice(platinum: 2 + Index / 3); NPC.HitSound = SoundID.NPCHit4; NPC.DeathSound = SoundID.NPCDeath14;
        NPC.BossBar = ModContent.GetInstance<PantheonBossBar>();
        if (!Main.dedServ) Music = Index is 2 or 6 or 10 ? MusicID.Boss2 : Index >= 14 ? MusicID.LunarBoss : MusicID.Boss5;
        Ready = Cancelled = VictoryRecorded = FinaleUsed = Finale = false;
        Serial = Age = Broken = Exposed = 0; Chosen = DashStart = -1;
        Arena = Aim = Origin = DashFrom = DashTo = Vector2.Zero;
        Spine = new Vector2[60]; shotStamp = new int[Main.maxProjectiles]; meleeStamp = new int[Main.maxPlayers];
        shotIdentity = new int[Main.maxProjectiles]; shotOwner = new int[Main.maxProjectiles];
        Array.Fill(shotStamp, -100); Array.Fill(meleeStamp, -100);
        Array.Fill(shotIdentity, int.MinValue); Array.Fill(shotOwner, -1);
    }
    public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
    {
        NPC.lifeMax = (int)Math.Min(1_500_000_000d, LifeFor(Index, Difficulty) * (double)balance);
        NPC.life = NPC.lifeMax; NPC.damage = ContactDamage;
    }
    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry entry)
        => entry.Info.Add(new FlavorTextBestiaryInfoElement("Mods.StarfallThrone.PantheonCombat.Bestiary" + Index));
    public override void BossLoot(ref int potionType) => potionType = ItemID.SuperHealingPotion;
    public override void ModifyNPCLoot(NPCLoot loot) => PantheonLoot.Add(loot, Index);
    public override bool CheckActive() => false;
    public override bool CheckDead() { if (!Cancelled && Prerequisite) return true; Cancel(); return false; }
    public override bool PreKill() => !Cancelled && Prerequisite;
    public override void OnKill()
    {
        Cleanup();
        if (Authority && !Cancelled && !VictoryRecorded && Prerequisite)
        { VictoryRecorded = true; PantheonLoot.OnVictory(NPC, Index); }
    }
    public override bool CanHitPlayer(Player target, ref int cooldownSlot)
        => Index != 8 && Ready && !Cancelled && Stage == PantheonStage.Attack && DashStart >= 0 && Timer >= DashStart + 75 && Timer < DashStart + 105;
    public override bool? CanBeHitByItem(Player player, Item item) => Index == 8 || !AcceptItem(player) ? false : null;
    public override bool? CanBeHitByProjectile(Projectile projectile) => Index == 8 || !AcceptProjectile(projectile) ? false : null;
    public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone) => StampItem(player);
    public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone) => StampProjectile(projectile);
    public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
    { if (Exposed > 0 || Stage == PantheonStage.Recovery) modifiers.FinalDamage *= 1.2f; }
    public static Vector2 Point(Vector2 v)
        => new(Math.Clamp(float.IsFinite(v.X) ? v.X : 100, 80, Math.Max(81, Main.maxTilesX * 16 - 80)),
            Math.Clamp(float.IsFinite(v.Y) ? v.Y : 100, 80, Math.Max(81, Main.maxTilesY * 16 - 80)));
    public override void AI()
    {
        if (Cancelled) return;
        if (Authority && !Prerequisite) { Cancel(); return; }
        if (Authority) foreach (NPC other in Main.ActiveNPCs)
            if (other.boss && other.whoAmI != NPC.whoAmI) { Cancel(); return; }
        NPC.TargetClosest(false);
        if (!NPC.HasValidTarget || NPC.Distance(Main.player[NPC.target].Center) > 4200)
        { if (Authority) Cancel(); return; }
        Player player = Main.player[NPC.target];
        if (!Ready)
        {
            if (!Authority) return;
            Ready = true; Serial = Main.rand.Next(1, int.MaxValue); Arena = Aim = Point(player.Center); Origin = NPC.Center;
            Array.Fill(Spine, NPC.Center); SpawnParts(); NPC.netUpdate = true;
        }
        NPC.timeLeft = 600; Age++; if (Exposed > 0) Exposed--;
        if (Authority && ExpectedPhase > Phase)
        { NPC.ai[3] = ExpectedPhase; Cleanup(false); Enter(PantheonStage.Transition); }
        if (Authority && Index == 16 && !FinaleUsed && NPC.life < NPC.lifeMax * .1f)
        { FinaleUsed = Finale = true; Cleanup(false); Enter(PantheonStage.Transition); }
        switch (Stage)
        {
            case PantheonStage.Approach:
                Fly(Point(player.Center + new Vector2(Index is 2 or 6 or 10 ? -340 : 0, Index == 6 ? 0 : -220)), 12);
                if (Timer >= 60 && Authority)
                {
                    Arena = Aim = Point(player.Center); Origin = NPC.Center; Broken = 0; Chosen = -1; DashStart = -1;
                    Cleanup(false); Enter(PantheonStage.Attack); CreateCounterplay();
                }
                break;
            case PantheonStage.Attack:
                NPC.velocity *= .91f;
                Perform(player);
                if (DashStart >= 0 && Timer >= DashStart + 75 && Timer < DashStart + 105)
                { NPC.Center = Vector2.Lerp(DashFrom, DashTo, (Timer - DashStart - 75) / 30f); NPC.velocity = Vector2.Zero; }
                if (Timer >= AttackLength && Authority)
                { Cleanup(false); Enter(PantheonStage.Recovery); }
                break;
            case PantheonStage.Recovery:
                NPC.velocity *= .9f;
                if (Timer >= (Finale ? 240 : 100) && Authority)
                { Finale = false; NPC.ai[2]++; Enter(PantheonStage.Approach); }
                break;
            case PantheonStage.Transition:
                NPC.velocity *= .85f;
                if (Timer >= 100) Enter(PantheonStage.Approach);
                break;
        }
        for (int i = Spine.Length - 1; i > 0; i--) Spine[i] = Spine[i - 1]; Spine[0] = NPC.Center;
        NPC.rotation = Math.Clamp(NPC.velocity.X * .012f, -.12f, .12f);
        NPC.velocity = Point(NPC.Center + NPC.velocity) - NPC.Center;
        NPC.ai[1]++; if (Authority && Age % 60 == 0) NPC.netUpdate = true;
    }
    public void Enter(PantheonStage stage)
    { if (!Authority) return; NPC.ai[0] = (int)stage; NPC.ai[1] = 0; DashStart = -1; NPC.netUpdate = true; }
    private void Fly(Vector2 to, float speed)
        => NPC.velocity = Vector2.Lerp(NPC.velocity, (to - NPC.Center).SafeNormalize(Vector2.Zero) * Math.Min(speed, NPC.Distance(to) * .075f), .12f);
    public bool IsBroken(int mark) => mark >= 0 && mark < 24 && (Broken & (1 << mark)) != 0;
    public void Break(int mark)
    {
        if (!Authority || Stage != PantheonStage.Attack || IsBroken(mark) || mark is < 0 or > 23) return;
        Broken |= 1 << mark; Exposed = Math.Max(Exposed, Index == 9 ? 360 : 240);
        if (Chosen < 0) Chosen = mark;
        ClearHazards(mark);
        if (Index == 7) ClearHazards((mark + 2) % 3);
        if (Index is 2 or 8 or 13) ClearHazards(20);
        if (Index == 15) ClearHazards((mark + 1) % 3);
        NPC.netUpdate = true;
    }
    // Deduplication happens on the native attack-owning side, where projectile identity is known.
    // Different owners, projectiles and recycled slots are independent; all accepted server deltas count.
    public bool AcceptProjectile(Projectile p)
    {
        int slot = p.whoAmI; if (PartCount == 0 || slot < 0 || slot >= shotStamp.Length) return true;
        if (shotIdentity[slot] != p.identity || shotOwner[slot] != p.owner) return true;
        int elapsed = Age - shotStamp[slot];
        int cooldown = p.usesLocalNPCImmunity ? (p.localNPCHitCooldown < 0 ? int.MaxValue : Math.Max(1, p.localNPCHitCooldown))
            : p.usesIDStaticNPCImmunity ? Math.Max(1, p.idStaticNPCHitCooldown) : 10;
        return elapsed < 0 || elapsed >= cooldown;
    }
    public void StampProjectile(Projectile p)
    {
        int slot = p.whoAmI; if (PartCount == 0 || slot < 0 || slot >= shotStamp.Length) return;
        shotStamp[slot] = Age; shotIdentity[slot] = p.identity; shotOwner[slot] = p.owner;
    }
    public bool AcceptItem(Player p) => PartCount == 0 || p.whoAmI < 0 || p.whoAmI >= meleeStamp.Length
        || Age - meleeStamp[p.whoAmI] < 0 || Age - meleeStamp[p.whoAmI] >= 10;
    public void StampItem(Player p)
    { if (PartCount > 0 && p.whoAmI >= 0 && p.whoAmI < meleeStamp.Length) meleeStamp[p.whoAmI] = Age; }
    public bool ForwardDamage(long amount)
    {
        if (!Authority || Cancelled || !NPC.active || amount <= 0) return false;
        // A first processed part can be lethal while another part still holds a same-frame hit.
        // Collect every owned part's contributors before cleanup/death, without replacing the
        // actual killing part's lastInteraction with a later unprocessed attacker's identity.
        int killer = NPC.lastInteraction;
        foreach (NPC helper in Main.ActiveNPCs)
            if (helper.ModNPC is PantheonNode node && node.OwnedBy(this))
            {
                for (int i = 0; i < Main.maxPlayers; i++)
                    if (helper.playerInteraction[i] || helper.lastInteraction == i) NPC.playerInteraction[i] = true;
                if ((killer < 0 || killer >= Main.maxPlayers) && helper.lastInteraction >= 0 && helper.lastInteraction < Main.maxPlayers)
                    killer = helper.lastInteraction;
            }
        NPC.lastInteraction = killer;
        long remaining = amount;
        while (remaining > 0 && NPC.active && NPC.life > 0)
        {
            int chunk = (int)Math.Min(remaining, 500_000_000L);
            NPC.SimpleStrikeNPC(chunk, 0, noPlayerInteraction: true); remaining -= chunk;
        }
        NPC.netUpdate = true;
        return true;
    }
    public void Cancel()
    {
        if (!Authority || Cancelled) return;
        Cancelled = true; Cleanup(); NPC.life = Math.Max(1, NPC.life); NPC.active = false;
        if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
    }
    public void ClearHazards(int mark = -1)
    {
        if (!Authority) return;
        foreach (Projectile p in Main.ActiveProjectiles)
            if (p.ModProjectile is PantheonHazard h && h.OwnedBy(this) && (mark < 0 || h.Mark == mark)) p.Kill();
    }
    public void Cleanup(bool parts = true)
    {
        if (!Authority) return; ClearHazards();
        foreach (NPC n in Main.ActiveNPCs)
            if (n.ModNPC is PantheonNode node && node.OwnedBy(this) && (parts || !node.Part))
            { n.active = false; if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: n.whoAmI); }
    }
    public override void SendExtraAI(BinaryWriter w)
    {
        w.Write(Ready); w.Write(Cancelled); w.Write(Serial); w.Write(NPC.lifeMax); w.Write(Age); w.Write(Broken); w.Write(Exposed); w.Write(Chosen);
        w.Write(FinaleUsed); w.Write(Finale); w.Write(DashStart); Write(w, Arena); Write(w, Aim); Write(w, Origin); Write(w, DashFrom); Write(w, DashTo);
        foreach (Vector2 v in Spine) Write(w, v);
    }
    public override void ReceiveExtraAI(BinaryReader r)
    {
        Ready = r.ReadBoolean(); Cancelled = r.ReadBoolean(); Serial = r.ReadInt32(); NPC.lifeMax = Math.Clamp(r.ReadInt32(), 1, 1_500_000_000);
        Age = Math.Max(0, r.ReadInt32()); Broken = r.ReadInt32() & 0xffffff; Exposed = Math.Clamp(r.ReadInt32(), 0, 600); Chosen = Math.Clamp(r.ReadInt32(), -1, 23);
        FinaleUsed = r.ReadBoolean(); Finale = r.ReadBoolean(); DashStart = Math.Clamp(r.ReadInt32(), -1, 1000);
        Arena = Point(Read(r)); Aim = Point(Read(r)); Origin = Point(Read(r)); DashFrom = Point(Read(r)); DashTo = Point(Read(r));
        for (int i = 0; i < Spine.Length; i++) Spine[i] = Point(Read(r));
    }
    internal static void Write(BinaryWriter w, Vector2 v) { w.Write(v.X); w.Write(v.Y); }
    internal static Vector2 Read(BinaryReader r) => new(r.ReadSingle(), r.ReadSingle());
    public override bool? DrawHealthBar(byte position, ref float scale, ref Vector2 at) => false;
    public override bool PreDraw(SpriteBatch batch, Vector2 screenPos, Color drawColor)
    {
        if (!Ready) return true;
        Texture2D texture = TextureAssets.Npc[Type].Value;
        Vector2 at = NPC.Center - screenPos;
        if (Index != 8)
        {
            Rectangle? source = Index == 2 ? new Rectangle(0, 28, 109, 101) : Index == 10 ? new Rectangle(0, 22, 111, 97) : null;
            Vector2 origin = source?.Size() / 2 ?? texture.Size() / 2;
            float scale = Index == 0 && Phase == 2 ? .68f : 1;
            Vector2 stretch = Index == 0 ? new(1 + MathF.Sin(Age * .06f) * .045f, 1 - MathF.Sin(Age * .06f) * .045f) : Vector2.One;
            for (int i = 3; i >= 1; i--)
                batch.Draw(texture, at - NPC.velocity * i * 1.4f + new Vector2(MathF.Sin(Age * .025f + i) * 3, 0), source,
                    Tint * (.035f * i), NPC.rotation, origin, stretch * scale, SpriteEffects.None, 0);
            batch.Draw(texture, at, source, Color.White * .94f, NPC.rotation, origin, stretch * scale, SpriteEffects.None, 0);
        }
        if (Index == 3 && Stage == PantheonStage.Attack)
            for (int i = 0; i < 3; i++)
            {
                Vector2 ghost = Arena + new Vector2((i - 1) * 250, -150 + MathF.Sin(Age * .025f + i) * 30) - screenPos;
                batch.Draw(texture, ghost, null, Tint * .18f, 0, texture.Size() / 2, .75f, SpriteEffects.None, 0);
            }
        if (Index == 16)
            for (int s = -1; s <= 1; s += 2)
                CombatDrawing.Line(batch, at + new Vector2(s * 85, 80), at + new Vector2(s * (Phase == 0 ? 140 : 195), 160), Tint * .6f, Phase == 0 ? 10 : 4);
        if (Exposed > 0 || Stage == PantheonStage.Recovery)
            CombatDrawing.Circle(batch, at, 45, Color.White * .65f, 2, Age * .025f, .45f);
        if (Stage == PantheonStage.Attack)
            Utils.DrawBorderString(batch, Language.GetTextValue("Mods.StarfallThrone.PantheonCombat.Move" + Index + "_" + Move), at + new Vector2(0, -155), Tint, .65f, .5f);
        return false;
    }
}

[AutoloadBossHead] public sealed class AelionNPC : PantheonBossNPC { public override int Index => 0; }
[AutoloadBossHead] public sealed class OphirNPC : PantheonBossNPC { public override int Index => 1; }
[AutoloadBossHead] public sealed class UrothNPC : PantheonBossNPC { public override int Index => 2; }
[AutoloadBossHead] public sealed class VelmoraNPC : PantheonBossNPC { public override int Index => 3; }
[AutoloadBossHead] public sealed class MelithNPC : PantheonBossNPC { public override int Index => 4; }
[AutoloadBossHead] public sealed class NerothNPC : PantheonBossNPC { public override int Index => 5; }
[AutoloadBossHead] public sealed class AcheronNPC : PantheonBossNPC { public override int Index => 6; }
[AutoloadBossHead] public sealed class IlythiaNPC : PantheonBossNPC { public override int Index => 7; }
[AutoloadBossHead] public sealed class EclipseDyadNPC : PantheonBossNPC { public override int Index => 8; }
[AutoloadBossHead] public sealed class ValtherNPC : PantheonBossNPC { public override int Index => 9; }
[AutoloadBossHead] public sealed class KhaldranNPC : PantheonBossNPC { public override int Index => 10; }
[AutoloadBossHead] public sealed class EphyraNPC : PantheonBossNPC { public override int Index => 11; }
[AutoloadBossHead] public sealed class OranthNPC : PantheonBossNPC { public override int Index => 12; }
[AutoloadBossHead] public sealed class ThalorNPC : PantheonBossNPC { public override int Index => 13; }
[AutoloadBossHead] public sealed class IriselleNPC : PantheonBossNPC { public override int Index => 14; }
[AutoloadBossHead] public sealed class AgnostosNPC : PantheonBossNPC { public override int Index => 15; }
[AutoloadBossHead] public sealed class AsterionNPC : PantheonBossNPC { public override int Index => 16; }
