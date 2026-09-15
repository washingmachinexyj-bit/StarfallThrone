#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using StarfallThrone.Content.Items;
using StarfallThrone.Content.NPCs;
using StarfallThrone.Content.Systems;
using StarfallThrone.Content.Voyage;
using StarfallThrone.Content.Voyage.Equipment;
using StarfallThrone.Content.Voyage.Utility;
using StarfallThrone.Content.Voyage.Weapons;

namespace StarfallThrone.Content.Mining;

/// <summary>All mining production recipes and the migration of existing formal-boss recipes.
/// No miniboss recipe, boss drop rule, combat statistic or existing item ID is changed here.</summary>
public sealed class MiningRecipes : ModSystem
{
    public const string CopperTinGroup = "StarfallThrone:MiningCopperTin";
    public const string GoldPlatinumGroup = "StarfallThrone:MiningGoldPlatinum";
    public const string IceSnowGroup = "StarfallThrone:MiningIceSnow";
    internal static readonly HashSet<Recipe> OwnedRecipes = new();
    internal static readonly Dictionary<Recipe, string> Gates = new();
    internal static readonly Dictionary<Recipe, string> MiniRecipeBefore = new();

    public static readonly string[] PrimordialSummons = {
        "ResinAcornSummon", "CopperCarapaceSummon", "SandBellSummon", "FrosthornFluteSummon", "MireLanternSummon",
        "WaxSealSummon", "EvilRootTotemSummon", "GravebellKeySummon", "MeteorGizzardSummon", "DawnshardSigilSummon" };
    public static readonly string[] StarfallSummons = {
        "EmperorCrown", "TerminalEye", "InfiniteBait", "CrimsonHeartSummon", "RoyalBeeSigil", "JudgmentBoneflute",
        "EternalVoodooDoll", "DynastyPrismSummon", "TwinOmen", "PrimeVerdictSummon", "StarEaterWorm",
        "WastelandBudSummon", "TempleStarCoreSummon", "AbyssLure", "CoronaButterfly", "AstralScripture", "EndMoonRite" };
    public static readonly int[] VoyageAccessoryBoss = { 1, 3, 5, 6, 7, 11, 12, 7, 15, 12, 15 };
    public static readonly int[] VoyageUtilityBoss = { 0, 1, 2, 4, 6, 11, 12, 15, 16 };
    public static readonly int[] StationOre = { -1, -1, 2, -1, 5, 6, 7, 8, 9, 10, 11, 12 };

    public override void Unload() { OwnedRecipes.Clear(); Gates.Clear(); MiniRecipeBefore.Clear(); }

    public override void AddRecipeGroups()
    {
        RecipeGroup.RegisterGroup(CopperTinGroup, new RecipeGroup(() => Text("CopperOrTin"), ItemID.CopperBar, ItemID.TinBar));
        RecipeGroup.RegisterGroup(GoldPlatinumGroup, new RecipeGroup(() => Text("GoldOrPlatinum"), ItemID.GoldBar, ItemID.PlatinumBar));
        RecipeGroup.RegisterGroup(IceSnowGroup, new RecipeGroup(() => Text("IceOrSnow"), ItemID.IceBlock, ItemID.SnowBlock));
    }

    public override void AddRecipes()
    {
        OwnedRecipes.Clear(); Gates.Clear(); MiniRecipeBefore.Clear();
        AddMaterialRecipes();
        AddStationRecipes();
        AddToolRecipes();
        AddFormalWeaponFallbacks();
    }

    private static string Text(string key) => Language.GetTextValue("Mods.StarfallThrone.MiningRecipes." + key);
    private static int I(string name) => MiningCatalog.ItemType(name);
    private static Recipe At(int result, int station, int count = 1)
        => Recipe.Create(result, count).AddTile(MiningCatalog.StationTileType(station));
    private static Recipe Bar(Recipe recipe, int ore, int count) => recipe.AddIngredient(MiningCatalog.BarType(ore), count);
    private static Recipe Raw(Recipe recipe, int ore, int count) => recipe.AddIngredient(MiningCatalog.OreType(ore), count);
    private static Recipe OldStation(Recipe recipe, int station) => recipe.AddIngredient(MiningCatalog.StationItemType(station));
    private static void Register(Recipe recipe, string gate = "", Func<bool>? predicate = null, string textKey = "")
    {
        if (predicate != null)
            recipe.AddCondition(new Condition(Language.GetText("Mods.StarfallThrone.MiningRecipes." + textKey), predicate));
        recipe.Register(); OwnedRecipes.Add(recipe);
        if (gate.Length > 0) Gates[recipe] = gate;
    }

    private static void AddMaterialRecipes()
    {
        for (int i = 0; i < MiningCatalog.OreCount; i++)
        {
            Register(Raw(At(MiningCatalog.BarType(i), MiningCatalog.SmeltStation(i)), i, i < 4 ? 3 : 4));
            Register(Bar(At(MiningCatalog.ComponentType(i), MiningCatalog.SmeltStation(i)), i, 2));
        }
    }

    private static void AddStationRecipes()
    {
        Register(Recipe.Create(MiningCatalog.StationItemType(0)).AddIngredient(I("ShellPatternFragment"), 4)
            .AddRecipeGroup(RecipeGroupID.Wood, 12).AddIngredient(ItemID.StoneBlock, 8).AddTile(TileID.WorkBenches));
        Register(Recipe.Create(MiningCatalog.StationItemType(1)).AddIngredient<ResinCore>()
            .AddRecipeGroup(RecipeGroupID.Wood, 20).AddIngredient(ItemID.StoneBlock, 30).AddIngredient(ItemID.Torch, 5)
            .AddTile(TileID.WorkBenches));
        // A cheaper optional upgrade, never a mandatory snail requirement for formal bosses.
        Register(OldStation(At(MiningCatalog.StationItemType(1), 0), 0).AddIngredient<ResinCore>()
            .AddRecipeGroup(RecipeGroupID.Wood, 12).AddIngredient(ItemID.StoneBlock, 22).AddIngredient(ItemID.Torch, 5));
        Register(Bar(Raw(OldStation(At(MiningCatalog.StationItemType(2), 1), 1), 2, 24), 1, 6));
        // This is a new independent era root. Residue/AstralIngot would deadlock the first lunar boss.
        Register(Recipe.Create(MiningCatalog.StationItemType(3)).AddIngredient(ItemID.LunarBar, 8)
            .AddIngredient(ItemID.FragmentSolar, 4).AddIngredient(ItemID.FragmentVortex, 4)
            .AddIngredient(ItemID.FragmentNebula, 4).AddIngredient(ItemID.FragmentStardust, 4)
            .AddIngredient(ItemID.Furnace).AddTile(TileID.LunarCraftingStation), "moonlord", () => NPC.downedMoonlord, "DefeatMoonLord");
        Register(Bar(Raw(OldStation(At(MiningCatalog.StationItemType(4), 3), 3), 5, 32), 4, 8));
        Register(Bar(Raw(OldStation(At(MiningCatalog.StationItemType(5), 4), 4), 6, 32), 5, 8));
        // Retains the old VoyageWorkbench ID. Its old recipe is disabled in PostAddRecipes.
        Register(Raw(At(MiningCatalog.StationItemType(6), 5), 7, 32).AddIngredient<AstralIngot>(8)
            .AddIngredient(ItemID.Wire, 20), "starfall:16", () => StarfallWorld.IsDowned(16), "DefeatEndMoon");
        for (int station = 7; station < MiningCatalog.StationCount; station++)
        {
            int ore = StationOre[station];
            Register(Bar(Raw(OldStation(At(MiningCatalog.StationItemType(station), station - 1), station - 1),
                ore, station == 11 ? 40 : 32), ore - 1, station == 11 ? 10 : 8));
        }
        // Non-consuming alternatives let a player craft beside their only predecessor station.
        // They also support collecting every station without dismantling a shared base.
        foreach (int station in new[] { 2, 4, 5, 7, 8, 9, 10, 11 })
        {
            int ore = StationOre[station];
            Recipe replacement = Bar(Raw(At(MiningCatalog.StationItemType(station), station - 1), ore,
                station == 2 ? 24 : station == 11 ? 40 : 32), ore - 1, station == 2 ? 10 : station == 11 ? 14 : 12)
                .AddIngredient(ItemID.StoneBlock, 20);
            if (station == 2) replacement.AddRecipeGroup(RecipeGroupID.Wood, 20);
            else replacement.AddIngredient(ItemID.Glass, 10);
            Register(replacement);
        }
    }

    private static void AddToolRecipes()
    {
        Register(At(MiningCatalog.ToolType(0), 0).AddIngredient(I("ShellPatternFragment"), 2)
            .AddRecipeGroup(RecipeGroupID.Wood, 8).AddIngredient(ItemID.StoneBlock, 6));
        Register(At(MiningCatalog.ToolType(1), 1).AddIngredient<CopperveinCore>()
            .AddRecipeGroup(CopperTinGroup, 8).AddRecipeGroup(RecipeGroupID.Wood, 6));
        Register(Bar(At(MiningCatalog.ToolType(2), 1).AddIngredient<FrosthornCore>(), 1, 6).AddRecipeGroup(IceSnowGroup, 20));
        Register(Bar(Bar(At(MiningCatalog.ToolType(3), 2).AddIngredient<DawnshardCore>(), 2, 8), 1, 6));
        Register(At(MiningCatalog.ToolType(4), 3).AddIngredient<ImperialGel>(12).AddIngredient(ItemID.LunarBar, 8));
        Register(Bar(At(MiningCatalog.ToolType(5), 3).AddIngredient<EternalFlesh>(12), 4, 10));
        Register(Bar(At(MiningCatalog.ToolType(6), 4).AddIngredient<StarEaterMagnet>(12), 5, 10));
        Register(Bar(At(MiningCatalog.ToolType(7), 5).AddIngredient<MoonGodCore>(8), 6, 12));
        // Tool 8 already registers one recipe in VoyageUtilityItem; migrate it in place below.
        for (int tool = 9; tool <= 12; tool++)
            Register(Bar(At(MiningCatalog.ToolType(tool), MiningCatalog.ToolStation(tool))
                .AddIngredient(MiningCatalog.BossMaterial(tool), 18).AddIngredient(MiningCatalog.BossCore(tool)), tool - 1, 12));
        Register(Bar(At(MiningCatalog.ToolType(13), 11).AddIngredient(MiningCatalog.ToolType(12)), 12, 12)
            .AddIngredient(MiningCatalog.ComponentType(11), 4));
    }

    private static void AddFormalWeaponFallbacks()
    {
        // Direct weapon drops stay unchanged; these are optional deterministic crafting alternatives.
        for (int boss = 0; boss < 10; boss++)
        {
            int[] types = boss == 9 ? new[] { PrimordialDropCatalog.WeaponOneType(boss), PrimordialDropCatalog.WeaponTwoType(boss),
                PrimordialDropCatalog.WeaponThreeType(boss) } : new[] { PrimordialDropCatalog.WeaponOneType(boss), PrimordialDropCatalog.WeaponTwoType(boss) };
            foreach (int type in types)
            {
                Recipe recipe = At(type, PrimordialStation(boss)).AddIngredient(PrimordialDropCatalog.CoreType(boss), 4);
                if (boss == 0) recipe.AddRecipeGroup(RecipeGroupID.Wood, 10).AddIngredient(ItemID.Gel, 8);
                else if (boss == 9 && type == PrimordialDropCatalog.WeaponTwoType(boss))
                    recipe.AddIngredient(MiningCatalog.ComponentType(3), 2);
                else Bar(recipe, PrimordialOre(boss), 4);
                Register(recipe);
            }
        }
        for (int boss = 0; boss < 17; boss++)
            Register(Bar(At(BossDropCatalog.WeaponType(boss), StarfallStation(boss))
                .AddIngredient(BossDropCatalog.CoreType(boss), 12).AddIngredient<AstralIngot>(4), StarfallOre(boss), 6));
    }

    /// <summary>Returns a catalog STATION INDEX, not a tile ID. Milestone summons use the older station.</summary>
    public static int VoyageStation(int bossIndex, bool summon = false)
    {
        int completed = summon ? bossIndex - 1 : bossIndex;
        return completed < 2 ? 6 : completed < 7 ? 7 : completed < 11 ? 8 : completed < 15 ? 9 : completed < 16 ? 10 : 11;
    }
    public static int VoyageOre(int bossIndex) => VoyageStation(bossIndex) + 1;
    public static int PrimordialStation(int bossIndex, bool summon = false) => (summon ? bossIndex - 1 : bossIndex) < 3 ? 1 : 2;
    public static int PrimordialOre(int bossIndex) => bossIndex < 1 ? -1 : bossIndex < 3 ? 1 : bossIndex < 9 ? 2 : 3;
    public static int StarfallStation(int bossIndex, bool summon = false)
    {
        int completed = summon ? bossIndex - 1 : bossIndex;
        return completed < 6 ? 3 : completed < 10 ? 4 : 5;
    }
    public static int StarfallOre(int bossIndex) => bossIndex < 6 ? 4 : bossIndex < 10 ? 5 : bossIndex < 16 ? 6 : 7;

    /// <summary>Returns the expected TILE ID for managed items, or -1 for an unrelated item.
    /// Station upgrade alternatives may additionally be crafted at their predecessor.</summary>
    public static int ExpectedStationForItem(Item item)
    {
        int type = item.type;
        if (item.ModItem is VoyageSummonBase summon) return MiningCatalog.StationTileType(VoyageStation(summon.Index, true));
        if (item.ModItem is VoyageWeapon weapon) return MiningCatalog.StationTileType(VoyageStation(VoyageArsenal.Boss(weapon.Index)));
        if (item.ModItem is VoyageArmor armor) return MiningCatalog.StationTileType(6 + armor.Tier);
        if (item.ModItem is VoyagePlateBase plate) return MiningCatalog.StationTileType(7 + plate.Index);
        if (item.ModItem is VoyageOrdinaryAccessory accessory) return MiningCatalog.StationTileType(VoyageStation(VoyageAccessoryBoss[accessory.Index]));
        if (item.ModItem is VoyageFurnitureItem furniture) return MiningCatalog.StationTileType(VoyageStation(furniture.Theme));
        if (item.ModItem is VoyageUtilityItem utility) return MiningCatalog.StationTileType(utility.Index == 2 ? 6 : VoyageStation(VoyageUtilityBoss[utility.Index]));
        for (int i = 0; i < 14; i++) if (type == MiningCatalog.ToolType(i)) return MiningCatalog.StationTileType(MiningCatalog.ToolStation(i));
        for (int i = 0; i < MiningCatalog.OreCount; i++)
            if (type == MiningCatalog.BarType(i) || type == MiningCatalog.ComponentType(i))
                return MiningCatalog.StationTileType(MiningCatalog.SmeltStation(i));
        for (int i = 0; i < 10; i++)
        {
            if (type == I(PrimordialSummons[i])) return i == 0 ? TileID.WorkBenches : MiningCatalog.StationTileType(PrimordialStation(i, true));
            if (type == PrimordialDropCatalog.WeaponOneType(i) || type == PrimordialDropCatalog.WeaponTwoType(i) ||
                i == 9 && type == PrimordialDropCatalog.WeaponThreeType(i)) return MiningCatalog.StationTileType(PrimordialStation(i));
        }
        for (int i = 0; i < 17; i++)
        {
            if (type == I(StarfallSummons[i])) return i == 0 ? TileID.LunarCraftingStation : MiningCatalog.StationTileType(StarfallStation(i, true));
            if (type == BossDropCatalog.WeaponType(i)) return MiningCatalog.StationTileType(StarfallStation(i));
        }
        if (type == VoyageCatalog.Alloy) return MiningCatalog.StationTileType(6);
        if (type == I("AstralIngot")) return MiningCatalog.StationTileType(3);
        if (type == MiningCatalog.StationItemType(6)) return MiningCatalog.StationTileType(5);
        int earlyArmorBoss = PrimordialArmorBoss(item.ModItem?.Name ?? "");
        if (earlyArmorBoss >= 0) return MiningCatalog.StationTileType(PrimordialStation(earlyArmorBoss));
        if (IsLateStarfallCraft(item)) return MiningCatalog.StationTileType(5);
        return -1;
    }

    public override void PostAddRecipes()
    {
        Recipe[] existing = Main.recipe.Take(Recipe.numRecipes).ToArray();
        foreach (Recipe recipe in existing)
        {
            if (recipe.createItem.ModItem?.Mod != Mod || OwnedRecipes.Contains(recipe)) continue;
            if (recipe.createItem.ModItem is MiniSummon or MiniAccessory or MiniWeapon or MiniTrophy ||
                recipe.createItem.ModItem.Name is "MothLantern" or "UmbrellaVanity")
            { MiniRecipeBefore[recipe] = Fingerprint(recipe); continue; }
            if (recipe.createItem.type == MiningCatalog.StationItemType(6)) { recipe.DisableRecipe(); continue; }
            int station = ExpectedStationForItem(recipe.createItem);
            if (station < 0) continue;
            recipe.requiredTile.Clear(); recipe.AddTile(station);
            if (recipe.createItem.ModItem is VoyageSummonBase) continue; // Keep 8 alloy + 10 previous drops exactly.
            if (recipe.createItem.ModItem is VoyageUtilityItem utility && utility.Index == 2)
            {
                recipe.requiredItem.Clear(); recipe.acceptedGroups.Clear();
                recipe.AddIngredient(VoyageCatalog.Material(2), 18).AddIngredient(VoyageCatalog.Core(2))
                    .AddIngredient(MiningCatalog.BarType(7), 10);
                continue;
            }
            if (recipe.createItem.ModItem is PrimordialSummonItem) { MigratePrimordialSummon(recipe); continue; }
            if (recipe.createItem.ModItem is StarfallSummonItem)
            {
                // The original first crown needed AstralIngot -> residue -> its own boss.
                if (recipe.createItem.type == I("EmperorCrown")) recipe.RemoveIngredient(I("AstralIngot"));
                continue;
            }
            if (recipe.createItem.ModItem is VoyageFurnitureItem) continue; // Cheap building blocks keep their old yield/cost.
            if (recipe.createItem.type == I("AstralIngot") || recipe.createItem.type == VoyageCatalog.Alloy) continue;
            MigrateEquipment(recipe);
        }
    }

    private static void MigratePrimordialSummon(Recipe recipe)
    {
        int boss = Array.FindIndex(PrimordialSummons, name => I(name) == recipe.createItem.type);
        if (boss <= 0) return; // The first acorn remains byte-for-byte equivalent.
        // Replace only era-breaking ingredients with accessible equivalents, preserving previous-boss cores.
        if (boss == 1) ReplaceGroup(recipe, ItemID.CopperBar, CopperTinGroup);
        if (boss == 2) { recipe.RemoveIngredient(ItemID.Stinger); recipe.AddIngredient(ItemID.Cactus, 8); }
        if (boss == 3) { recipe.RemoveIngredient(ItemID.Bone); recipe.AddRecipeGroup(RecipeGroupID.Wood, 6); }
        if (boss == 5)
        {
            recipe.RemoveIngredient(ItemID.HoneyBlock); recipe.RemoveIngredient(ItemID.BeeWax); recipe.RemoveIngredient(ItemID.Stinger);
            recipe.AddIngredient(ItemID.Gel, 15).AddIngredient(ItemID.Daybloom, 2).AddIngredient<ResinCore>();
        }
        if (boss == 6)
        {
            recipe.RemoveIngredient(ItemID.VilePowder); recipe.RemoveIngredient(ItemID.Vine);
            recipe.AddIngredient(ItemID.Mushroom, 6).AddRecipeGroup(RecipeGroupID.Wood, 8);
        }
        if (boss == 7)
        {
            recipe.RemoveIngredient(ItemID.Bone); recipe.RemoveIngredient(ItemID.RottenChunk);
            recipe.AddIngredient(ItemID.StoneBlock, 20).AddIngredient(ItemID.Mushroom, 3);
        }
        if (boss == 8)
        {
            recipe.RemoveIngredient(ItemID.MeteoriteBar); recipe.AddRecipeGroup(CopperTinGroup, 8);
            recipe.AddIngredient(MiningCatalog.BarType(2), 4);
        }
        if (boss == 9) ReplaceGroup(recipe, ItemID.GoldBar, GoldPlatinumGroup);
    }

    private static void MigrateEquipment(Recipe recipe)
    {
        Item item = recipe.createItem;
        if (item.ModItem is VoyageWeapon weapon)
        {
            int boss = VoyageArsenal.Boss(weapon.Index);
            SetIngredient(recipe, VoyageCatalog.Alloy, boss == 16 ? 8 : 4);
            AddStageMaterial(recipe, VoyageOre(boss), boss == 16 ? 8 : 6, item.DamageType == DamageClass.Magic || item.DamageType == DamageClass.Summon);
        }
        else if (item.ModItem is VoyageArmor armor)
        {
            // Class-helmet swaps are intentionally inexpensive; do not charge another whole helmet.
            if (recipe.requiredItem.Any(x => x.ModItem is VoyageArmor)) return;
            int count = armor.Part == 4 ? 10 : armor.Part == 5 ? 8 : 6;
            if (armor.Tier == 0) SetIngredient(recipe, VoyageCatalog.Alloy, armor.Part == 4 ? 8 : armor.Part == 5 ? 6 : 4);
            else SetIngredient(recipe, VoyageCatalog.Plate(armor.Tier == 5 ? 4 : armor.Tier - 1), armor.Part == 4 ? 8 : armor.Part == 5 ? 6 : 4);
            Bar(recipe, 7 + armor.Tier, count);
        }
        else if (item.ModItem is VoyagePlateBase plate)
        {
            SetIngredient(recipe, VoyageCatalog.Alloy, 6);
            // Ten plates still cost only six stage bars; major hardware has a distinct component use.
            recipe.AddIngredient(MiningCatalog.ComponentType(8 + plate.Index), 3);
        }
        else if (item.ModItem is VoyageOrdinaryAccessory accessory)
        {
            HalveAlloy(recipe);
            AddStageMaterial(recipe, VoyageOre(VoyageAccessoryBoss[accessory.Index]), 4, true);
        }
        else if (item.ModItem is VoyageUtilityItem utility)
        {
            HalveAlloy(recipe);
            AddStageMaterial(recipe, VoyageOre(VoyageUtilityBoss[utility.Index]), 4, true);
        }
        else
        {
            int boss = PrimordialArmorBoss(item.ModItem?.Name ?? "");
            if (boss >= 0)
            {
                if (boss == 0) return; // Resin equipment cannot require later/moss bosses.
                recipe.RemoveIngredient(ItemID.CopperBar); recipe.RemoveIngredient(ItemID.BeeWax); recipe.RemoveIngredient(ItemID.MeteoriteBar);
                int count = item.bodySlot >= 0 ? 6 : item.legSlot >= 0 ? 5 : 4;
                Bar(recipe, PrimordialOre(boss), count);
            }
            else if (IsLateStarfallCraft(item))
            {
                HalveAlloy(recipe);
                bool final = recipe.requiredItem.Any(x => x.type == I("MoonGodCore"));
                int count = item.bodySlot >= 0 ? 10 : item.legSlot >= 0 ? 8 : item.accessory ? 4 : 6;
                AddStageMaterial(recipe, final ? 7 : 6, count, item.accessory);
            }
        }
    }

    private static void AddStageMaterial(Recipe recipe, int ore, int bars, bool component)
    {
        if (component && ore is not (0 or 3)) recipe.AddIngredient(MiningCatalog.ComponentType(ore), (bars + 1) / 2);
        else Bar(recipe, ore, bars);
    }
    private static void HalveAlloy(Recipe recipe)
    {
        foreach (Item ingredient in recipe.requiredItem)
            if (ingredient.type == VoyageCatalog.Alloy || ingredient.type == I("AstralIngot")) ingredient.stack = Math.Max(1, (ingredient.stack + 1) / 2);
    }
    private static void SetIngredient(Recipe recipe, int type, int count)
    {
        Item? ingredient = recipe.requiredItem.FirstOrDefault(x => x.type == type);
        if (ingredient != null) ingredient.stack = count; else recipe.AddIngredient(type, count);
    }
    private static void ReplaceGroup(Recipe recipe, int type, string group)
    {
        int count = recipe.requiredItem.Where(x => x.type == type).Sum(x => x.stack);
        if (count > 0) { recipe.RemoveIngredient(type); recipe.AddRecipeGroup(group, count); }
    }
    private static int PrimordialArmorBoss(string name) => name.StartsWith("Resinweave", StringComparison.Ordinal) ? 0 :
        name.StartsWith("Coppervein", StringComparison.Ordinal) ? 1 : name.StartsWith("Frostmire", StringComparison.Ordinal) ? 3 :
        name.StartsWith("Waxguard", StringComparison.Ordinal) ? 5 : name.StartsWith("AstralExplorer", StringComparison.Ordinal) ? 9 : -1;
    private static bool IsLateStarfallCraft(Item item) => item.ModItem?.Name is "AstralThroneHelmet" or "AstralThronePlate" or "AstralThroneGreaves"
        or "ThroneAegis" or "VoidstepInsignia" or "AstralConductor" or "Thronebreaker";
    internal static string Fingerprint(Recipe recipe) => recipe.createItem.type + ":" + recipe.createItem.stack + ":" +
        string.Join(",", recipe.requiredItem.Select(x => x.type + "x" + x.stack)) + ":" + string.Join(",", recipe.requiredTile) + ":" +
        string.Join(",", recipe.acceptedGroups) + ":" + string.Join(",", recipe.Conditions.Select(x => x.Description.Key));
}
