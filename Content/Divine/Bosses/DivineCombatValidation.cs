using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Divine.Bosses;

/// <summary>Opt-in native engine fixtures in an isolated menu process; never run in a real world.</summary>
public static class DivineCombatValidation
{
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException("Divine combat: " + message); }
    private static void Log(string text) => ModContent.GetInstance<DivineWorld>().Mod.Logger.Info(text);
    public static void Data()
    {
        int mode = Main.GameMode;
        try
        {
            for (int d = 0; d < 3; d++)
            {
                Main.GameMode = d;
                for (int i = 0; i < 3; i++)
                {
                    NPC n = new(); n.SetDefaults(DivineCatalog.NPCType(i));
                    Check(n.ModNPC is DivineBossNPC b && b.Index == i && n.boss, "registered boss " + i);
                    var boss = (DivineBossNPC)n.ModNPC;
                    // Explicit single-player budget; the engine's shared-player balance is applied once.
                    boss.ApplyDifficultyAndPlayerScaling(1, 1, 1);
                    Check(n.lifeMax == DivineCatalog.Life(i, d) && n.damage == boss.ContactDamage, "difficulty budget " + i + "/" + d);
                    Check(n.defense == DivineCatalog.Defense[i] && n.BossBar is DivineBossBar && n.GetBossHeadTextureIndex() >= 0, "defense/bar/head " + i);
                    int multiplier = d == 0 ? 2 : d == 1 ? 4 : 6;
                    Check(boss.EngineProjectileDamage() * multiplier <= boss.ShotDamage() && boss.ShotDamage() - boss.EngineProjectileDamage() * multiplier < multiplier, "hostile multiplier rounding");
                    Check(boss.EngineProjectileDamage(true) * multiplier <= boss.ShotDamage(true), "heavy hostile multiplier");
                }
            }
        }
        finally { Main.GameMode = mode; }
        Log("DIVINE_COMBAT_DATA_PASS: 3 full bosses, native heads/bars, explicit normal/expert/master life/contact and 2/4/6 hostile projectile budgets.");
    }
    private static void ResetActors()
    {
        for (int i = 0; i < Main.npc.Length; i++) Main.npc[i] = new NPC { whoAmI = i };
        for (int i = 0; i < Main.projectile.Length; i++) Main.projectile[i] = new Projectile { whoAmI = i };
    }
    private static DivineBossNPC Spawn(int index, Player player, int phase = 0)
    {
        NPC n = new(); n.SetDefaults(DivineCatalog.NPCType(index)); n.whoAmI = 0; n.target = 0; n.active = true;
        n.Center = player.Center + new Vector2(-280, -200); Main.npc[0] = n;
        var b = (DivineBossNPC)n.ModNPC; b.AI();
        Check(b.Ready && n.active, "encounter initialization " + index);
        float ratio = phase == 0 ? 1 : phase == 1 ? .55f : phase == 2 ? .22f : .1f;
        n.life = Math.Max(1, (int)(n.lifeMax * ratio)); n.ai[3] = phase;
        return b;
    }
    private static void Tick(DivineBossNPC b, HashSet<DivineShape> shapes)
    {
        b.AI(); b.NPC.position += b.NPC.velocity;
        Check(float.IsFinite(b.NPC.Center.X) && float.IsFinite(b.NPC.Center.Y), "non-finite boss");
        foreach (NPC n in Main.ActiveNPCs) if (n.ModNPC is DivineBrazier brazier) brazier.AI();
        int count = 0;
        foreach (Projectile p in Main.ActiveProjectiles)
            if (p.ModProjectile is DivineHazard h)
            {
                count++; shapes.Add(h.Shape); h.AI();
                if (h.Age < h.Delay) Check(h.CanDamage() == false && h.Colliding(p.Hitbox, p.Hitbox) == false, "warning dealt damage");
                Check(!h.ShouldUpdatePosition(), "double projectile movement");
                if (--p.timeLeft <= 0) p.Kill();
                Check(float.IsFinite(p.Center.X) && float.IsFinite(p.Center.Y), "non-finite hazard");
            }
        Check(count <= (b.Index == 0 ? 18 : 28), "bounded active hazards");
    }
    public static void Run()
    {
        Check(Main.gameMenu && Environment.GetCommandLineArgs().Contains("-starfall-art-smoke"), "isolated smoke menu required");
        NPC[] oldNPCs = (NPC[])Main.npc.Clone(); Projectile[] oldShots = (Projectile[])Main.projectile.Clone();
        Player[] oldPlayers = (Player[])Main.player.Clone(); Item[] oldItems = (Item[])Main.item.Clone();
        int mode = Main.GameMode, net = Main.netMode, local = Main.myPlayer;
        bool slime = NPC.downedSlimeKing, hard = Main.hardMode, moon = NPC.downedMoonlord, day = Main.dayTime;
        bool[] downed = (bool[])DivineWorld.Downed.Clone(); Vector2 screen = Main.screenPosition;
        GraphicsDevice device = Main.instance.GraphicsDevice; var targets = device.GetRenderTargets(); Rectangle scissor = device.ScissorRectangle;
        using var atlas = new RenderTarget2D(device, 1440, 900);
        using var clip = new RasterizerState { ScissorTestEnable = true };
        try
        {
            Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0; NPC.downedSlimeKing = NPC.downedMoonlord = Main.hardMode = false;
            for (int i = 0; i < Main.maxPlayers; i++) Main.player[i] = new Player { whoAmI = i, active = false };
            Player player = Main.player[0]; player.active = true; player.position = new Vector2(5600, 2038); player.ResetEffects();
            for (int i = 0; i < Main.item.Length; i++) Main.item[i] = new Item();
            device.SetRenderTarget(atlas); device.Clear(new Color(15, 18, 29));
            for (int id = 0; id < 3; id++)
            {
                HashSet<int> moves = new(); HashSet<DivineShape> shapes = new(); bool captured = false;
                for (int difficulty = 0; difficulty < 3; difficulty++)
                    for (int phase = 0; phase < (id == 2 ? 4 : 3); phase++)
                    {
                        Main.GameMode = difficulty; ResetActors(); DivineBossNPC b = Spawn(id, player, phase);
                        Check(b.Participants[0] && !b.Damaged[0], "initial participant");
                        Main.player[1].active = true; Main.player[1].Center = player.Center + new Vector2(40, 0);
                        b.AI(); Check(b.Participants[1] && b.Damaged[1], "late-join no-hit exclusion"); Main.player[1].active = false;
                        HashSet<int> phaseMoves = new(); bool packet = false;
                        for (int tick = 0; tick < 2600; tick++)
                        {
                            player.Center = new Vector2(5600 + MathF.Sin(tick * .007f) * 110, 2059 + MathF.Sin(tick * .014f) * 30);
                            Tick(b, shapes);
                            if (b.Stage == DivineStage.Attack) { moves.Add(b.Move); phaseMoves.Add(b.Move); }
                            int cooldown = 0;
                            if (b.Stage != DivineStage.Attack) Check(!b.CanHitPlayer(player, ref cooldown), "contact during rest");
                            if (!packet && Main.projectile.FirstOrDefault(p => p.active && p.ModProjectile is DivineHazard h && h.OwnedBy(b))?.ModProjectile is DivineHazard shot)
                            { Network(b, shot); packet = true; }
                            int captureMove = id == 0 ? 2 : id == 1 ? 1 : 2;
                            if (!captured && difficulty == 0 && phase == 2 && b.Stage == DivineStage.Attack && b.Move == captureMove && b.Timer == (id == 2 ? 205 : 110))
                            { Draw(id, b, player, clip); captured = true; }
                        }
                        Check(phaseMoves.Count == (id == 2 ? 4 : 3) && packet, "phase move/network coverage " + id + "/" + phase);
                        if (id == 2 && phase > 0) Check(b.TraceCount >= 2, "recorded bounded trace");
                        // Dawn is not a hidden enrage or despawn trigger.
                        bool wasDay = Main.dayTime; Main.dayTime = !wasDay; b.AI(); Main.dayTime = wasDay;
                        Check(b.NPC.active && !b.Cancelled, "dawn cancellation");
                        b.NPC.ai[3] = 0; b.NPC.life = b.NPC.lifeMax / 10; b.AI();
                        Check(b.Stage == DivineStage.Transition && !Main.projectile.Any(p => p.active && p.ModProjectile is DivineHazard), "transition cleanup");
                        player.dead = true; b.AI(); player.dead = false;
                        Check(!b.NPC.active && !Main.npc.Any(n => n.active && n.ModNPC is DivineBrazier) && !Main.projectile.Any(p => p.active && p.ModProjectile is DivineHazard), "target-death cleanup");
                    }
                Check(captured && moves.Count == (id == 2 ? 4 : 3) && shapes.Count >= (id == 2 ? 3 : 4), "AI/render coverage " + id);
                Log($"DIVINE_BOSS_AI_PASS: {DivineCatalog.Keys[id]}; all phases/difficulties, distinct moves, warnings, participant rules, synchronized hazards, bounded arena and cleanup.");
            }
            Main.GameMode = 0; ResetActors(); DivineBossNPC native = Spawn(0, player);
            Geometry(native, player);
            Braziers(player);
            Boundaries(player);
            string path = Path.GetFullPath(Path.Combine(Main.SavePath, "..", "..", "outputs", "divine-combat-runtime.png"));
            using (var stream = File.Create(path)) atlas.SaveAsPng(stream, atlas.Width, atlas.Height);
            Log("DIVINE_COMBAT_RENDER_PASS: actual NPC/brazier/projectile drawing hooks captured in divine-combat-runtime.png.");
            Log("DIVINE_COMBAT_RUNTIME_PASS: native collision, telegraphs, trace replay, brazier progress/stagger, deadline PreKill/no loot, interruption, owner identity, client authority, network and cleanup.");
        }
        finally
        {
            for (int i = 0; i < oldNPCs.Length; i++) Main.npc[i] = oldNPCs[i];
            for (int i = 0; i < oldShots.Length; i++) Main.projectile[i] = oldShots[i];
            for (int i = 0; i < oldPlayers.Length; i++) Main.player[i] = oldPlayers[i];
            for (int i = 0; i < oldItems.Length; i++) Main.item[i] = oldItems[i];
            Main.GameMode = mode; Main.netMode = net; Main.myPlayer = local; Main.dayTime = day;
            NPC.downedSlimeKing = slime; NPC.downedMoonlord = moon; Main.hardMode = hard; DivineWorld.Downed = downed;
            Main.screenPosition = screen; device.SetRenderTargets(targets); device.ScissorRectangle = scissor;
        }
    }
    private static void Geometry(DivineBossNPC b, Player player)
    {
        Vector2 at = player.Center;
        Projectile p = b.Emit(DivineShape.Beam, at, Vector2.Zero, width: 20, length: 200);
        var h = (DivineHazard)p.ModProjectile;
        Rectangle hit = new((int)at.X + 90, (int)at.Y - 8, 16, 16), miss = new((int)at.X + 90, (int)at.Y + 70, 16, 16);
        Check(h.Colliding(p.Hitbox, hit) == false, "beam warning collision"); h.Age = h.Delay;
        Check(h.Colliding(p.Hitbox, hit) == true && h.Colliding(p.Hitbox, miss) == false, "native AABB beam hit geometry");
        p.Kill(); p = b.Emit(DivineShape.Pillar, at, Vector2.Zero, width: 30, length: 100); h = (DivineHazard)p.ModProjectile; h.Age = h.Delay;
        Check(h.Colliding(p.Hitbox, new Rectangle((int)at.X - 4, (int)at.Y - 90, 8, 8)) == true && h.Colliding(p.Hitbox, new Rectangle((int)at.X - 4, (int)at.Y + 12, 8, 8)) == false, "pillar geometry");
        p.Kill(); p = b.Emit(DivineShape.Ring, at, Vector2.Zero, duration: 100, length: 200, gap: .8f); h = (DivineHazard)p.ModProjectile; h.Age = h.Delay + 50;
        Check(h.Colliding(p.Hitbox, new Rectangle((int)at.X + 95, (int)at.Y - 5, 10, 10)) == false, "ring safe gap");
        Check(h.Colliding(p.Hitbox, new Rectangle((int)at.X - 105, (int)at.Y - 5, 10, 10)) == true, "ring visible arc");
        p.Kill(); b.TraceCount = 3; b.Trace[0] = at; b.Trace[1] = at + new Vector2(80, 0); b.Trace[2] = at + new Vector2(80, 80);
        p = b.SpawnTrace(60); h = (DivineHazard)p.ModProjectile; h.Age = h.Delay + 41; h.AI();
        Check(h.Count == 3 && Vector2.Distance(p.Center, b.Trace[1]) < 8, "native trace interpolation"); Network(b, h);
        h.Serial++; h.AI(); Check(!p.active, "slot reuse identity kills stale trace");
    }
    private static void Braziers(Player player)
    {
        ResetActors(); DivineBossNPC b = Spawn(1, player); b.NPC.ai[0] = (int)DivineStage.Attack; b.NPC.ai[1] = 1;
        DivineBrazier left = b.Brazier(0), right = b.Brazier(1);
        Check(left != null && right != null, "two braziers"); left.AI(); right.AI();
        Check(!left.NPC.dontTakeDamage && !right.NPC.dontTakeDamage, "open braziers accept attacks");
        left.OnHitByItem(player, new Item(ItemID.IronBroadsword), new NPC.HitInfo { Damage = 120 }, 120);
        Check(left.Progress == 120, "item-hit progress");
        left.OnHitByProjectile(new Projectile(), new NPC.HitInfo { Damage = 180 }, 180);
        Check(left.Progress == 300, "projectile-hit progress");
        left.Charge(left.Goal); Check(b.Stagger == 0, "one side cannot stagger"); right.Charge(right.Goal);
        Check(b.Stagger == 240 && !Main.projectile.Any(p => p.active && p.ModProjectile is DivineHazard), "two-sided stagger");
        for (int i = 0; i < 240; i++) b.AI();
        Check(b.Stagger == 0 && left.Progress == 0 && right.Progress == 0, "finite stagger reset");
        left.AI(); left.Charge(600); Check(left.Progress == 0 && left.NPC.dontTakeDamage, "closed brazier protected");
        b.NPC.ai[0] = (int)DivineStage.Attack; b.NPC.ai[1] = 1; left.AI();
        Projectile circle = b.Emit(DivineShape.Offering, player.Center, Vector2.Zero, delay: 30, duration: 150, width: 50, mark: 0);
        var offering = (DivineHazard)circle.ModProjectile;
        for (int i = 0; i < 90; i++) offering.AI();
        Check(left.Progress == 160 && offering.CanDamage() == false, "optional offering charge and non-damage");
        using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory, System.Text.Encoding.UTF8, true);
        left.SendExtraAI(writer); writer.Flush(); memory.Position = 0;
        NPC remote = new(); remote.SetDefaults(left.Type); remote.ai = (float[])left.NPC.ai.Clone(); var copy = (DivineBrazier)remote.ModNPC;
        copy.ReceiveExtraAI(new BinaryReader(memory)); Check(copy.Serial == left.Serial && copy.Progress == left.Progress, "brazier network");
        b.Cleanup(); Check(!Main.npc.Any(n => n.active && n.ModNPC is DivineBrazier), "brazier death cleanup");
    }
    private static void Boundaries(Player player)
    {
        for (int id = 0; id < 3; id++)
        {
            ResetActors(); DivineBossNPC b = Spawn(id, player); b.Emit(DivineShape.Drop, player.Center, Vector2.UnitY);
            if (id == 0) NPC.downedSlimeKing = true; else if (id == 1) Main.hardMode = true; else NPC.downedMoonlord = true;
            int items = Main.item.Count(i => i.active);
            b.NPC.NPCLoot(); // Actual NPCLoader.PreKill path, not just a direct boolean helper assertion.
            Check(Main.item.Count(i => i.active) == items && !b.NPC.active && b.Cancelled && !b.PreKill() && !b.CheckDead(), "closed window native no loot " + id);
            Check(!Main.projectile.Any(p => p.active && p.ModProjectile is DivineHazard) && !Main.npc.Any(n => n.active && n.ModNPC is DivineBrazier), "closed window cleanup");
            NPC.downedSlimeKing = NPC.downedMoonlord = Main.hardMode = false;
        }
        ResetActors(); DivineBossNPC boss = Spawn(0, player);
        NPC other = new(); other.SetDefaults(NPCID.KingSlime); other.whoAmI = 1; other.active = true; Main.npc[1] = other;
        boss.AI(); Check(boss.Cancelled && other.active, "another boss cancellation preserves other boss");
        ResetActors(); boss = Spawn(2, player);
        Main.netMode = NetmodeID.MultiplayerClient;
        Check(boss.Emit(DivineShape.Beam, player.Center, Vector2.Zero) == null, "client cannot emit hazards");
        Main.netMode = NetmodeID.SinglePlayer;
        Vector2 clamped = DivineBossNPC.WorldPoint(new Vector2(-100000, Main.maxTilesY * 16f + 100000), 400);
        Check(clamped.X >= 400 && clamped.Y <= Main.maxTilesY * 16f - 400, "world-edge clamp");
        Vector2 underworld = boss.ArenaPoint(new Vector2(5000, Main.maxTilesY * 16f - 200));
        Check(underworld.Y == Main.maxTilesY * 16f - 200, "arena does not lift Underworld into caves");
        boss.Cleanup();
    }
    private static void Network(DivineBossNPC b, DivineHazard h)
    {
        using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory, System.Text.Encoding.UTF8, true);
        b.SendExtraAI(writer); writer.Flush(); memory.Position = 0;
        NPC n = new(); n.SetDefaults(b.Type); n.ai = (float[])b.NPC.ai.Clone(); var copy = (DivineBossNPC)n.ModNPC; copy.ReceiveExtraAI(new BinaryReader(memory));
        Check(copy.Serial == b.Serial && copy.Ready == b.Ready && copy.Arena == b.Arena && copy.Aim == b.Aim && copy.TraceCount == b.TraceCount, "boss network");
        memory.SetLength(0); memory.Position = 0; h.SendExtraAI(writer); writer.Flush(); memory.Position = 0;
        Projectile p = new(); p.SetDefaults(h.Type); p.ai = (float[])h.Projectile.ai.Clone(); var remote = (DivineHazard)p.ModProjectile; remote.ReceiveExtraAI(new BinaryReader(memory));
        Check(remote.Serial == h.Serial && remote.Delay == h.Delay && remote.Duration == h.Duration && remote.Anchor == h.Anchor && remote.Launch == h.Launch && remote.Count == h.Count, "projectile network");
        for (int i = 0; i < h.Count; i++) Check(remote.Points[i] == h.Points[i], "trace points network");
    }
    private static void Draw(int row, DivineBossNPC b, Player player, RasterizerState clip)
    {
        // The high-hovering third god needs its own atlas camera: keep the outer clock rings
        // above the row edge while retaining the player and recorded trace below it.
        float renderScale = b.Index == 2 ? .52f : .72f;
        Main.screenPosition = b.Index == 2
            ? new Vector2(b.Arena.X - 1200, b.NPC.Center.Y - 180)
            : b.Arena - new Vector2(900, 330);
        Main.instance.GraphicsDevice.ScissorRectangle = new Rectangle(0, row * 300, 1440, 300);
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, clip, null, Matrix.CreateScale(renderScale) * Matrix.CreateTranslation(0, row * 300, 0));
        try
        {
            CombatDrawing.Circle(Main.spriteBatch, player.Center - Main.screenPosition, 16, Color.LightGreen, 3);
            b.PreDraw(Main.spriteBatch, Main.screenPosition, Color.White);
            foreach (NPC n in Main.ActiveNPCs) if (n.ModNPC is DivineBrazier brazier) brazier.PreDraw(Main.spriteBatch, Main.screenPosition, Color.White);
            foreach (Projectile p in Main.ActiveProjectiles) if (p.ModProjectile is DivineHazard h) { Color color = Color.White; h.PreDraw(ref color); }
        }
        finally { Main.spriteBatch.End(); }
        Main.spriteBatch.Begin();
        try { Utils.DrawBorderString(Main.spriteBatch, DivineCatalog.Names[b.Index], new Vector2(20, row * 300 + 270), Color.White, .85f); }
        finally { Main.spriteBatch.End(); }
    }
}
