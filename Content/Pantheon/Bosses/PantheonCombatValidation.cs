#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using StarfallThrone.Content.Systems;
namespace StarfallThrone.Content.Pantheon.Bosses;

/// <summary>Opt-in real tML hooks; isolated menu fixture, never a player world or manual playthrough.</summary>
public static class PantheonCombatValidation
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Pantheon combat: " + message); }
    private static void Log(string value) => ModContent.GetInstance<PantheonWorld>().Mod.Logger.Info(value);
    public static void Data()
    {
        Localization();
        int old = Main.GameMode;
        try
        {
            for (int difficulty = 0; difficulty < 3; difficulty++)
            {
                Main.GameMode = difficulty;
                for (int i = 0; i < 17; i++)
                {
                    NPC npc = new(); npc.SetDefaults(PantheonCatalog.BossType(i));
                    Check(npc.ModNPC is PantheonBossNPC && npc.boss, "registered boss " + i);
                    var b = (PantheonBossNPC)npc.ModNPC; b.ApplyDifficultyAndPlayerScaling(1, 1, 1);
                    Check(npc.lifeMax == PantheonBossNPC.LifeFor(i, difficulty), "single difficulty scaling " + i + "/" + difficulty);
                    Check(npc.damage == b.ContactDamage && npc.lifeMax > 0, "damage and overflow bounds " + i);
                    Check(npc.GetBossHeadTextureIndex() >= 0 && npc.BossBar is PantheonBossBar, "head and native bar " + i);
                    foreach (float factor in new[] { .7f, 1f, 1.4f })
                    {
                        int engine = difficulty == 2 ? 6 : difficulty == 1 ? 4 : 2;
                        int actual = b.EngineDamage(factor) * engine;
                        Check(actual <= b.ShotDamage(factor) && b.ShotDamage(factor) - actual < engine, "native hostile multiplier budget " + i);
                    }
                    if (i > 0) Check(npc.lifeMax > PantheonBossNPC.LifeFor(i - 1, difficulty), "monotonic progression");
                    b.ApplyDifficultyAndPlayerScaling(8, 20, 1);
                    Check(npc.lifeMax > 0 && npc.lifeMax <= 1_500_000_000, "multiplayer long clamp");
                }
            }
        }
        finally { Main.GameMode = old; }
        Log("PANTHEON_COMBAT_DATA_PASS: bosses=17 difficulties=3 nativeHeadsBars=true boundedLife=true hostileBudget=2/4/6");
    }
    private static void Localization()
    {
        // Check the same absolute keys used by PreDraw/Bestiary, after the parent's culture setup.
        // No culture mutation is needed: the current run's actual displayed language is validated.
        int count = 0;
        void Text(string key)
        {
            string value = Language.GetTextValue(key);
            Check(!string.IsNullOrWhiteSpace(value) && value != key && !value.Contains("Mods.StarfallThrone.", StringComparison.Ordinal),
                "unresolved localization key " + key);
            count++;
        }
        for (int id = 0; id < 17; id++)
        {
            Text("Mods.StarfallThrone.PantheonCombat.Node" + id);
            Text("Mods.StarfallThrone.PantheonCombat.Bestiary" + id);
            Text("Mods.StarfallThrone.NPCs." + PantheonCatalog.Keys[id] + "NPC.DisplayName");
            for (int move = 0; move < 3; move++) Text("Mods.StarfallThrone.PantheonCombat.Move" + id + "_" + move);
        }
        Text("Mods.StarfallThrone.NPCs.PantheonNode.DisplayName");
        Text("Mods.StarfallThrone.Projectiles.PantheonHazard.DisplayName");
        Log("PANTHEON_COMBAT_LOCALIZATION_PASS: keys=" + count + " rawKeyRejected=true culture=" + Language.ActiveCulture.Name);
    }
    private static void ResetActors()
    {
        for (int i = 0; i < Main.npc.Length; i++) Main.npc[i] = new NPC { whoAmI = i };
        for (int i = 0; i < Main.projectile.Length; i++) Main.projectile[i] = new Projectile { whoAmI = i };
    }
    private static PantheonBossNPC Spawn(int id, Player player, int phase = 0, int move = 0)
    {
        NPC npc = new(); npc.SetDefaults(PantheonCatalog.BossType(id)); npc.whoAmI = 0; npc.active = true; npc.target = 0;
        npc.Center = player.Center - new Vector2(0, 220); Main.npc[0] = npc;
        var b = (PantheonBossNPC)npc.ModNPC; b.ApplyDifficultyAndPlayerScaling(1, 1, 1); b.AI();
        Check(b.Ready && !b.Cancelled && npc.active, "encounter initialize " + id);
        npc.ai[3] = phase; npc.life = (int)(npc.lifeMax * (phase == 0 ? 1f : phase == 1 ? .55f : .22f)); npc.ai[2] = move;
        return b;
    }
    private static void Tick(PantheonBossNPC boss, HashSet<PantheonShape> shapes)
    {
        boss.AI(); boss.NPC.position += boss.NPC.velocity;
        Check(float.IsFinite(boss.NPC.Center.X) && float.IsFinite(boss.NPC.Center.Y), "finite NPC motion");
        int nodes = 0, projectiles = 0;
        foreach (NPC n in Main.ActiveNPCs)
            if (n.ModNPC is PantheonNode node && node.OwnedBy(boss))
            {
                nodes++; node.AI(); int cooldown = 0;
                Check(n.damage == 0 && !node.CanHitPlayer(Main.player[0], ref cooldown), "no decorative body collision");
                Check(float.IsFinite(n.Center.X) && float.IsFinite(n.Center.Y), "finite articulated body");
            }
        foreach (Projectile p in Main.ActiveProjectiles)
            if (p.ModProjectile is PantheonHazard h && h.OwnedBy(boss))
            {
                projectiles++; shapes.Add(h.Shape); h.AI();
                Check(h.Delay >= 60 && !h.ShouldUpdatePosition(), "local telegraph and single motion integration");
                if (h.Age < h.Delay) Check(h.CanDamage() == false && h.Colliding(p.Hitbox, p.Hitbox) == false, "telegraph harmless");
                if (h.Shape is PantheonShape.Echo or PantheonShape.Gravity) Check(h.CanDamage() == false, "non-damaging illusion / force motif");
                Check(float.IsFinite(p.Center.X) && float.IsFinite(p.Center.Y), "finite projectile motion");
                if (--p.timeLeft <= 0) p.Kill();
            }
        Check(nodes <= 17 && projectiles <= 38, "bounded encounter entity budget");
    }
    public static void Run()
    {
        Check(Main.gameMenu && Environment.GetCommandLineArgs().Contains("-starfall-art-smoke"), "isolated native menu required");
        Localization();
        NPC[] oldNpc = (NPC[])Main.npc.Clone(); Projectile[] oldProjectile = (Projectile[])Main.projectile.Clone();
        Player[] oldPlayer = (Player[])Main.player.Clone(); Item[] oldItem = (Item[])Main.item.Clone();
        bool[] downed = PantheonWorld.Downed, voyage = global::StarfallThrone.Content.Voyage.VoyageWorld.Downed;
        int mode = Main.GameMode, net = Main.netMode, local = Main.myPlayer; bool day = Main.dayTime;
        Vector2 screen = Main.screenPosition;
        GraphicsDevice device = Main.instance.GraphicsDevice; var targets = device.GetRenderTargets();
        using var atlas = new RenderTarget2D(device, 1920, 2040); using var frame = new RenderTarget2D(device, 640, 340);
        try
        {
            Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0;
            PantheonWorld.Downed = Enumerable.Repeat(true, 17).ToArray();
            global::StarfallThrone.Content.Voyage.VoyageWorld.Downed = Enumerable.Repeat(true, 17).ToArray();
            for (int i = 0; i < Main.maxPlayers; i++) Main.player[i] = new Player { whoAmI = i, active = false };
            for (int i = 0; i < Main.item.Length; i++) Main.item[i] = new Item();
            Player player = Main.player[0]; player.active = true; player.Center = new Vector2(5600, 2060); player.ResetEffects();
            var renders = new Texture2D[17];
            try
            {
                for (int id = 0; id < 17; id++)
                {
                    var shapes = new HashSet<PantheonShape>(); var moves = new HashSet<int>();
                    bool packet = false, captured = false;
                    for (int difficulty = 0; difficulty < 3; difficulty++)
                        for (int phase = 0; phase < 3; phase++)
                            for (int move = 0; move < 3; move++)
                            {
                                Main.GameMode = difficulty; ResetActors(); player.Center = new Vector2(5600, 2060);
                                var b = Spawn(id, player, phase, move);
                                for (int tick = 0; tick < b.AttackLength + 170; tick++)
                                {
                                    player.Center = new Vector2(5600 + MathF.Sin(tick * .012f) * 85, 2060 + MathF.Sin(tick * .02f) * 20);
                                    Tick(b, shapes);
                                    if (b.Stage == PantheonStage.Attack) moves.Add(b.Move);
                                    if (!packet && Main.projectile.FirstOrDefault(q => q.active && q.ModProjectile is PantheonHazard)?.ModProjectile is PantheonHazard hazard)
                                    { Network(b, hazard); packet = true; }
                                    if (!captured && difficulty == 0 && phase == 2 && move == 0 && b.Stage == PantheonStage.Attack && b.Timer == 112)
                                    { renders[id] = Draw(b, player, frame); captured = true; }
                                }
                                bool savedDay = Main.dayTime; Main.dayTime = !savedDay; b.AI(); Main.dayTime = savedDay;
                                Check(!b.Cancelled, "day/night and biome remain start-only");
                                b.NPC.ai[3] = 0; b.NPC.life = b.NPC.lifeMax / 5; b.AI();
                                Check(b.Stage == PantheonStage.Transition && !Main.projectile.Any(q => q.active && q.ModProjectile is PantheonHazard), "phase clears pending hazards");
                                player.dead = true; b.AI(); player.dead = false;
                                Check(b.Cancelled && !b.NPC.active && !Main.npc.Any(n => n.active && n.ModNPC is PantheonNode), "dead player cleanup");
                            }
                    Check(packet && captured && moves.Count == 3 && shapes.Count >= 2, "per-boss state / render / move coverage " + id);
                    Main.GameMode = 0; CountersAndDamage(id, player);
                    Log("PANTHEON_BOSS_AI_PASS: id=" + id + " phases=3 moves=3 difficulties=3 nativeRender=true counterplay=true singlePool=true");
                }
                device.SetRenderTarget(atlas); device.Clear(new Color(11, 14, 26)); Main.spriteBatch.Begin();
                for (int i = 0; i < 17; i++) Main.spriteBatch.Draw(renders[i], new Vector2(i % 3 * 640, i / 3 * 340), Color.White);
                Main.spriteBatch.End(); device.SetRenderTargets(targets);
                string path = Path.GetFullPath(Path.Combine(Main.SavePath, "..", "..", "outputs", "pantheon-combat-runtime.png"));
                using (var stream = File.Create(path)) atlas.SaveAsPng(stream, atlas.Width, atlas.Height);
            }
            finally { foreach (Texture2D? texture in renders) texture?.Dispose(); }
            Main.GameMode = 0; Geometry(player); AuthorityAndGates(player); FinalAndChoice(player);
            Log("PANTHEON_COMBAT_RENDER_PASS: nativeBossNodeProjectileHooks=17 frames=pantheon-combat-runtime.png alphaChecks=true twinHalfCrops=true wormHeadCrops=true");
            Log("PANTHEON_COMBAT_RUNTIME_PASS: nativeDamage=true phaseMoves=153 counterplay=17 nativeCollisions=true cleanup=true ownership=true serverNativeStrikes=true noHealthLock=true");
        }
        finally
        {
            for (int i = 0; i < oldNpc.Length; i++) Main.npc[i] = oldNpc[i];
            for (int i = 0; i < oldProjectile.Length; i++) Main.projectile[i] = oldProjectile[i];
            for (int i = 0; i < oldPlayer.Length; i++) Main.player[i] = oldPlayer[i];
            for (int i = 0; i < oldItem.Length; i++) Main.item[i] = oldItem[i];
            PantheonWorld.Downed = downed; global::StarfallThrone.Content.Voyage.VoyageWorld.Downed = voyage;
            Main.GameMode = mode; Main.netMode = net; Main.myPlayer = local; Main.dayTime = day; Main.screenPosition = screen;
            device.SetRenderTargets(targets);
        }
    }
    private static Texture2D Draw(PantheonBossNPC boss, Player player, RenderTarget2D target)
    {
        GraphicsDevice device = Main.instance.GraphicsDevice;
        if (boss.Index is 2 or 10)
        {
            Texture2D body = TextureAssets.Npc[boss.Type].Value; Rectangle crop = PantheonNode.SegmentSource(boss.Index);
            Check(crop.Width < body.Width / 2 && crop.Height < body.Height / 2 && body.Bounds.Contains(crop), "worm uses bounded scale patch, never whole body");
            Color[] scalePixels = new Color[crop.Width * crop.Height]; body.GetData(0, crop, scalePixels, 0, scalePixels.Length);
            Check(scalePixels.Count(c => c.A > 32) > 100, "actual source scale/fog patch has visible pixels " + boss.Index);
        }
        device.SetRenderTarget(target); device.Clear(Color.Transparent);
        Main.screenPosition = boss.Arena - new Vector2(600, 410);
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.CreateScale(.5f));
        try
        {
            boss.PreDraw(Main.spriteBatch, Main.screenPosition, Color.White);
            foreach (NPC n in Main.ActiveNPCs) if (n.ModNPC is PantheonNode node && node.OwnedBy(boss)) node.PreDraw(Main.spriteBatch, Main.screenPosition, Color.White);
            foreach (Projectile p in Main.ActiveProjectiles) if (p.ModProjectile is PantheonHazard h && h.OwnedBy(boss)) { Color light = Color.White; h.PreDraw(ref light); }
            CombatDrawing.Circle(Main.spriteBatch, player.Center - Main.screenPosition, 15, Color.LightGreen, 3);
        }
        finally { Main.spriteBatch.End(); }
        Main.spriteBatch.Begin();
        Utils.DrawBorderString(Main.spriteBatch, PantheonCatalog.Names[boss.Index], new Vector2(8, 312), Color.White, .62f);
        Main.spriteBatch.End(); device.SetRenderTarget(null);
        Color[] pixels = new Color[target.Width * target.Height]; target.GetData(pixels);
        Check(pixels.Count(c => c.A > 32) > 1500, "real render alpha coverage " + boss.Index);
        Texture2D copy = new(device, target.Width, target.Height); copy.SetData(pixels); return copy;
    }
    private static void CountersAndDamage(int id, Player player)
    {
        ResetActors(); var b = Spawn(id, player, 1); b.Enter(PantheonStage.Attack); b.CreateCounterplay();
        var node = Main.npc.First(n => n.active && n.ModNPC is PantheonNode x && x.OwnedBy(b) && !x.Part).ModNPC as PantheonNode;
        Check(node != null, "breakable objective " + id); node!.AI();
        var shot = b.Emit(PantheonShape.Beam, node.Anchor, Vector2.Zero, mark: node.Mark);
        node.OnHitByProjectile(new Projectile { whoAmI = 0 }, new NPC.HitInfo { Damage = node.Goal }, node.Goal);
        Check(b.IsBroken(node.Mark) && b.Exposed > 0 && b.Chosen == node.Mark && !shot!.Projectile.active, "real counter hit suppresses hazard " + id);
        Check(!node.Open && b.Emit(PantheonShape.Star, player.Center, Vector2.UnitX, mark: node.Mark) == null, "counter cannot be reactivated this round");
        int health = b.NPC.life; b.NPC.SimpleStrikeNPC(50000, 0, noPlayerInteraction: true);
        Check(b.NPC.life < health && b.NPC.life > 0, "native StrikeNPC reaches main health " + id);
        if (b.PartCount > 0)
        {
            var parts = Main.npc.Where(n => n.active && n.ModNPC is PantheonNode x && x.OwnedBy(b) && x.Part).Select(n => (PantheonNode)n.ModNPC).ToArray();
            Check(parts.Length == b.PartCount, "multipart count " + id);
            foreach (var part in parts) part.AI();
            int before = b.NPC.life;
            parts[0].NPC.playerInteraction[1] = true; parts[0].NPC.lastInteraction = 1;
            Projectile piercing = new() { whoAmI = 11, owner = 0, identity = 11 };
            Check(parts[0].CanBeHitByProjectile(piercing) != false, "first visible part permits source");
            parts[0].OnHitByProjectile(piercing, new NPC.HitInfo { Damage = 10000 }, 10000);
            int once = b.NPC.life;
            Check(parts[1].CanBeHitByProjectile(piercing) == false, "same piercing source blocked before second native hit");
            parts[1].OnHitByProjectile(piercing, new NPC.HitInfo { Damage = 10000 }, 10000);
            Check(once < before && b.NPC.life == once, "shared multipart damage and piercing throttle " + id);
            Check(b.NPC.playerInteraction[0] && b.NPC.playerInteraction[1] && b.NPC.lastInteraction == 0, "part hit transfers all helper contributors and last player before strike " + id);
            Projectile anotherPlayer = new() { whoAmI = 12, owner = 1, identity = 11 };
            Check(parts[1].CanBeHitByProjectile(anotherPlayer) != false, "different owner same identity not throttled");
            parts[1].OnHitByProjectile(anotherPlayer, new NPC.HitInfo { Damage = 10000 }, 10000);
            Check(b.NPC.life < once, "simultaneous other player still damages shared pool");
            Projectile recycled = new() { whoAmI = 11, owner = 0, identity = 12 };
            Check(parts[0].CanBeHitByProjectile(recycled) != false, "recycled projectile slot is a new source");
            Check(parts.All(x => x.NPC.value == 0 && !x.PreKill()), "no independent part loot");
            if (id == 8)
            {
                int cooldown = 0;
                Check(parts[0].NPC.Center != parts[1].NPC.Center, "independent eye orbits");
                Check(!b.NPC.chaseable && b.CanBeHitByProjectile(new Projectile()) == false && b.CanBeHitByItem(player, new Item()) == false
                    && !b.CanHitPlayer(player, ref cooldown), "invisible controller is neither chaseable, directly damageable nor contact-harmful");
                Check(parts.All(x => x.NPC.chaseable && !x.NPC.dontTakeDamage), "visible eyes remain valid minion targets");
            }
        }
        b.Cleanup();
    }
    private static void Network(PantheonBossNPC boss, PantheonHazard hazard)
    {
        using var memory = new MemoryStream(); using var w = new BinaryWriter(memory, System.Text.Encoding.UTF8, true);
        boss.SendExtraAI(w); w.Flush(); memory.Position = 0;
        NPC npc = new(); npc.SetDefaults(boss.Type); npc.ai = (float[])boss.NPC.ai.Clone(); var copy = (PantheonBossNPC)npc.ModNPC;
        copy.ReceiveExtraAI(new BinaryReader(memory));
        Check(copy.Serial == boss.Serial && copy.Arena == boss.Arena && copy.FinaleUsed == boss.FinaleUsed && copy.Chosen == boss.Chosen && copy.Spine.SequenceEqual(boss.Spine), "boss packet all committed state");
        memory.SetLength(0); memory.Position = 0; hazard.SendExtraAI(w); w.Flush(); memory.Position = 0;
        Projectile p = new(); p.SetDefaults(hazard.Type); p.ai = (float[])hazard.Projectile.ai.Clone(); var h = (PantheonHazard)p.ModProjectile;
        h.ReceiveExtraAI(new BinaryReader(memory));
        Check(h.Serial == hazard.Serial && h.Delay == hazard.Delay && h.Anchor == hazard.Anchor && h.Launch == hazard.Launch && h.Mark == hazard.Mark, "projectile geometry packet");
        PantheonNode node = Main.npc.Where(n => n.active && n.ModNPC is PantheonNode).Select(n => (PantheonNode)n.ModNPC).First();
        memory.SetLength(0); memory.Position = 0; node.SendExtraAI(w); w.Flush(); memory.Position = 0;
        NPC remote = new(); remote.SetDefaults(node.Type); remote.ai = (float[])node.NPC.ai.Clone(); var ncopy = (PantheonNode)remote.ModNPC;
        ncopy.ReceiveExtraAI(new BinaryReader(memory)); Check(ncopy.Ready && ncopy.Serial == node.Serial && ncopy.Anchor == node.Anchor && ncopy.Progress == node.Progress, "node packet");
    }
    private static void Geometry(Player player)
    {
        ResetActors(); var b = Spawn(0, player); Vector2 at = player.Center;
        PantheonHazard h = b.Emit(PantheonShape.Beam, at, Vector2.Zero, width: 20, length: 200)!;
        Rectangle hit = new((int)at.X + 90, (int)at.Y - 5, 10, 10), miss = new((int)at.X + 90, (int)at.Y + 70, 10, 10);
        Check(h.Colliding(h.Projectile.Hitbox, hit) == false, "beam prewarning harmless"); h.Age = h.Delay;
        Check(h.Colliding(h.Projectile.Hitbox, hit) == true && h.Colliding(h.Projectile.Hitbox, miss) == false, "actual native line collision");
        h = b.Emit(PantheonShape.Ring, at, Vector2.Zero, duration: 100, length: 200, gap: .8f)!; h.Age = h.Delay + 50;
        int radius = (int)h.Radius;
        Check(h.Colliding(h.Projectile.Hitbox, new Rectangle((int)at.X + radius - 5, (int)at.Y - 5, 10, 10)) == false, "ring gap collision");
        Check(h.Colliding(h.Projectile.Hitbox, new Rectangle((int)at.X - radius - 5, (int)at.Y - 5, 10, 10)) == true, "ring edge collision");
        h = b.Emit(PantheonShape.Wall, at, Vector2.UnitX, length: 600, gap: 100)!; h.Age = h.Delay;
        Check(h.Colliding(h.Projectile.Hitbox, player.Hitbox) == false && h.Colliding(h.Projectile.Hitbox, new Rectangle((int)at.X - 5, (int)at.Y + 160, 10, 10)) == true, "finite gate safe opening");
        h = b.Emit(PantheonShape.Arc, at, new Vector2(8, 2), length: 80, angle: .03f)!; h.Age = h.Delay + 4; h.AI();
        Check(h.Projectile.Center == h.PositionAt(5) && h.Colliding(h.Projectile.Hitbox, h.Projectile.Hitbox) == true, "analytic arc swept collision");
        h.Serial++; h.AI(); Check(!h.Projectile.active, "stale encounter serial kills hazard");
        b.Cleanup();
        ResetActors(); b = Spawn(11, player, 1); b.Enter(PantheonStage.Attack);
        b.SpawnNode(0, at); h = b.Emit(PantheonShape.Beam, at - new Vector2(200, 0), Vector2.Zero, length: 500)!; h.Age = h.Delay;
        Rectangle behind = new((int)at.X + 100, (int)at.Y - 5, 10, 10);
        Check(h.Colliding(h.Projectile.Hitbox, behind) == false, "seed husk physically shields beam");
        b.Break(0); Check(h.Colliding(h.Projectile.Hitbox, behind) == true, "destroying cover removes its protection"); b.Cleanup();
        Log("PANTHEON_COLLISION_PASS: warning beam ringGap finiteGate analyticArc breakableCover serialExpiry=true");
    }
    private static void AuthorityAndGates(Player player)
    {
        for (int id = 0; id < 17; id++)
        {
            ResetActors(); var b = Spawn(id, player); b.Emit(PantheonShape.Star, player.Center, Vector2.UnitX);
            if (id == 0) global::StarfallThrone.Content.Voyage.VoyageWorld.Downed[16] = false; else PantheonWorld.Downed[id - 1] = false;
            int loot = Main.item.Count(i => i.active); b.AI(); b.NPC.NPCLoot();
            Check(b.Cancelled && !b.PreKill() && Main.item.Count(i => i.active) == loot, "lost progression cancels without loot " + id);
            Check(!Main.npc.Any(n => n.active && n.ModNPC is PantheonNode) && !Main.projectile.Any(q => q.active && q.ModProjectile is PantheonHazard), "gate cleans helpers " + id);
            if (id == 0) global::StarfallThrone.Content.Voyage.VoyageWorld.Downed[16] = true; else PantheonWorld.Downed[id - 1] = true;
        }
        ResetActors(); var boss = Spawn(2, player, 1); boss.Enter(PantheonStage.Attack); boss.CreateCounterplay();
        int shots = Main.projectile.Count(q => q.active); Main.netMode = NetmodeID.MultiplayerClient;
        Check(boss.Emit(PantheonShape.Beam, player.Center, Vector2.Zero) == null && boss.SpawnNode(0, player.Center) == null, "clients cannot create combat entities");
        boss.Break(0); Check(boss.Broken == 0 && Main.projectile.Count(q => q.active) == shots, "clients cannot forge objective breaks");
        Main.netMode = NetmodeID.SinglePlayer;
        var node = Main.npc.Where(n => n.active && n.ModNPC is PantheonNode x && !x.Part).Select(n => (PantheonNode)n.ModNPC).First();
        using (var transport = new NativeTransport())
        {
            node.AI(); Main.netMode = NetmodeID.Server;
            using var optionalRecords = new ChecklistRecordFixture();
            node.NPC.StrikeNPC(new NPC.HitInfo { Damage = node.Goal, HitDirection = 0 }, fromNet: true, noPlayerInteraction: true); node.AI();
            Check(boss.IsBroken(node.Mark), "server consumes native strike delta for remote node hit");
            var parts = Main.npc.Where(n => n.active && n.ModNPC is PantheonNode x && x.Part).Select(n => (PantheonNode)n.ModNPC).Take(2).ToArray();
            foreach (var part in parts) part.AI();
            parts[0].NPC.playerInteraction[0] = true; parts[0].NPC.lastInteraction = 0;
            parts[1].NPC.playerInteraction[1] = true; parts[1].NPC.lastInteraction = 1;
            int firstDamage = boss.NPC.CalculateHitInfo(11000, 0, damageVariation: false).Damage;
            int secondDamage = boss.NPC.CalculateHitInfo(23000, 0, damageVariation: false).Damage;
            int sameFrame = boss.Age;
            void StrikeBoth()
            {
                parts[0].NPC.StrikeNPC(new NPC.HitInfo { Damage = 11000, HitDirection = 0 }, fromNet: true, noPlayerInteraction: true);
                parts[1].NPC.StrikeNPC(new NPC.HitInfo { Damage = 23000, HitDirection = 0 }, fromNet: true, noPlayerInteraction: true);
                Check(parts[0].NPC.life == parts[0].NPC.lifeMax - 11000 && parts[1].NPC.life == parts[1].NPC.lifeMax - 23000,
                    "two actual native remote hits pending on distinct parts before AI");
            }
            int before = boss.NPC.life; StrikeBoth(); parts[0].AI(); parts[1].AI();
            Check(boss.Age == sameFrame && before - boss.NPC.life == firstDamage + secondDamage,
                "same-frame two-part damage equals sum, no anonymous damage discarded");
            Check(boss.NPC.playerInteraction[0] && boss.NPC.playerInteraction[1] && boss.NPC.lastInteraction == 1,
                "both remote contributors credited before shared damage");
            before = boss.NPC.life; StrikeBoth(); parts[0].AI(); parts[1].AI();
            Check(before - boss.NPC.life == firstDamage + secondDamage, "further valid remote hits in same frame also preserved");
            // Progression serialization is tested by support. Keep this menu world's existing flag:
            // this fixture exercises native lethal damage/OnKill/loot without fabricating a loaded world.
            boss.NPC.life = firstDamage + secondDamage - 1;
            StrikeBoth(); parts[0].AI(); Check(boss.NPC.active && boss.NPC.life == secondDamage - 1, "first component of lethal combination remains alive");
            parts[1].AI();
            Check(boss.NPC.life <= 0 && boss.VictoryRecorded && PantheonWorld.IsDowned(2), "second same-frame part hit completes native boss kill and victory callback");
            Check(boss.NPC.playerInteraction[0] && boss.NPC.playerInteraction[1] && boss.NPC.lastInteraction == 1, "final helper kill retains both contributors");
            int rewards = Main.item.Count(i => i.active); boss.OnKill();
            Check(Main.item.Count(i => i.active) == rewards, "repeated victory callback does not mint rewards");
            Check(transport.Frames.Any(f => f.Length >= 3 && f[2] == MessageID.DamageNPC), "real framed native shared-pool strike broadcast");
            Main.netMode = NetmodeID.SinglePlayer; ResetActors(); boss = Spawn(2, player, 1);
            parts = Main.npc.Where(n => n.active && n.ModNPC is PantheonNode x && x.Part).Select(n => (PantheonNode)n.ModNPC).Take(2).ToArray();
            foreach (var part in parts) part.AI();
            parts[0].NPC.playerInteraction[0] = true; parts[0].NPC.lastInteraction = 0;
            parts[1].NPC.playerInteraction[1] = true; parts[1].NPC.lastInteraction = 1;
            Main.netMode = NetmodeID.Server; boss.NPC.life = 100;
            parts[0].NPC.StrikeNPC(new NPC.HitInfo { Damage = 11000, HitDirection = 0 }, fromNet: true, noPlayerInteraction: true);
            parts[1].NPC.StrikeNPC(new NPC.HitInfo { Damage = 23000, HitDirection = 0 }, fromNet: true, noPlayerInteraction: true);
            Check(parts[1].NPC.life < parts[1].NPC.lifeMax, "second player's native hit is pending before lethal first-part processing");
            parts[0].AI();
            Check(boss.NPC.life <= 0 && boss.VictoryRecorded && boss.NPC.playerInteraction[0] && boss.NPC.playerInteraction[1]
                && boss.NPC.lastInteraction == 0, "first processed part kills but pending second player's participation survives cleanup; killer remains player zero");
            Log("PANTHEON_MULTIPART_CONSERVATION_PASS: nativeRemoteHits=8 distinctParts=2 sameFrameSumExact=true sourceIdentityDedup=true bothPlayerKillCredit=true lethalSecondHit=true lethalFirstPendingCredit=true");
        }
        Main.netMode = NetmodeID.SinglePlayer; boss.Cleanup();
        Check(PantheonBossNPC.Point(new Vector2(float.NaN, float.PositiveInfinity)) == new Vector2(100), "finite world position sanitation");
        Log("PANTHEON_AUTHORITY_PASS: prerequisites=17 clientSpawnRejected=true serverNodeDamage=true serverPartDamage=true bothPlayerKillCredit=true nativePacketRoundtrips=true");
    }
    private static void FinalAndChoice(Player player)
    {
        ResetActors(); var b = Spawn(16, player, 2); b.NPC.life = b.NPC.lifeMax / 20; b.AI();
        Check(b.FinaleUsed && b.Finale && b.Stage == PantheonStage.Transition, "one-shot finale threshold");
        var shapes = new HashSet<PantheonShape>(); for (int i = 0; i < 1400; i++) Tick(b, shapes);
        Check(b.FinaleUsed && !b.Finale && !b.NPC.dontTakeDamage && b.CheckDead(), "finite final ritual no health lock");
        for (int i = 0; i < 200; i++) Tick(b, shapes); Check(!b.Finale, "finale cannot retrigger"); b.Cleanup();
        ResetActors(); b = Spawn(9, player, 1); b.Enter(PantheonStage.Attack); b.CreateCounterplay();
        b.Break(2); var other = Main.npc.Where(n => n.active && n.ModNPC is PantheonNode x && !x.Part && x.Mark == 1).Select(n => (PantheonNode)n.ModNPC).First();
        other.Charge(other.Goal); Check(b.Chosen == 2 && !b.IsBroken(1) && !other.Open, "one selected suppressed arm each round"); b.Cleanup();
        Log("PANTHEON_COUNTERPLAY_PASS: suppression=17 oneChoiceArm=true seedCover=true finiteFinale=true multipartSharedPool=true");
    }
    // Menu mode has no initialized server transport. Use complete native buffers and in-memory
    // sockets just for this fixture, never patch/suppress gameplay SendData or open a real listener.
    private sealed class NativeTransport : IDisposable
    {
        private readonly RemoteClient[] clients = (RemoteClient[])Netplay.Clients.Clone();
        private readonly MessageBuffer[] buffers = (MessageBuffer[])NetMessage.buffer.Clone();
        private readonly int mode = Main.netMode;
        private readonly FieldInfo idField;
        private readonly FieldInfo modsField;
        private readonly Mod[] mods;
        private readonly object?[] oldIds;
        private readonly object? oldMods;
        public readonly List<byte[]> Frames = new();
        public NativeTransport()
        {
            Check(Main.gameMenu && Environment.GetCommandLineArgs().Contains("-starfall-art-smoke"), "isolated transport only");
            idField = typeof(Mod).GetField("netID", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Native fixture: Mod.netID API changed");
            Type modNet = typeof(Mod).Assembly.GetType("Terraria.ModLoader.ModNet")
                ?? throw new InvalidOperationException("Native fixture: ModNet API changed");
            modsField = modNet.GetField("netMods", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Native fixture: ModNet.netMods API changed");
            mods = ModLoader.Mods.ToArray(); oldIds = mods.Select(m => idField.GetValue(m)).ToArray(); oldMods = modsField.GetValue(null);
            try
            {
                // Optional normal OnKill hooks may send their own ModPacket. Provide a complete
                // negotiated table just as the existing Divine native transport fixture does.
                for (int i = 0; i < mods.Length; i++) idField.SetValue(mods[i], (short)i);
                modsField.SetValue(null, mods);
                for (int i = 0; i < NetMessage.buffer.Length; i++) NetMessage.buffer[i] = new MessageBuffer { whoAmI = i, broadcast = i == 0 };
                for (int i = 0; i < Netplay.Clients.Length; i++) Netplay.Clients[i] = new RemoteClient { Id = i, State = i == 0 ? 10 : 0, Socket = new MemorySocket(Frames, i == 0) };
            }
            catch { Dispose(); throw; }
        }
        public void Dispose()
        {
            for (int i = 0; i < mods.Length; i++) idField.SetValue(mods[i], oldIds[i]);
            modsField.SetValue(null, oldMods);
            Array.Copy(clients, Netplay.Clients, clients.Length); Array.Copy(buffers, NetMessage.buffer, buffers.Length); Main.netMode = mode;
        }
    }
    private sealed class ChecklistRecordFixture : IDisposable
    {
        private readonly List<(FieldInfo Field, object? Value)> fields = new();
        private readonly Terraria.IO.WorldFileData oldWorld = Main.ActiveWorldFileData;
        private bool initialized;
        public ChecklistRecordFixture()
        {
            Check(Main.gameMenu && Main.netMode == NetmodeID.Server, "optional record fixture is isolated server-only");
            if (!ModLoader.TryGetMod("BossChecklist", out Mod checklist)) return;
            ModSystem records = checklist.GetContent<ModSystem>().Single(s => s.Name == "RecordSystem");
            Type type = records.GetType(); const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            try
            {
                // Replace, rather than clear, the original collections. Original records and array
                // references are restored even if a native OnKill or a test assertion throws.
                foreach (string name in new[] { "WorldRecordsForWorld", "WorldRecordsForWorld_Unloaded", "ActiveNPCEntryFlags", "ServerRecordCollection" })
                {
                    FieldInfo field = type.GetField(name, flags) ?? throw new InvalidOperationException("Boss Checklist record fixture API changed: " + name);
                    fields.Add((field, field.GetValue(null)));
                    field.SetValue(null, field.FieldType.IsArray ? null : Activator.CreateInstance(field.FieldType));
                }
                Main.ActiveWorldFileData = new Terraria.IO.WorldFileData("", false);
                records.OnWorldLoad(); records.LoadWorldData(new TagCompound());
                FieldInfo server = fields.Single(f => f.Field.Name == "ServerRecordCollection").Field;
                Check(server.GetValue(null) is Array array && array.Length == Main.maxPlayers && array.GetValue(0) != null && array.GetValue(1) != null,
                    "Boss Checklist real server record initialization includes both fixture players");
                initialized = true;
                Log("PANTHEON_CHECKLIST_RECORD_FIXTURE_READY: nativeOnWorldLoad=server nativeLoadWorldData=true normalOnKillEnabled=true originalReferencesPreserved=true");
            }
            catch { Dispose(); throw; }
        }
        public void Dispose()
        {
            foreach (var saved in fields) saved.Field.SetValue(null, saved.Value);
            Main.ActiveWorldFileData = oldWorld;
            if (initialized) Log("PANTHEON_CHECKLIST_RECORD_FIXTURE_RESTORED: originalCollections=true globalOnKillNeverDisabled=true");
            initialized = false;
        }
    }
    private sealed class MemorySocket : Terraria.Net.Sockets.ISocket
    {
        private readonly List<byte[]> frames; private readonly bool connected;
        public MemorySocket(List<byte[]> frames, bool connected) { this.frames = frames; this.connected = connected; }
        public void Close() { }
        public bool IsConnected() => connected;
        public void Connect(Terraria.Net.RemoteAddress address) => throw new InvalidOperationException("No external connections in native combat fixture");
        public void AsyncSend(byte[] data, int offset, int size, Terraria.Net.Sockets.SocketSendCallback callback, object state)
        { byte[] bytes = new byte[size]; Array.Copy(data, offset, bytes, 0, size); frames.Add(bytes); callback?.Invoke(state); }
        public void AsyncReceive(byte[] data, int offset, int size, Terraria.Net.Sockets.SocketReceiveCallback callback, object state)
            => throw new InvalidOperationException("No external receive in native combat fixture");
        public bool IsDataAvailable() => false;
        public void SendQueuedPackets() { }
        public bool StartListening(Terraria.Net.Sockets.SocketConnectionAccepted callback) => throw new InvalidOperationException("No listener in native combat fixture");
        public void StopListening() { }
        public Terraria.Net.RemoteAddress GetRemoteAddress() => new Terraria.Net.TcpAddress(System.Net.IPAddress.Loopback, 0);
    }
}
