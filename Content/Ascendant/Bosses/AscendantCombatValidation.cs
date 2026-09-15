using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.Divine;
using StarfallThrone.Content.Ecology;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Ascendant.Bosses;

/// <summary>Opt-in native smoke fixtures. Never call Run from a loaded gameplay world.</summary>
public static class AscendantCombatValidation
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Ascendant combat: " + message); }
    private static void Log(string message) => ModContent.GetInstance<AscendantWorld>().Mod.Logger.Info(message);
    public static void Data()
    {
        int saved = Main.GameMode;
        try
        {
            for (int difficulty = 0; difficulty < 3; difficulty++)
            {
                Main.GameMode = difficulty;
                for (int id = 0; id < 3; id++)
                {
                    NPC npc = new(); npc.SetDefaults(AscendantCatalog.NPCType(id));
                    Check(npc.ModNPC is AscendantBossNPC && npc.boss, "registered boss " + id);
                    var boss = (AscendantBossNPC)npc.ModNPC;
                    boss.ApplyDifficultyAndPlayerScaling(1, 1, 1);
                    Check(npc.lifeMax == AscendantCatalog.Life(id, difficulty), "difficulty life " + id + "/" + difficulty);
                    Check(npc.damage == boss.ContactDamage && npc.defense == AscendantCatalog.Defense[id], "contact/defense " + id);
                    Check(npc.BossBar is AscendantBossBar && npc.GetBossHeadTextureIndex() >= 0, "native bar/head " + id);
                    int factor = difficulty == 2 ? 6 : difficulty == 1 ? 4 : 2;
                    Check(boss.EngineProjectileDamage() * factor <= boss.ShotDamage() && boss.ShotDamage() - boss.EngineProjectileDamage() * factor < factor, "2/4/6 hostile damage budget");
                    Check(boss.EngineProjectileDamage(true) * factor <= boss.ShotDamage(true), "heavy hostile damage budget");
                    Check(boss.Participants.Length == Main.maxPlayers && boss.Damaged.Length == Main.maxPlayers, "participant arrays");
                }
            }
        }
        finally { Main.GameMode = saved; }
        Log("ASCENDANT_COMBAT_DATA_PASS: bosses=3 difficultyBudgets=9 nativeHeadsBars=true hostileMultipliers=2/4/6");
    }
    private static void ResetActors()
    {
        for (int i = 0; i < Main.npc.Length; i++) Main.npc[i] = new NPC { whoAmI = i };
        for (int i = 0; i < Main.projectile.Length; i++) Main.projectile[i] = new Projectile { whoAmI = i };
    }
    private static AscendantBossNPC Spawn(int id, Player player, int phase = 0, int round = 0)
    {
        NPC npc = new(); npc.SetDefaults(AscendantCatalog.NPCType(id)); npc.whoAmI = 0; npc.target = 0; npc.active = true;
        npc.Center = player.Center + new Vector2(-260, -180); Main.npc[0] = npc;
        var boss = (AscendantBossNPC)npc.ModNPC;
        boss.ApplyDifficultyAndPlayerScaling(1, 1, 1); boss.AI();
        Check(boss.Ready && npc.active && !boss.Cancelled, "initialized encounter " + id);
        npc.life = (int)(npc.lifeMax * (phase == 0 ? 1 : phase == 1 ? .55f : .22f)); npc.ai[3] = phase; npc.ai[2] = round;
        return boss;
    }
    private static void Tick(AscendantBossNPC boss, HashSet<AscendantShape> shapes)
    {
        boss.AI(); boss.NPC.position += boss.NPC.velocity;
        Check(float.IsFinite(boss.NPC.Center.X) && float.IsFinite(boss.NPC.Center.Y), "finite NPC position");
        int nodes = 0, projectiles = 0;
        foreach (NPC npc in Main.ActiveNPCs)
            if (npc.ModNPC is AscendantNode node)
            { nodes++; node.AI(); int cooldown = 0; Check(npc.damage == 0 && !node.CanHitPlayer(Main.player[0], ref cooldown), "harmless nodes"); }
        foreach (Projectile projectile in Main.ActiveProjectiles)
            if (projectile.ModProjectile is AscendantHazard hazard)
            {
                projectiles++; shapes.Add(hazard.Shape); hazard.AI();
                Check(!hazard.ShouldUpdatePosition(), "single analytical movement");
                if (hazard.Age < hazard.Delay) Check(hazard.CanDamage() == false && hazard.Colliding(projectile.Hitbox, projectile.Hitbox) == false, "warning cannot damage");
                Check(hazard.Delay >= 60, "at least one second local telegraph");
                Check(float.IsFinite(projectile.Center.X) && float.IsFinite(projectile.Center.Y), "finite projectile position");
                if (--projectile.timeLeft <= 0) projectile.Kill();
            }
        Check(nodes <= 4 && projectiles <= (boss.Index == 0 ? 26 : 34), "bounded helpers/hazards");
    }
    public static void Run()
    {
        Check(Main.gameMenu && Environment.GetCommandLineArgs().Contains("-starfall-art-smoke"), "isolated art-smoke menu required");
        NPC[] npcs = (NPC[])Main.npc.Clone(); Projectile[] projectiles = (Projectile[])Main.projectile.Clone();
        Player[] players = (Player[])Main.player.Clone(); Item[] items = (Item[])Main.item.Clone();
        bool[] mini = MiniBossWorld.Downed, divine = DivineWorld.Downed, ecology = EcologyWorld.Downed;
        int mode = Main.GameMode, net = Main.netMode, myPlayer = Main.myPlayer;
        bool slime = NPC.downedSlimeKing, hard = Main.hardMode, moon = NPC.downedMoonlord, day = Main.dayTime;
        Vector2 screen = Main.screenPosition;
        GraphicsDevice device = Main.instance.GraphicsDevice; var renderTargets = device.GetRenderTargets(); Rectangle scissor = device.ScissorRectangle;
        using var atlas = new RenderTarget2D(device, 1440, 1050);
        using var clip = new RasterizerState { ScissorTestEnable = true };
        try
        {
            Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0;
            NPC.downedSlimeKing = Main.hardMode = NPC.downedMoonlord = false;
            MiniBossWorld.Downed = Enumerable.Repeat(true, 8).ToArray(); DivineWorld.Downed = Enumerable.Repeat(true, 3).ToArray(); EcologyWorld.Downed = Enumerable.Repeat(true, 10).ToArray();
            for (int i = 0; i < Main.maxPlayers; i++) Main.player[i] = new Player { whoAmI = i, active = false };
            for (int i = 0; i < Main.item.Length; i++) Main.item[i] = new Item();
            Player player = Main.player[0]; player.active = true; player.position = new Vector2(5600, 2038); player.ResetEffects();
            device.SetRenderTarget(atlas); device.Clear(new Color(14, 20, 30));
            for (int id = 0; id < 3; id++)
            {
                var shapes = new HashSet<AscendantShape>(); var moves = new HashSet<int>(); bool captured = false;
                for (int difficulty = 0; difficulty < 3; difficulty++)
                    for (int phase = 0; phase < 3; phase++)
                    {
                        Main.GameMode = difficulty; ResetActors(); var boss = Spawn(id, player, phase);
                        Check(boss.Participants[0] && !boss.Damaged[0], "whole-fight participant");
                        Main.player[1].active = true; Main.player[1].Center = player.Center + new Vector2(40, 0);
                        boss.AI(); Check(boss.Participants[1] && boss.Damaged[1], "late join cannot claim no-hit"); Main.player[1].active = false;
                        bool network = false, recorded = false;
                        for (int tick = 0; tick < 4000; tick++)
                        {
                            player.Center = new Vector2(5600 + MathF.Sin(tick * .01f) * 110, 2059 + MathF.Sin(tick * .02f) * 25);
                            Tick(boss, shapes);
                            if (boss.TraceCount >= 2) recorded = true;
                            if (boss.Stage == AscendantStage.Attack) moves.Add(boss.Move);
                            if (!network && Main.projectile.FirstOrDefault(p => p.active && p.ModProjectile is AscendantHazard h && h.OwnedBy(boss))?.ModProjectile is AscendantHazard hazard)
                            { Network(boss, hazard); network = true; }
                            int captureAt = id == 0 ? 220 : id == 1 ? 535 : 282;
                            if (!captured && difficulty == 0 && phase == 2 && boss.Stage == AscendantStage.Attack && boss.Timer == captureAt)
                            { Draw(id, boss, player, clip); captured = true; }
                        }
                        Check(network, "packet roundtrip " + id + "/" + phase);
                        bool oldDay = Main.dayTime; Main.dayTime = !oldDay; boss.AI(); Main.dayTime = oldDay;
                        Check(boss.NPC.active && !boss.Cancelled, "day/night change does not cancel trial");
                        if (id == 2 && phase > 0) Check(recorded, "bounded recorded path");
                        boss.NPC.ai[3] = 0; boss.NPC.life = (int)(boss.NPC.lifeMax * .2f); boss.AI();
                        Check(boss.Stage == AscendantStage.Transition && !Main.projectile.Any(p => p.active && p.ModProjectile is AscendantHazard), "phase transition cleanup");
                        player.dead = true; boss.AI(); player.dead = false;
                        Check(boss.Cancelled && !boss.NPC.active && !Main.npc.Any(n => n.active && n.ModNPC is AscendantNode), "death/despawn cleanup");
                    }
                Check(captured && moves.Count == (id == 2 ? 5 : 4), "move/render coverage " + id);
                Check(shapes.Count >= (id == 2 ? 5 : 4), "distinct hazard coverage " + id);
                Log("ASCENDANT_BOSS_AI_PASS: id=" + id + " phases=3 difficulties=3 boundedHazards=true telegraphs=true nativeRender=true");
            }
            Main.GameMode = 0;
            Nodes(player); GeometryAndAuthority(player); Gates(player); ChoiceAndFinales(player);
            device.SetRenderTargets(renderTargets);
            string path = Path.GetFullPath(Path.Combine(Main.SavePath, "..", "..", "outputs", "ascendant-combat-runtime.png"));
            using (var stream = File.Create(path)) atlas.SaveAsPng(stream, atlas.Width, atlas.Height);
            Log("ASCENDANT_COMBAT_RENDER_PASS: nativeNPCNodeProjectileHooks=true image=ascendant-combat-runtime.png");
            Log("ASCENDANT_COMBAT_RUNTIME_PASS: dewBreak=true furnaceChoice=true fateKnot=true leftRightChoice=true oneShotFinales=true deadlines=true prerequisites=true serialOwnership=true clientAuthority=true");
        }
        finally
        {
            for (int i = 0; i < npcs.Length; i++) Main.npc[i] = npcs[i];
            for (int i = 0; i < projectiles.Length; i++) Main.projectile[i] = projectiles[i];
            for (int i = 0; i < players.Length; i++) Main.player[i] = players[i];
            for (int i = 0; i < items.Length; i++) Main.item[i] = items[i];
            MiniBossWorld.Downed = mini; DivineWorld.Downed = divine; EcologyWorld.Downed = ecology;
            Main.GameMode = mode; Main.netMode = net; Main.myPlayer = myPlayer;
            NPC.downedSlimeKing = slime; Main.hardMode = hard; NPC.downedMoonlord = moon; Main.dayTime = day;
            Main.screenPosition = screen; device.SetRenderTargets(renderTargets); device.ScissorRectangle = scissor;
        }
    }
    private static void Nodes(Player player)
    {
        ResetActors(); var boss = Spawn(0, player, 1); boss.NPC.ai[0] = (int)AscendantStage.Attack; boss.NPC.ai[1] = 20;
        var node = boss.SpawnNode(AscendantNodeKind.Dew, 0); node.AI();
        boss.Emit(AscendantShape.Ring, node.Anchor, Vector2.Zero, mark: 20);
        node.OnHitByItem(player, new Item(ItemID.IronBroadsword), new NPC.HitInfo { Damage = 100 }, 100);
        Check(node.Progress == 100, "node accepts item damage"); node.Charge(node.Goal);
        Check(boss.BrokenMask == 1 && boss.VulnerableTicks == 300 && Math.Abs(boss.Vulnerability - 1.2f) < .001f, "dew exposure exact");
        Check(!Main.projectile.Any(p => p.active && p.ModProjectile is AscendantHazard h && h.Mark == 20), "dew enhancement cancelled");
        NodeNetwork(node);
        ResetActors(); boss = Spawn(1, player, 1); boss.NPC.ai[0] = (int)AscendantStage.Attack; boss.NPC.ai[1] = 20;
        var first = boss.SpawnNode(AscendantNodeKind.Furnace, 0); var second = boss.SpawnNode(AscendantNodeKind.Furnace, 1);
        first.AI(); second.AI(); Check(first.Open && second.Open, "two active furnaces");
        first.OnHitByProjectile(new Projectile(), new NPC.HitInfo { Damage = first.Goal }, first.Goal);
        Check(boss.Suppressed == 0 && boss.VulnerableTicks == 360 && boss.Vulnerability == 1.25f, "selected furnace suppressed");
        second.Charge(second.Goal); Check(second.Progress == 0 && !second.Open, "only one furnace each round");
        ResetActors(); boss = Spawn(2, player, 1); boss.NPC.ai[0] = (int)AscendantStage.Attack; boss.NPC.ai[1] = 200;
        boss.TraceCount = 3; boss.Trace[0] = player.Center; boss.Trace[1] = player.Center + new Vector2(80, 0); boss.Trace[2] = player.Center + new Vector2(80, 80);
        boss.SpawnTrace(72, 98); boss.SpawnTrace(72, 99);
        node = boss.SpawnNode(AscendantNodeKind.Knot, 0); node.AI(); node.Charge(node.Goal);
        Check(boss.KnotBroken && boss.VulnerableTicks == 240, "fate knot exposes boss");
        Check(Main.projectile.Any(p => p.active && p.ModProjectile is AscendantHazard h && h.Mark == 98) && !Main.projectile.Any(p => p.active && p.ModProjectile is AscendantHazard h && h.Mark == 99), "only final replay removed");
        boss.Cleanup();
    }
    private static void GeometryAndAuthority(Player player)
    {
        ResetActors(); var boss = Spawn(0, player); Vector2 at = player.Center;
        Projectile p = boss.Emit(AscendantShape.Beam, at, Vector2.Zero, width: 20, length: 200); var h = (AscendantHazard)p.ModProjectile;
        Rectangle hit = new((int)at.X + 90, (int)at.Y - 8, 16, 16), miss = new((int)at.X + 90, (int)at.Y + 70, 16, 16);
        Check(h.Colliding(p.Hitbox, hit) == false, "beam warning harmless"); h.Age = h.Delay;
        Check(h.Colliding(p.Hitbox, hit) == true && h.Colliding(p.Hitbox, miss) == false, "native beam collision"); p.Kill();
        p = boss.Emit(AscendantShape.Pillar, at, Vector2.Zero, width: 30, length: 100); h = (AscendantHazard)p.ModProjectile; h.Age = h.Delay;
        Check(h.Colliding(p.Hitbox, new Rectangle((int)at.X - 4, (int)at.Y - 90, 8, 8)) == true && h.Colliding(p.Hitbox, new Rectangle((int)at.X - 4, (int)at.Y + 12, 8, 8)) == false, "native pillar bounds"); p.Kill();
        p = boss.Emit(AscendantShape.Ring, at, Vector2.Zero, duration: 100, length: 200, gap: .9f); h = (AscendantHazard)p.ModProjectile; h.Age = h.Delay + 50;
        Check(h.Colliding(p.Hitbox, new Rectangle((int)at.X + 95, (int)at.Y - 5, 10, 10)) == false && h.Colliding(p.Hitbox, new Rectangle((int)at.X - 105, (int)at.Y - 5, 10, 10)) == true, "render-matched ring opening"); p.Kill();
        p = boss.Emit(AscendantShape.Corridor, at, Vector2.Zero, length: 800, gap: 135); h = (AscendantHazard)p.ModProjectile; h.Age = h.Delay;
        Check(h.Colliding(p.Hitbox, player.Hitbox) == false && h.Colliding(p.Hitbox, new Rectangle((int)at.X + 200, (int)at.Y - 8, 16, 16)) == true, "corridor safe lane"); p.Kill();
        p = boss.Emit(AscendantShape.Seed, at, new Vector2(-3, 2)); h = (AscendantHazard)p.ModProjectile; Network(boss, h);
        h.Serial++; h.AI(); Check(!p.active, "stale slot serial rejected");
        int before = Main.projectile.Count(q => q.active); Main.netMode = NetmodeID.MultiplayerClient;
        Check(boss.Emit(AscendantShape.Beam, at, Vector2.Zero) == null && boss.SpawnNode(AscendantNodeKind.Dew, 0) == null, "clients cannot spawn combat helpers");
        Main.netMode = NetmodeID.SinglePlayer; Check(Main.projectile.Count(q => q.active) == before, "client spawn left no artifacts");
        Vector2 edge = AscendantBossNPC.WorldPoint(new Vector2(-100000, Main.maxTilesY * 16f + 100000));
        Check(edge.X >= 80 && edge.Y <= Main.maxTilesY * 16f - 80, "world bounds"); boss.Cleanup();
    }
    private static void Gates(Player player)
    {
        for (int id = 0; id < 3; id++)
        {
            ResetActors(); var boss = Spawn(id, player); boss.Emit(AscendantShape.Seed, player.Center, Vector2.UnitY); boss.SpawnNode(AscendantNodeKind.Dew, 0);
            if (id == 0) NPC.downedSlimeKing = true; else if (id == 1) Main.hardMode = true; else NPC.downedMoonlord = true;
            int count = Main.item.Count(item => item.active); boss.NPC.NPCLoot();
            Check(Main.item.Count(item => item.active) == count && boss.Cancelled && !boss.PreKill() && !boss.CheckDead(), "deadline native no loot " + id);
            Check(!Main.projectile.Any(p => p.active && p.ModProjectile is AscendantHazard) && !Main.npc.Any(n => n.active && n.ModNPC is AscendantNode), "deadline helper cleanup");
            NPC.downedSlimeKing = Main.hardMode = NPC.downedMoonlord = false;
            ResetActors(); boss = Spawn(id, player); DivineWorld.Downed[id] = false; boss.AI();
            Check(boss.Cancelled && !boss.PreKill(), "lost prerequisite cancels " + id); DivineWorld.Downed[id] = true;
        }
        ResetActors(); var trial = Spawn(0, player); NPC other = new(); other.SetDefaults(NPCID.KingSlime); other.whoAmI = 1; other.active = true; Main.npc[1] = other;
        trial.AI(); Check(trial.Cancelled && other.active, "another boss preserved on interruption");
    }
    private static void ChoiceAndFinales(Player player)
    {
        var signatures = new List<string>();
        for (int choice = 0; choice < 2; choice++)
        {
            ResetActors(); var boss = Spawn(2, player, 2); boss.NPC.ai[0] = (int)AscendantStage.Choice; boss.SelectedLaw = -1;
            boss.ResolveChoice(choice == 1); Check(boss.SelectedLaw == (choice == 0 ? 0 : 2), "left/right commits different law");
            boss.NPC.ai[0] = (int)AscendantStage.Attack; boss.NPC.ai[1] = 190; boss.AI();
            signatures.Add(string.Join(",", Main.projectile.Where(p => p.active && p.ModProjectile is AscendantHazard).Select(p => ((AscendantHazard)p.ModProjectile).Mark)));
        }
        Check(signatures[0] != signatures[1], "choice changes emitted attacks, not just text");
        foreach (int id in new[] { 0, 2 })
        {
            ResetActors(); var boss = Spawn(id, player, 2); boss.NPC.life = boss.NPC.lifeMax / 12; boss.AI();
            Check(boss.FinalUsed && boss.FinalActive && boss.Stage == AscendantStage.Transition, "one-shot finale threshold " + id);
            var shapes = new HashSet<AscendantShape>();
            for (int i = 0; i < 1700; i++) Tick(boss, shapes);
            Check(boss.FinalUsed && !boss.FinalActive && shapes.Count >= 4, "finite finale and recovery " + id);
            Check(!boss.NPC.dontTakeDamage && boss.CheckDead(), "no final health lock " + id);
            for (int i = 0; i < 100; i++) Tick(boss, shapes);
            Check(!boss.FinalActive, "finale cannot retrigger " + id);
        }
    }
    private static void Network(AscendantBossNPC boss, AscendantHazard hazard)
    {
        using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory, System.Text.Encoding.UTF8, true);
        boss.SendExtraAI(writer); writer.Flush(); memory.Position = 0;
        NPC npc = new(); npc.SetDefaults(boss.Type); npc.ai = (float[])boss.NPC.ai.Clone(); var remote = (AscendantBossNPC)npc.ModNPC; remote.ReceiveExtraAI(new BinaryReader(memory));
        Check(remote.Serial == boss.Serial && remote.Ready == boss.Ready && remote.Arena == boss.Arena && remote.SelectedLaw == boss.SelectedLaw && remote.FinalUsed == boss.FinalUsed, "NPC extraAI identity/choice/finale");
        Check(remote.TraceCount == boss.TraceCount && remote.Trace.SequenceEqual(boss.Trace), "NPC trace packet");
        memory.SetLength(0); memory.Position = 0; hazard.SendExtraAI(writer); writer.Flush(); memory.Position = 0;
        Projectile projectile = new(); projectile.SetDefaults(hazard.Type); projectile.ai = (float[])hazard.Projectile.ai.Clone(); var copy = (AscendantHazard)projectile.ModProjectile; copy.ReceiveExtraAI(new BinaryReader(memory));
        Check(copy.Serial == hazard.Serial && copy.Delay == hazard.Delay && copy.Duration == hazard.Duration && copy.Anchor == hazard.Anchor && copy.Launch == hazard.Launch, "hazard signed velocity and ownership packet");
        Check(copy.Count == hazard.Count, "hazard trace count packet");
        for (int i = 0; i < hazard.Count; i++) Check(copy.Points[i] == hazard.Points[i], "hazard trace coordinate packet");
    }
    private static void NodeNetwork(AscendantNode node)
    {
        using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory, System.Text.Encoding.UTF8, true);
        node.SendExtraAI(writer); writer.Flush(); memory.Position = 0;
        NPC npc = new(); npc.SetDefaults(node.Type); npc.ai = (float[])node.NPC.ai.Clone(); var copy = (AscendantNode)npc.ModNPC; copy.ReceiveExtraAI(new BinaryReader(memory));
        Check(copy.Serial == node.Serial && copy.Progress == node.Progress && copy.Anchor == node.Anchor && copy.Ready, "node pressure/anchor packet");
    }
    private static void Draw(int row, AscendantBossNPC boss, Player player, RasterizerState clip)
    {
        float scale = boss.Index == 0 ? .75f : .6f;
        Main.screenPosition = new Vector2(boss.Arena.X - 1000, Math.Min(boss.Arena.Y - 260, boss.NPC.Center.Y - 190));
        Main.instance.GraphicsDevice.ScissorRectangle = new Rectangle(0, row * 350, 1440, 350);
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, clip, null,
            Matrix.CreateScale(scale) * Matrix.CreateTranslation(0, row * 350, 0));
        try
        {
            CombatDrawing.Circle(Main.spriteBatch, player.Center - Main.screenPosition, 17, Color.LightGreen, 3);
            boss.PreDraw(Main.spriteBatch, Main.screenPosition, Color.White);
            foreach (NPC npc in Main.ActiveNPCs) if (npc.ModNPC is AscendantNode node) node.PreDraw(Main.spriteBatch, Main.screenPosition, Color.White);
            foreach (Projectile p in Main.ActiveProjectiles) if (p.ModProjectile is AscendantHazard h) { Color color = Color.White; h.PreDraw(ref color); }
        }
        finally { Main.spriteBatch.End(); }
        Main.spriteBatch.Begin();
        try { Utils.DrawBorderString(Main.spriteBatch, AscendantCatalog.Names[boss.Index], new Vector2(20, row * 350 + 318), Color.White, .8f); }
        finally { Main.spriteBatch.End(); }
    }
}
