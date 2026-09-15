#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;
using global::StarfallThrone.Content.Mining;
using global::StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Voyage.Weapons;

/// <summary>Explicit smoke-test entry point; never runs in normal gameplay or launches a client.</summary>
public static class VoyageWeaponValidation
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Voyage weapon runtime: " + message); }
    private static void Log(string message) => ModContent.GetInstance<VoyageWeapon0>().Mod.Logger.Info(message);
    private static IEnumerable<Projectile> Shots() => Main.projectile.Where(p => p.active);
    private static long FixtureHealth() { long total = 0; foreach (NPC npc in Main.ActiveNPCs) total += npc.life; return total; }
    public static void ValidateRegistered(Action<string> report)
    {
        int[] classes = new int[4];
        for (int id = 0; id < VoyageArsenal.Count; id++)
        {
            Item item = new(); item.SetDefaults(VoyageCatalog.Weapon(id));
            void Require(bool valid, string reason) { if (!valid) throw new InvalidOperationException($"Voyage weapon {id}: {reason}"); }
            Require(item.ModItem is VoyageWeapon weapon && weapon.Index == id, "stable registration");
            Require(item.damage == VoyageArsenal.Damage[id] && item.damage > 0, "base damage");
            Require(item.DamageType == VoyageArsenal.Class(id), "damage class");
            Require(item.ModItem.Tooltip.Value.Length > 8, "localized ability tooltip");
            Require(item.shoot > ProjectileID.None && item.noMelee, "projectile-only weapon");
            int category = item.DamageType == DamageClass.Melee ? 0 : item.DamageType == DamageClass.Ranged ? 1 : item.DamageType == DamageClass.Magic ? 2 : 3;
            classes[category]++;
            if (category == 1) Require(item.useAmmo != AmmoID.None, "ranged ammo is configured");
            if (category == 2) Require(item.mana > 0, "magic mana is configured");
            Require(item.channel == VoyageArsenal.Channel(id), "charge/channel flag");
            if (id is 7 or 23)
            {
                Projectile whip = new(); whip.SetDefaults(item.shoot);
                Require(ProjectileID.Sets.IsAWhip[item.shoot] && whip.MaxUpdates >= 2, "native whip must be simulated at MaxUpdates");
            }
            if (VoyageArsenal.Ally(id))
            {
                Projectile ally = new(); ally.SetDefaults(item.shoot);
                Require(ally.ModProjectile is VoyageAlly, "native ally type");
                Require(ally.DamageType == DamageClass.Summon, "ally damage class");
                if (VoyageArsenal.Sentry(id)) Require(item.sentry && ally.sentry && !ally.minion && ally.timeLeft == 7200, "two-minute sentry");
                else Require(item.buffType > 0 && ally.minion && ally.minionSlots == VoyageArsenal.Slots(id) && ProjectileID.Sets.MinionSacrificable[item.shoot], "minion slots/buff/sacrifice");
            }
            int boss = VoyageArsenal.Boss(id);
            Recipe[] recipes = Main.recipe.Take(Recipe.numRecipes).Where(r => !r.Disabled && r.createItem.type == item.type).ToArray();
            Require(recipes.Length == 1, "exactly one active weapon fallback recipe");
            Recipe recipe = recipes[0];
            int ore = MiningRecipes.VoyageOre(boss);
            // Whips use SummonMeleeSpeed, not Summon, and deliberately retain a physical bar frame.
            bool usesComponent = item.DamageType == DamageClass.Magic || item.DamageType == DamageClass.Summon;
            int stageMaterial = usesComponent ? MiningCatalog.ComponentType(ore) : MiningCatalog.BarType(ore);
            int stageCount = usesComponent ? (boss == 16 ? 4 : 3) : (boss == 16 ? 8 : 6);
            Dictionary<int, int> expected = new()
            {
                [VoyageCatalog.Material(boss)] = boss == 16 ? 36 : 24,
                [VoyageCatalog.Core(boss)] = 1,
                [VoyageCatalog.Alloy] = boss == 16 ? 8 : 4,
                [stageMaterial] = stageCount
            };
            Require(recipe.createItem.stack == 1 && recipe.requiredItem.Count == expected.Count &&
                recipe.requiredItem.Select(x => x.type).Distinct().Count() == expected.Count &&
                recipe.requiredItem.All(x => expected.TryGetValue(x.type, out int count) && x.stack == count),
                "exact migrated material/core/alloy/bar-or-component quantities");
            Require(recipe.acceptedGroups.Count == 0 && recipe.Conditions.Count == 0, "no unexpected recipe alternatives or extra gates");
            Require(recipe.requiredTile.SequenceEqual(new[] { MiningCatalog.StationTileType(MiningRecipes.VoyageStation(boss)) }),
                "exact migrated weapon workstation");
            report($"VOYAGE_WEAPON_DATA_PASS id={id} class={category} damage={item.damage}");
        }
        if (classes.Any(n => n != 9)) throw new InvalidOperationException("Voyage arsenal must provide exactly nine weapons per class");
        if (!ProjectileID.Sets.MinionShot[ModContent.ProjectileType<VoyageSummonShot>()]) throw new InvalidOperationException("Voyage summon children must support native whip tags");
        report("VOYAGE_WEAPONS_DATA_PASS count=36 classes=9/9/9/9");
    }

    private sealed class Metrics
    {
        public int Hits, Spawned, Peak, Roundtrips, SecondaryHits, AmmoSpent, ManaSpent;
        public long Damage;
        public readonly HashSet<ModProjectile> Seen = new();
        public readonly HashSet<VoyageShotKind> Shapes = new();
        public readonly HashSet<string> NetworkStates = new();
    }
    // Tile is a reference-like struct over global tile storage. Copy the values we actually modify,
    // not Tile references, and leave walls, wiring, frame data and custom tile data untouched.
    private readonly record struct SavedTile(int X, int Y, ushort Type, bool HasTile, bool Actuated, bool HalfBlock, SlopeType Slope, byte Liquid)
    {
        public static SavedTile Capture(int x, int y)
        { Tile t = Main.tile[x, y]; return new(x, y, t.TileType, t.HasTile, t.IsActuated, t.IsHalfBlock, t.Slope, t.LiquidAmount); }
        public void Restore()
        { Tile t = Main.tile[X, Y]; t.TileType = Type; t.HasTile = HasTile; t.IsActuated = Actuated; t.IsHalfBlock = HalfBlock; t.Slope = Slope; t.LiquidAmount = Liquid; }
    }

    /// <summary>
    /// Call on the graphics thread after recipes/assets load, in a throwaway main-menu smoke client.
    /// Runs the real Item.UseItem/Shoot hooks, vanilla ammo/mana selection, Projectile.AI and
    /// Projectile.Damage. NPC hits therefore execute real collision/immunity/modifier/OnHit hooks.
    /// Only the engine's outer movement and timer loop is supplied by this isolated fixture.
    /// </summary>
    public static void Run()
    {
        Check(Main.gameMenu && !Main.dedServ && Main.netMode != NetmodeID.Server, "Run is restricted to an isolated main-menu graphics client");
        Check(Main.tile.Width > 740 && Main.tile.Height > 170, "main-menu tile map is not initialized");
        ValidateRegistered(Log);
        var savedPlayers = (Player[])Main.player.Clone(); var savedNPCs = (NPC[])Main.npc.Clone();
        var savedProjectiles = (Projectile[])Main.projectile.Clone(); var savedDust = (Dust[])Main.dust.Clone(); var savedItems = (Item[])Main.item.Clone();
        var savedGore = (Gore[])Main.gore.Clone(); var savedText = (CombatText[])Main.combatText.Clone();
        var savedIdentities = (int[,])Main.projectileIdentity.Clone();
        var oldRandom = Main.rand;
        int oldMode = Main.GameMode, oldPlayer = Main.myPlayer, oldNetMode = Main.netMode, oldMouseX = Main.mouseX, oldMouseY = Main.mouseY;
        double oldSurface = Main.worldSurface; Vector2 oldScreen = Main.screenPosition;
        var tiles = new List<SavedTile>(65000);
        GraphicsDevice device = Main.instance.GraphicsDevice;
        var oldTargets = device.GetRenderTargets(); var oldViewport = device.Viewport; Rectangle oldScissor = device.ScissorRectangle;
        var oldRasterizer = device.RasterizerState; var oldBlend = device.BlendState;
        var oldDepth = device.DepthStencilState; var oldSampler = device.SamplerStates[0];
        using var atlas = new RenderTarget2D(device, 1760, 2250, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        using var clip = new RasterizerState { ScissorTestEnable = true };
        var allShapes = new HashSet<VoyageShotKind>();
        bool batchOpen = false;
        try
        {
            Main.GameMode = 0; Main.myPlayer = 0; Main.netMode = NetmodeID.SinglePlayer;
            Main.worldSurface = 120; Main.rand = new UnifiedRandom(607036);
            // No original entity is advanced or killed. Restore the entire reference arrays in finally.
            for (int i = 0; i < Main.player.Length; i++) Main.player[i] = new Player { whoAmI = i, active = false };
            for (int i = 0; i < Main.npc.Length; i++) Main.npc[i] = new NPC { whoAmI = i, active = false };
            for (int i = 0; i < Main.projectile.Length; i++) Main.projectile[i] = new Projectile { whoAmI = i, active = false };
            for (int i = 0; i < Main.dust.Length; i++) Main.dust[i] = new Dust { active = false };
            for (int i = 0; i < Main.item.Length; i++) Main.item[i] = new Item { active = false };
            for (int i = 0; i < Main.gore.Length; i++) Main.gore[i] = new Gore { active = false };
            for (int i = 0; i < Main.combatText.Length; i++) Main.combatText[i] = new CombatText { active = false };
            for (int x = 240; x < 740; x++) for (int y = 40; y < 170; y++)
            {
                tiles.Add(SavedTile.Capture(x, y));
                SetTile(x, y, y >= 130);
            }
            device.SetRenderTarget(atlas); device.Clear(new Color(15, 21, 32));
            for (int id = 0; id < VoyageArsenal.Count; id++)
            {
                ClearFixtureShots();
                Player player = FreshPlayer(); var metrics = new Metrics();
                Item item = player.inventory[0]; item.SetDefaults(VoyageCatalog.Weapon(id));
                SetDummies(player); SetAim(player);
                int casts = VoyageArsenal.Ally(id) || VoyageArsenal.Channel(id) ? 1 : 3;
                bool captured = false;
                for (int cast = 0; cast < casts; cast++)
                {
                    Cast(player, item, metrics);
                    int duration = VoyageArsenal.Ally(id) ? 360 : VoyageArsenal.Channel(id) ? 230 : 190;
                    for (int tick = 0; tick < duration; tick++)
                    {
                        player.channel = item.channel && tick < 60;
                        player.controlUseItem = player.channel;
                        Step(player, id, metrics);
                        if (!captured && CaptureAt(id, cast, tick, item))
                        {
                            Check(Shots().Any(p => p.ModProjectile is IVoyageWeaponProjectile v && v.WeaponIndex == id), "empty render capture for " + id);
                            if (id is 7 or 23)
                            {
                                Projectile? whip = Shots().FirstOrDefault(p => p.ModProjectile is VoyageWhip);
                                Check(whip != null, "native whip vanished before its swing midpoint");
                                var points = new List<Vector2>(); Projectile.FillWhipControlPoints(whip!, points);
                                Check(points.Count >= 20 && points.All(v => float.IsFinite(v.X) && float.IsFinite(v.Y)) && points.Any(v => Vector2.Distance(v, points[0]) > 70), "native whip failed to extend at MaxUpdates");
                            }
                            DrawCell(id, item, player, clip, ref batchOpen); captured = true;
                        }
                    }
                }
                Check(captured && metrics.Spawned > 0 && metrics.Hits > 0 && metrics.Damage > 0, $"weapon {id} failed to render or land a real hit (hits={metrics.Hits}, spawned={metrics.Spawned})");
                Check(metrics.Roundtrips > 0, "no network roundtrip " + id);
                Check(metrics.Spawned <= 96 && metrics.Peak <= 32, "unbounded attack graph " + id);
                if (item.useAmmo != AmmoID.None) Check(metrics.AmmoSpent == casts, "ammo not consumed once per cast " + id);
                if (item.mana > 0) Check(metrics.ManaSpent > 0, "free mana cast " + id);
                if (id == 34) Check(player.statMana < 10000 - metrics.ManaSpent, "sustained star did not consume maintenance mana");
                foreach (var shape in metrics.Shapes) allShapes.Add(shape);
                ValidateCleanup(player, item, id, metrics);
                WriteCellResult(id, metrics, ref batchOpen);
                Log($"VOYAGE_WEAPON_RUNTIME_PASS id={id} hits={metrics.Hits} secondaryHits={metrics.SecondaryHits} damage={metrics.Damage} spawned={metrics.Spawned} peak={metrics.Peak} packets={metrics.Roundtrips} casts={casts} ammo={metrics.AmmoSpent}");
            }
            Check(allShapes.Count >= 28, "insufficient distinct runtime attack coverage: " + allShapes.Count);
            ValidateBoundaries(FreshPlayer());
            string path = Path.GetFullPath(Path.Combine(Main.SavePath, "..", "..", "outputs", "voyage-weapons-runtime.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var output = File.Create(path)) atlas.SaveAsPng(output, atlas.Width, atlas.Height);
            Log($"VOYAGE_WEAPON_RENDER_PASS count=36 path={path}");
            Log($"VOYAGE_WEAPONS_RUNTIME_PASS count=36 shapes={allShapes.Count} nativeAI=true nativeDamage=true ownerOnly=true network=true cleanup=true");
        }
        finally
        {
            if (batchOpen) { Main.spriteBatch.End(); batchOpen = false; }
            // Restore even after a collision, graphics or assertion failure. No fixture world is saved.
            Array.Copy(savedPlayers, Main.player, savedPlayers.Length); Array.Copy(savedNPCs, Main.npc, savedNPCs.Length);
            Array.Copy(savedProjectiles, Main.projectile, savedProjectiles.Length); Array.Copy(savedDust, Main.dust, savedDust.Length);
            Array.Copy(savedItems, Main.item, savedItems.Length);
            Array.Copy(savedGore, Main.gore, savedGore.Length); Array.Copy(savedText, Main.combatText, savedText.Length);
            Array.Copy(savedIdentities, Main.projectileIdentity, savedIdentities.Length);
            foreach (SavedTile tile in tiles) tile.Restore();
            Main.rand = oldRandom; Main.GameMode = oldMode; Main.myPlayer = oldPlayer; Main.netMode = oldNetMode;
            Main.worldSurface = oldSurface; Main.screenPosition = oldScreen; Main.mouseX = oldMouseX; Main.mouseY = oldMouseY;
            device.SetRenderTargets(oldTargets); device.Viewport = oldViewport; device.ScissorRectangle = oldScissor;
            device.RasterizerState = oldRasterizer; device.BlendState = oldBlend; device.DepthStencilState = oldDepth; device.SamplerStates[0] = oldSampler;
        }
    }

    private static void SetTile(int x, int y, bool solid)
    {
        Tile t = Main.tile[x, y]; t.HasTile = solid; t.TileType = TileID.Dirt; t.IsActuated = false;
        t.IsHalfBlock = false; t.Slope = SlopeType.Solid; t.LiquidAmount = 0;
    }
    private static Player FreshPlayer()
    {
        Player p = new() { whoAmI = 0, active = true, dead = false, selectedItem = 0, position = new Vector2(5600, 2038), direction = 1 };
        Main.player[0] = p; p.ResetEffects(); p.statLife = p.statLifeMax2 = 10000; p.statMana = p.statManaMax2 = 10000;
        p.maxMinions = 8; p.maxTurrets = 4; p.MinionAttackTargetNPC = 0; p.velocity = Vector2.Zero;
        return p;
    }
    private static void SetDummies(Player p)
    {
        for (int i = 0; i < 3; i++)
        {
            NPC n = new(); n.SetDefaults(NPCID.BlueSlime); n.whoAmI = i; n.active = true;
            n.life = n.lifeMax = 100000000; n.defense = 0; n.damage = 0; n.knockBackResist = 0;
            n.width = 112; n.height = 176; n.Center = p.Center + new Vector2(i == 0 ? 210 : i == 1 ? 450 : 630, -55);
            n.noGravity = true; n.velocity = Vector2.Zero; n.HitSound = null; n.DeathSound = null;
            Main.npc[i] = n;
        }
    }
    private static void SetAim(Player p)
    { Main.screenPosition = p.Center - new Vector2(180, 190); Main.mouseX = 410; Main.mouseY = 150; }
    private static void ClearFixtureShots()
    {
        foreach (Projectile p in Main.ActiveProjectiles) p.active = false;
        // This helper is used only after all original arrays have been replaced by the fixture.
    }
    private static void Cast(Player player, Item item, Metrics metrics)
    {
        SetAim(player); player.itemAnimation = player.itemAnimationMax = item.useAnimation;
        player.itemTime = player.itemTimeMax = item.useTime; player.channel = item.channel; player.controlUseItem = true;
        Check(item.ModItem.CanUseItem(player), "weapon remained locked before next cast " + item.type);
        int type = item.shoot, damage = player.GetWeaponDamage(item), ammoId = 0;
        float speed = item.shootSpeed, knockback = item.knockBack;
        if (item.useAmmo != AmmoID.None)
        {
            Item ammo = player.inventory[54];
            ammo.SetDefaults(item.useAmmo == AmmoID.Arrow ? ItemID.WoodenArrow : item.useAmmo == AmmoID.Bullet ? ItemID.MusketBall : ItemID.RocketI);
            ammo.stack = 999;
            Check(player.PickAmmo(item, out type, out speed, out damage, out knockback, out ammoId), "no usable ammo");
            metrics.AmmoSpent += 999 - ammo.stack;
        }
        int mana = player.statMana;
        if (item.mana > 0) Check(player.CheckMana(item, -1, true), "mana payment failed");
        metrics.ManaSpent += mana - player.statMana;
        item.ModItem.UseItem(player);
        Vector2 velocity = (Main.MouseWorld - player.MountedCenter).SafeNormalize(Vector2.UnitX) * Math.Max(1, speed);
        var source = new EntitySource_ItemUse_WithAmmo(player, item, ammoId, "VoyageWeaponRuntime");
        if (item.ModItem.Shoot(player, source, player.MountedCenter, velocity, type, damage, knockback))
            Projectile.NewProjectile(source, player.MountedCenter, velocity, type, damage, knockback, player.whoAmI);
        Check(Shots().Any(p => p.owner == 0 && p.ModProjectile is IVoyageWeaponProjectile), "Shoot did not spawn a projectile");
    }

    private static void Step(Player player, int id, Metrics metrics)
    {
        Array.Clear(player.ownedProjectileCounts);
        foreach (Projectile p in Main.ActiveProjectiles) if (p.owner == 0) player.ownedProjectileCounts[p.type]++;
        for (int b = 0; b < player.buffType.Length; b++)
            if (player.buffType[b] > 0)
            {
                ModContent.GetModBuff(player.buffType[b])?.Update(player, ref b);
                if (b >= 0 && b < player.buffTime.Length && player.buffTime[b] > 0) player.buffTime[b]--;
            }
        foreach (NPC n in Main.ActiveNPCs)
        {
            for (int i = 0; i < n.immune.Length; i++) if (n.immune[i] > 0) n.immune[i]--;
            for (int b = 0; b < n.buffTime.Length; b++) if (n.buffTime[b] > 0 && --n.buffTime[b] == 0) n.buffType[b] = 0;
        }
        Projectile[] active = Shots().Where(p => p.ModProjectile is IVoyageWeaponProjectile).ToArray();
        metrics.Peak = Math.Max(metrics.Peak, active.Length);
        Check(active.Length <= 160, "projectile cap exceeded");
        foreach (Projectile shot in active)
        {
            var v = (IVoyageWeaponProjectile)shot.ModProjectile;
            Check(v.WeaponIndex == id, "cross-weapon projectile leaked between fixtures");
            if (metrics.Seen.Add(shot.ModProjectile)) metrics.Spawned++;
            Check(shot.DamageType == VoyageArsenal.Class(id), "projectile damage class mismatch " + id);
            if (shot.ModProjectile is VoyageShot shaped)
            {
                metrics.Shapes.Add(shaped.Kind);
                if (shaped.Age < shaped.Delay) Check(shaped.CanDamage() == false, "damaging windup");
            }
            if (shot.ModProjectile is VoyageAlly)
                Check(VoyageArsenal.Sentry(id) ? shot.sentry && shot.minionSlots == 0 : shot.minion && shot.minionSlots == VoyageArsenal.Slots(id), "ally slot mismatch");
            string networkKey = shot.type + ":" + (shot.ModProjectile is VoyageShot s ? s.Kind.ToString() + ":" + (s.Age > s.Delay ? "live" : "windup") : shot.ModProjectile is VoyageChannel c ? c.Released ? "released" : "charged" : "native");
            if (metrics.NetworkStates.Add(networkKey)) { NetworkRoundtrip(shot); metrics.Roundtrips++; }
            for (int update = 0; update < shot.MaxUpdates && shot.active; update++)
            {
                for (int i = 0; i < shot.localNPCImmunity.Length; i++) if (shot.localNPCImmunity[i] > 0) shot.localNPCImmunity[i]--;
                shot.AI(); // Includes native whip AI and ModProjectile/GlobalProjectile AI hooks.
                if (!shot.active) break;
                if (shot.ModProjectile.ShouldUpdatePosition())
                {
                    Vector2 wanted = shot.velocity;
                    Vector2 moved = shot.tileCollide ? Collision.TileCollision(shot.position, wanted, shot.width, shot.height) : wanted;
                    shot.position += moved;
                    if (wanted != moved) { shot.velocity = moved; if (shot.ModProjectile.OnTileCollide(wanted)) shot.Kill(); }
                }
                if (!shot.active) break;
                long before = FixtureHealth();
                shot.Damage(); // Real collision, local/global immunity, damage modifiers and OnHitNPC.
                long dealt = before - FixtureHealth();
                if (dealt > 0)
                {
                    metrics.Hits++; metrics.Damage += dealt; if (v.Secondary) metrics.SecondaryHits++;
                    if (id is 7 or 23)
                    {
                        int tag = id == 7 ? ModContent.BuffType<VoyageNeuralTag>() : ModContent.BuffType<VoyageRootTag>();
                        Check(Main.npc.Any(n => n.active && n.HasBuff(tag)), "real whip hit failed to apply its tag");
                    }
                }
                Check(float.IsFinite(shot.position.X) && float.IsFinite(shot.position.Y) && float.IsFinite(shot.velocity.X) && float.IsFinite(shot.velocity.Y) && float.IsFinite(shot.rotation), "NaN/Infinity in " + id);
                Check(Vector2.Distance(shot.Center, player.Center) < 6000 && shot.timeLeft <= 18000, "unbounded projectile position/lifetime " + id);
                if (shot.active && --shot.timeLeft <= 0) shot.Kill();
            }
        }
        if (player.itemAnimation > 0) player.itemAnimation--;
        if (player.itemTime > 0) player.itemTime--;
        player.GetModPlayer<VoyageWeaponPlayer>().PostUpdate();
    }

    private static void NetworkRoundtrip(Projectile shot)
    {
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, true)) shot.ModProjectile.SendExtraAI(writer);
        byte[] expected = buffer.ToArray(); buffer.Position = 0;
        Projectile remote = new(); remote.SetDefaults(shot.type);
        remote.owner = shot.owner; remote.ai = (float[])shot.ai.Clone(); remote.position = shot.position; remote.velocity = shot.velocity;
        remote.width = shot.width; remote.height = shot.height; remote.damage = shot.damage; remote.originalDamage = shot.originalDamage;
        remote.knockBack = shot.knockBack; remote.timeLeft = shot.timeLeft; remote.penetrate = shot.penetrate;
        remote.ModProjectile.ReceiveExtraAI(new BinaryReader(buffer));
        using var again = new MemoryStream();
        using (var writer = new BinaryWriter(again, System.Text.Encoding.UTF8, true)) remote.ModProjectile.SendExtraAI(writer);
        Check(expected.SequenceEqual(again.ToArray()) && remote.ai.SequenceEqual(shot.ai), "ExtraAI bytes changed in transit");
        Check(remote.DamageType == shot.DamageType && remote.tileCollide == shot.tileCollide && remote.timeLeft == shot.timeLeft && remote.penetrate == shot.penetrate && remote.MaxUpdates == shot.MaxUpdates && remote.minionSlots == shot.minionSlots, "network gameplay configuration mismatch " + shot.type);
    }

    private static void ValidateCleanup(Player p, Item item, int id, Metrics metrics)
    {
        var cleanup = new Metrics();
        if (VoyageArsenal.Ally(id) && !VoyageArsenal.Sentry(id))
        {
            p.ClearBuff(item.buffType); Step(p, id, cleanup); Step(p, id, cleanup);
            Check(!Shots().Any(q => q.ModProjectile is VoyageAlly), "dismissed minion remained active");
        }
        if (!VoyageArsenal.Ally(id)) Check(!Shots().Any(q => q.ModProjectile is IVoyageWeaponProjectile), "finite attack did not expire " + id);
        ClearFixtureShots(); Cast(p, item, cleanup);
        if (VoyageArsenal.Channel(id) || VoyageArsenal.Single(id))
        {
            p.inventory[0] = new Item();
            for (int tick = 0; tick < 180; tick++) Step(p, id, cleanup);
            Check(!Shots().Any(q => q.ModProjectile is VoyageChannel || q.ModProjectile is VoyageShot s && s.Attached), "held attack survived weapon switch " + id);
            p.inventory[0] = item;
        }
        p.dead = true;
        Step(p, id, cleanup); Step(p, id, cleanup);
        Check(!Shots().Any(q => q.ModProjectile is IVoyageWeaponProjectile), "death left attacks/sentries alive " + id);
        p.dead = false;
    }

    private static void ValidateBoundaries(Player player)
    {
        ClearFixtureShots(); SetDummies(player); SetAim(player);
        var source = player.GetSource_Misc("VoyageBoundarySmoke");
        int before = Shots().Count();
        Main.myPlayer = 1;
        try { Check(VoyageArsenal.Launch(source, 0, 9, VoyageShotKind.Arrow, player.Center, Vector2.UnitX, 100) == -1, "remote peer spawned owner-only children"); }
        finally { Main.myPlayer = 0; }
        Check(Shots().Count() == before, "remote spawn mutated projectile list");
        foreach (int side in new[] { -1, 1 })
        {
            Vector2 aim = Vector2.UnitX * side;
            int slot = VoyageArsenal.Launch(source, 0, 14, VoyageShotKind.OrbitArrow,
                player.MountedCenter + aim * 105, aim * 19, 100);
            var arrow = (VoyageShot)Main.projectile[slot].ModProjectile;
            for (int tick = 0; tick < 18; tick++)
            {
                arrow.Projectile.AI();
                Check(arrow.Projectile.active, "grounded crystal orbit hit floor, side=" + side);
                if (arrow.Age < 17) Check(arrow.CanDamage() == false, "crystal preparation dealt damage");
            }
            Check(arrow.Projectile.velocity.X * side > 20, "crystal arrow failed to release in aimed direction");
            arrow.Projectile.Kill();
            slot = VoyageArsenal.Launch(source, 0, 29, VoyageShotKind.Mirror, player.MountedCenter, aim, 100);
            var mirror = (VoyageShot)Main.projectile[slot].ModProjectile;
            mirror.Age = mirror.Delay + 1;
            Vector2 target = player.MountedCenter + aim * 210 - Vector2.UnitY * 50;
            Check(mirror.Colliding(mirror.Projectile.Hitbox, new Rectangle((int)target.X - 50, (int)target.Y - 70, 100, 140)) == true,
                "grounded mirror path was prematurely blocked by floor, side=" + side);
            mirror.Projectile.Kill();
        }
        for (int id = 0; id < 36; id++)
        {
            int slot = VoyageArsenal.LaunchProc(source, player, id, player.Center, Vector2.UnitX, 100);
            Check(slot >= 0, "proc helper failed");
            Projectile proc = Main.projectile[slot]; var contract = (IVoyageWeaponProjectile)proc.ModProjectile;
            Check(contract.Secondary, "proc not marked secondary");
            int count = Shots().Count();
            proc.ModProjectile.OnHitNPC(Main.npc[0], new NPC.HitInfo { Damage = 100, HitDirection = 1 }, 100);
            Check(Shots().Count() == count, "proc recursively spawned a hit child " + id);
            proc.Kill(); Check(Shots().Count() == count - 1, "proc recursively spawned a death child " + id);
        }
        NPC tagged = Main.npc[0];
        Projectile summonSample = new(); summonSample.SetDefaults(ModContent.ProjectileType<VoyageSummonShot>()); summonSample.owner = 0;
        var tags = tagged.GetGlobalNPC<VoyageTagDamage>();
        tags.Tag(0, false); tags.Tag(0, true);
        var modifiers = tagged.GetIncomingStrikeModifiers(DamageClass.Summon, 1, false);
        float original = modifiers.SourceDamage.ApplyTo(1000);
        tags.ModifyHitByProjectile(tagged, summonSample, ref modifiers);
        Check(Math.Abs(modifiers.SourceDamage.ApplyTo(1000) - original - 100) < .1f, "6% and 10% whip tags stacked instead of taking the stronger tag");
        summonSample.owner = 1; modifiers = tagged.GetIncomingStrikeModifiers(DamageClass.Summon, 1, false);
        original = modifiers.SourceDamage.ApplyTo(1000); tags.ModifyHitByProjectile(tagged, summonSample, ref modifiers);
        Check(Math.Abs(modifiers.SourceDamage.ApplyTo(1000) - original) < .1f, "whip tag leaked to a different player");
        int wallX = (int)((player.Center.X + 90) / 16);
        for (int y = 100; y < 131; y++) SetTile(wallX, y, true);
        try
        {
            int slot = VoyageArsenal.Launch(source, 0, 33, VoyageShotKind.Beam, player.Center, Vector2.UnitX, 100, variant: 100);
            var beam = (VoyageShot)Main.projectile[slot].ModProjectile; beam.Age = beam.Delay + 1;
            Rectangle beyond = new((int)player.Center.X + 180, (int)player.Center.Y - 25, 50, 50);
            Check(beam.Colliding(beam.Projectile.Hitbox, beyond) == false, "beam hit through the terrain wall");
            Check(VoyageShot.Clip(player.Center, Vector2.UnitX, 1400) < 110, "beam was not clipped by terrain");
            Vector2 reachable = VoyageArsenal.Reach(player, player.Center + Vector2.UnitX * 400);
            Check(reachable.X < wallX * 16, "portal/summon placement crossed the wall");
            ClearFixtureShots();
            player.inventory[0].SetDefaults(VoyageCatalog.Weapon(30));
            slot = VoyageArsenal.Launch(source, 0, 30, VoyageShotKind.GuideArrow, player.Center, Vector2.UnitX * 20, 100, variant: 100);
            foreach (NPC npc in Main.ActiveNPCs) npc.active = false;
            // Exercise Terraria's complete projectile movement path here, rather
            // than the simplified AI-loop movement used for long combat samples.
            for (int tick = 0; tick < 15 && Main.projectile[slot].active; tick++) Main.projectile[slot].Update(slot);
            Projectile remaining = Main.projectile[slot];
            Check(!remaining.active, $"arrow failed terrain: pos={remaining.position} vel={remaining.velocity} collide={remaining.tileCollide} wallX={wallX} player={player.Center} age={((VoyageShot)remaining.ModProjectile).Age} solid={Main.tileSolid[Main.tile[wallX,128].TileType]} has={Main.tile[wallX,128].HasUnactuatedTile}");
        }
        finally { for (int y = 100; y < 131; y++) SetTile(wallX, y, y >= 130); ClearFixtureShots(); }
        Log("VOYAGE_WEAPON_BOUNDARIES_PASS owner-only, grounded crystal orbit both directions, 36 nonrecursive proc types, nonstacking owner-specific whip tags, beam LOS, placement LOS and arrow tile collision");
    }

    private static bool CaptureAt(int id, int cast, int tick, Item item)
    {
        int chosenCast = id is 0 or 16 or 18 or 28 or 32 ? 2 : id is 17 or 24 ? 1 : 0;
        int frame = id switch {
            2 or 33 => 65, 34 => 45, 3 => 42, 5 => 28, 7 or 23 => item.useAnimation / 2,
            8 => 73, 11 => 48, 13 => 21, 15 => 72, 19 => 24, 21 => 30, 22 => 38,
            25 => 20, 27 => 43, 29 => 17, 31 => 28, 35 => 116, 0 or 24 => 20,
            6 => 8, 9 => 8, 14 => 1, 17 => 3, 30 => 3, _ => 13 };
        return cast == chosenCast && tick == frame;
    }
    private static void DrawCell(int id, Item item, Player p, RasterizerState clip, ref bool batchOpen)
    {
        int x = id % 4 * 440, y = id / 4 * 250;
        Vector2 oldScreen = Main.screenPosition; Main.screenPosition = p.Center - new Vector2(90, 230);
        Main.instance.GraphicsDevice.ScissorRectangle = new Rectangle(x, y + 30, 440, 190);
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, clip, null,
            Matrix.CreateScale(.5f) * Matrix.CreateTranslation(x, y + 30, 0)); batchOpen = true;
        CombatDrawing.Line(Main.spriteBatch, new Vector2(0, 252), new Vector2(880, 252), new Color(54, 70, 67), 5);
        CombatDrawing.Circle(Main.spriteBatch, p.Center - Main.screenPosition, 13, Color.LightGreen, 3);
        foreach (NPC npc in Main.ActiveNPCs)
        {
            Vector2 a = npc.TopLeft - Main.screenPosition, b = npc.BottomRight - Main.screenPosition;
            CombatDrawing.Line(Main.spriteBatch, a, new Vector2(b.X, a.Y), new Color(66, 76, 98), 2);
            CombatDrawing.Line(Main.spriteBatch, new Vector2(b.X, a.Y), b, new Color(66, 76, 98), 2);
            CombatDrawing.Line(Main.spriteBatch, b, new Vector2(a.X, b.Y), new Color(66, 76, 98), 2);
            CombatDrawing.Line(Main.spriteBatch, new Vector2(a.X, b.Y), a, new Color(66, 76, 98), 2);
        }
        foreach (Projectile shot in Main.ActiveProjectiles)
            if (shot.ModProjectile is IVoyageWeaponProjectile) { Color color = Color.White; shot.ModProjectile.PreDraw(ref color); }
        Main.spriteBatch.End(); batchOpen = false;
        Main.spriteBatch.Begin(); batchOpen = true;
        Utils.DrawBorderString(Main.spriteBatch, $"{id + 1:00}  {item.Name}", new Vector2(x + 9, y + 6), VoyageArsenal.Color(id), .72f);
        Main.spriteBatch.End(); batchOpen = false; Main.screenPosition = oldScreen;
    }
    private static void WriteCellResult(int id, Metrics metrics, ref bool batchOpen)
    {
        int x = id % 4 * 440, y = id / 4 * 250;
        Main.spriteBatch.Begin(); batchOpen = true;
        Utils.DrawBorderString(Main.spriteBatch, $"HITS {metrics.Hits}  SHOTS {metrics.Spawned}  NET {metrics.Roundtrips}", new Vector2(x + 10, y + 224), Color.LightSlateGray, .62f);
        Main.spriteBatch.End(); batchOpen = false;
    }
}
