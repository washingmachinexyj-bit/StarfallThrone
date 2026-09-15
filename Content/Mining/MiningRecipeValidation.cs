#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.Items;
using StarfallThrone.Content.NPCs;
using StarfallThrone.Content.Voyage;
using StarfallThrone.Content.Voyage.Equipment;
using StarfallThrone.Content.Voyage.Utility;
using StarfallThrone.Content.Voyage.Weapons;

namespace StarfallThrone.Content.Mining;

/// <summary>Read-only recipe audit and an independent AND/OR dependency solver.
/// It models renewable ingredient access, not a player's inventory or combat skill, and never
/// changes world flags, grants items, calls FindRecipes or places/mines real tiles.</summary>
public static class MiningRecipeValidation
{
    public sealed record Edge(string Result, string[] Requires, string Reason);
    public sealed record Proof(Dictionary<string, Edge> Witnesses, string[] Order);
    public sealed record Report(int Recipes, int ItemsReached, int BossesReached, int OreChecks, int ToolChecks, int StationChecks);

    private static readonly int[] RequiredPicks = { 35,40,45,50,230,260,290,320,340,380,430,480,550 };
    private static readonly int[] ExpectedPicks = { 35,40,45,50,240,270,300,330,350,390,440,490,560,600 };
    private static readonly int[] ExpectedToolStations = { 0,1,1,2,3,3,4,5,6,7,8,9,10,11 };
    private static readonly string[] OreBoss = { "mossboss", "primordial:1", "primordial:3", "primordial:9", "starfall:0",
        "starfall:6", "starfall:10", "starfall:16", "voyage:2", "voyage:7", "voyage:11", "voyage:15", "voyage:16" };

    private static string ItemNode(int type) => "item:" + type;
    private static string BossNode(string id) => "boss:" + id;
    private static int I(string name) => MiningCatalog.ItemType(name);
    private static void Check(bool ok, string why)
    { if (!ok) throw new InvalidOperationException("Mining recipe validation: " + why); }

    /// <summary>Public, deterministic pure solver. Alternatives are OR; each edge's requirements are AND.
    /// Cyclic helmet-conversion alternatives cannot invent their own source items.</summary>
    public static Proof Solve(IEnumerable<Edge> graph, IEnumerable<string> roots, ISet<string>? forbidden = null)
    {
        List<Edge> pending = graph.ToList();
        Dictionary<string, Edge> witnesses = new(StringComparer.Ordinal);
        List<string> order = new();
        foreach (string root in roots)
            if (forbidden?.Contains(root) != true && witnesses.TryAdd(root, new Edge(root, Array.Empty<string>(), "root"))) order.Add(root);
        bool progress;
        do
        {
            progress = false;
            foreach (Edge edge in pending)
            {
                if (witnesses.ContainsKey(edge.Result) || forbidden?.Contains(edge.Result) == true) continue;
                if (!edge.Requires.All(witnesses.ContainsKey)) continue;
                witnesses.Add(edge.Result, edge); order.Add(edge.Result); progress = true;
            }
        } while (progress);
        return new Proof(witnesses, order.ToArray());
    }

    /// <summary>Run after every mod's PostAddRecipes, e.g. from the isolated smoke runner.</summary>
    public static Report Validate()
    {
        Recipe[] recipes = Main.recipe.Take(Recipe.numRecipes).Where(r => !r.Disabled && r.createItem.ModItem?.Mod.Name == "StarfallThrone").ToArray();
        Recipe[] managed = recipes.Where(r => MiningRecipes.OwnedRecipes.Contains(r) || MiningRecipes.ExpectedStationForItem(r.createItem) >= 0
            || r.createItem.ModItem is MiningSeed or MiningAccessory or MiningSurvey or MiningUtilityItem).ToArray();
        ValidateShape(recipes, managed);
        List<Edge> graph = BuildGraph(managed);
        string[] roots = { "base", "moonlord", BossNode("snail"), BossNode("mossboss") };
        Proof proof = Solve(graph, roots);
        AssertProofIsDag(proof);
        foreach (Recipe recipe in managed)
            Reach(proof, ItemNode(recipe.createItem.type), "unreachable managed recipe output: " + recipe.createItem.Name);

        for (int i = 0; i < 10; i++) Reach(proof, BossNode("primordial:" + i));
        for (int i = 0; i < 17; i++) { Reach(proof, BossNode("starfall:" + i)); Reach(proof, BossNode("voyage:" + i)); }
        for (int i = 0; i < 14; i++) Reach(proof, ItemNode(MiningCatalog.ToolType(i)));
        for (int i = 0; i < MiningCatalog.StationCount; i++) Reach(proof, ItemNode(MiningCatalog.StationItemType(i)));
        for (int i = 0; i < MiningCatalog.OreCount; i++)
        {
            Reach(proof, ItemNode(MiningCatalog.OreType(i)));
            Reach(proof, ItemNode(MiningCatalog.BarType(i)));
            Reach(proof, ItemNode(MiningCatalog.ComponentType(i)));
            HashSet<string> noNewOres = new();
            for (int j = i; j < MiningCatalog.OreCount; j++)
            {
                noNewOres.Add(ItemNode(MiningCatalog.OreType(j)));
                noNewOres.Add(ItemNode(MiningCatalog.BarType(j)));
                noNewOres.Add(ItemNode(MiningCatalog.ComponentType(j)));
            }
            Proof beforeOre = Solve(graph, roots, noNewOres);
            Reach(beforeOre, ItemNode(MiningCatalog.ToolType(i)), "tool must precede its own and all later ores: " + i);
            if (i > 0)
            {
                var noBoss = new HashSet<string> { BossNode(OreBoss[i]) };
                Check(!Solve(graph, roots, noBoss).Witnesses.ContainsKey(ItemNode(MiningCatalog.ToolType(i))), "tool skips its boss: " + i);
            }
        }
        for (int station = 0; station < MiningCatalog.StationCount; station++)
        {
            int ore = MiningRecipes.StationOre[station];
            if (ore < 0) continue;
            HashSet<string> forbidden = new() { ItemNode(MiningCatalog.BarType(ore)), ItemNode(MiningCatalog.ComponentType(ore)) };
            Reach(Solve(graph, roots, forbidden), ItemNode(MiningCatalog.StationItemType(station)), "station must precede own refined bar: " + station);
        }

        // Optional minibosses must not silently become a new requirement for either formal era.
        Proof noMinis = Solve(graph, new[] { "base", "moonlord" });
        Reach(noMinis, ItemNode(MiningCatalog.StationItemType(1)), "independent resin workshop");
        Reach(noMinis, BossNode("primordial:9"), "early formal progression without miniboss mining");
        Reach(noMinis, BossNode("voyage:16"), "post-lunar progression without miniboss mining");
        Proof earlyOnly = Solve(graph, new[] { "base" });
        Reach(earlyOnly, BossNode("primordial:9"), "early progression must not need Moon Lord");
        Check(!earlyOnly.Witnesses.ContainsKey(ItemNode(MiningCatalog.StationItemType(3))), "lunar root gate");
        Check(!earlyOnly.Witnesses.ContainsKey(BossNode("starfall:0")), "first post-lunar boss gate");
        ValidateCultivation(graph, recipes, managed, roots);
        SelfTest();
        Report report = new(managed.Length, proof.Witnesses.Keys.Count(x => x.StartsWith("item:", StringComparison.Ordinal)), 44, 13, 14, 12);
        ModContent.GetInstance<MiningRecipes>().Mod.Logger.Info($"MINING_RECIPE_PASS recipes={report.Recipes} reachableItems={report.ItemsReached} bosses=44 ores=13 tools=14 stations=12 expertIngredients=0 independentRoots=3 dag=pass ownOreLoops=0 ownBarStationLoops=0 miniRecipes=preserved cultivationOnly=13 importedSeedGates=13 importedMaterialGates=13");
        return report;
    }

    public static void Data() => Validate();

    private static void ValidateShape(Recipe[] all, Recipe[] managed)
    {
        Check(MiningCatalog.PickRequirements.SequenceEqual(RequiredPicks), "ore pick threshold contract");
        Check(MiningCatalog.ToolPower.SequenceEqual(ExpectedPicks), "tool pick power contract");
        foreach (Recipe recipe in managed)
        {
            Check(recipe.createItem.stack > 0 && recipe.createItem.stack <= recipe.createItem.maxStack, "invalid output quantity " + recipe.createItem.Name);
            Check(recipe.requiredItem.Count > 0 && recipe.requiredItem.All(x => !x.IsAir && x.stack > 0 && x.stack <= x.maxStack), "invalid ingredient quantity " + recipe.createItem.Name);
            Check(recipe.requiredItem.Select(x => x.type).Distinct().Count() == recipe.requiredItem.Count, "duplicate ingredient " + recipe.createItem.Name);
            Check(!recipe.requiredItem.Any(x => x.expert || x.master), "normal progression uses expert/master reward " + recipe.createItem.Name);
            int tile = MiningRecipes.ExpectedStationForItem(recipe.createItem);
            if (tile < 0) tile = MiningUtilityRecipeTile(recipe.createItem);
            if (tile >= 0) Check(recipe.requiredTile.Count == 1 && recipe.requiredTile[0] == tile, "wrong station " + recipe.createItem.Name);
            if (recipe.createItem.ModItem is MiningSeed)
                Check(recipe.Conditions.Count == 1 && recipe.Conditions[0].Description.Key == "Mods.StarfallThrone.MiningToolsUI.SeedUnlocked",
                    "seed needs explicit current-world boss condition " + recipe.createItem.Name);
            else if (recipe.Conditions.Count > 0)
                Check(MiningRecipes.Gates.ContainsKey(recipe), "unmodelled recipe condition " + recipe.createItem.Name);
            for (int ore = 0; ore < MiningCatalog.OreCount; ore++)
                Check(!(Has(recipe, MiningCatalog.BarType(ore)) && Has(recipe, MiningCatalog.ComponentType(ore))), "charges both a bar and its component " + recipe.createItem.Name);
        }
        foreach (var pair in MiningRecipes.MiniRecipeBefore)
            Check(!pair.Key.Disabled && pair.Value == MiningRecipes.Fingerprint(pair.Key), "miniboss recipe changed " + pair.Key.createItem.Name);

        for (int i = 0; i < 13; i++)
        {
            Recipe[] bars = Result(all, MiningCatalog.BarType(i));
            Check(bars.Length == 1 && bars[0].createItem.stack == 1 && bars[0].requiredItem.Count == 1 && Has(bars[0], MiningCatalog.OreType(i), i < 4 ? 3 : 4), "smelting ratio " + i);
            Recipe[] components = Result(all, MiningCatalog.ComponentType(i));
            Check(components.Length == 1 && components[0].createItem.stack == 1 && components[0].requiredItem.Count == 1 && Has(components[0], MiningCatalog.BarType(i), 2), "component ratio " + i);
            Check(all.Any(r => Has(r, MiningCatalog.BarType(i))), "unused bar " + i);
            Check(all.Any(r => Has(r, MiningCatalog.ComponentType(i))), "unused component " + i);
            Recipe seed = Result(all, I("MiningSeed" + i)).Single();
            Check(seed.createItem.stack == 1 && Has(seed, MiningCatalog.BossMaterial(i), i < 4 ? 1 : 8), "seed material/quantity " + i);
            for (int later = i; later < 13; later++)
                Check(!Has(seed, MiningCatalog.OreType(later)) && !Has(seed, MiningCatalog.BarType(later)) && !Has(seed, MiningCatalog.ComponentType(later)),
                    "seed consumes its own or later ore " + i);
        }
        for (int i = 0; i < 14; i++)
        {
            Item tool = new(MiningCatalog.ToolType(i));
            Check(tool.pick == ExpectedPicks[i], "actual tool pick " + i);
            Check(MiningCatalog.ToolStation(i) == ExpectedToolStations[i], "catalog tool station " + i);
            Recipe[] entries = Result(all, tool.type);
            Check(entries.Length == 1 && entries[0].createItem.stack == 1, "tool recipe count " + i);
            Check(entries[0].requiredTile.SequenceEqual(new[] { MiningCatalog.StationTileType(ExpectedToolStations[i]) }), "old tool station " + i);
            if (i < 13)
                for (int ore = i; ore < 13; ore++)
                    Check(!Has(entries[0], MiningCatalog.OreType(ore)) && !Has(entries[0], MiningCatalog.BarType(ore)) && !Has(entries[0], MiningCatalog.ComponentType(ore)), "tool consumes its own/later ore " + i);
        }
        for (int station = 0; station < 12; station++)
        {
            Recipe[] entries = Result(all, MiningCatalog.StationItemType(station));
            Check(entries.Length > 0 && entries.All(r => r.createItem.stack == 1), "station recipe count " + station);
            int ore = MiningRecipes.StationOre[station];
            if (ore >= 0)
                foreach (Recipe recipe in entries)
                {
                    Check(Has(recipe, MiningCatalog.OreType(ore)), "new station needs raw ore " + station);
                    Check(!Has(recipe, MiningCatalog.BarType(ore)) && !Has(recipe, MiningCatalog.ComponentType(ore)), "station needs its own refined bar " + station);
                }
        }
        Recipe drill = Result(all, MiningCatalog.ToolType(8)).Single();
        Check(drill.requiredItem.Count == 3 && Has(drill, VoyageCatalog.Material(2), 18) && Has(drill, VoyageCatalog.Core(2), 1)
            && Has(drill, MiningCatalog.BarType(7), 10), "exact ring engineering drill recipe");
        Recipe crown = Result(all, I("EmperorCrown")).Single();
        Check(!Has(crown, I("AstralIngot")) && !Has(crown, I("StarfallResidue")) && crown.requiredTile.Contains(TileID.LunarCraftingStation), "first post-lunar summon deadlock");
        Recipe acorn = Result(all, I("ResinAcornSummon")).Single();
        Check(acorn.requiredItem.Count == 3 && Has(acorn, ItemID.Gel, 15) && Has(acorn, ItemID.Wood, 5)
            && Has(acorn, ItemID.Acorn, 1) && acorn.requiredTile.Contains(TileID.WorkBenches), "first primordial summon preserved");
        for (int i = 0; i < 17; i++)
        {
            Recipe summon = Result(all, VoyageCatalog.Summon(i)).Single();
            Check(Has(summon, VoyageCatalog.Alloy, 8), "voyage summon alloy " + i);
            if (i > 0) Check(Has(summon, VoyageCatalog.Material(i - 1), 10), "voyage previous-boss gate " + i);
        }
        // Explicit checks prevent a permissive vanilla-source model hiding an early-era recipe regression.
        HashSet<int> lateEarlyIngredients = new() { ItemID.Bone, ItemID.BeeWax, ItemID.MeteoriteBar, ItemID.HellstoneBar,
            ItemID.SoulofLight, ItemID.SoulofNight, ItemID.LunarBar, I("AstralIngot"), I("StarfallResidue") };
        foreach (Recipe recipe in managed.Where(r => IsEarlyFormal(r.createItem)))
            Check(!recipe.requiredItem.Any(x => lateEarlyIngredients.Contains(x.type)), "early item requires later progression " + recipe.createItem.Name);
    }

    private static bool IsEarlyFormal(Item item) => item.ModItem is PrimordialSummonItem or PrimordialWeapon ||
        item.ModItem?.Name is "ResinweaveHelmet" or "ResinweaveChest" or "ResinweaveGreaves" or "CopperveinHelmet" or "CopperveinChest" or "CopperveinGreaves"
        or "FrostmireHood" or "FrostmireCoat" or "FrostmireGreaves" or "WaxguardMask" or "WaxguardChest" or "WaxguardGreaves"
        or "AstralExplorerHelmet" or "AstralExplorerChest" or "AstralExplorerGreaves";
    private static int MiningUtilityRecipeTile(Item item)
    {
        int station = item.ModItem switch
        {
            MiningSeed seed => MiningCatalog.ToolStation(seed.Index),
            MiningAccessory accessory => new[] { 0, 2, 5, 8, 11 }[accessory.Index],
            MiningSurvey survey => new[] { 1, 5, 10, 10 }[survey.Index],
            MiningUtilityItem utility => new[] { 0, 0, 0, 0, 2, 2, 4, 0, 9, 7, 11, 8, -2, 3, 7 }[utility.Index],
            _ => -1
        };
        return station == -2 ? TileID.WorkBenches : station < 0 ? -1 : MiningCatalog.StationTileType(station);
    }
    private static bool Has(Recipe recipe, int type, int count = -1) => recipe.requiredItem.Any(x => x.type == type && (count < 0 || x.stack == count));
    private static Recipe[] Result(Recipe[] recipes, int type) => recipes.Where(r => r.createItem.type == type).ToArray();
    private static void Reach(Proof proof, string node, string? reason = null)
    { Check(proof.Witnesses.ContainsKey(node), reason ?? "unreachable " + node); }

    private static List<Edge> BuildGraph(Recipe[] recipes)
    {
        List<Edge> graph = new();
        void Add(string result, string reason, params string[] requires) => graph.Add(new Edge(result, requires.Distinct().ToArray(), reason));
        Dictionary<int, int> tileStations = Enumerable.Range(0, 12).ToDictionary(MiningCatalog.StationTileType, x => x);
        foreach (Recipe recipe in recipes)
        {
            List<string> requires = new();
            foreach (Item ingredient in recipe.requiredItem)
            {
                if (ingredient.type < ItemID.Count)
                {
                    // Resource availability is abstracted, not vanilla recipe simulation. Copper/tin
                    // groups use their representative item. Both evil-material sets are obtainable
                    // by the post-Moon-Lord root; this does not give them to the early-only root.
                    Add(ItemNode(ingredient.type), "obtain vanilla ingredient", VanillaRoot(ingredient.type));
                }
                requires.Add(ItemNode(ingredient.type));
            }
            foreach (int tile in recipe.requiredTile)
            {
                if (tileStations.TryGetValue(tile, out int station)) requires.Add(ItemNode(MiningCatalog.StationItemType(station)));
                else requires.Add(tile == TileID.LunarCraftingStation ? "moonlord" : "base");
            }
            if (MiningRecipes.Gates.TryGetValue(recipe, out string? gate)) requires.Add(gate == "moonlord" ? gate : BossNode(gate));
            // The seed condition belongs to another ModItem and is not in MiningRecipes.Gates.
            // Model it explicitly, including when its boss material has been imported from a different world.
            if (recipe.createItem.ModItem is MiningSeed seed) requires.Add(BossNode(OreBoss[seed.Index]));
            Add(ItemNode(recipe.createItem.type), "recipe " + recipe.RecipeIndex, requires.ToArray());
        }
        Add(ItemNode(I("ShellPatternFragment")), "snail common drop", BossNode("snail"));
        Add(ItemNode(MiningCatalog.BossMaterial(0)), "moss miniboss common drop", BossNode("mossboss"));
        for (int i = 0; i < 10; i++)
        {
            string boss = BossNode("primordial:" + i);
            Add(boss, "defeat early formal boss", ItemNode(I(MiningRecipes.PrimordialSummons[i])), i == 0 ? "base" : BossNode("primordial:" + (i - 1)));
            Add(ItemNode(PrimordialDropCatalog.CoreType(i)), "normal boss drop / expert bag", boss);
        }
        for (int i = 0; i < 17; i++)
        {
            string boss = BossNode("starfall:" + i);
            Add(boss, "defeat post-lunar boss", ItemNode(I(MiningRecipes.StarfallSummons[i])), i == 0 ? "moonlord" : BossNode("starfall:" + (i - 1)));
            Add(ItemNode(BossDropCatalog.CoreType(i)), "normal boss drop / expert bag", boss);
            Add(ItemNode(I("StarfallResidue")), "normal boss drop / expert bag", boss);
            string voyage = BossNode("voyage:" + i);
            Add(voyage, "defeat voyage boss", ItemNode(VoyageCatalog.Summon(i)), i == 0 ? BossNode("starfall:16") : BossNode("voyage:" + (i - 1)));
            Add(ItemNode(VoyageCatalog.Material(i)), "normal boss drop / expert bag", voyage);
            Add(ItemNode(VoyageCatalog.Core(i)), "normal boss drop / expert bag", voyage);
        }
        for (int i = 0; i < 13; i++)
        {
            Add(ItemNode(MiningCatalog.OreType(i)), "first-kill mineable vein", BossNode(OreBoss[i]), i == 0 ? "base" : ItemNode(MiningCatalog.ToolType(i)));
            // Cultivation requires a basic vat, not a moss upgrade attachment. Its output is
            // still mined with the correct tool; imported seeds cannot skip world boss flags.
            Add(ItemNode(MiningCatalog.OreType(i)), "cultivated mineable crystal", BossNode(OreBoss[i]), ItemNode(I("MiningSeed" + i)),
                ItemNode(I("MiningUtilityItem12")), i == 0 ? "base" : ItemNode(MiningCatalog.ToolType(i)));
        }
        return graph;
    }

    private static string VanillaRoot(int type) => type is ItemID.LunarBar or ItemID.FragmentSolar or ItemID.FragmentVortex
        or ItemID.FragmentNebula or ItemID.FragmentStardust or ItemID.WormFood or ItemID.RottenChunk or ItemID.BloodySpine
        or ItemID.TissueSample or ItemID.ShadowScale or ItemID.VilePowder or ItemID.ViciousPowder ? "moonlord" : "base";

    private static void ValidateCultivation(List<Edge> graph, Recipe[] all, Recipe[] managed, string[] roots)
    {
        Edge[] cultivatedOnly = graph.Where(e => e.Reason != "first-kill mineable vein").ToArray();
        Proof fallback = Solve(cultivatedOnly, roots);
        AssertProofIsDag(fallback);
        foreach (Recipe recipe in managed)
            Reach(fallback, ItemNode(recipe.createItem.type), "cultivation-only unreachable item: " + recipe.createItem.Name);
        for (int i = 0; i < 13; i++)
        {
            string seed = ItemNode(I("MiningSeed" + i)), ore = ItemNode(MiningCatalog.OreType(i));
            Reach(fallback, seed, "cultivation-only seed " + i);
            Reach(fallback, ore, "cultivation-only ore " + i);
            HashSet<string> forbidden = new() { BossNode(OreBoss[i]) };
            Recipe recipe = Result(all, I("MiningSeed" + i)).Single();
            List<string> importedMaterials = roots.Concat(recipe.requiredItem.Select(x => ItemNode(x.type))).ToList();
            importedMaterials.Add(ItemNode(MiningCatalog.StationItemType(MiningCatalog.ToolStation(i))));
            Check(!Solve(cultivatedOnly, importedMaterials, forbidden).Witnesses.ContainsKey(seed),
                "imported materials bypass current-world seed crafting condition " + i);
            importedMaterials.Add(seed);
            importedMaterials.Add(ItemNode(I("MiningUtilityItem12")));
            importedMaterials.Add(ItemNode(MiningCatalog.ToolType(i)));
            Check(!Solve(cultivatedOnly, importedMaterials, forbidden).Witnesses.ContainsKey(ore),
                "imported seed/tool bypass current-world cultivation condition " + i);
        }
        // Late-world fallback must not require going back for moss/shell upgrades.
        Proof noMinis = Solve(cultivatedOnly, new[] { "base", "moonlord" });
        Reach(noMinis, BossNode("primordial:9"), "cultivation-only early formal progression without minis");
        Reach(noMinis, BossNode("voyage:16"), "cultivation-only final boss without minis");
        Reach(noMinis, ItemNode(MiningCatalog.ToolType(13)), "cultivation-only graduate tool without minis");
        Reach(Solve(cultivatedOnly, new[] { "base" }), BossNode("primordial:9"), "cultivation-only pre-King-Slime progression");
    }

    private static void AssertProofIsDag(Proof proof)
    {
        Dictionary<string, int> order = proof.Order.Select((node, index) => (node, index)).ToDictionary(x => x.node, x => x.index);
        foreach (var pair in proof.Witnesses)
            foreach (string requirement in pair.Value.Requires)
                Check(order.TryGetValue(requirement, out int before) && before < order[pair.Key], "cyclic witness " + pair.Key);
    }

    /// <summary>Negative controls: proves the validator really rejects self-ore and own-bar loops.</summary>
    public static void SelfTest()
    {
        Edge[] valid = { new("tool", new[] { "drop", "oldStation" }, "tool"), new("ore", new[] { "tool" }, "mine"),
            new("newStation", new[] { "ore", "oldStation" }, "upgrade"), new("bar", new[] { "newStation", "ore" }, "smelt") };
        string[] roots = { "drop", "oldStation" };
        Check(Solve(valid, roots).Witnesses.ContainsKey("bar"), "solver positive control");
        Edge[] badTool = valid.Select(e => e.Result == "tool" ? e with { Requires = new[] { "drop", "bar" } } : e).ToArray();
        Check(!Solve(badTool, roots).Witnesses.ContainsKey("tool"), "solver own-ore negative control");
        Edge[] badStation = valid.Select(e => e.Result == "newStation" ? e with { Requires = new[] { "bar", "oldStation" } } : e).ToArray();
        Check(!Solve(badStation, roots).Witnesses.ContainsKey("newStation"), "solver own-bar negative control");
        Edge[] alternatives = { new("a", new[] { "b" }, "swap"), new("b", new[] { "a" }, "swap"), new("a", new[] { "drop" }, "craft") };
        Check(Solve(alternatives, Array.Empty<string>()).Witnesses.Count == 0, "cycle cannot create items");
        AssertProofIsDag(Solve(alternatives, new[] { "drop" }));
    }
}
