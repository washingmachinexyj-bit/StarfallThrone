using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.Utilities;

namespace StarfallThrone.Content.Ecology;

/// <summary>Explicitly invoked by main's isolated smoke runner; never executes in a player world.</summary>
public static class EcologyContentValidation
{
    private static int checks;
    private static void Check(bool ok, string why)
    {
        if (!ok) throw new InvalidOperationException("Ecology infrastructure: " + why);
        checks++;
    }
    private static void Guard() => Check(Main.gameMenu &&
        Terraria.Program.LaunchParameters.ContainsKey("-starfall-art-smoke"), "isolated menu only");
    private static void Log(string text) => ModContent.GetInstance<EcologyStationAdjacency>().Mod.Logger.Info(text);
    private static Recipe[] Recipes(int item) => Main.recipe.Take(Recipe.numRecipes)
        .Where(r => !r.Disabled && r.createItem.type == item).ToArray();
    private static List<DropRateInfo> ItemDrops(int item)
    {
        var results = new List<DropRateInfo>();
        foreach (var rule in Main.ItemDropsDB.GetRulesForItemID(item))
            rule.ReportDroprates(results, new DropRateInfoChainFeed(1f));
        return results;
    }
    public static void Data()
    {
        Guard(); checks = 0;
        for (int i = 0; i < 10; i++)
        {
            var bag = new Item(EcologyCatalog.Bag(i));
            Check(bag.expert && ItemID.Sets.BossBag[bag.type] && ItemID.Sets.OpenableBag[bag.type] &&
                bag.ModItem.CanRightClick(), "native bag " + i);
            var drops = ItemDrops(bag.type);
            Check(drops.Any(d => d.itemId == EcologyCatalog.Core(i) && d.stackMin == 30 && d.stackMax == 40 &&
                d.dropRate > .999f), "bag core range " + i);
            Check(drops.Any(d => d.itemId == EcologyCatalog.Expert(i) && d.dropRate > .999f), "guaranteed expert " + i);
            Check(Enumerable.Range(i * 4, 4).All(w => drops.Any(d => d.itemId == EcologyCatalog.Weapon(w) &&
                Math.Abs(d.dropRate - .25f) < .001f)), "one-of-four bag weapons " + i);
            Check(new Item(EcologyCatalog.Relic(i)).master, "master relic " + i);
            Check(new Item(EcologyCatalog.Mask(i)).headSlot >= 0, "wearable mask " + i);
            Item pet = new(EcologyCatalog.Pet(i));
            Check(pet.master && pet.buffType > 0 && pet.shoot > 0 && Main.vanityPet[pet.buffType], "real pet " + i);
            Check(new Item(EcologyCatalog.Item("EcologyTool" + i)).pick == EcologyCatalog.ToolPower[i] &&
                EcologyCatalog.ToolPower[i] >= EcologyCatalog.PickRequirements[i], "tool can mine native ore " + i);
            var tool = Recipes(EcologyCatalog.Item("EcologyTool" + i)).Single();
            Check(tool.requiredItem.Any(it => it.type == EcologyCatalog.Core(i) && it.stack == 8), "direct core tool " + i);
            Check(!tool.requiredItem.Any(it => it.ModItem is EcologyBarBase || it.type == EcologyCatalog.Ore(i)), "no initial ore loop " + i);
            Check(tool.requiredTile.SequenceEqual(new[] { EcologyCatalog.StationTile(EcologyCatalog.Station(i)) }), "tool base station " + i);
            Recipe bar = Recipes(EcologyCatalog.Bar(i)).Single();
            Check(bar.requiredItem.Count == 1 && bar.requiredItem[0].type == EcologyCatalog.Ore(i) &&
                bar.requiredItem[0].stack == (i < 4 ? 3 : 4) && bar.createItem.stack == 1, "exact smelting " + i);
            Check(bar.requiredTile.SequenceEqual(tool.requiredTile) && bar.Conditions.Count > 0, "bar world unlock and stage " + i);
            Recipe component = Recipes(EcologyCatalog.Component(i)).Single();
            Check(component.requiredItem.Any(it => it.type == EcologyCatalog.Bar(i) && it.stack == 2) &&
                component.requiredItem.Any(it => it.type == EcologyCatalog.Material(i) && it.stack == 3), "component recipe " + i);
            Recipe seed = Recipes(EcologyCatalog.Item("EcologyMineralSeed" + i)).Single();
            Check(seed.requiredItem.Count == 2 && seed.requiredItem.Any(it => it.type == EcologyCatalog.Core(i) && it.stack == 1) &&
                seed.requiredItem.Any(it => it.type == ItemID.StoneBlock && it.stack == 30) &&
                seed.Conditions.Count > 0 && EcologyMineralSeedBase.OreYield == 9, "mineral seed contract " + i);
            Check(seed.requiredTile.SequenceEqual(tool.requiredTile), "seed no advanced station gate " + i);
            var crate = new Item(EcologyCatalog.Item("EcologyCrate" + i));
            Check(crate.ModItem.CanRightClick() && ItemID.Sets.IsFishingCrate[crate.type], "openable fishing crate " + i);
            foreach (var drop in ItemDrops(crate.type))
            {
                ModItem m = new Item(drop.itemId).ModItem;
                Check(m is not EcologyCoreBase && m is not EcologyBarBase &&
                    drop.itemId != EcologyCatalog.Ore(i) && drop.itemId != EcologyCatalog.Expert(i) &&
                    drop.itemId != EcologyCatalog.Material(i, 3), "crate excludes progression loot " + i);
            }
            Check(Main.recipe.Take(Recipe.numRecipes).Any(r => !r.Disabled && r.createItem.type == ItemID.CookedFish &&
                r.requiredItem.Any(it => it.type == EcologyCatalog.Item("EcologyFish" + i))), "fish cooked use " + i);
            Check(Recipes(EcologyCatalog.Material(i)).Any(r => r.requiredItem.Any(it => it.type == EcologyCatalog.Item("EcologyFish" + i))), "fish material use " + i);
            Furniture(EcologyCatalog.Relic(i), 3, 3);
            Furniture(EcologyCatalog.Trophy(i), 3, 3);
            Furniture(EcologyCatalog.Item("EcologyLamp" + i), 1, 2);
        }
        for (int i = 0; i < 8; i++)
        {
            int tile = EcologyCatalog.StationTile(i);
            int[] parents = EcologyInfrastructure.StationParents(i).Select(EcologyCatalog.StationTile).Prepend(TileID.WorkBenches).ToArray();
            Check(TileLoader.GetTile(tile).AdjTiles.Order().SequenceEqual(parents.Order()), "station inheritance " + i);
            Recipe[] recipes = Recipes(EcologyCatalog.StationItem(i));
            Check(recipes.Length == (i == 0 ? 1 : 2), "upgrade and nonconsuming-base alternatives " + i);
            Check(recipes.All(r => r.requiredTile.SequenceEqual(new[] { (int)TileID.WorkBenches }) &&
                r.Conditions.Count > 0), "station no crafting loop " + i);
            Check(recipes.Any(r => r.requiredItem.All(it => it.ModItem is not EcologyStationBase)), "build station without consuming old bench " + i);
            Furniture(EcologyCatalog.StationItem(i), 3, 3);
        }
        Check(!TileLoader.GetTile(EcologyCatalog.StationTile(5)).AdjTiles.Contains(EcologyCatalog.StationTile(4)), "tide independent from sun");
        Check(!TileLoader.GetTile(EcologyCatalog.StationTile(2)).AdjTiles.Contains(EcologyCatalog.StationTile(1)), "tuner independent from hive");
        for (int i = 0; i < 40; i++)
        {
            Item banner = new(EcologyCatalog.Item("EcologyBanner" + i));
            NPC sample = new(); sample.SetDefaults(ModContent.Find<ModNPC>("StarfallThrone", "EcologyMob" + i).Type);
            ModNPC mob = sample.ModNPC;
            Check(mob.Banner == mob.Type && mob.BannerItem == banner.type, "native NPC banner mapping " + i);
            Check(ItemID.Sets.KillsToBanner[banner.type] == (i % 4 == 3 ? 25 : 50) &&
                ItemID.Sets.BannerStrength[banner.type].Enabled, "actual banner benefit " + i);
            Furniture(banner.type, 1, 3);
        }
        Log("ECOLOGY_CONTENT_DATA_PASS checks=" + checks + " bags=10 tools=10 bars=10 stations=8 banners=40 fish=10 crates=10 mineralSeeds=10");
    }
    private static void Furniture(int itemType, int width, int height)
    {
        Item item = new(itemType); Check(item.createTile >= 0, "placeable " + itemType);
        ModTile tile = TileLoader.GetTile(item.createTile); var data = TileObjectData.GetTileData(item.createTile, 0);
        Check(data != null && data.Width == width && data.Height == height, "native footprint " + tile.Name);
        var drops = tile.GetItemDrops(0, 0).ToArray();
        Check(drops.Length == 1 && drops[0].type == itemType && drops[0].stack == 1, "single furniture drop " + tile.Name);
        if (!Main.dedServ)
        {
            Texture2D texture = ModContent.Request<Texture2D>(tile.Texture).Value;
            Check(texture.Width >= width * 18 && texture.Height >= height * 18, "furniture texture frame extent " + tile.Name);
        }
    }
    private static int Amount(int item) => Main.item.Where(i => i.active && i.type == item).Sum(i => i.stack);
    private static void ClearDrops() { for (int i = 0; i < Main.item.Length; i++) Main.item[i] = new Item(); }
    public static void Runtime()
    {
        Guard();
        var players = (Player[])Main.player.Clone(); var npcs = (NPC[])Main.npc.Clone();
        var items = (Item[])Main.item.Clone(); var projectiles = (Projectile[])Main.projectile.Clone();
        var rand = Main.rand; int mode = Main.GameMode, net = Main.netMode, my = Main.myPlayer;
        bool golem = NPC.downedGolemBoss;
        try
        {
            Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0; Main.rand = new UnifiedRandom(909341);
            for (int i = 0; i < Main.player.Length; i++) Main.player[i] = new Player { active = false, whoAmI = i };
            for (int i = 0; i < Main.npc.Length; i++) Main.npc[i] = new NPC { active = false, whoAmI = i };
            for (int i = 0; i < Main.projectile.Length; i++) Main.projectile[i] = new Projectile { active = false, whoAmI = i };
            Player p = Main.player[0] = new Player { active = true, whoAmI = 0, position = new Vector2(16000, 2400) };
            p.ResetEffects(); p.selectedItem = 0;
            var resolver = new ItemDropResolver(Main.ItemDropsDB);
            for (int biome = 0; biome < 10; biome++)
            {
                for (int difficulty = 0; difficulty < 3; difficulty++)
                {
                    Main.GameMode = difficulty; ClearDrops();
                    NPC boss = new(); boss.SetDefaults(EcologyCatalog.NPCType(biome));
                    boss.whoAmI = 0; boss.position = p.position; boss.active = true; boss.playerInteraction[0] = true; Main.npc[0] = boss;
                    resolver.TryDropping(new DropAttemptInfo { npc = boss, player = p, rng = new UnifiedRandom(1900 + biome),
                        IsExpertMode = difficulty > 0, IsMasterMode = difficulty == 2 });
                    int weaponCount = Enumerable.Range(biome * 4, 4).Sum(w => Amount(EcologyCatalog.Weapon(w)));
                    if (difficulty == 0)
                        Check(Amount(EcologyCatalog.Core(biome)) is >= 24 and <= 32 && weaponCount == 1 &&
                            Amount(EcologyCatalog.Bag(biome)) == 0, "native normal loot " + biome);
                    else
                    {
                        Check(Amount(EcologyCatalog.Bag(biome)) == 1 && Amount(EcologyCatalog.Core(biome)) == 0 &&
                            Amount(EcologyCatalog.Expert(biome)) == 0 && weaponCount == 0 &&
                            Amount(EcologyCatalog.Mask(biome)) == 0, "expert loot bag only " + biome);
                        Check(Amount(EcologyCatalog.Relic(biome)) == (difficulty == 2 ? 1 : 0), "master relic native " + biome);
                        ClearDrops();
                        resolver.TryDropping(new DropAttemptInfo { item = EcologyCatalog.Bag(biome), player = p,
                            rng = new UnifiedRandom(2900 + biome), IsExpertMode = true, IsMasterMode = difficulty == 2 });
                        Check(Amount(EcologyCatalog.Core(biome)) is >= 30 and <= 40 && Amount(EcologyCatalog.Expert(biome)) == 1 &&
                            Enumerable.Range(biome * 4, 4).Sum(w => Amount(EcologyCatalog.Weapon(w))) == 1, "native bag contents " + biome);
                    }
                    boss.active = false;
                }
                ClearDrops();
                resolver.TryDropping(new DropAttemptInfo { item = EcologyCatalog.Item("EcologyCrate" + biome),
                    player = p, rng = new UnifiedRandom(3900 + biome), IsExpertMode = true });
                Check(Amount(EcologyCatalog.Material(biome)) is >= 5 and <= 9 && Amount(EcologyCatalog.Core(biome)) == 0 &&
                    Amount(EcologyCatalog.Ore(biome)) == 0 && Amount(EcologyCatalog.Expert(biome)) == 0, "native crate " + biome);
                Pet(p, biome);
                p.inventory[0] = new Item(EcologyCatalog.Item("EcologyTool" + biome)); NPC.downedGolemBoss = false;
                Check(EcologyPickHooks.TempleBlocked(p, TileID.LihzahrdBrick) &&
                    !EcologyPickHooks.TempleBlocked(p, TileID.Stone), "temple tool gate " + biome);
                NPC.downedGolemBoss = true;
                Check(!EcologyPickHooks.TempleBlocked(p, TileID.LihzahrdBrick), "temple after golem " + biome);
            }
            NPC.downedGolemBoss = false; p.inventory[0] = new Item(ItemID.Picksaw);
            Check(!EcologyPickHooks.TempleBlocked(p, TileID.LihzahrdBrick), "vanilla tools unchanged");
            Log("ECOLOGY_CONTENT_RUNTIME_PASS checks=" + checks +
                " normalLoot=10 expertBagOnly=20 nativeBags=20 crates=10 harmlessPets=10 templeToolGates=10");
        }
        finally
        {
            for (int i = 0; i < Main.player.Length; i++) Main.player[i] = players[i];
            for (int i = 0; i < Main.npc.Length; i++) Main.npc[i] = npcs[i];
            for (int i = 0; i < Main.item.Length; i++) Main.item[i] = items[i];
            for (int i = 0; i < Main.projectile.Length; i++) Main.projectile[i] = projectiles[i];
            Main.rand = rand; Main.GameMode = mode; Main.netMode = net; Main.myPlayer = my; NPC.downedGolemBoss = golem;
        }
    }
    private static void Pet(Player player, int biome)
    {
        Item item = new(EcologyCatalog.Pet(biome)); item.ModItem.UseItem(player);
        int slot = player.FindBuffIndex(item.buffType); Check(slot >= 0, "native pet buff " + biome);
        BuffLoader.GetBuff(item.buffType).Update(player, ref slot);
        Projectile pet = Main.projectile.Single(p => p.active && p.type == item.shoot);
        Check(pet.owner == player.whoAmI && pet.ModProjectile.CanDamage() == false, "harmless owned pet " + biome);
        for (int tick = 0; tick < 90; tick++)
        {
            pet.AI(); pet.position += pet.velocity;
            Check(float.IsFinite(pet.Center.X) && float.IsFinite(pet.Center.Y) && pet.velocity.Length() <= 12.01f,
                "finite pet movement " + biome);
        }
        Check(pet.timeLeft == 2, "pet maintained " + biome);
        player.ClearBuff(item.buffType); pet.AI(); Check(!pet.active, "pet dismissed " + biome);
    }
}
