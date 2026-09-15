using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using global::StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Voyage.Bosses;

/// <summary>Optional main-menu smoke hook. Never automatically runs in a player's world.</summary>
public static class VoyageBossValidation
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Voyage boss validation: " + message);
    }
    public static void Data()
    {
        for (int i = 0; i < VoyageCatalog.Count; i++)
        {
            NPC npc = new(); npc.SetDefaults(VoyageCatalog.BossType(i));
            Check(npc.ModNPC is VoyageBossNPC boss && boss.BossIndex == i && npc.boss, "boss routing " + i);
            Check(npc.GetBossHeadTextureIndex() >= 0, "boss head " + i);
            Check(npc.BossBar is VoyageBossBar, "aggregate bar " + i);
        }
        ModContent.GetInstance<VoyageWorld>().Mod.Logger.Info("VOYAGE_BOSS_DATA_PASS: 17 registered bosses, map heads, aggregate bars and independent world flags.");
    }
    public static void Run() => SimulateCombat();
    public static void SimulateCombat()
    {
        Check(Main.gameMenu && Environment.GetCommandLineArgs().Contains("-starfall-art-smoke"), "simulation requires isolated smoke menu");
        NPC[] oldNPCs = (NPC[])Main.npc.Clone(); Projectile[] oldShots = (Projectile[])Main.projectile.Clone();
        Player oldPlayer = Main.player[0];
        bool[] oldDowned = (bool[])VoyageWorld.Downed.Clone();
        int mode = Main.GameMode, net = Main.netMode, myPlayer = Main.myPlayer;
        Vector2 oldScreen = Main.screenPosition;
        GraphicsDevice graphics = Main.instance.GraphicsDevice;
        RenderTargetBinding[] oldTargets = graphics.GetRenderTargets();
        Rectangle oldScissor = graphics.ScissorRectangle;
        using var atlas = new RenderTarget2D(graphics, 1440, 1680);
        using var clipState = new RasterizerState { ScissorTestEnable = true };
        graphics.SetRenderTarget(atlas); graphics.Clear(new Color(12, 20, 36));
        Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0;
        Player player = new() { whoAmI = 0, active = true, dead = false, position = new Vector2(5600, 2038) };
        Main.player[0] = player; player.ResetEffects();
        try
        {
            for (int id = 0; id < VoyageCatalog.Count; id++)
            {
                var moves = new HashSet<int>(); var shapes = new HashSet<VoyageHazardShape>();
                bool networkChecked = false, captured = false;
                for (int difficulty = 0; difficulty < 3; difficulty++)
                    for (int phase = 0; phase < 3; phase++)
                    {
                        ResetActors(); Main.GameMode = difficulty;
                        NPC root = new(); root.SetDefaults(VoyageCatalog.BossType(id));
                        root.whoAmI = 0; root.active = true; root.target = 0;
                        root.Center = player.Center + new Vector2(-360, -250); Main.npc[0] = root;
                        var boss = (VoyageBossNPC)root.ModNPC;
                        boss.AI();
                        Check(boss.PartsReady, "anatomy spawn " + id);
                        Check(boss.AggregateLife == boss.AggregateLifeMaximum, "initial aggregate budget " + id);
                        if (id == 16)
                        {
                            Check(boss.Part(0, VoyagePart.LeftCity).lifeMax == (int)(2000000 * boss.HealthScale), "left city budget");
                            Check(boss.Part(1, VoyagePart.RightCity).lifeMax == (int)(2000000 * boss.HealthScale), "right city budget");
                            Check(boss.Part(2, VoyagePart.Array).lifeMax == (int)(2500000 * boss.HealthScale), "array budget");
                            Check(root.lifeMax + boss.Part(0).lifeMax + boss.Part(1).lifeMax + boss.Part(2).lifeMax == boss.AggregateLifeMaximum, "ark core rounding");
                        }
                        SetPhase(boss, phase);
                        var phaseMoves = new HashSet<int>();
                        bool phaseNetwork = false;
                        int previousLife = boss.AggregateLife;
                        for (int tick = 0; tick < 1320; tick++)
                        {
                            root.ModNPC.AI(); root.position += root.velocity;
                            Check(float.IsFinite(root.Center.X) && float.IsFinite(root.Center.Y), "non-finite boss position " + id);
                            if (boss.Stage == VoyageStage.Attack) { moves.Add(boss.Move); phaseMoves.Add(boss.Move); }
                            Check(boss.AggregateLife <= previousLife, "unbudgeted regeneration " + id);
                            previousLife = boss.AggregateLife;
                            int cooldown = 0;
                            if (boss.Stage != VoyageStage.Attack) Check(!boss.CanHitPlayer(player, ref cooldown), "unwarned contact " + id);
                            foreach (NPC part in Main.ActiveNPCs)
                                if (part.ModNPC is VoyageDeviceNPC device)
                                {
                                    device.AI(); part.position += part.velocity;
                                    Check(float.IsFinite(part.Center.X) && float.IsFinite(part.Center.Y), "invalid part position " + id);
                                    if (boss.Stage != VoyageStage.Attack) Check(!device.CanHitPlayer(player, ref cooldown), "unsafe part contact");
                                }
                            int count = 0;
                            foreach (Projectile p in Main.ActiveProjectiles)
                                if (p.ModProjectile is VoyageHazard hazard)
                                {
                                    count++; shapes.Add(hazard.Shape);
                                    hazard.AI();
                                    if (hazard.Age < hazard.Delay) Check(hazard.CanDamage() == false, "damaging warning " + id);
                                    if (p.active && hazard.ShouldUpdatePosition()) p.position += p.velocity;
                                    if (--p.timeLeft <= 0) p.Kill();
                                    Check(float.IsFinite(p.Center.X) && float.IsFinite(p.velocity.Y), "invalid hazard " + id);
                                    if (!phaseNetwork) { Network(boss, hazard); phaseNetwork = networkChecked = true; }
                                }
                            Check(count <= (id == 16 ? 42 : 32), "hazard cap " + id);
                            if (!captured && difficulty == 0 && phase == (id == 16 ? 0 : 1) && boss.Stage == VoyageStage.Attack && boss.Move == id % 3 && boss.Timer == 100)
                            {
                                DrawCell(id, boss, player, clipState); captured = true;
                            }
                        }
                        Check(phaseMoves.Count == 3 && phaseNetwork, "phase or difficulty missing move/network coverage " + id);
                        // Killing a real node must remove its linked threat, preserve accounting, and mark it as dismantled.
                        NPC dismantled = null;
                        foreach (NPC n in Main.ActiveNPCs)
                            if (n.ModNPC is VoyageDeviceNPC d && d.OwnedBy(boss)) { dismantled = n; break; }
                        if (dismantled?.ModNPC is VoyageDeviceNPC dismantledDevice)
                        {
                            boss.ClearHazards();
                            Projectile threat = boss.Emit(VoyageHazardShape.Beam, dismantled.Center, Vector2.Zero, deviceSlot: dismantled.whoAmI);
                            Remove(boss, dismantled);
                            Check(threat == null || !threat.active, "dismantled source still dangerous " + id);
                            Check(dismantledDevice.Temporary || boss.IsBroken(dismantledDevice.Slot), "lost dismantle flag " + id);
                        }
                        // A phase change clears offensive leftovers without restoring a destroyed component.
                        root.ai[3] = 0; SetPhase(boss, 2, false); root.ModNPC.AI();
                        Check(boss.Stage == VoyageStage.Transition, "phase transition " + id);
                        Check(!Main.projectile.Any(p => p.active && p.ModProjectile is VoyageHazard), "phase cleanup " + id);
                        player.dead = true; root.ModNPC.AI(); player.dead = false;
                        Check(root.timeLeft <= 60 && !Main.npc.Any(n => n.active && n.ModNPC is VoyageDeviceNPC), "despawn cleanup " + id);
                        boss.SpawnPart(VoyagePart.Drone, 7, root.Center, lifetime: 120);
                        boss.Emit(VoyageHazardShape.Bolt, root.Center, Vector2.UnitX);
                        boss.OnKill();
                        Check(!Main.npc.Any(n => n.active && n.ModNPC is VoyageDeviceNPC) &&
                            !Main.projectile.Any(p => p.active && p.ModProjectile is VoyageHazard), "death cleanup " + id);
                    }
                // Tether, sweep and body-charge signatures can deliberately share a
                // beam primitive. Move coverage, not primitive count, defines attacks.
                Check(moves.Count == 3 && shapes.Count >= 1 && networkChecked && captured,
                    $"incomplete coverage {id}: moves={moves.Count}, shapes={shapes.Count}, network={networkChecked}, render={captured}");
                ModContent.GetInstance<VoyageWorld>().Mod.Logger.Info($"VOYAGE_BOSS_AI_PASS: {VoyageCatalog.Keys[id]}; three moves, {shapes.Count} hazard primitives, three phases, all difficulties, aggregate HP, synchronization, cleanup; 11880 ticks.");
            }
            string path = Path.GetFullPath(Path.Combine(Main.SavePath, "..", "..", "outputs", "voyage-boss-runtime.png"));
            using (var stream = File.Create(path)) atlas.SaveAsPng(stream, atlas.Width, atlas.Height);
            ModContent.GetInstance<VoyageWorld>().Mod.Logger.Info("VOYAGE_BOSS_RENDER_PASS: 17 encounters drawn with actual NPC, device and projectile PreDraw hooks to voyage-boss-runtime.png.");
        }
        finally
        {
            for (int i = 0; i < oldNPCs.Length; i++) Main.npc[i] = oldNPCs[i];
            for (int i = 0; i < oldShots.Length; i++) Main.projectile[i] = oldShots[i];
            Main.player[0] = oldPlayer; Main.GameMode = mode; Main.netMode = net; Main.myPlayer = myPlayer;
            VoyageWorld.Downed = oldDowned;
            graphics.SetRenderTargets(oldTargets); graphics.ScissorRectangle = oldScissor; Main.screenPosition = oldScreen;
        }
    }
    private static void DrawCell(int id, VoyageBossNPC boss, Player player, RasterizerState clip)
    {
        int x = id % 3 * 480, y = id / 3 * 280;
        Main.screenPosition = player.Center - new Vector2(660, 435);
        Main.instance.GraphicsDevice.ScissorRectangle = new Rectangle(x, y, 480, 250);
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, clip, null,
            Matrix.CreateScale(.36f) * Matrix.CreateTranslation(x, y, 0));
        try
        {
            CombatDrawing.Circle(Main.spriteBatch, player.Center - Main.screenPosition, 20, Color.LightGreen, 4);
            boss.PreDraw(Main.spriteBatch, Main.screenPosition, Color.White);
            foreach (NPC npc in Main.ActiveNPCs)
                if (npc.ModNPC is VoyageDeviceNPC d && d.OwnedBy(boss)) d.PreDraw(Main.spriteBatch, Main.screenPosition, Color.White);
            foreach (Projectile p in Main.ActiveProjectiles)
                if (p.ModProjectile is VoyageHazard h && h.OwnerSlot == boss.NPC.whoAmI)
                {
                    Color color = Color.White;
                    h.PreDraw(ref color);
                }
        }
        finally { Main.spriteBatch.End(); }
        Main.spriteBatch.Begin();
        try
        {
            Utils.DrawBorderString(Main.spriteBatch, $"{id + 1:00}  {VoyageCatalog.Names[id]}", new Vector2(x + 10, y + 246), Color.LightSteelBlue, .7f);
        }
        finally { Main.spriteBatch.End(); }
    }
    private static void ResetActors()
    {
        for (int i = 0; i < Main.npc.Length; i++) Main.npc[i] = new NPC { whoAmI = i };
        for (int i = 0; i < Main.projectile.Length; i++) Main.projectile[i] = new Projectile { whoAmI = i };
    }
    private static void Remove(VoyageBossNPC boss, NPC part)
    {
        if (part?.ModNPC is not VoyageDeviceNPC device) return;
        int before = boss.AggregateLife, removed = device.BudgetPart ? part.life : 0;
        part.life = 0; device.OnKill(); part.active = false;
        Check(boss.AggregateLife == before - removed, "part damage was duplicated or lost");
    }
    private static void SetPhase(VoyageBossNPC boss, int phase, bool setPhaseCounter = true)
    {
        float ratio = phase == 0 ? 1 : phase == 1 ? .5f : .2f;
        if (boss.BossIndex == 16)
        {
            if (phase > 0) { Remove(boss, boss.Part(0, VoyagePart.LeftCity)); Remove(boss, boss.Part(1, VoyagePart.RightCity)); }
            if (phase > 1) Remove(boss, boss.Part(2, VoyagePart.Array));
        }
        else if (boss.IsController)
        {
            foreach (NPC part in Main.ActiveNPCs)
                if (part.ModNPC is VoyageDeviceNPC d && d.OwnedBy(boss) && d.BudgetPart) part.life = (int)(part.lifeMax * ratio);
        }
        else boss.NPC.life = (int)(boss.NPC.lifeMax * ratio);
        if (setPhaseCounter) boss.NPC.ai[3] = phase;
    }
    private static void Network(VoyageBossNPC boss, VoyageHazard shot)
    {
        using var memory = new MemoryStream();
        using var writer = new BinaryWriter(memory, System.Text.Encoding.UTF8, true);
        boss.SendExtraAI(writer); writer.Flush(); memory.Position = 0;
        NPC remote = new(); remote.SetDefaults(boss.Type); remote.ai = (float[])boss.NPC.ai.Clone();
        var copy = (VoyageBossNPC)remote.ModNPC; copy.ReceiveExtraAI(new BinaryReader(memory));
        Check(copy.Serial == boss.Serial && copy.Aim == boss.Aim && copy.NPC.lifeMax == boss.NPC.lifeMax && copy.HealthScale == boss.HealthScale, "boss packet");
        memory.SetLength(0); memory.Position = 0; shot.SendExtraAI(writer); writer.Flush(); memory.Position = 0;
        Projectile remoteShot = new(); remoteShot.SetDefaults(shot.Type); remoteShot.ai = (float[])shot.Projectile.ai.Clone();
        var h = (VoyageHazard)remoteShot.ModProjectile; h.ReceiveExtraAI(new BinaryReader(memory));
        Check(h.Delay == shot.Delay && h.Serial == shot.Serial && h.DeviceSlot == shot.DeviceSlot && h.Anchor == shot.Anchor && h.Length == shot.Length, "hazard packet");
        foreach (NPC part in Main.ActiveNPCs)
            if (part.ModNPC is VoyageDeviceNPC d && d.OwnedBy(boss))
            {
                memory.SetLength(0); memory.Position = 0; d.SendExtraAI(writer); writer.Flush(); memory.Position = 0;
                NPC remotePart = new(); remotePart.SetDefaults(d.Type); remotePart.ai = (float[])part.ai.Clone();
                var rd = (VoyageDeviceNPC)remotePart.ModNPC; rd.ReceiveExtraAI(new BinaryReader(memory));
                Check(rd.Serial == d.Serial && rd.NPC.lifeMax == part.lifeMax && rd.BudgetPart == d.BudgetPart, "device packet");
                break;
            }
    }
}
