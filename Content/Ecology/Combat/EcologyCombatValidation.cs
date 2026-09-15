using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Ecology.Combat;

/// <summary>Native, opt-in isolated-menu fixtures; never invoked while a real world is loaded.</summary>
public static class EcologyCombatValidation
{
    private static void Check(bool value, string why) { if (!value) throw new InvalidOperationException("Ecology combat: " + why); }
    private static void Log(string message) => ModContent.GetInstance<EcologyChecklist>().Mod.Logger.Info(message);
    public static void Data()
    {
        Localizations();
        int mode = Main.GameMode;
        try
        {
            for (int d = 0; d < 3; d++)
            {
                Main.GameMode = d;
                for (int i = 0; i < 10; i++)
                {
                    NPC n = new(); n.SetDefaults(EcologyCatalog.NPCType(i));
                    Check(n.ModNPC is EcologyBossNPC b && b.Index == i && n.boss, "boss registration " + i);
                    var boss = (EcologyBossNPC)n.ModNPC; boss.ApplyDifficultyAndPlayerScaling(1, 1, 1);
                    Check(n.lifeMax == (int)(EcologyCatalog.Lives[i] * EcologyBossNPC.DifficultyLife), "HP budget " + i + "/" + d);
                    Check(n.damage == boss.ContactDamage && n.defense == EcologyCatalog.Defenses[i], "contact/defense " + i);
                    Check(n.BossBar is EcologyBossBar && n.GetBossHeadTextureIndex() >= 0, "native bar/head " + i);
                    Bestiary(n.type, i);
                    int multiplier = d == 0 ? 2 : d == 1 ? 4 : 6;
                    foreach (bool heavy in new[] { false, true })
                        Check(boss.EngineDamage(heavy) * multiplier <= boss.ShotDamage(heavy) && boss.ShotDamage(heavy) - boss.EngineDamage(heavy) * multiplier < multiplier, "hostile multiplier budget");
                    Item summon = new(); summon.SetDefaults(EcologyCatalog.Summon(i));
                    Check(summon.ModItem is EcologySummonBase s && s.Index == i && !summon.consumable && summon.maxStack == 1, "reusable summon " + i);
                    Check(Recipe.numRecipes == 0 || Main.recipe.Take(Recipe.numRecipes).Any(r => r.createItem.type == summon.type), "summon recipe " + i);
                }
            }
            for (int i = 0; i < 40; i++)
            {
                NPC n = new(); n.SetDefaults(ModContent.Find<ModNPC>("StarfallThrone", "EcologyMob" + i).Type);
                Check(n.ModNPC is EcologyMobNPC m && m.Index == i && !n.boss && m.Banner == n.type && m.BannerItem == EcologyCatalog.Item("EcologyBanner" + i), "mob/banner " + i);
                Bestiary(n.type, i / 4);
                string key = "Mods.StarfallThrone.EcologyCombat.MobBestiary" + i;
                Check(Language.GetTextValue(key) != key, "localized mob bestiary " + i);
            }
        }
        finally { Main.GameMode = mode; }
        Log("ECOLOGY_COMBAT_DATA_PASS: 10 bosses, 40 mobs/banners, reusable summons, native heads/bars, normal/expert/master budgets and 2/4/6 projectile normalization.");
    }
    private static void Bestiary(int npcType, int biomeIndex)
    {
        ModBiome biome = ModContent.Find<ModBiome>("StarfallThrone", "EcologyBiome" + biomeIndex);
        ModNPC template = NPCLoader.GetNPC(npcType);
        Check(template.SpawnModBiomes?.SequenceEqual(new[] { biome.Type }) == true, "template bestiary biome " + template.Name);
        var entry = Main.BestiaryDB.FindEntryByNPCID(npcType);
        var element = biome.ModBiomeBestiaryInfoElement;
        Check(entry != null && element != null && entry.Info.Count(info => ReferenceEquals(info, element)) == 1,
            "actual bestiary entry biome " + template.Name + " / " + biome.Name);
    }
    public static void Localizations()
    {
        GameCulture original = Language.ActiveCulture;
        try
        {
            foreach (GameCulture.CultureName culture in new[] { GameCulture.CultureName.English, GameCulture.CultureName.Chinese })
            {
                LanguageManager.Instance.SetLanguage(GameCulture.FromCultureName(culture));
                bool chinese = culture == GameCulture.CultureName.Chinese;
                void Resolved(string key)
                {
                    string value = Language.GetTextValue(key);
                    Check(!string.IsNullOrWhiteSpace(value) && value != key, "unresolved full localization key " + key);
                    Check(chinese == value.Any(c => c >= '\u4e00' && c <= '\u9fff'), "wrong language at " + key + " => " + value);
                }
                for (int i = 0; i < 10; i++)
                {
                    string npcKey = "Mods.StarfallThrone.NPCs.EcologyBoss" + i + ".DisplayName";
                    string itemKey = "Mods.StarfallThrone.Items.EcologySummon" + i;
                    ModNPC npc = ModContent.Find<ModNPC>("StarfallThrone", "EcologyBoss" + i);
                    ModItem item = ModContent.Find<ModItem>("StarfallThrone", "EcologySummon" + i);
                    Check(npc.DisplayName.Key == npcKey && item.DisplayName.Key == itemKey + ".DisplayName" && item.Tooltip.Key == itemKey + ".Tooltip", "registered localization category " + i);
                    Resolved(npcKey); Resolved(itemKey + ".DisplayName"); Resolved(itemKey + ".Tooltip");
                    Resolved("Mods.StarfallThrone.EcologyCombat.Bestiary" + i); Resolved("Mods.StarfallThrone.EcologyCombat.Spawn" + i);
                }
                for (int i = 0; i < 40; i++)
                {
                    string key = "Mods.StarfallThrone.NPCs.EcologyMob" + i + ".DisplayName";
                    Check(ModContent.Find<ModNPC>("StarfallThrone", "EcologyMob" + i).DisplayName.Key == key, "mob localization category " + i);
                    Resolved(key); Resolved("Mods.StarfallThrone.EcologyCombat.MobBestiary" + i);
                }
                Resolved("Mods.StarfallThrone.NPCs.EcologyPart.DisplayName");
                Resolved("Mods.StarfallThrone.Projectiles.EcologyHazard.DisplayName");
                Resolved("Mods.StarfallThrone.EcologyCombat.Despawn");
            }
        }
        finally { LanguageManager.Instance.SetLanguage(original); }
        Log("ECOLOGY_COMBAT_LOCALIZATION_PASS: both cultures resolve all 51 NPC names, 10 summon names/tooltips, 50 bestiary texts and 10 Checklist spawn texts at their full registered keys.");
    }
    private static void ResetActors()
    {
        for (int i = 0; i < Main.npc.Length; i++) Main.npc[i] = new NPC { whoAmI = i };
        for (int i = 0; i < Main.projectile.Length; i++) Main.projectile[i] = new Projectile { whoAmI = i };
    }
    private static EcologyBossNPC SpawnFixture(int i, Player player, int phase)
    {
        NPC n = new(); n.SetDefaults(EcologyCatalog.NPCType(i)); n.whoAmI = 0; n.active = true; n.target = 0;
        n.Center = player.Center + new Vector2(-210, -130); Main.npc[0] = n;
        var b = (EcologyBossNPC)n.ModNPC;
        // Enter after production arena setup. Main's world fixture separately covers physical anchors and summon eligibility.
        b.Ready = true; b.Serial = 77000 + i; b.RegionId = -1; b.ArenaBounds = new Rectangle(270, 85, 120, 100);
        b.Arena = player.Center; b.Aim = player.Center; b.Origin = n.Center; b.Mark = player.Center;
        n.ai[3] = phase; if (phase == 1) n.life = n.lifeMax / 3;
        return b;
    }
    private static void Tick(EcologyBossNPC b, HashSet<EcologyShape> shapes)
    {
        b.AI(); b.NPC.position += b.NPC.velocity;
        Check(float.IsFinite(b.NPC.Center.X) && float.IsFinite(b.NPC.Center.Y), "finite boss motion");
        foreach (NPC n in Main.ActiveNPCs)
            if (n.ModNPC is EcologyPart part) { part.AI(); n.position += n.velocity; }
        int shots = 0, parts = 0;
        foreach (NPC n in Main.ActiveNPCs) if (n.ModNPC is EcologyPart) parts++;
        foreach (Projectile p in Main.ActiveProjectiles)
            if (p.ModProjectile is EcologyHazard h)
            {
                shots++; shapes.Add(h.Shape); h.AI();
                if (h.Age < h.Delay) Check(h.CanDamage() == false && h.Colliding(p.Hitbox, p.Hitbox) == false, "warning damaged player");
                Check(!h.ShouldUpdatePosition(), "double integration");
                if (--p.timeLeft <= 0) p.Kill();
                Check(float.IsFinite(p.Center.X) && float.IsFinite(p.Center.Y), "finite projectile");
            }
        Check(shots <= 64 && parts <= 10, "encounter cap");
    }
    public static void Run()
    {
        Check(Main.gameMenu && Environment.GetCommandLineArgs().Contains("-starfall-art-smoke"), "isolated smoke menu required");
        NPC[] npcs = (NPC[])Main.npc.Clone(); Projectile[] shots = (Projectile[])Main.projectile.Clone();
        Player[] players = (Player[])Main.player.Clone(); Item[] items = (Item[])Main.item.Clone();
        int mode = Main.GameMode, net = Main.netMode, local = Main.myPlayer; bool day = Main.dayTime;
        try
        {
            Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0;
            for (int i = 0; i < Main.maxPlayers; i++) Main.player[i] = new Player { whoAmI = i, active = false };
            for (int i = 0; i < Main.item.Length; i++) Main.item[i] = new Item();
            Player player = Main.player[0]; player.active = true; player.Center = new Vector2(5200, 2150); player.ResetEffects();
            for (int id = 0; id < 10; id++)
            {
                HashSet<EcologyShape> shapes = new();
                for (int d = 0; d < 3; d++) for (int phase = 0; phase < 2; phase++)
                {
                    Main.GameMode = d; ResetActors(); EcologyBossNPC b = SpawnFixture(id, player, phase);
                    HashSet<int> moves = new(); bool packet = false;
                    for (int tick = 0; tick < 1750; tick++)
                    {
                        player.Center = new Vector2(5200 + MathF.Sin(tick * .008f) * 100, 2150 + MathF.Sin(tick * .013f) * 35);
                        Tick(b, shapes);
                        Check(b.NPC.active && !b.Cancelled, "unexpected arena cancellation " + id);
                        if (b.Stage == EcologyStage.Attack) moves.Add(b.Move);
                        int cooldown = 0; if (b.Stage != EcologyStage.Attack) Check(!b.CanHitPlayer(player, ref cooldown), "contact during warning/recovery");
                        if (!packet && Main.projectile.FirstOrDefault(p => p.active && p.ModProjectile is EcologyHazard candidate && candidate.OwnedBy(b))?.ModProjectile is EcologyHazard h)
                        { Network(b, h); packet = true; }
                    }
                    Check(moves.Count == 4 && packet, "all move/network coverage " + id + "/" + d + "/" + phase);
                    int health = b.NPC.life; Main.dayTime = !Main.dayTime; b.AI(); Check(b.NPC.active && b.NPC.life == health, "dawn/no hidden healing");
                    b.NPC.ai[3] = 0; b.NPC.life = b.NPC.lifeMax / 3; b.AI();
                    Check(b.Stage == EcologyStage.Transition && !Main.projectile.Any(p => p.active && p.ModProjectile is EcologyHazard), "phase cleanup");
                    player.dead = true; b.AI(); player.dead = false;
                    Check(!b.NPC.active && !Main.npc.Any(n => n.active && n.ModNPC is EcologyPart), "wipe cleanup");
                }
                Check(shapes.Count >= 2, "distinct projectile geometry " + id);
                Log("ECOLOGY_BOSS_AI_PASS: " + EcologyCatalog.Keys[id] + "; 4 moves, 2 phases, 3 difficulties, warnings, packets, finite hazards and wipe cleanup.");
            }
            GeometryAndParts(player); Mobs(player); Render(player);
            Log("ECOLOGY_COMBAT_RUNTIME_PASS: 10 native phase encounters, 40 mob AIs, server authority, serial ownership, breakable walls, exposure, native collision and draw hooks.");
        }
        finally
        {
            Array.Copy(npcs, Main.npc, npcs.Length); Array.Copy(shots, Main.projectile, shots.Length);
            Array.Copy(players, Main.player, players.Length); Array.Copy(items, Main.item, items.Length);
            Main.GameMode = mode; Main.netMode = net; Main.myPlayer = local; Main.dayTime = day;
        }
    }
    private static void GeometryAndParts(Player player)
    {
        Main.GameMode = 0; ResetActors(); EcologyBossNPC b = SpawnFixture(6, player, 0);
        Vector2 p = player.Center;
        EcologyHazard beam = b.Emit(EcologyShape.Beam, p - new Vector2(20, 0), Vector2.Zero, 30, 60, 10, p + new Vector2(20, 0));
        Rectangle target = new((int)p.X - 30, (int)p.Y - 30, 60, 60);
        Check(beam != null && beam.Colliding(target, target) == false, "warning collision");
        beam.Age = beam.Delay; Check(beam.Colliding(target, target) == true, "contained beam collision");
        Check(beam.Colliding(target, new Rectangle((int)p.X + 300, (int)p.Y, 20, 20)) == false, "beam miss");
        EcologyPart root = b.Part(7, p, Vector2.Zero, 150);
        Check(root != null, "native breakable part"); beam.PartSlot = root.NPC.whoAmI;
        root.CheckDead(); beam.AI(); Check(!beam.Projectile.active, "breaking root removes wall");
        EcologyPart weak = b.Part(1, p, new Vector2(0, -40)); weak.CheckDead(); Check(b.Exposed > 0, "weakpoint exposes boss");
        EcologyHazard stale = b.Emit(EcologyShape.Shard, p, Vector2.UnitX);
        b.Serial++; stale.AI(); Check(!stale.Projectile.active, "stale serial cleanup"); b.Serial--;
        Main.netMode = NetmodeID.MultiplayerClient;
        Check(b.Emit(EcologyShape.Shard, p, Vector2.Zero) == null && b.Part(7, p, Vector2.Zero) == null, "client cannot spawn hazards/parts");
        Check(!EcologySummonBase.TrySummon(player, 0), "client cannot spawn boss");
        Main.netMode = NetmodeID.SinglePlayer;
        using (var invalid = new BinaryReader(new MemoryStream(new byte[] { 255 }))) EcologySummonBase.ReceivePacket(invalid, -1);
        Check(!EcologySummonBase.Valid(player, -1) && !EcologySummonBase.Valid(player, 10), "summon range validation");
        b.Cleanup();
    }
    private static void Mobs(Player player)
    {
        for (int i = 0; i < 40; i++)
        {
            ResetActors(); NPC n = new(); n.SetDefaults(ModContent.Find<ModNPC>("StarfallThrone", "EcologyMob" + i).Type);
            n.whoAmI = 0; n.active = true; n.target = 0; n.Center = player.Center + new Vector2(-120, -60); Main.npc[0] = n;
            var mob = (EcologyMobNPC)n.ModNPC;
            for (int tick = 0; tick < 365; tick++)
            {
                mob.AI(); n.position += n.velocity;
                Check(float.IsFinite(n.Center.X) && float.IsFinite(n.Center.Y), "mob position " + i);
                foreach (Projectile p in Main.ActiveProjectiles) if (p.ModProjectile is EcologyHazard h) { h.AI(); if (--p.timeLeft <= 0) p.Kill(); }
                Check(Main.projectile.Count(p => p.active && p.ModProjectile is EcologyHazard) <= 12, "mob projectile cap " + i);
            }
            Check(mob.Serial != 0, "mob serial " + i);
            using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory, System.Text.Encoding.UTF8, true);
            mob.SendExtraAI(writer); writer.Flush(); memory.Position = 0;
            NPC copy = new(); copy.SetDefaults(n.type); var remote = (EcologyMobNPC)copy.ModNPC; remote.ReceiveExtraAI(new BinaryReader(memory));
            Check(remote.Serial == mob.Serial && remote.Aim == mob.Aim, "mob packet " + i);
        }
        Log("ECOLOGY_MOBS_RUNTIME_PASS: 40 native AIs, bounded shots, material/banner registration and committed-aim network roundtrips.");
    }
    private static void Network(EcologyBossNPC b, EcologyHazard h)
    {
        using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory, System.Text.Encoding.UTF8, true);
        b.SendExtraAI(writer); writer.Flush(); memory.Position = 0;
        NPC n = new(); n.SetDefaults(b.Type); n.ai = (float[])b.NPC.ai.Clone(); var remote = (EcologyBossNPC)n.ModNPC; remote.ReceiveExtraAI(new BinaryReader(memory));
        Check(remote.Serial == b.Serial && remote.Ready && remote.Aim == b.Aim && remote.ArenaBounds == b.ArenaBounds, "boss packet roundtrip");
        memory.SetLength(0); memory.Position = 0; h.SendExtraAI(writer); writer.Flush(); memory.Position = 0;
        Projectile p = new(); p.SetDefaults(h.Type); p.ai = (float[])h.Projectile.ai.Clone(); var shot = (EcologyHazard)p.ModProjectile; shot.ReceiveExtraAI(new BinaryReader(memory));
        Check(shot.Serial == h.Serial && shot.Delay == h.Delay && shot.Anchor == h.Anchor && shot.Launch == h.Launch && shot.PartSlot == h.PartSlot, "hazard packet roundtrip");
    }
    private static void Render(Player player)
    {
        GraphicsDevice device = Main.instance.GraphicsDevice; var targets = device.GetRenderTargets(); Rectangle scissor = device.ScissorRectangle; Vector2 screen = Main.screenPosition;
        using var canvas = new RenderTarget2D(device, 1800, 1250); using var clip = new RasterizerState { ScissorTestEnable = true };
        try
        {
            device.SetRenderTarget(canvas); device.Clear(new Color(16, 21, 32));
            for (int i = 0; i < 10; i++)
            {
                ResetActors(); player.Center = new Vector2(5200, 2150); EcologyBossNPC b = SpawnFixture(i, player, 1);
                b.NPC.ai[0] = (int)EcologyStage.Attack; b.NPC.ai[1] = 1; b.NPC.ai[2] = i == 5 || i == 6 || i == 9 ? 1 : 0;
                HashSet<EcologyShape> shapes = new(); for (int t = 0; t < 40; t++) Tick(b, shapes);
                int x = i % 2 * 900, y = i / 2 * 250;
                Main.screenPosition = b.NPC.Center - new Vector2(650, 165);
                device.ScissorRectangle = new Rectangle(x, y, 900, 250);
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, clip, null, Matrix.CreateScale(.65f) * Matrix.CreateTranslation(x, y, 0));
                try
                {
                    b.PreDraw(Main.spriteBatch, Main.screenPosition, Color.White);
                    foreach (NPC n in Main.ActiveNPCs) if (n.ModNPC is EcologyPart part) part.PreDraw(Main.spriteBatch, Main.screenPosition, Color.White);
                    foreach (Projectile p in Main.ActiveProjectiles) if (p.ModProjectile is EcologyHazard h) { Color c = Color.White; h.PreDraw(ref c); }
                }
                finally { Main.spriteBatch.End(); }
                Main.spriteBatch.Begin();
                try { Utils.DrawBorderString(Main.spriteBatch, EcologyCatalog.BossNames[i], new Vector2(x + 20, y + 220), Color.White, .8f); }
                finally { Main.spriteBatch.End(); }
            }
            string path = Path.GetFullPath(Path.Combine(Main.SavePath, "..", "..", "outputs", "ecology-combat-runtime.png"));
            using var stream = File.Create(path); canvas.SaveAsPng(stream, canvas.Width, canvas.Height);
            Log("ECOLOGY_COMBAT_RENDER_PASS: actual boss/part/projectile hooks in ecology-combat-runtime.png.");
        }
        finally { device.SetRenderTargets(targets); device.ScissorRectangle = scissor; Main.screenPosition = screen; }
    }
}
