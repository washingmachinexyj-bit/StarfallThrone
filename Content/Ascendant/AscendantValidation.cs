using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;
using Terraria.GameContent.ItemDropRules;
using Terraria.Utilities;
using StarfallThrone.Content.Divine;
using StarfallThrone.Content.Ecology;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Ascendant;

// Menu-only integration checks. Boss AI and equipment simulations have their own validators.
public static class AscendantValidation
{
    private static int checks;
    private static void Check(bool ok, string why)
    { if (!ok) throw new InvalidOperationException("Ascendant support: " + why); checks++; }
    private static void Log(string message) => ModContent.GetInstance<AscendantWorld>().Mod.Logger.Info(message);
    private static void Guard() => Check(Main.gameMenu && !Main.dedServ &&
        Terraria.Program.LaunchParameters.ContainsKey("-starfall-art-smoke"), "isolated native menu guard");

    private sealed class GateSnapshot : IDisposable
    {
        private readonly bool king = NPC.downedSlimeKing, hard = Main.hardMode, moon = NPC.downedMoonlord;
        private readonly bool[] minis = MiniBossWorld.Downed, ecology = EcologyWorld.Downed, divine = DivineWorld.Downed;
        public GateSnapshot()
        {
            MiniBossWorld.Downed = (bool[])minis.Clone();
            EcologyWorld.Downed = (bool[])ecology.Clone();
            DivineWorld.Downed = (bool[])divine.Clone();
        }
        public void Dispose()
        {
            NPC.downedSlimeKing = king; Main.hardMode = hard; NPC.downedMoonlord = moon;
            MiniBossWorld.Downed = minis; EcologyWorld.Downed = ecology; DivineWorld.Downed = divine;
        }
    }

    private static void Deadlines(int mask)
    { NPC.downedSlimeKing = (mask & 1) != 0; Main.hardMode = (mask & 2) != 0; NPC.downedMoonlord = (mask & 4) != 0; }
    private static void AllPrerequisites()
    { Array.Fill(MiniBossWorld.Downed, true); Array.Fill(EcologyWorld.Downed, true); Array.Fill(DivineWorld.Downed, true); EcologyWorld.Downed[9] = false; }
    private static void OnlyPrerequisites(int tier)
    {
        Array.Clear(MiniBossWorld.Downed); Array.Clear(EcologyWorld.Downed); Array.Clear(DivineWorld.Downed);
        DivineWorld.Downed[tier] = true;
        if (tier == 0) Array.Fill(MiniBossWorld.Downed, true);
        else for (int i = tier == 1 ? 0 : 4; i < (tier == 1 ? 4 : 9); i++) EcologyWorld.Downed[i] = true;
    }

    public static void Data()
    {
        Guard(); checks = 0;
        using var gates = new GateSnapshot();
        for (int mask = 0; mask < 8; mask++)
        {
            Deadlines(mask);
            for (int tier = 0; tier < 3; tier++)
                Check(AscendantWorld.Open(tier) == ((mask & (1 << tier)) == 0), "independent deadline " + mask + "/" + tier);
        }
        Check(!AscendantWorld.Open(-1) && !AscendantWorld.Open(3), "invalid deadline indices");
        Check(!AscendantWorld.Prerequisites(-1) && !AscendantWorld.Prerequisites(3), "invalid prerequisite indices");
        // Exhaustive gate truth tables, including all eight minis and both ecology subsets.
        for (int tier = 0; tier < 3; tier++)
        {
            int count = tier == 0 ? 8 : tier == 1 ? 4 : 5;
            for (int baseWon = 0; baseWon < 2; baseWon++)
            for (int mask = 0; mask < (1 << count); mask++)
            {
                OnlyPrerequisites(tier); DivineWorld.Downed[tier] = baseWon != 0;
                for (int i = 0; i < count; i++)
                    if (tier == 0) MiniBossWorld.Downed[i] = (mask & (1 << i)) != 0;
                    else EcologyWorld.Downed[(tier == 1 ? 0 : 4) + i] = (mask & (1 << i)) != 0;
                Check(AscendantWorld.Prerequisites(tier) == (baseWon == 1 && mask == (1 << count) - 1),
                    "all prerequisite combinations " + tier + "/" + baseWon + "/" + mask);
            }
            OnlyPrerequisites(tier);
            Check(AscendantWorld.Prerequisites(tier), "no unrelated branch or other enhanced god required " + tier);
        }
        Check(!EcologyWorld.Downed[9] && AscendantWorld.Prerequisites(2), "NoDay explicitly excludes post-Moon ecology9");

        for (int tier = 0; tier < 3; tier++)
        {
            Item summon = new(AscendantCatalog.Summon(tier)), bag = new(AscendantCatalog.Bag(tier));
            Check(!summon.consumable && summon.maxStack == 1, "reusable summon " + tier);
            Recipe r = Main.recipe.Take(Recipe.numRecipes).Single(r => !r.Disabled && r.createItem.type == summon.type);
            int station = tier == 0 ? TileID.WorkBenches : tier == 1 ? TileID.Anvils : TileID.LunarCraftingStation;
            Check(r.requiredTile.SequenceEqual(new[] { station }), "pre-deadline vanilla station " + tier);
            Check(r.Conditions.Count == 0, "recipe remains usable after closing " + tier);
            var expected = new Dictionary<int, int> { [DivineCatalog.Material(tier)] = tier == 0 ? 12 : tier == 1 ? 16 : 20 };
            if (tier == 0) for (int i = 0; i < 8; i++) expected.Add(MiniBossData.MaterialType(i), 4);
            else for (int i = tier == 1 ? 0 : 4; i < (tier == 1 ? 4 : 9); i++) expected.Add(EcologyCatalog.Core(i), tier == 1 ? 8 : 10);
            Check(r.requiredItem.Count == expected.Count && r.requiredItem.All(it => expected.TryGetValue(it.type, out int amount) && it.stack == amount),
                "exact summon ingredients and quantities " + tier);
            Check(r.acceptedGroups.Count == 0 && r.requiredItem.All(it => !it.expert && !it.master),
                "no recipe substitution or difficulty-exclusive gate " + tier);
            Check(!r.requiredItem.Any(it => it.type == ItemID.LunarBar || it.type == EcologyCatalog.Core(9) ||
                it.type == AscendantCatalog.Material(tier)), "no circular or post-Moon material " + tier);
            Check(bag.expert && bag.ModItem.CanRightClick() && ItemID.Sets.BossBag[bag.type], "native expert bag " + tier);
            var bagDrops = new List<DropRateInfo>();
            foreach (var rule in Main.ItemDropsDB.GetRulesForItemID(bag.type)) rule.ReportDroprates(bagDrops, new DropRateInfoChainFeed(1f));
            Check(bagDrops.Any(d => d.itemId == AscendantCatalog.Material(tier) && d.stackMin == 44 && d.stackMax == 56 && d.dropRate > .999f), "reported bag material " + tier);
            Check(bagDrops.Any(d => d.itemId == AscendantCatalog.Expert(tier) && d.dropRate > .999f), "guaranteed expert item " + tier);
            for (int choice = 0; choice < 4; choice++)
                Check(bagDrops.Any(d => d.itemId == AscendantCatalog.Weapon(tier * 4 + choice)), "four-class bag pool " + tier + "/" + choice);
            Check(!bagDrops.Any(d => d.itemId == AscendantCatalog.Cache(tier)), "first cache is entitlement, not repeatable bag loot " + tier);
            Check(new Item(AscendantCatalog.Relic(tier)).master, "master relic " + tier);
            Check(new Item(AscendantCatalog.Mask(tier)).headSlot >= 0, "native wearable mask " + tier);
            Check(new Item(AscendantCatalog.Pet(tier)).buffType > 0, "native pet buff " + tier);
            Check(!new Item(AscendantCatalog.Cache(tier)).ModItem.ConsumeItem(Main.LocalPlayer), "opening choice UI does not consume " + tier);
            Check(AscendantCatalog.Progression(tier) > DivineCatalog.Progression(tier) &&
                AscendantCatalog.Progression(tier) < (tier == 0 ? 1 : tier == 1 ? 7 : 18), "checklist order " + tier);
        }
        if (ModLoader.TryGetMod("BossChecklist", out Mod checklist))
        {
            var entries = checklist.Call("GetBossInfoDictionary", ModContent.GetInstance<AscendantWorld>().Mod, "2.0.0") as System.Collections.IDictionary;
            for (int tier = 0; tier < 3; tier++)
            {
                string key = "StarfallThrone " + AscendantCatalog.Keys[tier];
                Check(entries != null && entries.Contains(key), "checklist entry " + key);
                var entry = entries[key] as System.Collections.IDictionary;
                Check(entry != null && Equals(entry["isBoss"], true) &&
                    Convert.ToSingle(entry["progression"]) == AscendantCatalog.Progression(tier), "native checklist metadata " + tier);
            }
        }
        bool[] downed = AscendantWorld.Downed;
        try
        {
            AscendantWorld.Downed = new bool[3];
            for (int tier = 0; tier < 3; tier++)
            {
                Deadlines(7); Check(AscendantChecklist.SpawnInfo(tier).Key.EndsWith("ChecklistMissed"), "missed challenge visible " + tier);
                AscendantWorld.Downed[tier] = true;
                Check(AscendantChecklist.SpawnInfo(tier).Key.EndsWith("ChecklistClosedWon"), "completed closed challenge visible " + tier);
                Deadlines(0); Check(AscendantChecklist.SpawnInfo(tier).Key.EndsWith("Spawn" + tier), "open requirements text " + tier);
            }
        }
        finally { AscendantWorld.Downed = downed; }
        Log("ASCENDANT_SUPPORT_DATA_PASS checks=" + checks + " windows=24 prerequisiteTruthTable=608 exactRecipes=3 bags=3 noPostMoonDeadlock=true");
    }

    public static void Run()
    {
        Guard();
        using var gates = new GateSnapshot();
        var players = (Player[])Main.player.Clone(); var npcs = (NPC[])Main.npc.Clone(); var items = (Item[])Main.item.Clone();
        var projectiles = (Projectile[])Main.projectile.Clone(); var dust = (Dust[])Main.dust.Clone();
        var gore = (Gore[])Main.gore.Clone(); var text = (CombatText[])Main.combatText.Clone();
        var identities = (int[,])Main.projectileIdentity.Clone(); var oldRand = Main.rand;
        var oldWorldFile = Main.ActiveWorldFileData;
        bool day = Main.dayTime; int mode = Main.GameMode, net = Main.netMode, my = Main.myPlayer, countdown = NPC.MoonLordCountdown;
        AscendantWorld world = ModContent.GetInstance<AscendantWorld>(); TagCompound saved = new(); world.SaveWorldData(saved);
        try
        {
            for (int i = 0; i < Main.player.Length; i++) Main.player[i] = new Player { active = false, whoAmI = i };
            for (int i = 0; i < Main.npc.Length; i++) Main.npc[i] = new NPC { active = false, whoAmI = i };
            for (int i = 0; i < Main.projectile.Length; i++) Main.projectile[i] = new Projectile { active = false, whoAmI = i };
            for (int i = 0; i < Main.dust.Length; i++) Main.dust[i] = new Dust();
            for (int i = 0; i < Main.gore.Length; i++) Main.gore[i] = new Gore();
            for (int i = 0; i < Main.combatText.Length; i++) Main.combatText[i] = new CombatText();
            Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0; Main.GameMode = 0;
            Main.rand = new UnifiedRandom(909003); ClearItems();
            Main.ActiveWorldFileData = new Terraria.IO.WorldFileData("", false);
            if (ModLoader.TryGetMod("BossChecklist", out Mod checklist))
            {
                ModSystem records = checklist.GetContent<ModSystem>().Single(s => s.Name == "RecordSystem");
                records.OnWorldLoad(); records.LoadWorldData(new TagCompound());
            }
            Deadlines(0); AllPrerequisites(); NPC.MoonLordCountdown = 0;
            Player p = new() { active = true, whoAmI = 0, position = new Vector2(16000, 2400) };
            p.ResetEffects(); p.selectedItem = 0; Main.player[0] = p;
            SummonChecks(p); LootChecks(p); StateAndGiftChecks(p, world); ChoiceChecks(p); PacketChecks(p, world);
            PetChecks(p); PetRender(p); WearRender(); FurnitureRender(); ChoiceRender(p);
            Log("ASCENDANT_SUPPORT_RUNTIME_PASS checks=" + checks + " nativeSummons=3 missingOne=20 normalLoot=3 expertBagRolls=6 firstClaimsPersist=3 cacheChoices=12");
        }
        finally
        {
            for (int i = 0; i < Main.player.Length; i++) Main.player[i] = players[i];
            for (int i = 0; i < Main.npc.Length; i++) Main.npc[i] = npcs[i];
            for (int i = 0; i < Main.item.Length; i++) Main.item[i] = items[i];
            for (int i = 0; i < Main.projectile.Length; i++) Main.projectile[i] = projectiles[i];
            for (int i = 0; i < Main.dust.Length; i++) Main.dust[i] = dust[i];
            for (int i = 0; i < Main.gore.Length; i++) Main.gore[i] = gore[i];
            for (int i = 0; i < Main.combatText.Length; i++) Main.combatText[i] = text[i];
            Array.Copy(identities, Main.projectileIdentity, identities.Length);
            Main.GameMode = mode; Main.netMode = net; Main.myPlayer = my; Main.rand = oldRand;
            Main.dayTime = day; Main.ActiveWorldFileData = oldWorldFile; NPC.MoonLordCountdown = countdown;
            world.LoadWorldData(saved); AscendantChoiceUI.Tier = AscendantChoiceUI.Slot = -1;
        }
    }

    private static void ClearItems() { for (int i = 0; i < Main.item.Length; i++) Main.item[i] = new Item(); }
    private static int Amount(int type) => Main.item.Where(i => i.active && i.type == type).Sum(i => i.stack);
    private static void ClearNPCs() { foreach (NPC npc in Main.npc) npc.active = false; }
    private static void Arena(Player p, int tier)
    {
        Main.dayTime = tier != 2; p.ZoneOverworldHeight = tier != 1; p.ZoneUnderworldHeight = tier == 1; p.ZoneSkyHeight = false;
        p.ZoneDesert = p.ZoneSnow = p.ZoneJungle = p.ZoneCorrupt = p.ZoneCrimson = p.ZoneHallow = p.ZoneBeach = false;
        p.ZoneTowerSolar = p.ZoneTowerVortex = p.ZoneTowerNebula = p.ZoneTowerStardust = false;
        p.active = true; p.dead = p.ghost = false; p.selectedItem = 0; p.inventory[0] = new Item(AscendantCatalog.Summon(tier));
    }
    private static void SummonChecks(Player p)
    {
        for (int tier = 0; tier < 3; tier++)
        {
            ClearNPCs(); Deadlines(0); OnlyPrerequisites(tier); Arena(p, tier);
            Check(AscendantSummonBase.Valid(p, tier), "only own prerequisites accepted " + tier);
            int count = tier == 0 ? 8 : tier == 1 ? 4 : 5;
            for (int missing = 0; missing < count; missing++)
            {
                if (tier == 0) MiniBossWorld.Downed[missing] = false;
                else EcologyWorld.Downed[(tier == 1 ? 0 : 4) + missing] = false;
                Check(!AscendantSummonBase.Valid(p, tier) && !AscendantSummonBase.TrySummon(p, tier) &&
                    !Main.npc.Any(n => n.active), "each missing prerequisite rejects actual summon " + tier + "/" + missing);
                OnlyPrerequisites(tier);
            }
            DivineWorld.Downed[tier] = false;
            Check(!AscendantSummonBase.TrySummon(p, tier), "base god must be defeated " + tier);
            DivineWorld.Downed[tier] = true;
            for (int mask = 0; mask < 8; mask++)
            { Deadlines(mask); Check(AscendantSummonBase.Valid(p, tier) == ((mask & (1 << tier)) == 0), "summon deadline matrix " + tier + "/" + mask); }
            Deadlines(0);
            p.inventory[0] = new Item(ItemID.DirtBlock);
            Check(!AscendantSummonBase.TrySummon(p, tier), "held item authorization " + tier);
            p.inventory[0] = new Item(AscendantCatalog.Summon((tier + 1) % 3));
            Check(!AscendantSummonBase.TrySummon(p, tier), "wrong-tier held summon " + tier);
            p.inventory[0] = new Item(AscendantCatalog.Summon(tier));
            p.dead = true; Check(!AscendantSummonBase.TrySummon(p, tier), "dead player"); p.dead = false;
            p.ghost = true; Check(!AscendantSummonBase.TrySummon(p, tier), "ghost player"); p.ghost = false;
            p.active = false; Check(!AscendantSummonBase.TrySummon(p, tier), "inactive player"); p.active = true;
            Main.netMode = NetmodeID.MultiplayerClient;
            Check(!AscendantSummonBase.TrySummon(p, tier), "client cannot spawn boss"); Main.netMode = NetmodeID.SinglePlayer;
            Check(AscendantSummonBase.TrySummon(p, tier), "native summon " + tier);
            Check(Main.npc.Count(n => n.active && n.type == AscendantCatalog.NPCType(tier)) == 1, "exactly one spawned boss " + tier);
            Check(Main.npc.Single(n => n.active).Distance(p.Center) < 650, "initial target range " + tier);
            Check(!AscendantSummonBase.TrySummon(p, tier), "same boss duplicate rejected " + tier);
            ClearNPCs();
            Main.npc[0].SetDefaults(NPCID.EyeofCthulhu); Main.npc[0].active = true;
            Check(!AscendantSummonBase.TrySummon(p, tier), "another vanilla boss rejected " + tier); ClearNPCs();
            Main.npc[0].SetDefaults(MiniBossData.NPCType(6)); Main.npc[0].active = true;
            Check(!AscendantSummonBase.TrySummon(p, tier), "live miniboss rejected " + tier); ClearNPCs();
        }
        OnlyPrerequisites(0); Arena(p, 0); Main.dayTime = false; Check(!AscendantSummonBase.Valid(p, 0), "Miroen requires day");
        Main.dayTime = true; p.ZoneJungle = true; Check(!AscendantSummonBase.Valid(p, 0), "Miroen forest not jungle");
        OnlyPrerequisites(1); Arena(p, 1); p.ZoneUnderworldHeight = false; Check(!AscendantSummonBase.Valid(p, 1), "Velsa requires Underworld");
        OnlyPrerequisites(2); Arena(p, 2);
        NPC.MoonLordCountdown = 60; Check(!AscendantSummonBase.Valid(p, 2), "Moon Lord arrival blocks NoDay"); NPC.MoonLordCountdown = 0;
        for (int tower = 0; tower < 4; tower++)
        {
            p.ZoneTowerSolar = tower == 0; p.ZoneTowerVortex = tower == 1; p.ZoneTowerNebula = tower == 2; p.ZoneTowerStardust = tower == 3;
            Check(!AscendantSummonBase.Valid(p, 2), "each local pillar zone rejects " + tower);
        }
        Arena(p, 2); p.ZoneOverworldHeight = false; p.ZoneSkyHeight = true;
        Check(AscendantSummonBase.Valid(p, 2) && !EcologyWorld.Downed[9], "pre-Moon sky valid without ecology9");
        Main.dayTime = true; Check(!AscendantSummonBase.Valid(p, 2), "NoDay requires night");
        Check(!AscendantSummonBase.TrySummon(p, -1) && !AscendantSummonBase.TrySummon(p, 255), "invalid summon indices");
        AllPrerequisites(); Deadlines(0); Arena(p, 0);
        Log("ASCENDANT_SUMMON_RUNTIME_PASS nativeSummons=3 missingOne=20 deadlineMatrix=24 heldAuthorization=3 duplicates=3 noEcology9=true");
    }

    private static void LootChecks(Player p)
    {
        var resolver = new ItemDropResolver(Main.ItemDropsDB);
        for (int tier = 0; tier < 3; tier++)
        {
            for (int difficulty = 0; difficulty < 3; difficulty++)
            {
                Main.GameMode = difficulty; ClearItems();
                NPC boss = new(); boss.SetDefaults(AscendantCatalog.NPCType(tier)); boss.whoAmI = 0;
                boss.position = p.position; boss.active = true; boss.playerInteraction[0] = true; Main.npc[0] = boss;
                resolver.TryDropping(new DropAttemptInfo { npc = boss, player = p, rng = new UnifiedRandom(2234 + tier), IsExpertMode = difficulty > 0, IsMasterMode = difficulty == 2 });
                if (difficulty == 0)
                {
                    Check(Amount(AscendantCatalog.Material(tier)) is >= 36 and <= 44, "native normal material " + tier);
                    Check(Enumerable.Range(tier * 4, 4).Sum(n => Amount(AscendantCatalog.Weapon(n))) == 1, "native normal one weapon " + tier);
                    Check(Amount(AscendantCatalog.Bag(tier)) == 0 && Amount(AscendantCatalog.Expert(tier)) == 0 &&
                        Amount(AscendantCatalog.Relic(tier)) == 0, "normal excludes expert and master loot " + tier);
                }
                else
                {
                    Check(Amount(AscendantCatalog.Bag(tier)) == 1, "native expert/master one bag " + tier);
                    Check(Amount(AscendantCatalog.Material(tier)) == 0 && Amount(AscendantCatalog.Expert(tier)) == 0 &&
                        Enumerable.Range(tier * 4, 4).Sum(n => Amount(AscendantCatalog.Weapon(n))) == 0 &&
                        Amount(AscendantCatalog.Mask(tier)) == 0 && Amount(AscendantCatalog.Cache(tier)) == 0, "no loose combat rewards " + tier);
                    Check(Amount(AscendantCatalog.Relic(tier)) == (difficulty == 2 ? 1 : 0), "master-exclusive guaranteed relic " + tier);
                    ClearItems();
                    resolver.TryDropping(new DropAttemptInfo { item = AscendantCatalog.Bag(tier), player = p, rng = new UnifiedRandom(2300 + tier), IsExpertMode = true, IsMasterMode = difficulty == 2 });
                    Check(Amount(AscendantCatalog.Material(tier)) is >= 44 and <= 56 && Amount(AscendantCatalog.Expert(tier)) == 1, "actual bag material/expert " + tier);
                    Check(Enumerable.Range(tier * 4, 4).Sum(n => Amount(AscendantCatalog.Weapon(n))) == 1, "actual bag one weapon " + tier);
                    Check(Amount(AscendantCatalog.Cache(tier)) == 0, "repeatable bag table never mints first cache " + tier);
                }
                boss.active = false;
            }
            for (int difficulty = 0; difficulty < 3; difficulty++)
            {
                Main.GameMode = difficulty; ClearItems(); Deadlines(7);
                NPC closed = new(); closed.SetDefaults(AscendantCatalog.NPCType(tier)); closed.position = p.position; closed.playerInteraction[0] = true;
                resolver.TryDropping(new DropAttemptInfo { npc = closed, player = p, rng = new UnifiedRandom(1), IsExpertMode = difficulty > 0, IsMasterMode = difficulty == 2 });
                Check(!Main.item.Any(i => i.active), "closed deadline drops nothing in every difficulty " + tier + "/" + difficulty);
                Deadlines(0);
            }
            for (int difficulty = 0; difficulty < 3; difficulty++)
            for (int missing = 0; missing < 2; missing++)
            {
                Main.GameMode = difficulty; ClearItems(); AllPrerequisites();
                if (missing == 0) DivineWorld.Downed[tier] = false;
                else if (tier == 0) MiniBossWorld.Downed[0] = false;
                else EcologyWorld.Downed[tier == 1 ? 0 : 4] = false;
                NPC forced = new(); forced.SetDefaults(AscendantCatalog.NPCType(tier)); forced.position = p.position; forced.playerInteraction[0] = true;
                resolver.TryDropping(new DropAttemptInfo { npc = forced, player = p, rng = new UnifiedRandom(2), IsExpertMode = difficulty > 0, IsMasterMode = difficulty == 2 });
                Check(!Main.item.Any(it => it.active), "forced spawn cannot bypass loot prerequisites " + tier + "/" + difficulty + "/" + missing);
            }
            AllPrerequisites();
        }
        Main.GameMode = 0; ClearNPCs();
        Log("ASCENDANT_LOOT_RUNTIME_PASS normal=3 expertBags=6 closedWindowRolls=9 prerequisiteBypassRolls=18 looseExpertRewards=0 masterRelics=3");
    }

    private static TagCompound Saved(ModSystem world) { var tag = new TagCompound(); world.SaveWorldData(tag); return tag; }
    private static byte[] Encoded(TagCompound tag)
    { using var stream = new MemoryStream(); TagIO.ToStream(tag, stream); return stream.ToArray(); }
    private static void Reload(AscendantWorld world)
    {
        using var stream = new MemoryStream(Encoded(Saved(world)));
        world.ClearWorld(); world.LoadWorldData(TagIO.FromStream(stream));
    }
    private static NPC VictoryNPC(int tier, Player p)
    { NPC boss = new(); boss.SetDefaults(AscendantCatalog.NPCType(tier)); boss.playerInteraction[p.whoAmI] = true; return boss; }
    private static void StateAndGiftChecks(Player p, AscendantWorld world)
    {
        AllPrerequisites(); Deadlines(0); Main.GameMode = 0; ClearItems(); world.ClearWorld(); world.OnWorldLoad();
        var legacy = new ModSystem[] { ModContent.GetInstance<MiniBossWorld>(), ModContent.GetInstance<DivineWorld>(),
            ModContent.GetInstance<EcologyWorld>(), ModContent.GetInstance<StarfallWorld>(), ModContent.GetInstance<PrimordialWorld>(),
            ModContent.GetInstance<global::StarfallThrone.Content.Voyage.VoyageWorld>(), ModContent.GetInstance<global::StarfallThrone.Content.Mining.MiningWorld>() };
        var legacyBefore = legacy.Select(w => Encoded(Saved(w))).ToArray();
        Guid worldKey = AscendantWorld.WorldKey, identity = Guid.NewGuid();
        Check(worldKey != Guid.Empty && !AscendantWorld.TryClaim(Guid.Empty, 0) &&
            !AscendantWorld.TryClaim(identity, -1) && !AscendantWorld.TryClaim(identity, 3), "valid world identity and bounded claim");
        for (int tier = 0; tier < 3; tier++)
        {
            Check(AscendantWorld.TryClaim(identity, tier) && !AscendantWorld.TryClaim(identity, tier), "once-only first entitlement " + tier);
            AscendantWorld.Downed[tier] = true; AscendantWorld.Pending[identity] = (byte)(AscendantWorld.Pending.GetValueOrDefault(identity) | (1 << tier));
        }
        Reload(world);
        Check(AscendantWorld.WorldKey == worldKey && AscendantWorld.Pending[identity] == 7, "binary persistence retains world and pending identity");
        for (int tier = 0; tier < 3; tier++) Check(AscendantWorld.Downed[tier] && !AscendantWorld.TryClaim(identity, tier), "rejoin never resets first claim " + tier);
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) world.NetSend(writer);
            AscendantWorld.Downed = new bool[3]; AscendantWorld.WorldKey = Guid.Empty;
            stream.Position = 0; using var reader = new BinaryReader(stream); world.NetReceive(reader);
            Check(AscendantWorld.WorldKey == worldKey && AscendantWorld.Downed.All(b => b) && AscendantWorld.Pending[identity] == 7, "network world state does not erase server entitlements");
        }
        world.ClearWorld(); world.LoadWorldData(new TagCompound());
        Check(AscendantWorld.Downed.All(b => !b) && AscendantWorld.Claims.Count == 0 && AscendantWorld.Pending.Count == 0, "legacy world without ascendant data has no fabricated victories");
        for (int i = 0; i < legacy.Length; i++) Check(legacyBefore[i].SequenceEqual(Encoded(Saved(legacy[i]))), "old world state byte-identical after new serialization " + legacy[i].Name);

        var state = p.GetModPlayer<AscendantChallengePlayer>(); state.Identity = Guid.NewGuid(); state.IdentityBound = true; state.Victories = 0;
        bool[] participants = new bool[Main.maxPlayers], damaged = new bool[Main.maxPlayers]; participants[0] = damaged[0] = true;
        for (int tier = 0; tier < 3; tier++)
        {
            NPC boss = VictoryNPC(tier, p);
            AscendantWorld.RecordVictory(boss, tier, participants, damaged); AscendantWorld.RecordVictory(boss, tier, participants, damaged);
            Check(Amount(AscendantCatalog.Cache(tier)) == 1 && !AscendantWorld.Pending.ContainsKey(state.Identity), "normal first cache direct exactly once " + tier);
            Check(Amount(AscendantCatalog.Halo(tier)) == 0, "damaged contributor has no flawless halo " + tier);
        }
        Check(Amount(ModContent.ItemType<AscendantMonument>()) == 1 && Amount(ModContent.ItemType<AscendantEpochTitle>()) == 1, "trinity cosmetics awarded once");
        TagCompound playerTag = new(); state.SaveData(playerTag); Guid normalIdentity = state.Identity;
        Reload(world); state.Identity = Guid.NewGuid(); state.LoadData(playerTag); state.OnEnterWorld();
        Check(state.Identity == normalIdentity && state.Victories == 7 && state.IdentityBound, "player identity and victories survive rejoin");
        ClearItems();
        for (int tier = 0; tier < 3; tier++) AscendantWorld.RecordVictory(VictoryNPC(tier, p), tier, participants, damaged);
        Check(!Main.item.Any(it => it.active), "normal first gift not reset by save or rejoin");

        for (int tier = 0; tier < 3; tier++)
        {
            Main.GameMode = tier == 1 ? 2 : 1; Deadlines(0); ClearItems();
            state.Identity = Guid.NewGuid(); state.IdentityBound = true; Guid expertIdentity = state.Identity;
            NPC boss = VictoryNPC(tier, p);
            AscendantWorld.RecordVictory(boss, tier, participants, damaged); AscendantWorld.RecordVictory(boss, tier, participants, damaged);
            Check(Amount(AscendantCatalog.Cache(tier)) == 0 && AscendantWorld.Pending.TryGetValue(expertIdentity, out byte pending) && pending == 1 << tier,
                "expert first victory stores pending instead of loose cache " + tier);
            TagCompound expertPlayerTag = new(); state.SaveData(expertPlayerTag);
            Reload(world); state.Identity = Guid.NewGuid(); state.LoadData(expertPlayerTag); state.OnEnterWorld();
            Check(state.Identity == expertIdentity && state.Victories == 1 << tier, "expert player rejoin retains entitlement " + tier);
            Deadlines(7); Main.GameMode = 0; // An earned bag remains valid after its challenge window closes and difficulty changes.
            var bag = new Item(AscendantCatalog.Bag(tier)); bag.ModItem.RightClick(p);
            Check(Amount(AscendantCatalog.Cache(tier)) == 1 && (AscendantWorld.Pending[expertIdentity] & (1 << tier)) == 0, "closed-window bag RightClick claims pending " + tier);
            ClearItems(); bag.ModItem.RightClick(p);
            Check(!AscendantWorld.ClaimPending(p, tier) && !Main.item.Any(it => it.active), "bag spam and direct duplicate claim grant nothing " + tier);
            Reload(world); state.OnEnterWorld(); bag.ModItem.RightClick(p);
            Check(!Main.item.Any(it => it.active), "redeemed pending remains spent after save and rejoin " + tier);
        }
        Main.GameMode = 0; Deadlines(0); ClearItems(); state.Identity = Guid.NewGuid();
        for (int tier = 0; tier < 3; tier++)
        {
            NPC clean = VictoryNPC(tier, p); AscendantWorld.RecordVictory(clean, tier, participants, new bool[Main.maxPlayers]);
            Check(Amount(AscendantCatalog.Halo(tier)) == 1, "flawless contributing survivor gets halo " + tier);
            ClearItems(); clean.playerInteraction[0] = false; AscendantWorld.RecordVictory(clean, tier, participants, new bool[Main.maxPlayers]);
            Check(!Main.item.Any(it => it.active), "noncontributor gets no reward " + tier);
            clean.playerInteraction[0] = true; participants[0] = false;
            AscendantWorld.RecordVictory(clean, tier, participants, new bool[Main.maxPlayers]);
            Check(!Main.item.Any(it => it.active), "nonparticipant gets no reward " + tier); participants[0] = true;
        }
        for (int tier = 0; tier < 3; tier++)
        {
            state.Identity = Guid.NewGuid(); Deadlines(7); ClearItems();
            AscendantWorld.RecordVictory(VictoryNPC(tier, p), tier, participants, new bool[Main.maxPlayers]);
            Check(!Main.item.Any(it => it.active) && !AscendantWorld.Claims.ContainsKey(state.Identity), "deadline blocks all victory rewards " + tier);
            Deadlines(0); DivineWorld.Downed[tier] = false;
            AscendantWorld.RecordVictory(VictoryNPC(tier, p), tier, participants, new bool[Main.maxPlayers]);
            Check(!Main.item.Any(it => it.active) && !AscendantWorld.Claims.ContainsKey(state.Identity), "forced kill without prerequisite gives no reward " + tier);
            DivineWorld.Downed[tier] = true;
        }
        Log("ASCENDANT_STATE_RUNTIME_PASS legacySystemsUntouched=7 firstNormal=3 expertPending=3 binarySave=true playerRejoin=true closedWindowClaimOnce=true trinity=true");
    }

    private static void ChoiceChecks(Player p)
    {
        Deadlines(7); Main.GameMode = 0; // Reward crafting/selection is not limited by the original challenge window.
        for (int tier = 0; tier < 3; tier++)
        for (int choice = 0; choice < 4; choice++)
        {
            ClearItems(); p.inventory[0] = new Item(AscendantCatalog.Cache(tier));
            foreach (int badChoice in new[] { -1, 4, 255 })
                Check(!AscendantChoiceUI.Claim(p, 0, tier, badChoice) && p.inventory[0].stack == 1, "invalid choice cannot consume");
            foreach (int badSlot in new[] { -1, 58, 255 })
                Check(!AscendantChoiceUI.Claim(p, badSlot, tier, choice), "invalid inventory slot");
            Check(!AscendantChoiceUI.Claim(p, 0, (tier + 1) % 3, choice), "wrong-tier cache rejected");
            Check(!AscendantChoiceUI.Claim(p, 0, -1, choice) && !AscendantChoiceUI.Claim(p, 0, 255, choice), "invalid cache tier");
            p.dead = true; Check(!AscendantChoiceUI.Claim(p, 0, tier, choice), "dead cannot redeem"); p.dead = false;
            p.ghost = true; Check(!AscendantChoiceUI.Claim(p, 0, tier, choice), "ghost cannot redeem"); p.ghost = false;
            Main.netMode = NetmodeID.MultiplayerClient;
            Check(!AscendantChoiceUI.Claim(p, 0, tier, choice), "client cannot mint choice reward"); Main.netMode = NetmodeID.SinglePlayer;
            Check(AscendantChoiceUI.Claim(p, 0, tier, choice), "all twelve selected choices");
            Check(Amount(AscendantCatalog.Weapon(tier * 4 + choice)) == 1 && Main.item.Count(it => it.active) == 1, "only exact selected weapon created");
            Check(p.inventory[0].IsAir && !AscendantChoiceUI.Claim(p, 0, tier, choice), "cache atomically consumed before replay");
        }
        Deadlines(0); ClearItems();
        Log("ASCENDANT_CHOICE_RUNTIME_PASS choices=12 invalidSlots=true wrongTier=true repeatedClaimBlocked=true closedWindowsAllowed=true");
    }

    private static void PacketChecks(Player p, AscendantWorld world)
    {
        ClearNPCs(); ClearItems(); Arena(p, 0); AllPrerequisites(); Deadlines(0);
        p.inventory[0] = new Item(AscendantCatalog.Cache(0));
        var state = p.GetModPlayer<AscendantChallengePlayer>(); state.Identity = Guid.NewGuid(); state.IdentityBound = true; state.Victories = 0;
        Guid playerIdentity = state.Identity, otherIdentity = Guid.NewGuid();
        AscendantWorld.Claims[otherIdentity] = 7; AscendantWorld.Pending[otherIdentity] = 7;
        byte[] before = Encoded(Saved(world)); int oldNet = Main.netMode;
        void Packet(int sender, params byte[] body)
        {
            for (int tick = 0; tick < 31; tick++) state.PostUpdate(); // Exercise every request, not only the throttle branch.
            using var reader = new BinaryReader(new MemoryStream(new byte[] { 60 }.Concat(body).ToArray()));
            world.Mod.HandlePacket(reader, sender);
        }
        try
        {
            Main.netMode = NetmodeID.Server;
            foreach (int sender in new[] { -1, Main.maxPlayers, 1 })
            {
                Packet(sender, 1, 0); Packet(sender, 2, 0, 0, 0); Packet(sender, 5, 0);
            }
            foreach (byte[] malformed in new[] { Array.Empty<byte>(), new byte[] { 255 }, new byte[] { 0, 1 },
                new byte[] { 1 }, new byte[] { 2 }, new byte[] { 2, 0 }, new byte[] { 2, 0, 0 }, new byte[] { 5 } })
                Packet(0, malformed);
            Packet(0, 1, 255); Packet(0, 1, 0); // Valid tier but cache, not summon, is held.
            Packet(0, 2, 255, 0, 0); Packet(0, 2, 0, 255, 0); Packet(0, 2, 0, 0, 255); Packet(0, 2, 0, 1, 0);
            Packet(0, new byte[] { 0 }.Concat(otherIdentity.ToByteArray()).ToArray()); // Bound connection cannot steal identity.
            Packet(0, 4, 7); // Client cannot publish a victory record to the server.
            for (byte tier = 0; tier < 3; tier++) Packet(0, 5, tier);
            Packet(0, 5, 255);
            Check(state.Identity == playerIdentity && state.IdentityBound && state.Victories == 0 && before.SequenceEqual(Encoded(Saved(world))),
                "malicious/truncated packets cannot mutate claims, pending, identity or world");
            Check(p.inventory[0].type == AscendantCatalog.Cache(0) && p.inventory[0].stack == 1 &&
                !Main.item.Any(it => it.active) && !Main.npc.Any(n => n.active), "packets cannot consume unrelated cache or spawn rewards/bosses");
            Main.netMode = NetmodeID.MultiplayerClient;
            for (int tier = 0; tier < 3; tier++) Check(!AscendantWorld.ClaimPending(p, tier), "client pending claim rejected");
            Packet(0, 5, 0); Packet(0, 2, 0, 0, 0); Packet(0, 1, 0);
            Check(before.SequenceEqual(Encoded(Saved(world))) && !Main.item.Any(it => it.active), "client never executes authoritative packet actions");
        }
        finally { Main.netMode = oldNet; }
        Log("ASCENDANT_PACKET_RUNTIME_PASS invalidSender=true truncatedPackets=true wrongHeldItem=true identityRebindBlocked=true unearnedCacheBlocked=true clientAuthorityBlocked=true");
    }

    private readonly record struct Cell(TileTypeData Type, WallTypeData Wall, TileWallWireStateData State, LiquidData Liquid, TileWallBrightnessInvisibilityData Light)
    {
        public Cell(Tile t) : this(t.Get<TileTypeData>(), t.Get<WallTypeData>(), t.Get<TileWallWireStateData>(), t.Get<LiquidData>(), t.Get<TileWallBrightnessInvisibilityData>()) { }
        public void Restore(Tile t)
        { t.Get<TileTypeData>() = Type; t.Get<WallTypeData>() = Wall; t.Get<TileWallWireStateData>() = State; t.Get<LiquidData>() = Liquid; t.Get<TileWallBrightnessInvisibilityData>() = Light; }
    }

    private static void PetRender(Player p)
    {
        var device=Main.instance.GraphicsDevice;var targets=device.GetRenderTargets();Vector2 screen=Main.screenPosition;
        using var atlas=new RenderTarget2D(device,480,160);
        try
        {
            device.SetRenderTarget(atlas);device.Clear(new Color(19,26,36));Main.screenPosition=Vector2.Zero;
            Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);
            for(int tier=0;tier<3;tier++)
            {
                Item item=new(AscendantCatalog.Pet(tier));Projectile pet=new();pet.SetDefaults(item.shoot);pet.owner=p.whoAmI;
                pet.Center=new Vector2(80+tier*160,80);pet.spriteDirection=tier==1?-1:1;
                Color light=Color.White;Check(!pet.ModProjectile.PreDraw(ref light),"native custom pet draw "+tier);
            }
            Main.spriteBatch.End();device.SetRenderTargets(targets);
            SaveImage(atlas,"ascendant-pets-runtime.png");
            Log("ASCENDANT_PET_RENDER_PASS nativePreDraw=3 distinctFollowers=3");
        }
        finally { device.SetRenderTargets(targets);Main.screenPosition=screen; }
    }
    private static void PetChecks(Player p)
    {
        foreach(Projectile projectile in Main.projectile)projectile.active=false;
        for(int tier=0;tier<3;tier++)
        {
            Item item=new(AscendantCatalog.Pet(tier));item.ModItem.UseItem(p);
            int slot=p.FindBuffIndex(item.buffType);Check(slot>=0,"petnativebuff "+tier);
            BuffLoader.GetBuff(item.buffType).Update(p,ref slot);
            Projectile pet=Main.projectile.Single(pr=>pr.active&&pr.type==item.shoot);
            Check(pet.owner==p.whoAmI&&pet.ModProjectile.CanDamage()==false,"harmlessownedpet "+tier);
            for(int frame=0;frame<180;frame++)
            {pet.AI();pet.position+=pet.velocity;Check(float.IsFinite(pet.Center.X)&&float.IsFinite(pet.Center.Y)&&pet.velocity.Length()<=12.01f,"boundedpetfollow "+tier);}
            Check(pet.timeLeft==2,"petmaintainedbybuff "+tier);
            p.ClearBuff(item.buffType);pet.AI();Check(pet.timeLeft<=2,"petexpireswithoutbuff "+tier);
            pet.Kill();Check(!pet.active,"petdismissal "+tier);
        }
        Log("ASCENDANT_PET_RUNTIME_PASS nativeBuffs=3 harmlessFollowers=3 finiteMotion=true dismissal=true");
    }
    private static void SaveImage(RenderTarget2D image,string name)
    {string path=Path.GetFullPath(Path.Combine(Main.SavePath,"..","..","outputs",name));using FileStream stream=File.Create(path);image.SaveAsPng(stream,image.Width,image.Height);}
    private static void WearRender()
    {
        var device=Main.instance.GraphicsDevice;var targets=device.GetRenderTargets();Vector2 screen=Main.screenPosition;
        using var atlas=new RenderTarget2D(device,600,320);
        try
        {
            device.SetRenderTarget(atlas);device.Clear(new Color(19,26,36));Main.screenPosition=Vector2.Zero;
            for(int id=0;id<12;id++)
            {
                int tier=id<9?id/3:id-9,pose=id<9?id%3:0;
                Player p=new(){active=true,whoAmI=0,isDisplayDollOrInanimate=true,direction=id%2==0?1:-1};p.ResetEffects();
                p.head=new Item(AscendantCatalog.Mask(tier)).headSlot;
                if(id>=9)p.GetModPlayer<AscendantChallengePlayer>().HaloIndex=tier;
                p.bodyFrame=new Rectangle(0,(pose==1?3:0)*56,40,56);p.legFrame=new Rectangle(0,(pose==2?10:0)*56,40,56);
                Vector2 at=new(id%6*100+40,id/6*135+50);p.position=at;
                Main.PlayerRenderer.DrawPlayer(Main.Camera,p,at,0,Vector2.Zero,0,1.4f);
            }
            device.SetRenderTargets(targets);SaveImage(atlas,"ascendant-wear-runtime.png");Log("ASCENDANT_WEAR_RENDER_PASS masks=3 poses=3 halos=3 nativePlayerRenderer=true");
        }
        finally{device.SetRenderTargets(targets);Main.screenPosition=screen;}
    }
    private static void FurnitureRender()
    {
        const int left=1230,top=100;var device=Main.instance.GraphicsDevice;var targets=device.GetRenderTargets();
        bool noActions=WorldGen.noTileActions,gen=WorldGen.gen;WorldGen.noTileActions=WorldGen.gen=false;
        using var atlas=new RenderTarget2D(device,700,180);
        const int pad=8,cellWidth=24,cellHeight=25;
        var cells=new Cell[cellWidth*cellHeight];
        for(int x=0;x<cellWidth;x++)for(int y=0;y<cellHeight;y++)cells[x*cellHeight+y]=new Cell(Main.tile[left-pad+x,top-pad+y]);
        try
        {
            device.SetRenderTarget(atlas);device.Clear(new Color(19,26,36));
            for(int n=0;n<7;n++)
            {
                int itemType=n<3?AscendantCatalog.Relic(n):n<6?AscendantCatalog.Trophy(n-3):ModContent.ItemType<AscendantMonument>();
                Item item=new(itemType);int type=item.createTile;
                for(int x=left-2;x<left+7;x++)for(int y=top-2;y<top+8;y++)
                {Tile t=Main.tile[x,y];t.ClearEverything();t.WallType=WallID.Stone;if(y>=top+4){t.HasTile=true;t.TileType=TileID.Dirt;}}
                ClearItems();TileObjectData data=TileObjectData.GetTileData(type,0);int placedY=top+4-data.Height;
                WorldGen.PlaceObject(left+data.Origin.X,placedY+data.Origin.Y,type);
                Check(Main.tile[left,placedY].HasTile&&Main.tile[left,placedY].TileType==type,"nativefurnitureplaced "+n);
                var texture=ModContent.Request<Texture2D>(TileLoader.GetTile(type).Texture).Value;
                var pixels=new Color[texture.Width*texture.Height];texture.GetData(pixels);
                Check(pixels.Count(c=>c.A>20)>100,"nonempty furniture texture "+n);
                int drawnOpaque=0;
                Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,DepthStencilState.None,RasterizerState.CullNone);
                for(int x=0;x<data.Width;x++)for(int y=0;y<data.Height;y++)
                {
                    Tile t=Main.tile[left+x,placedY+y];Rectangle src=new(t.TileFrameX,t.TileFrameY,16,data.CoordinateHeights[y]);
                    Check(src.Right<=texture.Width&&src.Bottom<=texture.Height,"furnitureframeinside "+n);
                    for(int py=src.Top;py<src.Bottom;py++)for(int px=src.Left;px<src.Right;px++)if(pixels[py*texture.Width+px].A>20)drawnOpaque++;
                    Main.spriteBatch.Draw(texture,new Vector2(n*98+20+x*16,26+y*16),src,Color.White);
                }
                Main.spriteBatch.End();Check(drawnOpaque>100,"placed furniture is visible "+n);WorldGen.KillTile(left,placedY);WorldGen.SquareTileFrame(left,placedY,true);
                Check(Amount(itemType)==1,"nativeexactonefurnituredrop "+n);
            }
            device.SetRenderTargets(targets);SaveImage(atlas,"ascendant-furniture-runtime.png");Log("ASCENDANT_FURNITURE_RENDER_PASS nativePlace=7 nativeBreak=7 singleDrops=7 frameBounds=true");
        }
        finally
        {
            device.SetRenderTargets(targets);WorldGen.noTileActions=noActions;WorldGen.gen=gen;
            for(int x=0;x<cellWidth;x++)for(int y=0;y<cellHeight;y++)cells[x*cellHeight+y].Restore(Main.tile[left-pad+x,top-pad+y]);
        }
    }
    private static void ChoiceRender(Player p)
    {
        var device=Main.instance.GraphicsDevice;var targets=device.GetRenderTargets();bool menu=Main.gameMenu;
        bool click=Main.mouseLeft,release=Main.mouseLeftRelease;
        using var atlas=new RenderTarget2D(device,Main.screenWidth,Main.screenHeight);
        try
        {
            for(int tier=0;tier<3;tier++)
            {
                p.inventory[0]=new Item(AscendantCatalog.Cache(tier));AscendantChoiceUI.Open(tier,0);Main.gameMenu=false;Main.mouseLeft=false;Main.mouseLeftRelease=false;
                device.SetRenderTarget(atlas);device.Clear(new Color(19,26,36));Main.spriteBatch.Begin();AscendantChoiceUI.Draw();Main.spriteBatch.End();
                Check(p.inventory[0].stack==1,"drawdoesnotconsumecache "+tier);
                device.SetRenderTargets(targets);SaveImage(atlas,"ascendant-choice"+tier+"-runtime.png");
            }
            Log("ASCENDANT_CHOICE_RENDER_PASS tiers=3 renderedChoices=12 openConsumes=0");
        }
        finally{device.SetRenderTargets(targets);Main.gameMenu=menu;Main.mouseLeft=click;Main.mouseLeftRelease=release;AscendantChoiceUI.Tier=AscendantChoiceUI.Slot=-1;}
    }
}
