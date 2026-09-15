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
using global::StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Voyage.Bosses;

public enum VoyageStage { Approach, Telegraph, Attack, Recovery, Transition }

/// <summary>Synced encounter director; signatures and breakable anatomy live in separate files.</summary>
public abstract partial class VoyageBossNPC : ModNPC
{
    public abstract int BossIndex { get; }
    public override string Texture => VoyageCatalog.Root + "Boss" + BossIndex;
    public override string BossHeadTexture => Texture + "_Head_Boss";
    public VoyageStage Stage => (VoyageStage)(int)NPC.ai[0];
    public int Timer => (int)NPC.ai[1];
    public int Move => (int)NPC.ai[2] % 3;
    public int Phase => (int)NPC.ai[3];
    public Vector2 Aim, Origin, Arena;
    public Vector2[] Trace = new Vector2[8];
    public uint BrokenMask;
    public bool PartsReady;
    public int Stagger;
    public int Serial;
    public int LastAggregate;
    public float HealthScale = 1;
    public bool IsController => BossIndex is 2 or 8 or 10;
    public bool IsComposite => IsController || BossIndex == 16;
    public int AggregateLifeMaximum => (int)(VoyageCatalog.Life[BossIndex] * HealthScale);
    public bool IsServer => Main.netMode != NetmodeID.MultiplayerClient;
    public Color Tint => VoyageCatalog.Colors[BossIndex];
    public bool ContactActive => Stage == VoyageStage.Attack && Stagger == 0 && ContactMove && NPC.velocity.LengthSquared() > 9;
    public bool ContactMove => BossIndex switch
    {
        0 => Move == 1, 1 => Move == 2, 2 => Move != 1, 4 => Move == 2,
        5 => Move == 1, 10 => Move == 0, 11 => Move == 2 && Phase > 0,
        13 => Move == 0, _ => false
    };
    public int AggregateLife
    {
        get
        {
            if (!PartsReady) return AggregateLifeMaximum;
            int life = IsController ? 0 : Math.Max(0, NPC.life);
            if (IsComposite)
                foreach (NPC part in Main.ActiveNPCs)
                    if (part.ModNPC is VoyageDeviceNPC device && device.OwnedBy(this) && device.BudgetPart)
                        life += Math.Max(0, part.life);
            return Math.Min(AggregateLifeMaximum, life);
        }
    }

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 1;
        NPCID.Sets.MPAllowedEnemies[Type] = true;
        NPCID.Sets.BossBestiaryPriority.Add(Type);
        NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
    }
    public override void SetDefaults()
    {
        NPC.width = BossIndex is 6 or 16 ? 260 : BossIndex == 13 ? 210 : 160;
        NPC.height = BossIndex is 6 or 16 ? 280 : 140;
        NPC.lifeMax = VoyageCatalog.Life[BossIndex];
        NPC.damage = 530 + BossIndex * 14;
        NPC.defense = 110 + BossIndex * 5;
        NPC.knockBackResist = 0;
        NPC.boss = true;
        NPC.aiStyle = -1;
        NPC.noGravity = true;
        NPC.noTileCollide = true;
        NPC.npcSlots = 20;
        NPC.value = Item.buyPrice(platinum: 22 + BossIndex * 2);
        NPC.HitSound = SoundID.NPCHit4;
        NPC.DeathSound = SoundID.NPCDeath14;
        NPC.BossBar = ModContent.GetInstance<VoyageBossBar>();
        if (!Main.dedServ) Music = BossIndex == 16 ? MusicID.LunarBoss : BossIndex == 14 ? MusicID.EmpressOfLight : BossIndex == 13 ? MusicID.DukeFishron : BossIndex == 11 ? MusicID.Plantera : BossIndex is 4 or 7 ? MusicID.Boss2 : MusicID.Boss3;
        HealthScale = 1;
        PartsReady = false;
        Serial = Stagger = LastAggregate = 0;
        BrokenMask = 0;
        Aim = Origin = Arena = Vector2.Zero;
        Trace = new Vector2[8];
    }
    public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
    {
        // Explicit budget, not vanilla's implicit x2/x3 boss life and contact multiplier.
        HealthScale = balance * (Main.masterMode ? 1.25f : 1.12f);
        NPC.lifeMax = (int)(VoyageCatalog.Life[BossIndex] * HealthScale);
        NPC.life = NPC.lifeMax;
        NPC.damage = 530 + BossIndex * 14;
    }
    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry entry)
    {
        entry.Info.Add(new MoonLordPortraitBackgroundProviderBestiaryInfoElement());
        entry.Info.Add(new FlavorTextBestiaryInfoElement("Mods.StarfallThrone.VoyageBestiary." + VoyageCatalog.Keys[BossIndex]));
    }
    public override void BossLoot(ref int potionType) => potionType = ItemID.SuperHealingPotion;
    public override bool CanHitPlayer(Player target, ref int cooldownSlot) => !IsController && ContactActive;
    public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
    {
        if (Stage is VoyageStage.Recovery or VoyageStage.Transition || Stagger > 0) modifiers.FinalDamage *= 1.12f;
    }
    public override bool CheckDead()
    {
        if (IsController && AggregateLife > 0 || BossIndex == 16 && BudgetPartsAlive() > 0)
        {
            NPC.life = Math.Max(1, NPC.life);
            return false;
        }
        return true;
    }

    public override void AI()
    {
        if (!PartsReady)
        {
            if (!IsServer) return;
            NPC.TargetClosest();
            if (!NPC.HasValidTarget) return;
            // Spawn identity protects against projectiles surviving an NPC-slot reuse.
            Serial = Main.rand.Next(1, int.MaxValue);
            Arena = Main.player[NPC.target].Center;
            Aim = Arena;
            Origin = NPC.Center;
            for (int i = 0; i < Trace.Length; i++) Trace[i] = Arena;
            if (!InitializeAnatomy()) { NPC.velocity *= .8f; return; }
            PartsReady = true;
            NPC.netUpdate = true;
        }
        NPC.TargetClosest(false);
        if (!NPC.HasValidTarget || NPC.Distance(Main.player[NPC.target].Center) > 5000)
        {
            if (IsServer) Cleanup();
            NPC.dontTakeDamage = true;
            NPC.velocity = Vector2.Lerp(NPC.velocity, new Vector2(0, -15), .05f);
            NPC.timeLeft = Math.Min(NPC.timeLeft, 60);
            return;
        }
        NPC.timeLeft = Math.Max(NPC.timeLeft, 600);
        Player player = Main.player[NPC.target];
        int aggregate = AggregateLife;
        if (IsController)
        {
            NPC.dontTakeDamage = true;
            NPC.life = Math.Max(1, aggregate);
            if (IsServer && BudgetPartsAlive() == 0)
            {
                NPC.dontTakeDamage = false;
                NPC.life = 0;
                NPC.checkDead();
                return;
            }
        }
        else NPC.dontTakeDamage = BossIndex == 16 && BudgetPartsAlive() > 0;
        int expectedPhase = BossIndex == 16 ? (CityAlive ? 0 : BudgetPartsAlive() > 0 ? 1 : 2)
            : aggregate < AggregateLifeMaximum * .32f ? 2 : aggregate < AggregateLifeMaximum * .68f ? 1 : 0;
        if (IsServer && expectedPhase > Phase)
        {
            NPC.ai[3] = expectedPhase;
            ClearHazards();
            Enter(VoyageStage.Transition);
            if (Main.netMode == NetmodeID.SinglePlayer)
                Main.NewText(Language.GetTextValue("Mods.StarfallThrone.VoyageCombat.Phase" + expectedPhase), Tint);
            else Terraria.Chat.ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Mods.StarfallThrone.VoyageCombat.Phase" + expectedPhase), Tint);
        }
        if (IsServer && Timer % 15 == 0 && Stage is VoyageStage.Approach or VoyageStage.Telegraph)
        {
            Array.Copy(Trace, 1, Trace, 0, Trace.Length - 1);
            Trace[^1] = player.Center;
            NPC.netUpdate = true;
        }
        if (Stagger > 0) { Stagger--; NPC.velocity *= .9f; }
        else switch (Stage)
        {
            case VoyageStage.Approach:
                Approach(player);
                if (Timer >= 70) BeginTelegraph(player);
                break;
            case VoyageStage.Telegraph:
                NPC.velocity *= .85f;
                if (Timer >= WindupLength) Enter(VoyageStage.Attack);
                break;
            case VoyageStage.Attack:
                TickSignature(player);
                if (Timer >= AttackLength) Enter(VoyageStage.Recovery);
                break;
            case VoyageStage.Recovery:
                NPC.velocity *= .94f;
                if (Timer >= RecoveryLength)
                {
                    if (IsServer) NPC.ai[2]++;
                    Enter(VoyageStage.Approach);
                }
                break;
            case VoyageStage.Transition:
                NPC.velocity *= .9f;
                if (Timer >= 90) Enter(VoyageStage.Approach);
                break;
        }
        NPC.ai[1]++;
        NPC.spriteDirection = NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
        NPC.rotation = BossIndex is 2 or 10 or 13 ? NPC.velocity.X * .01f : NPC.velocity.X * .002f;
        if (IsServer && Math.Abs(aggregate - LastAggregate) > AggregateLifeMaximum / 100)
        {
            LastAggregate = aggregate;
            NPC.netUpdate = true;
        }
    }
    public int WindupLength => BossIndex == 16 && Phase == 2 ? 110 : 70;
    public int AttackLength => BossIndex switch { 0 => 150, 2 or 10 => 165, 6 => 200, 8 => 180, 13 => 180, 14 or 15 => 170, 16 => 220, _ => 150 };
    public int RecoveryLength => BossIndex == 16 && Phase == 2 ? 115 : Phase == 2 ? 50 : Phase == 1 ? 58 : 65;
    private void Enter(VoyageStage state)
    {
        if (!IsServer) return;
        NPC.ai[0] = (int)state;
        NPC.ai[1] = 0;
        NPC.netUpdate = true;
    }
    private void BeginTelegraph(Player player)
    {
        if (!IsServer) return;
        Aim = player.Center;
        Origin = NPC.Center;
        // The battlefield follows the player only BETWEEN committed attacks.
        Arena = Vector2.Lerp(Arena, player.Center, .35f);
        Enter(VoyageStage.Telegraph);
    }
    private void Approach(Player player)
    {
        float side = ((int)NPC.ai[2] & 1) == 0 ? -1 : 1;
        Vector2 offset = BossIndex switch
        {
            0 => new(side * 380, -60), 1 => new(side * 440, -200),
            2 or 10 => new(side * 620, -140), 4 => new(side * 430, -280),
            5 => new(0, -310), 6 => new(side * 600, 0),
            8 => new(0, -260), 11 => new(side * 320, -170),
            12 => new(0, -240), 13 => new(side * 550, -120),
            14 => new(0, -360), 16 => new(0, -300), _ => new(side * 320, -250)
        };
        Fly(player.Center + offset, BossIndex == 13 ? 15 : 11);
    }
    private void Fly(Vector2 target, float speed, float inertia = 24)
    {
        Vector2 wanted = (target - NPC.Center).SafeNormalize(Vector2.UnitY) * Math.Min(speed, NPC.Distance(target) * .07f);
        NPC.velocity = (NPC.velocity * (inertia - 1) + wanted) / inertia;
    }
    private void Rest() => NPC.velocity *= .9f;
    private void Dash(int start, int duration, float speed, Vector2 destination)
    {
        if (Timer == start) NPC.velocity = (destination - NPC.Center).SafeNormalize(Vector2.UnitX) * speed;
        if (Timer >= start + duration) NPC.velocity *= .86f;
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(PartsReady); writer.Write(Serial); writer.Write(HealthScale); writer.Write(BrokenMask); writer.Write(Stagger); writer.Write(NPC.lifeMax);
        WriteVector(writer, Aim); WriteVector(writer, Origin); WriteVector(writer, Arena);
        for (int i = 0; i < Trace.Length; i++) WriteVector(writer, Trace[i]);
    }
    public override void ReceiveExtraAI(BinaryReader reader)
    {
        PartsReady = reader.ReadBoolean(); Serial = reader.ReadInt32(); HealthScale = reader.ReadSingle(); BrokenMask = reader.ReadUInt32(); Stagger = reader.ReadInt32(); NPC.lifeMax = reader.ReadInt32();
        Aim = ReadVector(reader); Origin = ReadVector(reader); Arena = ReadVector(reader);
        for (int i = 0; i < Trace.Length; i++) Trace[i] = ReadVector(reader);
    }
    internal static void WriteVector(BinaryWriter writer, Vector2 v) { writer.Write(v.X); writer.Write(v.Y); }
    internal static Vector2 ReadVector(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle());

    public override void ModifyNPCLoot(NPCLoot loot)
    {
        loot.Add(ItemDropRule.Common(VoyageCatalog.Trophy(BossIndex), 10));
        var normal = new LeadingConditionRule(new Conditions.NotExpert());
        normal.OnSuccess(ItemDropRule.Common(VoyageCatalog.Material(BossIndex), 1, BossIndex == 16 ? 60 : 24, BossIndex == 16 ? 72 : 34));
        normal.OnSuccess(ItemDropRule.Common(VoyageCatalog.Core(BossIndex), 1, BossIndex == 16 ? 2 : 1, BossIndex == 16 ? 2 : 1));
        int first = BossIndex * 2;
        int[] weapons = BossIndex == 16 ? new[] { VoyageCatalog.Weapon(32), VoyageCatalog.Weapon(33), VoyageCatalog.Weapon(34), VoyageCatalog.Weapon(35) }
            : new[] { VoyageCatalog.Weapon(first), VoyageCatalog.Weapon(first + 1) };
        normal.OnSuccess(ItemDropRule.OneFromOptions(1, weapons));
        normal.OnSuccess(ItemDropRule.Common(VoyageCatalog.Mask(BossIndex), 7));
        loot.Add(normal);
        loot.Add(ItemDropRule.BossBag(VoyageCatalog.Bag(BossIndex)));
        loot.Add(ItemDropRule.MasterModeCommonDrop(VoyageCatalog.Relic(BossIndex)));
    }
    public override void OnKill() { Cleanup(); VoyageWorld.SetDowned(BossIndex); }
    public void Cleanup()
    {
        if (!IsServer) return;
        ClearHazards();
        foreach (NPC part in Main.ActiveNPCs)
            if (part.ModNPC is VoyageDeviceNPC device && device.OwnedBy(this))
            {
                part.active = false;
                if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: part.whoAmI);
            }
    }
    public void ClearHazards()
    {
        if (!IsServer) return;
        foreach (Projectile p in Main.ActiveProjectiles)
            if (p.ModProjectile is VoyageHazard h && h.OwnerSlot == NPC.whoAmI && h.Serial == Serial) p.Kill();
    }
    public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false;
    public override bool PreDraw(SpriteBatch batch, Vector2 screenPos, Color drawColor)
    {
        if (!IsController)
        {
            // The array and the two cities are separate damageable anatomy, never duplicated on the torso.
            Texture2D tex = BossIndex == 16 ? ModContent.Request<Texture2D>(VoyageCatalog.Root + "ArkBody").Value : TextureAssets.Npc[Type].Value;
            float scale = Math.Min((float)NPC.width / tex.Width, (float)NPC.height / tex.Height) * 1.25f;
            Color color = NPC.dontTakeDamage ? Color.Lerp(drawColor, Tint, .35f) : Color.Lerp(drawColor, Color.White, .22f);
            batch.Draw(tex, NPC.Center - screenPos, null, color, NPC.rotation, tex.Size() / 2, scale, SpriteEffects.None, 0);
            if (BossIndex == 16 && BudgetPartsAlive() > 0)
                CombatDrawing.Circle(batch, NPC.Center - screenPos, 80, Tint * .45f, 3);
        }
        DrawTelegraphs(batch, screenPos);
        DrawSignatureHints(batch, screenPos);
        return false;
    }
    private void DrawSignatureHints(SpriteBatch batch, Vector2 screen)
    {
        bool showing = Stage is VoyageStage.Telegraph or VoyageStage.Attack;
        if (BossIndex == 3 && Move == 1 && showing)
        {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            for (int i = -1; i <= 1; i += 2)
            {
                Vector2 at = NPC.Center + new Vector2(i * 320, 35) - screen;
                batch.Draw(tex, at, null, Tint * .32f, 0, tex.Size() / 2, NPC.width / (float)tex.Width, SpriteEffects.None, 0);
                CombatDrawing.Circle(batch, at, 65, Tint * .3f, 2, Timer * .02f, .55f);
            }
        }
        if (BossIndex == 8 && showing)
        {
            NPC red = Part(0, VoyagePart.RedEye), blue = Part(1, VoyagePart.BlueEye);
            if (Move != 1)
            {
                NPC dashEye = red ?? blue;
                if (dashEye != null && (Stage == VoyageStage.Telegraph || Timer < 25))
                    CombatDrawing.Line(batch, dashEye.Center - screen, Aim - screen, new Color(255, 130, 100) * .6f, 3);
            }
            else if (red != null && blue != null)
                CombatDrawing.Line(batch, red.Center - screen, blue.Center - screen, Tint * .4f, 2);
        }
        if (BossIndex == 15 && Move == 2 && showing)
        {
            for (int i = -1; i <= 1; i += 2)
            {
                Vector2 p = Arena + new Vector2(i * 240, -100) - screen;
                // Hollow, disconnected false beacons cannot damage or punish the player.
                CombatDrawing.Circle(batch, p, 40, Tint * .24f, 2, Timer * .03f, .5f);
            }
        }
        if (BossIndex == 16 && Phase == 2 && Move == 2 && showing)
            for (int row = 0; row < 4; row++)
            {
                Vector2 from = Aim + new Vector2(MathF.Sin(row * .7f) * 140, -240 + row * 120);
                Vector2 to = Aim + new Vector2(MathF.Sin((row + 1) * .7f) * 140, -120 + row * 120);
                CombatDrawing.Line(batch, from - screen, to - screen, Color.LightGreen * .4f, 4);
            }
    }
    private void DrawTelegraphs(SpriteBatch batch, Vector2 screen)
    {
        if (Stage != VoyageStage.Telegraph) return;
        float pulse = .4f + .25f * MathF.Sin(Timer * .13f);
        CombatDrawing.Circle(batch, Aim - screen, 35, Tint * pulse, 3);
        if (ContactMove)
            CombatDrawing.Line(batch, Origin - screen, Origin + (Aim - Origin).SafeNormalize(Vector2.UnitX) * 1150 - screen, Tint * .42f, 3);
        if (BossIndex == 1 && Move == 0)
            for (int i = 1; i < Trace.Length; i++) CombatDrawing.Line(batch, Trace[i - 1] - screen, Trace[i] - screen, Tint * .7f, 3);
        Vector2 bar = NPC.Top - screen - new Vector2(50, 18);
        CombatDrawing.Line(batch, bar, bar + new Vector2(100, 0), Color.Black * .7f, 7);
        CombatDrawing.Line(batch, bar, bar + new Vector2(100 * Math.Min(1, Timer / (float)WindupLength), 0), Tint, 4);
    }
}

[AutoloadBossHead] public sealed class StarportSovereignNPC : VoyageBossNPC { public override int BossIndex => 0; }
[AutoloadBossHead] public sealed class FarwatcherNPC : VoyageBossNPC { public override int BossIndex => 1; }
[AutoloadBossHead] public sealed class BeltDevourerNPC : VoyageBossNPC { public override int BossIndex => 2; }
[AutoloadBossHead] public sealed class ConsensusBrainNPC : VoyageBossNPC { public override int BossIndex => 3; }
[AutoloadBossHead] public sealed class HiveCarrierNPC : VoyageBossNPC { public override int BossIndex => 4; }
[AutoloadBossHead] public sealed class OrbitalAnchorNPC : VoyageBossNPC { public override int BossIndex => 5; }
[AutoloadBossHead] public sealed class WarpBulwarkNPC : VoyageBossNPC { public override int BossIndex => 6; }
[AutoloadBossHead] public sealed class CrystalOrbitQueenNPC : VoyageBossNPC { public override int BossIndex => 7; }
[AutoloadBossHead] public sealed class BinaryHuntersNPC : VoyageBossNPC { public override int BossIndex => 8; }
[AutoloadBossHead] public sealed class FourArmTribunalNPC : VoyageBossNPC { public override int BossIndex => 9; }
[AutoloadBossHead] public sealed class SunpiercerFleetNPC : VoyageBossNPC { public override int BossIndex => 10; }
[AutoloadBossHead] public sealed class WorldseedNPC : VoyageBossNPC { public override int BossIndex => 11; }
[AutoloadBossHead] public sealed class PrecursorGuardianNPC : VoyageBossNPC { public override int BossIndex => 12; }
[AutoloadBossHead] public sealed class AetherLeviathanNPC : VoyageBossNPC { public override int BossIndex => 13; }
[AutoloadBossHead] public sealed class SolarSailEmpressNPC : VoyageBossNPC { public override int BossIndex => 14; }
[AutoloadBossHead] public sealed class StarNavigatorNPC : VoyageBossNPC { public override int BossIndex => 15; }
[AutoloadBossHead] public sealed class CelestialArkNPC : VoyageBossNPC { public override int BossIndex => 16; }
