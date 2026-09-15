using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using StarfallThrone.Content.Items;
using StarfallThrone.Content.NPCs;
using StarfallThrone.Content.Projectiles;

namespace StarfallThrone.Content.Systems;

// Explicit isolated-test launch parameter only. Never touches real worlds or players.
internal static class MiniBossValidation
{
    private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException("Mini validation: " + message); }
    public static void Run()
    {
        int[] expected = { 900, 1000, 1110, 1220, 1340, 1460, 1580, 1690, 1790, 1880 };
        Assert(PrimordialBossData.Life.SequenceEqual(expected), "primordial life table mismatch");
        Assert(MiniBossData.Order.Select(i => MiniBossData.Life[i]).SequenceEqual(new[] { 90, 145, 200, 310, 435, 580, 735, 880 }), "mini life table mismatch");
        for (int rank = 1; rank < MiniBossData.Count; rank++)
            Assert(MiniBossData.Progression(MiniBossData.Order[rank]) > MiniBossData.Progression(MiniBossData.Order[rank - 1]), "checklist rank mismatch");
        int previousMode = Main.GameMode;
        try
        {
            for (int mode = 0; mode <= 2; mode++)
            {
                Main.GameMode = mode;
                var resin = new NPC(); resin.SetDefaults(ModContent.NPCType<ResinWhelpNPC>());
                int previousLife = 0;
                foreach (int i in MiniBossData.Order)
                {
                    var npc = new NPC(); npc.SetDefaults(MiniBossData.NPCType(i));
                    Assert(npc.lifeMax > previousLife && npc.lifeMax < resin.lifeMax, $"mode {mode} progression {i}");
                    Assert(!npc.boss, "mini must not be a full boss");
                    if (mode == 0) Assert(npc.lifeMax == MiniBossData.Life[i], "normal life mismatch");
                    previousLife = npc.lifeMax;
                }
            }
        }
        finally { Main.GameMode = previousMode; }
        var mod = ModContent.GetInstance<MiniBossWorld>().Mod;
        foreach (ModItem item in mod.GetContent<ModItem>().Where(x => x.Texture.Contains("/Minis/")))
        {
            if (item is MiniMaterial)
                Assert(Main.recipe.Take(Recipe.numRecipes).Any(r => r.requiredItem.Any(ingredient => ingredient.type == item.Type)), "unused material: " + item.Name);
            else Assert(Main.recipe.Take(Recipe.numRecipes).Any(r => r.createItem.type == item.Type), "missing recipe: " + item.Name);
        }
        var world = ModContent.GetInstance<MiniBossWorld>();
        for (int i = 0; i < MiniBossData.Count; i++)
        {
            Recipe recipe = Main.recipe.Take(Recipe.numRecipes).Single(r => r.createItem.type == MiniBossData.SummonType(i));
            int prior = MiniBossData.Prerequisite(i);
            if (prior >= 0)
                Assert(recipe.requiredItem.Any(item => item.type == MiniBossData.MaterialType(prior) && item.stack == 3), "missing prerequisite ingredient");
            else
                Assert(!recipe.requiredItem.Any(item => item.ModItem is MiniMaterial), "starter summon unexpectedly gated");
            Assert(!recipe.createItem.consumable, "summon must be reusable");
        }
        var gearPlayer = new Player(); int life = gearPlayer.statLifeMax2; float speed = gearPlayer.moveSpeed;
        mod.Find<ModItem>("DewBrooch").UpdateAccessory(gearPlayer, false);
        mod.Find<ModItem>("FluffAnklet").UpdateAccessory(gearPlayer, false);
        Assert(gearPlayer.statLifeMax2 == life + 5 && Math.Abs(gearPlayer.moveSpeed - speed - 0.02f) < 0.0001f, "starter accessory effects");
        Item wand = new(); wand.SetDefaults(ModContent.ItemType<DandelionWand>());
        Assert(wand.damage == 6 && wand.mana == 3 && wand.DamageType == DamageClass.Magic, "starter wand stats");
        bool[] old = (bool[])MiniBossWorld.Downed.Clone();
        try
        {
            TagCompound legacy = new();
            for (int i = 0; i < 6; i++) legacy["MiniDowned" + MiniBossData.Keys[i]] = true;
            world.LoadWorldData(legacy);
            Assert(MiniBossWorld.Downed.Take(6).All(flag => flag) && !MiniBossWorld.Downed[6] && !MiniBossWorld.Downed[7], "v0.3 world migration failed");
            MiniBossWorld.Downed = new[] { true, false, true, false, true, true, false, true };
            TagCompound tag = new(); world.SaveWorldData(tag); world.ClearWorld(); world.LoadWorldData(tag);
            Assert(MiniBossWorld.Downed.SequenceEqual(new[] { true, false, true, false, true, true, false, true }), "world persistence failed");
            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream, System.Text.Encoding.UTF8, true);
            world.NetSend(writer); writer.Flush(); stream.Position = 0; world.ClearWorld();
            world.NetReceive(new System.IO.BinaryReader(stream));
            Assert(MiniBossWorld.Downed.SequenceEqual(new[] { true, false, true, false, true, true, false, true }), "world network roundtrip failed");
        }
        finally { MiniBossWorld.Downed = old; }
        mod.Logger.Info("MINI_DATA_PASS: eight life progressions in normal/expert/master, recipes and accessory stats, v0.3 save migration, world save/network roundtrips.");
    }

    public static void SimulateCombat()
    {
        // Main-menu sandbox: use a small temporary arena in memory, never save it.
        int previousMode = Main.GameMode, oldMyPlayer = Main.myPlayer;
        Player previousPlayer = Main.player[0]; NPC previousNPC = Main.npc[0];
        double previousSurface = Main.worldSurface;
        bool previousDay = Main.dayTime;
        bool[] oldDowned = (bool[])MiniBossWorld.Downed.Clone();
        Main.GameMode = 0; Main.myPlayer = 0; Main.worldSurface = 90;
        var player = new Player { active = true, dead = false, whoAmI = 0, position = new Vector2(3200, 1600 - 42) };
        Main.player[0] = player;
        player.ZoneOverworldHeight = true;
        // The smoke save directory has no loaded world; this arena is discarded when the process exits.
        for (int x = 160; x < 260; x++) for (int y = 70; y < 110; y++)
        {
            Tile tile = Main.tile[x, y]; tile.ClearEverything();
            if (y >= 100) { tile.HasTile = true; tile.TileType = TileID.Dirt; }
        }
        try
        {
            Main.npc[0].active = false;
            foreach (int index in MiniBossData.Order)
            {
                MiniBossWorld.Downed = new bool[MiniBossData.Count];
                Main.dayTime = index is not (2 or 3);
                player.inventory[0].SetDefaults(MiniBossData.SummonType(index)); player.selectedItem = 0;
                var summon = (MiniSummon)player.HeldItem.ModItem;
                int prior = MiniBossData.Prerequisite(index);
                if (prior >= 0)
                {
                    Assert(!summon.CanUseItem(player), "client allowed a locked summon");
                    // Directly invoke the packet receiver's entry point, bypassing the item hook.
                    MiniSummon.TrySummon(player, index);
                    Assert(!Main.npc.Any(n => n.active && n.ModNPC is MiniBossNPC), "imported summon bypassed authoritative world gate");
                    MiniBossWorld.MarkDowned(prior);
                }
                Assert(summon.CanUseItem(player), "unlocked summon rejected");
                MiniSummon.TrySummon(player, index);
                NPC spawned = Main.npc.FirstOrDefault(n => n.active && n.ModNPC is MiniBossNPC);
                Assert(spawned != null && spawned.type == MiniBossData.NPCType(index), "summon failed: " + MiniBossData.Keys[index]);
                spawned.active = false;
            }
            ModContent.GetInstance<MiniBossWorld>().Mod.Logger.Info("MINI_GATE_PASS: four locked client calls and direct authoritative summon calls rejected; prior kills unlock all four; four entry summons remain ungated.");
            foreach (int sleepingIndex in new[] { 0, 6 })
            {
            var sleeper = new NPC(); sleeper.SetDefaults(MiniBossData.NPCType(sleepingIndex));
            sleeper.whoAmI = 0; sleeper.active = true; sleeper.Bottom = player.Bottom + new Vector2(-64, 0); sleeper.ai[3] = -1;
            Main.npc[0] = sleeper; sleeper.ModNPC.AI();
            Assert(sleeper.friendly && sleeper.dontTakeDamage && !MiniBossData.AnyActive(), "dormant moss is not passive");
            MiniBossNPC.TryWake(player, 0);
            Assert(!((MiniBossNPC)sleeper.ModNPC).Dormant && !sleeper.friendly && !sleeper.dontTakeDamage, "moss wake failed");
            sleeper.active = false;
            }
            ModContent.GetInstance<MiniBossWorld>().Mod.Logger.Info("MINI_SUMMON_PASS: all eight summons spawn the intended NPC; dormant moss and dew snail wake on interaction.");
            for (int index = 0; index < MiniBossData.Count; index++)
            {
                HashSet<int> visited = new(); int maxHazards = 0; bool networkChecked = false;
                for (int phase = 0; phase < 2; phase++)
                {
                    var npc = new NPC(); npc.SetDefaults(MiniBossData.NPCType(index));
                    npc.whoAmI = 0; npc.active = true; npc.target = 0; npc.Bottom = new Vector2(3080, index is 2 or 3 ? 1500 : 1600);
                    if (phase == 1) npc.life = npc.lifeMax / 3;
                    Main.npc[0] = npc;
                    for (int tick = 0; tick < 1500; tick++)
                    {
                        npc.ModNPC.AI(); visited.Add(((MiniBossNPC)npc.ModNPC).State);
                        if (index >= 6)
                        {
                            var starter = (MiniBossNPC)npc.ModNPC; int cooldown = 0;
                            Assert(!npc.dontTakeDamage && !npc.noTileCollide, "starter used invulnerability or terrain phasing");
                            if (index == 7 && starter.State is 0 or 2 or 4 or 6)
                                Assert(!starter.CanHitPlayer(player, ref cooldown), "puff windup/recovery contact damage");
                        }
                        if (!npc.noGravity) npc.velocity.Y = Math.Min(8f, npc.velocity.Y + 0.3f);
                        Vector2 wanted = npc.velocity;
                        Vector2 resolved = npc.noTileCollide ? wanted : Collision.TileCollision(npc.position, wanted, npc.width, npc.height);
                        npc.collideX = resolved.X != wanted.X; npc.collideY = resolved.Y != wanted.Y;
                        npc.position += resolved; npc.velocity = resolved;
                        Assert(float.IsFinite(npc.position.X) && float.IsFinite(npc.position.Y), "non-finite movement");
                        int hazards = 0;
                        foreach (Projectile shot in Main.ActiveProjectiles)
                        {
                            if (shot.ModProjectile is not MiniHostileProjectile mini) continue;
                            if (!networkChecked && mini.Mode is 1 or 3)
                            {
                                using var netBuffer = new System.IO.MemoryStream();
                                using var netWriter = new System.IO.BinaryWriter(netBuffer, System.Text.Encoding.UTF8, true);
                                mini.SendExtraAI(netWriter); netWriter.Flush(); netBuffer.Position = 0;
                                var remote = new Projectile(); remote.SetDefaults(shot.type); remote.ai = (float[])shot.ai.Clone();
                                remote.ModProjectile.ReceiveExtraAI(new System.IO.BinaryReader(netBuffer));
                                Assert(remote.width == shot.width && remote.height == shot.height && remote.timeLeft == shot.timeLeft && remote.localAI[0] == shot.localAI[0] && !remote.tileCollide,
                                    "remote telegraph state differs");
                                networkChecked = true;
                            }
                            shot.ModProjectile.AI(); shot.position += shot.velocity; shot.timeLeft--;
                            if (shot.timeLeft <= 0 || (shot.tileCollide && Collision.SolidCollision(shot.position, shot.width, shot.height))) { shot.Kill(); continue; }
                            if (mini.Mode is 1 or 3) hazards++;
                            if (mini.Mode is 1 or 3 && shot.localAI[0] < (mini.Kind == 3 ? 60 : mini.Mode == 3 ? 66 : 54))
                                Assert(mini.CanDamage() == false, "warning projectile deals damage");
                        }
                        maxHazards = Math.Max(maxHazards, hazards);
                    }
                    foreach (Projectile shot in Main.ActiveProjectiles) if (shot.ModProjectile is MiniHostileProjectile) shot.Kill();
                }
                int states = index switch { 0 => 4, 1 or 4 or 7 => 7, _ => 6 };
                Assert(visited.Count == states, $"{MiniBossData.Keys[index]} only visited {visited.Count}/{states} states");
                Assert(maxHazards <= 2, "more than two persistent hazards");
                if (index is 3 or 5) Assert(networkChecked, "ground hazard network state untested");
                ModContent.GetInstance<MiniBossWorld>().Mod.Logger.Info($"MINI_AI_PASS: {MiniBossData.Keys[index]} visited {states} states over two phases / 3000 simulated ticks; max ground hazards {maxHazards}.");
            }
        }
        finally
        {
            foreach (Projectile shot in Main.ActiveProjectiles) if (shot.ModProjectile is MiniHostileProjectile) shot.Kill();
            Main.npc[0] = previousNPC; Main.player[0] = previousPlayer; Main.myPlayer = oldMyPlayer;
            Main.GameMode = previousMode; Main.worldSurface = previousSurface;
            Main.dayTime = previousDay;
            MiniBossWorld.Downed = oldDowned;
        }
    }
}
