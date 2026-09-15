using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent.ItemDropRules;
using StarfallThrone.Content.Pantheon;
using StarfallThrone.Content.Divine;
using StarfallThrone.Content.Ascendant;

namespace StarfallThrone.Content.Oaths.Bosses;

/// <summary>Callable by main after content/loot registration. No graphics device, filesystem writes,
/// terrain edits, mining unlocks or legitimate victory calls. Run only in an explicitly isolated process.</summary>
public static class CombatValidation
{
    private static int checks;
    private static void Check(bool ok, string message)
    { checks++; if (!ok) throw new InvalidOperationException("Oaths combat: " + message); }
    private static void Log(string message) => ModContent.GetInstance<OathWorld>().Mod.Logger.Info(message);
    private static void Guard()
    {
        bool isolated = Main.gameMenu && Terraria.Program.LaunchParameters.ContainsKey("-starfall-art-smoke")
            || Main.dedServ && Terraria.Program.LaunchParameters.ContainsKey("-testservermodloading");
        Check(isolated, "Run requires isolated art-smoke menu or testservermodloading process");
    }
    public static void Data()
    {
        int mode = Main.GameMode; checks = 0;
        try
        {
            Check(ModContent.GetInstance<OathWorld>().Mod.GetContent<ModNPC>().Count(n => n is SupremeBossNPC) == 3, "three registered supreme bosses");
            for (int d = 0; d < 3; d++) for (int i = 0; i < 3; i++)
            {
                Main.GameMode = d; NPC n = new(); n.SetDefaults(OathCatalog.Boss(i));
                Check(n.ModNPC is SupremeBossNPC b && b.Index == i && n.boss, "native boss classification");
                var boss = (SupremeBossNPC)n.ModNPC; boss.ApplyDifficultyAndPlayerScaling(1, 100, 100);
                Check(n.lifeMax == SupremeBossNPC.LifeFor(i, d) && n.life == n.lifeMax, "final health independent of double engine balance " + i + "/" + d);
                Check(n.defense == OathCatalog.Defense[i] && n.BossBar is SupremeBossBar, "native defense and bar");
                if (!Main.dedServ) Check(n.GetBossHeadTextureIndex() >= 0, "native head slot");
                boss.ApplyDifficultyAndPlayerScaling(255, 1, 1);
                Check(n.lifeMax == 1500000000 && n.lifeMax > 0, "255-player health overflow cap");
                var rates = new List<DropRateInfo>();
                foreach (IItemDropRule rule in Main.ItemDropsDB.GetRulesForNPCID(n.type, false)) rule.ReportDroprates(rates, new DropRateInfoChainFeed(1));
                Check(rates.Any(r => r.itemId == OathCatalog.Material(i) && r.stackMin == 40 && r.stackMax == 55), "normal materials registered");
                Check(rates.Any(r => r.itemId == OathCatalog.Item("SupremeBag" + i)), "expert bag rule");
                Check(rates.Any(r => r.itemId == OathCatalog.Item("SupremeRelic" + i)), "master relic rule");
                Check(Enumerable.Range(0, 4).All(c => rates.Any(r => r.itemId == OathCatalog.Weapon(i * 4 + c))), "four-class drop choices");
            }
            for (int i = 0; i < 3; i++)
            {
                Check(SupremeBossNPC.PhaseFor(i, 1) == 0 && SupremeBossNPC.PhaseFor(i, .05) == 3, "first/final phases");
                Check(SupremeBossNPC.PhaseFor(i, .6) == 1 && SupremeBossNPC.PhaseFor(i, .25) == 2, "middle phases");
            }
            Check(!SupremeHazard.RingHit(new Rectangle(192, -8, 16, 16), Vector2.Zero, 200, 0, 1.15f, 14), "safe wedge has no collision");
            Check(SupremeHazard.RingHit(new Rectangle(-208, -8, 16, 16), Vector2.Zero, 200, 0, 1.15f, 14), "solid ring arc collides");
            Check(!SupremeHazard.RingHit(new Rectangle(-8, -8, 16, 16), Vector2.Zero, 200, 0, 1.15f, 14), "ring interior is not a filled disk");
        }
        finally { Main.GameMode = mode; }
        Log("OATHS_COMBAT_DATA_PASS checks=" + checks + " bosses=3 modes=3 nativeBars=true damageVerification=runtimeFixture");
    }
    public static void Run()
    {
        Guard(); checks = 0;
        NPC[] npcs = (NPC[])Main.npc.Clone(); Projectile[] shots = (Projectile[])Main.projectile.Clone(); Player[] players = (Player[])Main.player.Clone();
        bool[] seeds = OathWorld.SeedWon, supreme = OathWorld.SupremeWon, pantheon = PantheonWorld.Downed, divine = DivineWorld.Downed, ascendant = AscendantWorld.Downed;
        int mode = Main.GameMode, net = Main.netMode, my = Main.myPlayer;
        var world = Main.ActiveWorldFileData;
        try
        {
            Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0;
            Main.ActiveWorldFileData = new Terraria.IO.WorldFileData("", false);
            OathWorld.SeedWon = new[] { true, true, true }; OathWorld.SupremeWon = new bool[3];
            DivineWorld.Downed = new[] { true, true, true }; AscendantWorld.Downed = new[] { true, true, true };
            PantheonWorld.Downed = new bool[17]; PantheonWorld.Downed[16] = true;
            for (int i = 0; i < Main.maxPlayers; i++) Main.player[i] = new Player { whoAmI = i, active = false };
            Player p = Main.player[0]; p.active = true; p.statLife = p.statLifeMax = p.statLifeMax2 = 10000;
            p.width = 20; p.height = 42; p.Center = SupremeBossNPC.Point(new Vector2(5600, 2000));
            for (int id = 0; id < 3; id++)
            {
                var shapes = new HashSet<SupremeShape>(); var laws = new HashSet<int>();
                for (int d = 0; d < 3; d++) for (int phase = 0; phase < 4; phase++)
                {
                    Main.GameMode = d; ResetActors(); SupremeBossNPC b = Spawn(id, p, phase);
                    int ticks = id == 1 && phase == 1 ? 2900 : id == 2 && phase == 2 ? 2200 : 1500;
                    bool packet = false, recovery = false;
                    for (int t = 0; t < ticks; t++)
                    {
                        p.Center = b.Arena + new Vector2(MathF.Sin(t * .014f) * 230, MathF.Sin(t * .023f) * 110);
                        b.AI(); b.NPC.position += b.NPC.velocity;
                        Check(b.NPC.active && !b.Cancelled && float.IsFinite(b.NPC.Center.X + b.NPC.Center.Y), "finite active AI " + id + "/" + phase);
                        if (b.Stage == SupremeStage.Recovery) recovery = true;
                        if (id == 1 && phase == 1 && b.Stage == SupremeStage.Assault) laws.Add(b.Round % 4);
                        int contactSlot = 0;
                        if (b.Stage != SupremeStage.Assault || b.Timer < 90) Check(!b.CanHitPlayer(p, ref contactSlot), "no contact during setup/recovery");
                        foreach (NPC n in Main.ActiveNPCs) if (n.ModNPC is SupremeNode node) node.AI();
                        int count = 0, nodes = 0;
                        foreach (Projectile projectile in Main.ActiveProjectiles) if (projectile.ModProjectile is SupremeHazard h)
                        {
                            count++; shapes.Add(h.Shape);
                            Check(h.Delay >= 75 && h.Duration <= 600, "bounded duration and safe warning");
                            if (h.Age < h.Delay) Check(h.CanDamage() == false && h.Colliding(projectile.Hitbox, p.Hitbox) == false, "telegraph harmless");
                            if (!packet) { Network(b, h); packet = true; }
                            h.AI();
                        }
                        foreach (NPC n in Main.ActiveNPCs) if (n.ModNPC is SupremeNode node && node.OwnedBy(b)) nodes++;
                        Check(count <= 72 && nodes <= 3, "bounded encounter actors");
                    }
                    Check(packet && recovery, "phase network and recovery coverage");
                    // A phase transition owns cleanup; never kill unrelated projectiles.
                    if (phase < 3)
                    {
                        b.NPC.life = b.NPC.lifeMax / 20; b.AI();
                        Check(b.Stage == SupremeStage.Transition && !Main.projectile.Any(q => q.active && q.ModProjectile is SupremeHazard), "transition cleanup");
                    }
                    p.dead = true; b.AI(); p.dead = false;
                    Check(!b.NPC.active && !Main.npc.Any(n => n.active && n.ModNPC is SupremeNode) && !Main.projectile.Any(q => q.active && q.ModProjectile is SupremeHazard), "death cleanup");
                }
                Check(shapes.Count >= 4, "distinct shape coverage " + id);
                if (id == 1) Check(laws.Count == 4, "all four mutually exclusive laws");
                Log("OATHS_BOSS_AI_PASS index=" + id + " phases=4 difficulties=3 shapes=" + shapes.Count);
            }
            Main.GameMode = 0; Objectives(p); Geometry(p); IdentityAndAuthority(p); NativeProjectileDamage();
            Check(!OathWorld.SupremeWon.Any(x => x), "combat fixture never records victory or generates ore");
        }
        finally
        {
            Array.Copy(npcs, Main.npc, npcs.Length); Array.Copy(shots, Main.projectile, shots.Length); Array.Copy(players, Main.player, players.Length);
            OathWorld.SeedWon = seeds; OathWorld.SupremeWon = supreme; PantheonWorld.Downed = pantheon; DivineWorld.Downed = divine; AscendantWorld.Downed = ascendant;
            Main.GameMode = mode; Main.netMode = net; Main.myPlayer = my; Main.ActiveWorldFileData = world;
        }
        Log("OATHS_COMBAT_RUNTIME_PASS checks=" + checks + " scenarios=36 geometry=true conservation=true serialCleanup=true nativeProjectileDamage=true headlessSafe=true");
    }
    private static void ResetActors()
    {
        for (int i = 0; i < Main.maxNPCs; i++) Main.npc[i] = new NPC { whoAmI = i, active = false };
        for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i] = new Projectile { whoAmI = i, active = false };
    }
    private static SupremeBossNPC Spawn(int index, Player player, int phase)
    {
        NPC n = Main.npc[0]; n.SetDefaults(OathCatalog.Boss(index)); n.active = true; n.whoAmI = 0; n.target = player.whoAmI;
        var b = (SupremeBossNPC)n.ModNPC; b.ApplyDifficultyAndPlayerScaling(1, 1, 1);
        b.Ready = true; b.Serial = 9200 + index; b.Arena = b.Aim = SupremeBossNPC.Point(new Vector2(5600, 2000));
        player.Center = b.Arena; n.Center = b.Arena - new Vector2(0, 220);
        double ratio = phase == 0 ? .9 : phase == 1 ? .6 : phase == 2 ? .25 : .05;
        n.life = (int)(n.lifeMax * ratio); n.ai[3] = phase; n.ai[0] = (int)SupremeStage.Assault;
        Array.Fill(b.History, player.Center); b.SpawnObjectives(); return b;
    }
    private static void Network(SupremeBossNPC b, SupremeHazard h)
    {
        using var stream = new MemoryStream(); using var w = new BinaryWriter(stream);
        b.SendExtraAI(w); w.Flush(); stream.Position = 0;
        NPC n = new(); n.SetDefaults(OathCatalog.Boss(b.Index)); var copy = (SupremeBossNPC)n.ModNPC;
        using var r = new BinaryReader(stream); copy.ReceiveExtraAI(r);
        Check(copy.Serial == b.Serial && copy.Arena == b.Arena && copy.NPC.lifeMax == b.NPC.lifeMax, "boss extra AI round trip");
        using var stream2 = new MemoryStream(); using var w2 = new BinaryWriter(stream2);
        h.SendExtraAI(w2); w2.Flush(); stream2.Position = 0;
        Projectile p = new(); p.SetDefaults(ModContent.ProjectileType<SupremeHazard>()); p.ai[0] = h.Projectile.ai[0]; p.ai[2] = h.Projectile.ai[2];
        var hc = (SupremeHazard)p.ModProjectile; using var r2 = new BinaryReader(stream2); hc.ReceiveExtraAI(r2);
        Check(hc.Serial == h.Serial && hc.Delay == h.Delay && hc.Anchor == h.Anchor && hc.Gap == h.Gap, "hazard extra AI round trip");
        int serial = copy.Serial;
        try { using var shortStream = new MemoryStream(new byte[] { 1 }); using var shortReader = new BinaryReader(shortStream); copy.ReceiveExtraAI(shortReader); Check(false, "truncated packet accepted"); }
        catch (EndOfStreamException) { }
        Check(copy.Serial == serial && copy.Ready == b.Ready, "truncated packet has no partial mutation");
    }
    private static void Objectives(Player player)
    {
        ResetActors(); SupremeBossNPC b = Spawn(0, player, 1);
        SupremeNode[] nodes = Main.npc.Where(n => n.active && n.ModNPC is SupremeNode).Select(n => (SupremeNode)n.ModNPC).ToArray();
        Check(nodes.Length == 3, "three independent buds");
        Projectile p = Main.projectile[900]; p.active = true; p.whoAmI = 900; p.identity = 555; p.owner = 0; p.type = ProjectileID.WoodenArrowFriendly;
        p.usesLocalNPCImmunity = true; p.localNPCHitCooldown = 20;
        Check(nodes.All(n => n.CanBeHitByProjectile(p) != false) && b.CanBeHitByProjectile(p) != false, "initial shared attack accepted");
        int before = b.NPC.life;
        nodes[0].OnHitByProjectile(p, new NPC.HitInfo { Damage = 101 }, 101);
        Check(before - b.NPC.life == 50 && nodes[0].Progress == 101, "exact 50 percent transfer without defense/exposure reapplication");
        Check(nodes[1].CanBeHitByProjectile(p) == false && b.CanBeHitByProjectile(p) == false, "piercing cannot multiply through body and buds");
        Check(b.NPC.playerInteraction[0], "proxy contributor reaches native boss ledger");
        b.Age += 20; Check(b.AcceptProjectile(p), "native local immunity expires");
        nodes[0].Charge(1); Check(before - b.NPC.life == 51, "odd transfer remainder conserved");
        int goal = nodes[0].Goal; before = b.NPC.life; int progress = nodes[0].Progress;
        nodes[0].Charge(long.MaxValue);
        Check(before - b.NPC.life == (goal - progress) / 2 && b.IsBroken(0), "overkill capped to remaining bud budget");
        before = b.NPC.life; nodes[0].Charge(long.MaxValue); Check(before == b.NPC.life, "broken node cannot forward again");
        p.identity++; Check(b.AcceptProjectile(p), "recycled projectile identity is independent"); b.StampProjectile(p);
        p.owner = 1; Check(b.AcceptProjectile(p), "different projectile owner is independent");
        p.owner = 0; p.identity++; p.usesIDStaticNPCImmunity = true; p.idStaticNPCHitCooldown = 30; b.StampProjectile(p);
        Projectile sibling = new() { owner = 0, type = p.type, whoAmI = 901, identity = 123, usesIDStaticNPCImmunity = true, idStaticNPCHitCooldown = 30 };
        Check(!b.AcceptProjectile(sibling), "static type immunity shared across siblings");
        // Native server path: remote strikes arrive as bank health deltas, not OnHit callbacks.
        before = b.NPC.life; int mode = Main.netMode; Main.netMode = NetmodeID.Server;
        nodes[1].NPC.life -= 200; nodes[1].NPC.playerInteraction[1] = true; nodes[1].NPC.lastInteraction = 1; nodes[1].AI();
        Check(before - b.NPC.life == 100 && b.NPC.playerInteraction[1], "remote delta and credit forwarded once");
        before = b.NPC.life; nodes[1].AI(); Check(b.NPC.life == before, "remote delta bank resets"); Main.netMode = mode;
        ResetActors(); b = Spawn(1, player, 2); SupremeNode wing = Main.npc.Where(n => n.active).Select(n => n.ModNPC).OfType<SupremeNode>().Single();
        b.NPC.ai[1] = 45; b.Perform(player); Check(Main.projectile.Any(q => q.active), "wing attack spawned");
        wing.Charge(wing.Goal); Check(b.Exposed > 0 && !Main.projectile.Any(q => q.active), "wing breaks cancel existing attack");
        b.NPC.ai[1] = 205; b.Perform(player); Check(!Main.projectile.Any(q => q.active), "wing interrupt suppresses later attack events");
    }
    private static void Geometry(Player player)
    {
        ResetActors(); SupremeBossNPC b = Spawn(2, player, 0);
        var h = b.Hazard(SupremeShape.Beam, b.Arena, Vector2.Zero, 3000, 75, 30, 12, 300, 0);
        Check(h != null, "native beam spawn");
        Rectangle on = new((int)b.Arena.X + 100, (int)b.Arena.Y - 10, 20, 20);
        h.Age = 74; Check(h.Colliding(h.Projectile.Hitbox, on) == false, "beam before warning boundary");
        h.Age = 75; Check(h.Colliding(h.Projectile.Hitbox, on) == true, "beam after warning boundary");
        h.Age = 105; Check(h.Colliding(h.Projectile.Hitbox, on) == false, "beam after expiry");
        var wall = b.Hazard(SupremeShape.Wall, b.Arena, Vector2.Zero, 3000, 75, 30, 12, 600, 0, 220);
        wall.Age = 75;
        Check(wall.Colliding(wall.Projectile.Hitbox, new Rectangle((int)b.Arena.X - 10, (int)b.Arena.Y - 20, 20, 40)) == false, "wall passage is genuinely empty");
        Check(wall.Colliding(wall.Projectile.Hitbox, new Rectangle((int)b.Arena.X - 10, (int)b.Arena.Y + 200, 20, 40)) == true, "wall outside passage collides");
    }
    private static void NativeProjectileDamage()
    {
        int mode = Main.GameMode, net = Main.netMode, my = Main.myPlayer;
        bool menu = Main.gameMenu; Player savedPlayer = Main.player[0]; var random = Main.rand;
        MonoMod.RuntimeDetour.Hook textHook = null;
        try
        {
            // Only font/combat-text rendering is suppressed. No hurt, damage variation, collision,
            // immunity, difficulty, projectile or mod-player hook is replaced.
            if (Main.dedServ)
                textHook = new MonoMod.RuntimeDetour.Hook(typeof(CombatText).GetMethod("NewText",
                    new[] { typeof(Rectangle), typeof(Color), typeof(string), typeof(bool), typeof(bool) }),
                    (Func<Rectangle, Color, string, bool, bool, int>)((a, b, c, d, e) => 100));
            Main.gameMenu = false; Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0;
            for (int difficulty = 0; difficulty < 3; difficulty++)
            {
                Main.GameMode = difficulty; ResetActors();
                Player player = new() { whoAmI = 0, active = true, width = 20, height = 42 };
                Main.player[0] = player; player.ResetEffects();
                player.statDefense = Player.DefenseStat.Default; player.endurance = 0; player.Center = SupremeBossNPC.Point(new Vector2(5600, 2000));
                var boss = Spawn(2, player, 0);
                int cases = 0; double rawRatioSum = 0, normalizedRatioSum = 0;
                // These are independent approved budgets, not EngineDamage/FinalDamage helper output.
                int[] expected = difficulty == 0 ? new[] { 2200, 2800, 3300, 3600, 4300, 5000 }
                    : difficulty == 1 ? new[] { 2530, 3220, 3795, 4140, 4945, 5750 }
                    : new[] { 2860, 3640, 4290, 4680, 5590, 6500 };
                int[] classic = { 2200, 2800, 3300, 3600, 4300, 5000 };
                for (int budget = 0; budget < classic.Length; budget++)
                    foreach (SupremeShape shape in Enum.GetValues<SupremeShape>())
                        for (int sample = 0; sample < 4; sample++)
                        {
                            int seed = 9173 + budget * 101 + (int)shape * 17 + sample;
                            // A vanilla projectile supplies an independent engine baseline at the
                            // same stored damage. Re-seeding pairs native damage-variation samples.
                            int slot = Projectile.NewProjectile(boss.NPC.GetSource_FromAI(), player.Center, Vector2.Zero,
                                ProjectileID.DeathLaser, expected[budget], 0, 0);
                            Check(slot >= 0 && slot < Main.maxProjectiles, "native baseline allocated");
                            Projectile baseline = Main.projectile[slot];
                            baseline.hostile = true; baseline.friendly = false; baseline.Center = player.Center;
                            int raw = MeasureNativeDamage(baseline, player, seed);
                            baseline.active = false;
                            int nativeMultiplier = difficulty == 2 ? 6 : difficulty == 1 ? 4 : 2;
                            Check(raw >= expected[budget] * nativeMultiplier * .82 && raw <= expected[budget] * nativeMultiplier * 1.18,
                                "native hostile baseline mode=" + difficulty + " budget=" + expected[budget] + " observed=" + raw);

                            Vector2 anchor = player.Center;
                            float angle = 0, length = 160, gap = 1.15f;
                            if (shape == SupremeShape.Ring) { anchor -= new Vector2(80, 0); angle = MathHelper.Pi; length = 700; }
                            if (shape == SupremeShape.Beam) anchor -= new Vector2(60, 0);
                            if (shape == SupremeShape.Wall) { anchor -= new Vector2(0, 200); length = 650; gap = 220; }
                            var hazard = boss.Hazard(shape, anchor, Vector2.UnitX, classic[budget], 75, 120, 20, length, angle, gap);
                            Check(hazard != null, "native supreme damage case allocated");
                            hazard.Age = hazard.Delay - 1;
                            Check(MeasureNativeDamage(hazard.Projectile, player, seed) == 0,
                                "native warning causes zero damage " + difficulty + "/" + shape);
                            hazard.Age = hazard.Delay; hazard.Previous = hazard.Projectile.Center;
                            int hurt = MeasureNativeDamage(hazard.Projectile, player, seed);
                            Check(hurt >= expected[budget] * .82 && hurt <= expected[budget] * 1.18,
                                "native supreme final budget mode=" + difficulty + " shape=" + shape + " intended=" + expected[budget] + " observed=" + hurt);
                            // The one SourceDamage half-factor is paired against the same native
                            // engine result in every difficulty; this catches double compensation.
                            Check(Math.Abs(raw - hurt * nativeMultiplier) <= Math.Max(32, expected[budget] / 20),
                                "paired native hostile multiplier and one half factor mode=" + difficulty + " raw=" + raw + " actual=" + hurt);
                            rawRatioSum += raw / (double)expected[budget]; normalizedRatioSum += hurt / (double)expected[budget];
                            cases++;
                            hazard.Age = hazard.Delay + hazard.Duration;
                            Check(MeasureNativeDamage(hazard.Projectile, player, seed) == 0, "native expired hazard causes zero damage");
                            hazard.Projectile.active = false;
                        }
                Log("OATHS_NATIVE_PROJECTILE_DAMAGE_PASS mode=" + difficulty + " cases=" + cases
                    + " vanillaStoredToHurt=" + (rawRatioSum / cases).ToString("F4", System.Globalization.CultureInfo.InvariantCulture)
                    + " supremeBudgetToHurt=" + (normalizedRatioSum / cases).ToString("F4", System.Globalization.CultureInfo.InvariantCulture)
                    + " native=Projectile.Damage shapes=5 warningAndExpiryZero=true");
            }
        }
        finally
        {
            textHook?.Dispose(); Main.player[0] = savedPlayer; Main.GameMode = mode;
            Main.netMode = net; Main.myPlayer = my; Main.gameMenu = menu; Main.rand = random;
        }
    }
    private static int MeasureNativeDamage(Projectile projectile, Player player, int seed)
    {
        player.statLife = player.statLifeMax = player.statLifeMax2 = 100000;
        player.dead = player.ghost = player.immune = false; player.immuneTime = 0;
        player.statDefense = Player.DefenseStat.Default; player.endurance = 0; player.velocity = Vector2.Zero;
        Array.Clear(player.hurtCooldowns); Array.Clear(player.buffType); Array.Clear(player.buffTime);
        Main.rand = new Terraria.Utilities.UnifiedRandom(seed);
        projectile.Damage(); // Real native collision -> combined hooks -> Player.Hurt -> life subtraction.
        return 100000 - player.statLife;
    }
    private static void IdentityAndAuthority(Player player)
    {
        ResetActors(); SupremeBossNPC b = Spawn(0, player, 1);
        var h = b.Hazard(SupremeShape.Star, b.Arena, Vector2.UnitY, 2200);
        h.Serial++; h.AI(); Check(!h.Projectile.active, "stale projectile cannot bind recycled NPC slot");
        SupremeNode node = Main.npc.Where(n => n.active).Select(n => n.ModNPC).OfType<SupremeNode>().First(); node.Serial++; node.AI();
        Check(!node.NPC.active, "stale node cannot bind recycled slot");
        var foreign = Main.projectile[900]; foreign.active = true; foreign.type = ProjectileID.WoodenArrowFriendly;
        b.Cleanup(); Check(foreign.active, "cleanup preserves unrelated projectiles"); foreign.active = false;
        Main.netMode = NetmodeID.MultiplayerClient; b.NPC.ai[1] = 15;
        Check(b.Hazard(SupremeShape.Star, b.Arena, Vector2.UnitY, 2200) == null, "client cannot spawn hostile hazards");
        bool active = b.NPC.active; b.Cancel(); Check(b.NPC.active == active, "client cannot authoritatively cancel");
        Main.netMode = NetmodeID.SinglePlayer;
        OathWorld.SeedWon[0] = false; Check(!b.PreKill(), "ineligible encounter cannot produce loot"); b.AI();
        Check(b.Cancelled && !b.NPC.active, "revoked prerequisite cancels cleanly"); OathWorld.SeedWon[0] = true;
    }
}
