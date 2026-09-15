using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.Items;
using StarfallThrone.Content.NPCs;
using StarfallThrone.Content.Projectiles;

namespace StarfallThrone.Content.Systems;

// Explicit smoke-launch only. Arena, players and NPCs live solely in a throwaway main-menu process.
internal static class FormalCombatValidation
{
    static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException("Combat test: " + message); }
    static void Log(string message) => ModContent.GetInstance<ArtIntegritySystem>().Mod.Logger.Info(message);
    public static void Data()
    {
        for (int id = 0; id < CombatProfiles.Count; id++)
        {
            NPC n = new(); n.SetDefaults(CombatProfiles.NPCType(id));
            Check(n.ModNPC is ReforgedBossNPC b && b.EncounterId == id && n.boss, "formal boss routing " + id);
        }
        for (int id = 0; id < ReforgedWeapons.Count; id++)
        {
            Item item = new(); item.SetDefaults(ReforgedWeapons.Type(id));
            Check(item.DamageType == ReforgedWeapons.Class(id), "weapon class " + id);
            Check(item.ModItem.Tooltip.Value.Length > 10, "missing weapon description " + id);
            if (item.DamageType == DamageClass.Magic) Check(item.mana > 0, "free magic weapon");
            if (id is 11 or 32) Check(item.buffType > 0, "missing minion buff");
            if (id == 36) Check(item.sentry, "missing sentry flag");
            if (id == 9) Check(ProjectileID.Sets.IsAWhip[item.shoot], "tongue whip is not a whip");
            Check(item.shoot != ModContent.ProjectileType<StarfallPlayerBolt>() && item.shoot != ModContent.ProjectileType<PrimordialPlayerProjectile>(), "legacy generic bolt still used");
        }
        Log("COMBAT_DATA_PASS: 27 full bosses routed to the new director; all 39 weapons use their actual damage classes and documented attack types.");
    }
    static void ClearShots()
    {
        foreach (Projectile p in Main.ActiveProjectiles)
            if (p.ModProjectile is CombatHazard or ReforgedWeaponShot or ReforgedTongueWhip or ReforgedAlly) p.active = false;
    }
    static void TickNPC(NPC n)
    {
        n.ModNPC.AI();
        if (!n.noGravity) n.velocity.Y = Math.Min(12, n.velocity.Y + .3f);
        Vector2 wanted = n.velocity, moved = n.noTileCollide ? wanted : Collision.TileCollision(n.position, wanted, n.width, n.height);
        n.collideX = moved.X != wanted.X; n.collideY = moved.Y != wanted.Y; n.position += moved; n.velocity = moved;
        Check(float.IsFinite(n.position.X) && float.IsFinite(n.position.Y), "invalid NPC movement");
    }
    static void TickHazards()
    {
        foreach (Projectile p in Main.ActiveProjectiles)
        {
            if (p.ModProjectile is not CombatHazard hazard) continue;
            hazard.AI(); if (!p.active) continue;
            if (hazard.Age < hazard.Delay) Check(hazard.CanDamage() == false, "damaging telegraph");
            if (hazard.ShouldUpdatePosition())
            {
                Vector2 desired = p.velocity;
                Vector2 moved = p.tileCollide ? Collision.TileCollision(p.position, desired, p.width, p.height) : desired;
                p.position += moved;
                if (desired != moved) { p.velocity = moved; if (hazard.OnTileCollide(desired)) p.Kill(); }
            }
            if (--p.timeLeft <= 0) p.Kill();
            Check(float.IsFinite(p.position.X) && float.IsFinite(p.velocity.Y), "invalid hazard movement");
        }
    }
    static void CheckNetwork(CombatHazard h)
    {
        using var buffer = new MemoryStream(); using var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, true);
        h.SendExtraAI(writer); writer.Flush(); buffer.Position = 0;
        Projectile remote = new(); remote.SetDefaults(h.Type); remote.ai = (float[])h.Projectile.ai.Clone();
        remote.ModProjectile.ReceiveExtraAI(new BinaryReader(buffer)); var copy = (CombatHazard)remote.ModProjectile;
        Check(copy.Age == h.Age && copy.Delay == h.Delay && copy.Duration == h.Duration && copy.Anchor == h.Anchor && copy.Angle == h.Angle && copy.Projectile.width == h.Projectile.width, "hazard network mismatch");
    }
    public static void Run()
    {
        int oldMode = Main.GameMode, oldPlayerId = Main.myPlayer, oldMouseX = Main.mouseX, oldMouseY = Main.mouseY;
        Player oldPlayer = Main.player[0]; NPC oldNPC = Main.npc[0]; double oldSurface = Main.worldSurface;
        Vector2 oldScreen = Main.screenPosition;
        GraphicsDevice device = Main.instance.GraphicsDevice;
        RenderTargetBinding[] oldTargets = device.GetRenderTargets();
        using var atlas = new RenderTarget2D(device, 1440, 2520);
        using var clipState = new RasterizerState { ScissorTestEnable = true };
        Main.myPlayer = 0; Main.worldSurface = 120;
        Player player = new() { whoAmI = 0, active = true, dead = false, position = new Vector2(5600, 2038) };
        Main.player[0] = player; player.selectedItem = 0;
        // A new Player has not had an in-world update yet: initialize normal combat stats,
        // including the vanilla whip range multiplier (otherwise it remains zero).
        player.ResetEffects();
        for (int x = 240; x < 480; x++) for (int y = 50; y < 150; y++)
        { Tile tile = Main.tile[x, y]; tile.ClearEverything(); if (y >= 130) { tile.HasTile = true; tile.TileType = TileID.Dirt; } }
        device.SetRenderTarget(atlas); device.Clear(new Color(19, 25, 36));
        try
        {
            for (int id = 0; id < CombatProfiles.Count; id++)
            {
                var moves = new HashSet<int>(); var shapes = new HashSet<HazardShape>(); bool network = false, captured = false;
                for (int difficulty = 0; difficulty < 3; difficulty++) for (int phase = 0; phase < 3; phase++)
                {
                    Main.GameMode = difficulty; ClearShots();
                    NPC n = new(); n.SetDefaults(CombatProfiles.NPCType(id)); n.whoAmI = 0; n.active = true; n.target = 0;
                    n.Bottom = player.Bottom + new Vector2(-300, CombatProfiles.Grounded(id) ? 0 : -200);
                    n.life = (int)(n.lifeMax * (phase == 0 ? 1f : phase == 1 ? .5f : .2f)); n.ai[3] = phase; Main.npc[0] = n;
                    for (int tick = 0; tick < 1200; tick++)
                    {
                        TickNPC(n); var boss = (ReforgedBossNPC)n.ModNPC;
                        int cooldown = 0;
                        if (boss.CombatState != 2) Check(!boss.CanHitPlayer(player, ref cooldown), "unsafe approach/windup/recovery");
                        if (boss.CombatState == 2) moves.Add(boss.Move);
                        foreach (Projectile shot in Main.ActiveProjectiles)
                            if (shot.ModProjectile is CombatHazard h)
                            {
                                shapes.Add(h.Shape);
                                if (!network) { CheckNetwork(h); network = true; }
                            }
                        TickHazards();
                        if (!captured && difficulty == 0 && phase == 2 && boss.CombatState == 2 && boss.Move == id % 3 && boss.CombatTimer == 60)
                        {
                            DrawCell(id, boss, player, clipState); captured = true;
                        }
                    }
                    // Transition interrupts old hazards without making the boss invulnerable.
                    n.ai[3] = 0; n.life = n.lifeMax / 5; TickNPC(n);
                    Check(((ReforgedBossNPC)n.ModNPC).CombatState == 4 && !n.dontTakeDamage, "phase transition did not pause safely");
                    Check(!Main.projectile.Any(p => p.active && p.ModProjectile is CombatHazard), "phase transition left hazards alive");
                    // A dead target cannot leave permanent attacks behind.
                    player.dead = true; TickNPC(n); player.dead = false;
                    Check(n.timeLeft <= 60, "despawn timeout not restored");
                }
                Check(moves.Count == 3 && network && captured, "incomplete encounter coverage " + id);
                Log($"COMBAT_BOSS_PASS: {CombatProfiles.Key(id)}; three moves, {shapes.Count} hazard forms, normal/expert/master, three phases, network and cleanup; 10800 AI ticks.");
            }
            Main.GameMode = 0; ClearShots();
            using (var stream = File.Create(Path.GetFullPath(Path.Combine(Main.SavePath, "..", "..", "outputs", "combat-boss-runtime.png")))) atlas.SaveAsPng(stream, atlas.Width, atlas.Height);
            device.SetRenderTargets(oldTargets);
            Weapons(player);
            Log("COMBAT_RENDER_PASS: all 27 new encounter scripts rendered to combat-boss-runtime.png using actual game drawing hooks.");
        }
        finally
        {
            ClearShots(); device.SetRenderTargets(oldTargets); Main.player[0] = oldPlayer; Main.npc[0] = oldNPC;
            Main.GameMode = oldMode; Main.myPlayer = oldPlayerId; Main.worldSurface = oldSurface; Main.screenPosition = oldScreen;
            Main.mouseX = oldMouseX; Main.mouseY = oldMouseY;
        }
    }
    static void DrawCell(int id, ReforgedBossNPC boss, Player p, RasterizerState clip)
    {
        int x = id % 3 * 480, y = id / 3 * 280;
        Main.screenPosition = p.Center - new Vector2(520, 330);
        Main.instance.GraphicsDevice.ScissorRectangle = new Rectangle(x, y, 480, 280);
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, clip, null,
            Matrix.CreateScale(.45f) * Matrix.CreateTranslation(x, y + 5, 0));
        CombatDrawing.Line(Main.spriteBatch, new Vector2(0, 370), new Vector2(1060, 370), new Color(61, 83, 64), 5);
        CombatDrawing.Circle(Main.spriteBatch, p.Center - Main.screenPosition, 16, Color.LightGreen, 3);
        boss.PreDraw(Main.spriteBatch, Main.screenPosition, Color.White);
        foreach (Projectile shot in Main.ActiveProjectiles) if (shot.ModProjectile is CombatHazard) { Color c = Color.White; shot.ModProjectile.PreDraw(ref c); }
        Main.spriteBatch.End();
        Main.spriteBatch.Begin();
        Utils.DrawBorderString(Main.spriteBatch, (id < 10 ? PrimordialBossData.DisplayName(id) : BossData.DisplayName(id - 10)) + " · " + (boss.Move + 1), new Vector2(x + 14, y + 245), Color.LightSteelBlue, .8f);
        Main.spriteBatch.End();
    }
    static void Weapons(Player player)
    {
        var shapes = new HashSet<WeaponShape>();
        var device = Main.instance.GraphicsDevice;
        var previousTargets = device.GetRenderTargets();
        using var weaponAtlas = new RenderTarget2D(device, 1440, 1820);
        using var clipState = new RasterizerState { ScissorTestEnable = true };
        device.SetRenderTarget(weaponAtlas); device.Clear(new Color(19, 25, 36));
        for (int id = 0; id < ReforgedWeapons.Count; id++)
        {
            ClearShots();
            NPC dummy = new(); dummy.SetDefaults(NPCID.BlueSlime); dummy.whoAmI = 0; dummy.active = true; dummy.life = dummy.lifeMax = 1000000;
            dummy.Center = player.Center + new Vector2(300, 0); Main.npc[0] = dummy;
            Item item = player.inventory[0]; item.SetDefaults(ReforgedWeapons.Type(id));
            player.itemAnimation = player.itemAnimationMax = item.useAnimation; player.itemTime = item.useTime;
            Main.screenPosition = player.Center - new Vector2(200); Main.mouseX = 340; Main.mouseY = 200;
            var source = new Terraria.DataStructures.EntitySource_ItemUse_WithAmmo(player, item, 0, "CombatSmoke");
            bool spawnVanilla = item.ModItem.Shoot(player, source, player.Center, Vector2.UnitX * item.shootSpeed, item.shoot, item.damage, item.knockBack);
            if (spawnVanilla) Projectile.NewProjectile(source, player.Center, Vector2.UnitX * item.shootSpeed, item.shoot, item.damage, item.knockBack, 0);
            Check(Main.projectile.Any(p => p.active && p.owner == 0), "weapon did not spawn " + id);
            bool synced = false; HashSet<int> collided = new();
            for (int tick = 0; tick < 180; tick++)
            {
                foreach (Projectile shot in Main.ActiveProjectiles)
                {
                    if (shot.ModProjectile is not (ReforgedWeaponShot or ReforgedTongueWhip or ReforgedAlly)) continue;
                    if (shot.ModProjectile is ReforgedWeaponShot s)
                    {
                        shapes.Add(s.Shape);
                        Check(shot.DamageType == ReforgedWeapons.Class(id), "projectile class mismatch " + id);
                        if (!synced)
                        {
                            using var buffer = new MemoryStream(); using var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, true);
                            s.SendExtraAI(writer); writer.Flush(); buffer.Position = 0;
                            Projectile remote = new(); remote.SetDefaults(shot.type); remote.ai = (float[])shot.ai.Clone(); remote.ModProjectile.ReceiveExtraAI(new BinaryReader(buffer));
                            var copy = (ReforgedWeaponShot)remote.ModProjectile;
                            Check(copy.Delay == s.Delay && copy.Range == s.Range && remote.DamageType == shot.DamageType && remote.tileCollide == shot.tileCollide && remote.timeLeft == shot.timeLeft, "weapon net roundtrip " + id);
                            synced = true;
                        }
                    }
                    // Vanilla whip AI runs through the engine; custom projectiles use their own hook.
                    if (shot.ModProjectile is ReforgedTongueWhip)
                        for (int update = 0; update < shot.MaxUpdates && shot.active; update++) shot.AI();
                    else shot.ModProjectile.AI();
                    if (!shot.active) continue;
                    if (shot.ModProjectile.CanDamage() != false && !collided.Contains(shot.identity)
                        && shot.Colliding(shot.Hitbox, dummy.Hitbox))
                    {
                        collided.Add(shot.identity); shot.ModProjectile.OnHitNPC(dummy, new NPC.HitInfo { Damage = shot.damage, HitDirection = 1 }, shot.damage);
                        if (shot.penetrate > 0 && --shot.penetrate == 0) { shot.Kill(); continue; }
                    }
                    if (shot.ModProjectile.ShouldUpdatePosition())
                    {
                        Vector2 desired = shot.velocity, moved = shot.tileCollide ? Collision.TileCollision(shot.position, desired, shot.width, shot.height) : desired;
                        shot.position += moved;
                        if (moved != desired) { shot.velocity = moved; if (shot.ModProjectile.OnTileCollide(desired)) shot.Kill(); }
                    }
                    if (--shot.timeLeft <= 0) shot.Kill();
                    Check(float.IsFinite(shot.position.X) && float.IsFinite(shot.velocity.Y), "invalid weapon movement " + id);
                }
                if (player.itemAnimation > 0) player.itemAnimation--;
                if (tick == (id is 11 or 32 or 36 ? 75 : id == 22 ? 20 : id == 29 ? 15 : id == 26 ? 30 : id == 9 ? 14 : 10))
                {
                    if (id == 9)
                    {
                        Projectile whip = Main.projectile.FirstOrDefault(p => p.active && p.ModProjectile is ReforgedTongueWhip);
                        Check(whip != null, "whip vanished before its swing midpoint");
                        var points = new List<Vector2>(); Projectile.FillWhipControlPoints(whip, points);
                        Check(points.Count >= 2 && points.All(p => float.IsFinite(p.X) && float.IsFinite(p.Y)) && points.Any(p => Vector2.Distance(p, points[0]) > 30), "whip did not extend at swing midpoint");
                    }
                    DrawWeaponCell(id, item, player, clipState);
                }
            }
            if (id is not (9 or 11 or 32 or 36)) Check(synced, "missing projectile network test " + id);
            if (id is 11 or 32)
            {
                player.ClearBuff(item.buffType);
                foreach (Projectile shot in Main.ActiveProjectiles) if (shot.ModProjectile is ReforgedAlly) { shot.ModProjectile.AI(); Check(!shot.active, "minion could not be dismissed"); }
            }
            Log($"COMBAT_WEAPON_PASS: {ReforgedWeapons.Names[id]}; cast, class, AI, impacts, finite positions and applicable network/dismissal checks.");
        }
        Check(shapes.Count >= 17, "weapons still share too few attack types");
        using (var stream = File.Create(Path.GetFullPath(Path.Combine(Main.SavePath, "..", "..", "outputs", "combat-weapons-runtime.png")))) weaponAtlas.SaveAsPng(stream, weaponAtlas.Width, weaponAtlas.Height);
        device.SetRenderTargets(previousTargets);
        Log($"COMBAT_WEAPONS_PASS: all 39 weapons; {shapes.Count} distinct projectile behaviors plus native whip, two minion types and a sentry.");
    }
    static void DrawWeaponCell(int id, Item item, Player p, RasterizerState clip)
    {
        int x = id % 4 * 360, y = id / 4 * 182;
        Vector2 savedScreen = Main.screenPosition;
        Main.screenPosition = p.Center - new Vector2(80, 145);
        Main.instance.GraphicsDevice.ScissorRectangle = new Rectangle(x, y, 360, 182);
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, clip, null,
            Matrix.CreateScale(.65f) * Matrix.CreateTranslation(x, y, 0));
        CombatDrawing.Circle(Main.spriteBatch, p.Center - Main.screenPosition, 12, Color.LightGreen, 2);
        foreach (Projectile shot in Main.ActiveProjectiles)
            if (shot.ModProjectile is ReforgedWeaponShot or ReforgedTongueWhip or ReforgedAlly)
            { Color c = Color.White; shot.ModProjectile.PreDraw(ref c); }
        Main.spriteBatch.End();
        Main.spriteBatch.Begin();
        Utils.DrawBorderString(Main.spriteBatch, item.Name, new Vector2(x + 10, y + 153), Color.LightSteelBlue, .75f);
        Main.spriteBatch.End(); Main.screenPosition = savedScreen;
    }
}
