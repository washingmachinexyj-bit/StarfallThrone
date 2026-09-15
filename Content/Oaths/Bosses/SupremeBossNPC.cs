#nullable enable
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

namespace StarfallThrone.Content.Oaths.Bosses;

public enum SupremeStage { Arrival, Assault, Recovery, Transition }

/// <summary>One life pool; server-owned schedules, immutable arena, serial-bound helpers.</summary>
public abstract partial class SupremeBossNPC : ModNPC
{
    public abstract int Index { get; }
    public const string Root = "StarfallThrone/Content/Assets/Oaths/";
    public override string Texture => Root + "SupremeBoss" + Index;
    public override string BossHeadTexture => Texture + "_Head_Boss";
    public bool Ready, Cancelled, VictoryRecorded;
    public int Serial, Age, Broken, Exposed;
    public Vector2 Arena, Aim;
    public Vector2[] History = new Vector2[180];
    public SupremeStage Stage => (SupremeStage)(int)NPC.ai[0];
    public int Timer => (int)NPC.ai[1];
    public int Round => (int)NPC.ai[2];
    public int Phase => (int)NPC.ai[3];
    public bool Authority => Main.netMode != NetmodeID.MultiplayerClient;
    public Color Tint => OathCatalog.Colors[Index];
    public static int Difficulty => Main.masterMode ? 2 : Main.expertMode ? 1 : 0;
    public static int LifeFor(int i, int mode, int players = 1) => (int)Math.Min(1_500_000_000d,
        OathCatalog.Life[i] * (mode == 2 ? 1.65d : mode == 1 ? 1.35d : 1d) * (1 + .3d * Math.Max(0, players - 1)));
    public static int FinalDamage(int damage, int mode) => (int)Math.Round(damage * (mode == 2 ? 1.3d : mode == 1 ? 1.15d : 1d));
    // Store the intended pre-defense damage, including our explicit difficulty multiplier.
    // Projectile.Damage doubles hostile damage in every mode; SupremeHazard cancels that
    // fixed factor in ModifyHitPlayer; the native damage fixture tests the complete hurt path.
    // Projectile.Damage applies Terraria's hostile multiplier (2/4/6 in classic/expert/master),
    // while SupremeHazard halves SourceDamage once to expose the catalogued final hit. The
    // stored value therefore compensates for both native layers exactly once.
    public static int EngineDamage(int damage, int mode) => Math.Max(1, FinalDamage(damage, mode) / (mode == 2 ? 3 : mode == 1 ? 2 : 1));
    public static int PhaseFor(int i, double ratio) => i == 0 ? ratio <= .10 ? 3 : ratio <= .35 ? 2 : ratio <= .70 ? 1 : 0
        : i == 1 ? ratio <= .15 ? 3 : ratio <= .40 ? 2 : ratio <= .75 ? 1 : 0
        : ratio <= .15 ? 3 : ratio <= .45 ? 2 : ratio <= .75 ? 1 : 0;
    public int AssaultLength => Index == 2 && Phase == 3 ? 720 : Phase == 3 ? 600 : 540;
    public bool Prerequisite => OathWorld.SupremeOpen(Index);
    private int[] shotAge = new int[Main.maxProjectiles], shotIdentity = new int[Main.maxProjectiles], shotOwner = new int[Main.maxProjectiles];
    private int[] itemAge = new int[Main.maxPlayers];
    private System.Collections.Generic.Dictionary<(int, int), int> staticAge = new();

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 1;
        NPCID.Sets.MPAllowedEnemies[Type] = true;
        NPCID.Sets.BossBestiaryPriority.Add(Type);
        NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
    }
    public override void SetDefaults()
    {
        NPC.width = 120; NPC.height = 150; NPC.aiStyle = -1; NPC.boss = true;
        NPC.noGravity = NPC.noTileCollide = true; NPC.knockBackResist = 0; NPC.npcSlots = 20;
        NPC.lifeMax = OathCatalog.Life[Index]; NPC.defense = OathCatalog.Defense[Index];
        NPC.damage = 0; NPC.value = Item.buyPrice(platinum: 8 + Index * 2);
        NPC.HitSound = SoundID.NPCHit4; NPC.DeathSound = SoundID.NPCDeath14;
        NPC.BossBar = ModContent.GetInstance<SupremeBossBar>();
        if (!Main.dedServ) Music = MusicID.LunarBoss;
        Ready = Cancelled = VictoryRecorded = false; Serial = Age = Broken = Exposed = 0;
        shotAge = new int[Main.maxProjectiles]; shotIdentity = new int[Main.maxProjectiles]; shotOwner = new int[Main.maxProjectiles];
        itemAge = new int[Main.maxPlayers]; staticAge = new(); History = new Vector2[180];
        Array.Fill(shotAge, -100000); Array.Fill(itemAge, -100000); Array.Fill(shotIdentity, int.MinValue);
        Array.Fill(shotOwner, -1); staticAge.Clear(); Array.Fill(History, Vector2.Zero);
    }
    public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
    { NPC.lifeMax = LifeFor(Index, Difficulty, numPlayers); NPC.life = NPC.lifeMax; }
    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry entry)
        => entry.Info.Add(new FlavorTextBestiaryInfoElement("Mods.StarfallThrone.OathsCombat.Bestiary" + Index));
    public override void BossLoot(ref int potionType) => potionType = ItemID.SuperHealingPotion;
    public override void ModifyNPCLoot(NPCLoot loot)
    {
        int ItemType(string name) => OathCatalog.Item(name + Index);
        loot.Add(ItemDropRule.BossBag(ItemType("SupremeBag")));
        var normal = new LeadingConditionRule(new Conditions.NotExpert());
        normal.OnSuccess(ItemDropRule.Common(ItemType("SupremeMaterial"), 1, 40, 55));
        normal.OnSuccess(ItemDropRule.OneFromOptions(1, OathCatalog.Weapon(Index * 4), OathCatalog.Weapon(Index * 4 + 1), OathCatalog.Weapon(Index * 4 + 2), OathCatalog.Weapon(Index * 4 + 3)));
        normal.OnSuccess(ItemDropRule.Common(ItemType("SupremeMask"), 7)); loot.Add(normal);
        loot.Add(ItemDropRule.Common(ItemType("SupremeTrophy"), 10));
        loot.Add(ItemDropRule.MasterModeCommonDrop(ItemType("SupremeRelic")));
        loot.Add(ItemDropRule.MasterModeDropOnAllPlayers(ItemType("SupremePetItem"), 4));
    }
    public override bool CheckActive() => false;
    public override bool CheckDead() { if (Ready && !Cancelled && Prerequisite) return true; Cancel(); return false; }
    public override bool PreKill() => Ready && !Cancelled && Prerequisite;
    public override void OnKill()
    {
        Cleanup();
        if (Authority && Ready && !Cancelled && !VictoryRecorded && Prerequisite)
        { VictoryRecorded = true; OathWorld.RecordSupreme(NPC, Index); }
    }
    public override bool CanHitPlayer(Player target, ref int cooldownSlot) => Ready && !Cancelled && Stage == SupremeStage.Assault && Timer >= 90;
    public override bool? CanBeHitByItem(Player player, Item item) => !Ready || Cancelled || !AcceptItem(player) ? false : null;
    public override bool? CanBeHitByProjectile(Projectile projectile) => !Ready || Cancelled || !AcceptProjectile(projectile) ? false : null;
    public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone) { StampItem(player); Credit(player.whoAmI); }
    public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone) { StampProjectile(projectile); Credit(projectile.owner); }
    public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
    {
        if (Index == 0 && Phase == 1 && Stage == SupremeStage.Assault)
            modifiers.FinalDamage *= .85f + .05f * System.Numerics.BitOperations.PopCount((uint)(Broken & 7));
        if (Exposed > 0 || Stage == SupremeStage.Recovery) modifiers.FinalDamage *= 1.15f;
    }
    public void Credit(int player)
    { if (player >= 0 && player < Main.maxPlayers) { NPC.playerInteraction[player] = true; NPC.lastInteraction = player; } }
    public bool AcceptItem(Player p) => Age - itemAge[p.whoAmI] >= Math.Max(10, p.itemAnimationMax);
    public void StampItem(Player p) => itemAge[p.whoAmI] = Age;
    public bool AcceptProjectile(Projectile p)
    {
        if (p.usesIDStaticNPCImmunity && staticAge.TryGetValue((p.owner, p.type), out int stamp) && Age - stamp < Math.Max(1, p.idStaticNPCHitCooldown)) return false;
        int s = p.whoAmI; if (s < 0 || s >= shotAge.Length || shotIdentity[s] != p.identity || shotOwner[s] != p.owner) return true;
        int cooldown = p.usesLocalNPCImmunity ? p.localNPCHitCooldown < 0 ? int.MaxValue : Math.Max(1, p.localNPCHitCooldown) : 10;
        return Age - shotAge[s] >= cooldown;
    }
    public void StampProjectile(Projectile p)
    {
        if (p.usesIDStaticNPCImmunity) staticAge[(p.owner, p.type)] = Age;
        int s = p.whoAmI; if (s < 0 || s >= shotAge.Length) return;
        shotAge[s] = Age; shotIdentity[s] = p.identity; shotOwner[s] = p.owner;
    }
    public static Vector2 Point(Vector2 p) => new(Math.Clamp(float.IsFinite(p.X) ? p.X : 400, 160, Math.Max(161, Main.maxTilesX * 16 - 160)),
        Math.Clamp(float.IsFinite(p.Y) ? p.Y : 400, 160, Math.Max(161, Main.maxTilesY * 16 - 160)));
    public override void AI()
    {
        if (Cancelled) return;
        if (Authority && !Prerequisite) { Cancel(); return; }
        if (Authority)
            foreach (NPC other in Main.ActiveNPCs)
                if (other.boss && other.whoAmI != NPC.whoAmI) { Cancel(); return; }
        NPC.TargetClosest(false);
        if (!NPC.HasValidTarget || NPC.Distance(Main.player[NPC.target].Center) > 5000) { if (Authority) Cancel(); return; }
        Player target = Main.player[NPC.target];
        if (!Ready)
        {
            if (!Authority) return;
            Ready = true; Serial = Main.rand.Next(1, int.MaxValue); Arena = Aim = Point(target.Center);
            Array.Fill(History, Aim); NPC.netUpdate = true;
        }
        NPC.timeLeft = 600; Age++; if (Exposed > 0) Exposed--;
        for (int n = History.Length - 1; n > 0; n--) History[n] = History[n - 1]; History[0] = Point(target.Center);
        int phase = PhaseFor(Index, NPC.life / (double)NPC.lifeMax);
        if (Authority && phase > Phase) { NPC.ai[3] = phase; Cleanup(); Enter(SupremeStage.Transition); }
        NPC.damage = Stage == SupremeStage.Assault && Timer >= 90 ? FinalDamage(OathCatalog.Contact[Index], Difficulty) : 0;
        Vector2 destination = Arena + new Vector2(MathF.Sin(Age * .006f) * 180, -220 + MathF.Sin(Age * .01f) * 45);
        NPC.velocity = Vector2.Lerp(NPC.velocity, (destination - NPC.Center).SafeNormalize(Vector2.Zero) * Math.Min(9, NPC.Distance(destination) * .04f), .08f);
        if (Stage is SupremeStage.Arrival or SupremeStage.Transition)
        { if (Authority && Timer >= 120) { Broken = 0; Enter(SupremeStage.Assault); SpawnObjectives(); } }
        else if (Stage == SupremeStage.Assault)
        {
            if (Authority) Perform(target);
            if (Authority && Timer >= AssaultLength) { Cleanup(); Enter(SupremeStage.Recovery); }
        }
        else if (Authority && Timer >= 150) { NPC.ai[2]++; Broken = 0; Enter(SupremeStage.Assault); SpawnObjectives(); }
        NPC.velocity = Point(NPC.Center + NPC.velocity) - NPC.Center;
        NPC.ai[1]++; if (Authority && Age % 60 == 0) NPC.netUpdate = true;
    }
    public void Enter(SupremeStage stage) { if (!Authority) return; NPC.ai[0] = (int)stage; NPC.ai[1] = 0; NPC.netUpdate = true; }
    public bool IsBroken(int mark) => mark >= 0 && mark < 6 && (Broken & (1 << mark)) != 0;
    public void Break(int mark)
    {
        if (!Authority || IsBroken(mark) || mark < 0 || mark > 5) return;
        Broken |= 1 << mark; Exposed = 240; ClearHazards(Index == 1 ? -1 : mark); NPC.netUpdate = true;
    }
    public void Cancel()
    {
        if (!Authority) return;
        Cancelled = true; Cleanup(); NPC.active = false; NPC.life = Math.Max(1, NPC.life);
        if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
    }
    public void ClearHazards(int mark = -1)
    {
        if (!Authority) return;
        foreach (Projectile p in Main.ActiveProjectiles)
            if (p.ModProjectile is SupremeHazard h && h.OwnedBy(this) && (mark < 0 || h.Mark == mark)) p.Kill();
    }
    public void Cleanup()
    {
        if (!Authority) return; ClearHazards();
        foreach (NPC n in Main.ActiveNPCs) if (n.ModNPC is SupremeNode node && node.OwnedBy(this))
        { n.active = false; if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: n.whoAmI); }
    }
    public override void SendExtraAI(BinaryWriter w)
    { w.Write(Ready); w.Write(Cancelled); w.Write(Serial); w.Write(Age); w.Write(Broken); w.Write(Exposed); w.Write(NPC.lifeMax); Write(w, Arena); Write(w, Aim); }
    public override void ReceiveExtraAI(BinaryReader r)
    {
        // Stage and round are native ai[]. History is only consumed on the server; hazards sync frozen paths.
        bool ready = r.ReadBoolean(), cancelled = r.ReadBoolean(); int serial = r.ReadInt32(), age = r.ReadInt32(), broken = r.ReadInt32(), exposed = r.ReadInt32(), life = r.ReadInt32();
        Vector2 arena = Read(r), aim = Read(r);
        Ready = ready; Cancelled = cancelled; Serial = serial; Age = Math.Max(0, age); Broken = broken & 63;
        Exposed = Math.Clamp(exposed, 0, 600); NPC.lifeMax = Math.Clamp(life, 1, 1_500_000_000); Arena = Point(arena); Aim = Point(aim);
    }
    internal static void Write(BinaryWriter w, Vector2 p) { w.Write(p.X); w.Write(p.Y); }
    internal static Vector2 Read(BinaryReader r) => new(r.ReadSingle(), r.ReadSingle());
    public override bool? DrawHealthBar(byte position, ref float scale, ref Vector2 at) => false;
    public override bool PreDraw(SpriteBatch batch, Vector2 screenPos, Color drawColor)
    {
        Texture2D tex = TextureAssets.Npc[Type].Value; Vector2 at = NPC.Center - screenPos;
        float size = Index == 2 && Phase == 3 ? .45f : 1f;
        for (int j = 3; j > 0; j--) batch.Draw(tex, at - NPC.velocity * j * 2, null, Tint * (.055f * j), 0, tex.Size() / 2, size, SpriteEffects.None, 0);
        batch.Draw(tex, at, null, Color.White * .94f, MathF.Sin(Age * .008f) * .025f, tex.Size() / 2, size, SpriteEffects.None, 0);
        CombatDrawing.Circle(batch, at, 74, Tint * .65f, 2); // Explicit collision core; decorative wings do not collide.
        if (Exposed > 0 || Stage == SupremeStage.Recovery) CombatDrawing.Circle(batch, at, 90, Color.White * .7f, 3);
        string key = Stage == SupremeStage.Recovery ? "Recovery" : "Phase" + Index + "_" + Phase;
        Utils.DrawBorderString(batch, Language.GetTextValue("Mods.StarfallThrone.OathsCombat." + key), at - new Vector2(0, 260), Tint, .75f, .5f);
        if (Index == 1 && Phase == 1 && Stage == SupremeStage.Assault)
            Utils.DrawBorderString(batch, Language.GetTextValue("Mods.StarfallThrone.OathsCombat.Law" + Round % 4), at - new Vector2(0, 232), Color.White, .65f, .5f);
        return false;
    }
}
[AutoloadBossHead] public sealed class SupremeBoss0 : SupremeBossNPC { public override int Index => 0; }
[AutoloadBossHead] public sealed class SupremeBoss1 : SupremeBossNPC { public override int Index => 1; }
[AutoloadBossHead] public sealed class SupremeBoss2 : SupremeBossNPC { public override int Index => 2; }
