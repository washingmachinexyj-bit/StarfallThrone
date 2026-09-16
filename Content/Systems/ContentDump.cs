using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Systems;

/// <summary>
/// Headless-only content dump used to generate documentation (wiki pages).
/// Every dump runs only when the game is launched with the matching launch parameter:
///   -starfall-equipment-dump &lt;file&gt;  equipment only, with recipes and stations
///   -starfall-sprite-dump    &lt;file&gt;  every item and tile with its texture path
///   -starfall-item-dump      &lt;file&gt;  every item with stats and a coarse category
///   -starfall-boss-dump      &lt;file&gt;  every NPC with life/damage/defense in all three modes
/// Nothing here runs during normal play and it never touches world or save data.
/// </summary>
public sealed class ContentDump : ModSystem
{
    private static Mod Self => ModContent.GetInstance<ContentDump>().Mod;

    public override void PostAddRecipes()
    {
        var p = Terraria.Program.LaunchParameters;
        string equipFile = p.TryGetValue("-starfall-equipment-dump", out string e) ? e : null;
        string spriteFile = p.TryGetValue("-starfall-sprite-dump", out string s) ? s : null;
        string itemFile = p.TryGetValue("-starfall-item-dump", out string i) ? i : null;
        string bossFile = p.TryGetValue("-starfall-boss-dump", out string b) ? b : null;
        string armorFile = p.TryGetValue("-starfall-armor-dump", out string a) ? a : null;
        if (string.IsNullOrWhiteSpace(equipFile) && string.IsNullOrWhiteSpace(spriteFile) && string.IsNullOrWhiteSpace(itemFile) && string.IsNullOrWhiteSpace(bossFile) && string.IsNullOrWhiteSpace(armorFile))
            return;

        string original = LanguageManager.Instance.ActiveCulture.Name;
        try
        {
            var zh = new Dictionary<string, string>();
            var zhTooltip = new Dictionary<string, string>();
            var en = new Dictionary<string, string>();
            var enTooltip = new Dictionary<string, string>();
            var zhTile = new Dictionary<int, string>();
            var enTile = new Dictionary<int, string>();
            var zhItem = new Dictionary<int, string>();
            var enItem = new Dictionary<int, string>();
            var zhNpc = new Dictionary<string, string>();
            var enNpc = new Dictionary<string, string>();

            var recipeTypes = new HashSet<int>();
            var ingredientTypes = new HashSet<int>();
            for (int r = 0; r < Recipe.numRecipes; r++)
            {
                Recipe recipe = Main.recipe[r];
                if (recipe?.createItem == null || recipe.createItem.type <= ItemID.None)
                    continue;
                recipeTypes.Add(recipe.createItem.type);
                foreach (Item req in recipe.requiredItem)
                {
                    if (req == null || req.type <= ItemID.None)
                        continue;
                    recipeTypes.Add(req.type);
                    ingredientTypes.Add(req.type);
                }
            }

            LanguageManager.Instance.SetLanguage(GameCulture.FromName("zh-Hans"));
            CollectItems(zh, zhTooltip);
            CollectTiles(zhTile);
            CollectNpcs(zhNpc);
            foreach (int t in recipeTypes) zhItem[t] = Lang.GetItemNameValue(t);

            LanguageManager.Instance.SetLanguage(GameCulture.FromName("en-US"));
            CollectItems(en, enTooltip);
            CollectTiles(enTile);
            CollectNpcs(enNpc);
            foreach (int t in recipeTypes) enItem[t] = Lang.GetItemNameValue(t);

            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

            if (!string.IsNullOrWhiteSpace(equipFile))
            {
                var list = Equipment(zh, zhTooltip, en, enTooltip, zhTile, enTile, zhItem, enItem);
                File.WriteAllText(equipFile, JsonSerializer.Serialize(list, options), new UTF8Encoding(false));
                Mod.Logger.Info($"EQUIPMENT_DUMP_WROTE items={list.Count} path={equipFile}");
            }

            if (!string.IsNullOrWhiteSpace(spriteFile))
            {
                var itemsOut = new List<Dictionary<string, object>>();
                foreach (ModItem mi in ModContent.GetContent<ModItem>())
                {
                    if (mi.Mod != Self) continue;
                    itemsOut.Add(new Dictionary<string, object>
                    {
                        ["internal"] = mi.Name,
                        ["nameZh"] = Pick(zh, mi.Name),
                        ["nameEn"] = Pick(en, mi.Name),
                        ["sprite"] = SpritePath(mi.Texture)
                    });
                }
                var tilesOut = new List<Dictionary<string, object>>();
                foreach (ModTile mt in ModContent.GetContent<ModTile>())
                {
                    if (mt.Mod != Self) continue;
                    tilesOut.Add(new Dictionary<string, object>
                    {
                        ["internal"] = mt.Name,
                        ["nameZh"] = Pick(zhTile, mt.Type),
                        ["nameEn"] = Pick(enTile, mt.Type),
                        ["sprite"] = SpritePath(mt.Texture)
                    });
                }
                var payload = new Dictionary<string, object> { ["items"] = itemsOut, ["tiles"] = tilesOut };
                File.WriteAllText(spriteFile, JsonSerializer.Serialize(payload, options), new UTF8Encoding(false));
                Mod.Logger.Info($"SPRITE_DUMP_WROTE items={itemsOut.Count} tiles={tilesOut.Count} path={spriteFile}");
            }

            if (!string.IsNullOrWhiteSpace(itemFile))
            {
                var rows = new List<Dictionary<string, object>>();
                foreach (ModItem mi in ModContent.GetContent<ModItem>())
                {
                    if (mi.Mod != Self) continue;
                    Item item = mi.Item;
                    rows.Add(new Dictionary<string, object>
                    {
                        ["internal"] = mi.Name,
                        ["nameZh"] = Pick(zh, mi.Name),
                        ["nameEn"] = Pick(en, mi.Name),
                        ["tooltipZh"] = Pick(zhTooltip, mi.Name),
                        ["tooltipEn"] = Pick(enTooltip, mi.Name),
                        ["sprite"] = SpritePath(mi.Texture),
                        ["kind"] = Kind(mi, item),
                        ["ingredient"] = ingredientTypes.Contains(item.type),
                        ["equipment"] = IsEquipment(item),
                        ["damage"] = item.damage,
                        ["defense"] = item.defense,
                        ["rare"] = item.rare,
                        ["sellCopper"] = item.value / 5,
                        ["maxStack"] = item.maxStack,
                        ["material"] = item.material,
                        ["createTile"] = item.createTile,
                        ["createTileName"] = item.createTile >= 0
                            ? (item.createTile > TileID.Count ? ModContent.GetModTile(item.createTile)?.Name ?? item.createTile.ToString() : TileID.Search.GetName(item.createTile))
                            : "",
                        ["tileFrameImportant"] = item.createTile >= 0 && Main.tileFrameImportant[item.createTile],
                        ["placeStyle"] = item.placeStyle,
                        ["createWall"] = item.createWall,
                        ["ammo"] = item.ammo,
                        ["bait"] = item.bait,
                        ["makeNpc"] = item.makeNPC,
                        ["accessory"] = item.accessory,
                        ["headSlot"] = item.headSlot,
                        ["bodySlot"] = item.bodySlot,
                        ["legSlot"] = item.legSlot,
                        ["bag"] = ItemID.Sets.BossBag[item.type],
                        ["recipes"] = Recipes(item, zhTile, enTile, zhItem, enItem)
                    });
                }
                File.WriteAllText(itemFile, JsonSerializer.Serialize(rows, options), new UTF8Encoding(false));
                Mod.Logger.Info($"ITEM_DUMP_WROTE items={rows.Count} path={itemFile}");
            }

            if (!string.IsNullOrWhiteSpace(bossFile))
            {
                var rows = new List<Dictionary<string, object>>();
                int savedMode = Main.GameMode;
                try
                {
                    foreach (ModNPC mn in ModContent.GetContent<ModNPC>())
                    {
                        if (mn.Mod != Self) continue;
                        var life = new int[3];
                        var damage = new int[3];
                        var defense = new int[3];
                        string err = "";
                        bool isBoss = false;
                        for (int mode = 0; mode < 3; mode++)
                        {
                            try
                            {
                                Main.GameMode = mode;
                                NPC n = new NPC();
                                n.SetDefaults(mn.Type);
                                life[mode] = n.lifeMax;
                                damage[mode] = n.damage;
                                defense[mode] = n.defense;
                                if (mode == 0) isBoss = n.boss;
                            }
                            catch (Exception ex)
                            {
                                err = ex.GetType().Name;
                                life[mode] = damage[mode] = defense[mode] = -1;
                            }
                        }
                        Main.GameMode = savedMode;
                        var drops = new List<Dictionary<string, object>>();
                        try
                        {
                            var loot = new NPCLoot();
                            mn.ModifyNPCLoot(loot);
                            var rates = new List<DropRateInfo>();
                            var feed = new DropRateInfoChainFeed(1f);
                            var rulesField = typeof(NPCLoot).GetField("ruleEntries",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            var rules = rulesField?.GetValue(loot) as System.Collections.IEnumerable;
                            if (rules != null)
                            {
                                foreach (object ruleObj in rules)
                                {
                                    if (ruleObj is IItemDropRule rule)
                                    {
                                        try { rule.ReportDroprates(rates, feed); } catch { }
                                    }
                                }
                            }
                            foreach (DropRateInfo d in rates)
                            {
                                if (d.itemId <= ItemID.None) continue;
                                string key = "Mods.StarfallThrone.Items." + (ItemID.Search.GetName(d.itemId)) + ".DisplayName";
                                drops.Add(new Dictionary<string, object>
                                {
                                    ["type"] = d.itemId,
                                    ["mod"] = d.itemId >= ItemID.Count,
                                    ["internal"] = d.itemId >= ItemID.Count ? ItemLoader.GetItem(d.itemId)?.Name ?? "" : ItemID.Search.GetName(d.itemId),
                                    ["nameEn"] = Lang.GetItemNameValue(d.itemId),
                                    ["chance"] = Math.Round(d.dropRate, 4),
                                    ["min"] = d.stackMin,
                                    ["max"] = d.stackMax
                                });
                            }
                        }
                        catch (Exception ex) { Mod.Logger.Info("BOSS_DUMP_LOOT_SKIP " + mn.Name + " " + ex.GetType().Name); }

                        rows.Add(new Dictionary<string, object>
                        {
                            ["internal"] = mn.Name,
                            ["nameZh"] = Pick(zhNpc, mn.Name),
                            ["nameEn"] = Pick(enNpc, mn.Name),
                            ["boss"] = isBoss,
                            ["life"] = life,
                            ["damage"] = damage,
                            ["defense"] = defense,
                            ["drops"] = drops,
                            ["note"] = err
                        });
                    }
                }
                finally { Main.GameMode = savedMode; }
                File.WriteAllText(bossFile, JsonSerializer.Serialize(rows, options), new UTF8Encoding(false));
                Mod.Logger.Info($"BOSS_DUMP_WROTE npcs={rows.Count} path={bossFile}");
            }

            if (!string.IsNullOrWhiteSpace(armorFile))
            {
                var heads = new List<ModItem>();
                var bodies = new List<ModItem>();
                var legs = new List<ModItem>();
                foreach (ModItem mi in ModContent.GetContent<ModItem>())
                {
                    if (mi.Mod != Self) continue;
                    Item it = mi.Item;
                    if (it.headSlot >= 0) heads.Add(mi);
                    else if (it.bodySlot >= 0) bodies.Add(mi);
                    else if (it.legSlot >= 0) legs.Add(mi);
                }

                var player = new Player { whoAmI = 0, active = true };
                player.ResetEffects();
                var sets = new List<Dictionary<string, object>>();
                foreach (ModItem head in heads)
                {
                    Item hi = head.Item;
                    foreach (ModItem body in bodies)
                    {
                        Item bi = body.Item;
                        foreach (ModItem leg in legs)
                        {
                            Item li = leg.Item;
                            bool match;
                            try { match = head.IsArmorSet(hi, bi, li); }
                            catch { continue; }
                            if (!match) continue;

                            player.armor[0] = hi; player.armor[1] = bi; player.armor[2] = li;
                            string zhBonus, enBonus;
                            LanguageManager.Instance.SetLanguage(GameCulture.FromName("zh-Hans"));
                            player.setBonus = "";
                            try { head.UpdateArmorSet(player); } catch { }
                            zhBonus = player.setBonus ?? "";
                            LanguageManager.Instance.SetLanguage(GameCulture.FromName("en-US"));
                            player.setBonus = "";
                            try { head.UpdateArmorSet(player); } catch { }
                            enBonus = player.setBonus ?? "";
                            LanguageManager.Instance.SetLanguage(GameCulture.FromName(original));

                            sets.Add(new Dictionary<string, object>
                            {
                                ["head"] = head.Name, ["body"] = body.Name, ["legs"] = leg.Name,
                                ["headZh"] = Pick(zh, head.Name), ["bodyZh"] = Pick(zh, body.Name), ["legsZh"] = Pick(zh, leg.Name),
                                ["headEn"] = Pick(en, head.Name), ["bodyEn"] = Pick(en, body.Name), ["legsEn"] = Pick(en, leg.Name),
                                ["headDefense"] = hi.defense, ["bodyDefense"] = bi.defense, ["legsDefense"] = li.defense,
                                ["headTooltipEn"] = Pick(enTooltip, head.Name),
                                ["setBonusZh"] = zhBonus,
                                ["setBonusEn"] = enBonus
                            });
                            goto nextHead;
                        }
                    }
                    nextHead: ;
                }
                File.WriteAllText(armorFile, JsonSerializer.Serialize(sets, options), new UTF8Encoding(false));
                Mod.Logger.Info($"ARMOR_DUMP_WROTE sets={sets.Count} path={armorFile}");
            }
        }
        finally
        {
            LanguageManager.Instance.SetLanguage(GameCulture.FromName(original));
        }
    }

    private static void CollectItems(Dictionary<string, string> names, Dictionary<string, string> tooltips)
    {
        foreach (ModItem mi in ModContent.GetContent<ModItem>())
        {
            if (mi.Mod != Self) continue;
            names[mi.Name] = Text(mi, "DisplayName");
            tooltips[mi.Name] = Text(mi, "Tooltip");
        }
    }

    private static void CollectTiles(Dictionary<int, string> tiles)
    {
        foreach (ModTile mt in ModContent.GetContent<ModTile>())
        {
            if (mt.Mod != Self) continue;
            string entry = Language.GetTextValue("Mods.StarfallThrone.Tiles." + mt.Name + ".MapEntry");
            if (string.IsNullOrWhiteSpace(entry) || entry.StartsWith("Mods.", StringComparison.Ordinal))
                entry = mt.Name;
            tiles[mt.Type] = entry;
        }
    }

    private static void CollectNpcs(Dictionary<string, string> names)
    {
        foreach (ModNPC mn in ModContent.GetContent<ModNPC>())
        {
            if (mn.Mod != Self) continue;
            string value = Language.GetTextValue("Mods.StarfallThrone.NPCs." + mn.Name + ".DisplayName");
            names[mn.Name] = value.StartsWith("Mods.", StringComparison.Ordinal) ? mn.Name : value;
        }
    }

    private static string Pick(Dictionary<string, string> map, string key) => map.TryGetValue(key, out string v) ? v : "";
    private static string Pick(Dictionary<int, string> map, int key) => map.TryGetValue(key, out string v) ? v : "";

    private static string Text(ModItem mi, string key)
    {
        string value = Language.GetTextValue("Mods.StarfallThrone.Items." + mi.Name + "." + key);
        return value.StartsWith("Mods.", StringComparison.Ordinal) ? "" : value;
    }

    private static string SpritePath(string texture)
    {
        string t = texture ?? "";
        const string prefix = "StarfallThrone/";
        if (t.StartsWith(prefix, StringComparison.Ordinal))
            t = t.Substring(prefix.Length);
        return t + ".png";
    }

    private static bool IsEquipment(Item item) =>
        item.headSlot >= 0 || item.bodySlot >= 0 || item.legSlot >= 0 || item.accessory ||
        (item.damage > 0 && item.pick == 0 && item.axe == 0 && item.hammer == 0 && item.ammo == 0);

    private static string Kind(ModItem mi, Item item)
    {
        if (item.headSlot >= 0) return "armor-head";
        if (item.bodySlot >= 0) return "armor-body";
        if (item.legSlot >= 0) return "armor-legs";
        if (item.accessory) return "accessory";
        if (item.damage > 0 && item.pick == 0 && item.axe == 0 && item.hammer == 0 && item.ammo == 0)
        {
            if (item.CountsAsClass(DamageClass.Melee)) return "weapon-melee";
            if (item.CountsAsClass(DamageClass.Ranged)) return "weapon-ranged";
            if (item.CountsAsClass(DamageClass.Magic)) return "weapon-magic";
            if (item.CountsAsClass(DamageClass.Summon)) return "weapon-summon";
            return "weapon-other";
        }

        string name = mi.Name;
        if (ItemID.Sets.BossBag[item.type] || name.Contains("Bag")) return "bag";
        if (name.Contains("Relic")) return "relic";
        if (name.Contains("Trophy")) return "trophy";
        if (name.Contains("Mask")) return "mask";
        if (name.Contains("Pet")) return "pet";
        if (name.Contains("Halo")) return "halo";
        if (name.Contains("Monument")) return "monument";
        if (name.Contains("Summon") || name.Contains("Sigil") || name.Contains("Beacon")) return "summon";
        if (item.pick > 0 || item.axe > 0 || item.hammer > 0) return "tool";
        if (item.ammo > 0) return "ammo";
        if (item.createWall >= 0) return "wall";
        if (item.createTile >= 0) return "tile";
        if (item.makeNPC > 0) return "critter";
        if (item.bait > 0) return "bait";
        if (item.material) return "material";
        return "misc";
    }

    private static List<Dictionary<string, object>> Equipment(
        Dictionary<string, string> zh, Dictionary<string, string> zhTooltip,
        Dictionary<string, string> en, Dictionary<string, string> enTooltip,
        Dictionary<int, string> zhTile, Dictionary<int, string> enTile,
        Dictionary<int, string> zhItem, Dictionary<int, string> enItem)
    {
        var list = new List<Dictionary<string, object>>();
        foreach (ModItem mi in ModContent.GetContent<ModItem>())
        {
            Item item = mi.Item;
            if (mi.Mod != Self || !IsEquipment(item))
                continue;
            list.Add(new Dictionary<string, object>
            {
                ["internal"] = mi.Name,
                ["nameZh"] = Pick(zh, mi.Name),
                ["nameEn"] = Pick(en, mi.Name),
                ["tooltipZh"] = Pick(zhTooltip, mi.Name),
                ["tooltipEn"] = Pick(enTooltip, mi.Name),
                ["texture"] = mi.Texture,
                ["sprite"] = SpritePath(mi.Texture),
                ["category"] = Kind(mi, item),
                ["damage"] = item.damage,
                ["damageType"] = item.CountsAsClass(DamageClass.Melee) ? "Melee"
                    : item.CountsAsClass(DamageClass.Ranged) ? "Ranged"
                    : item.CountsAsClass(DamageClass.Magic) ? "Magic"
                    : item.CountsAsClass(DamageClass.Summon) ? "Summon" : "Generic",
                ["useTime"] = item.useTime,
                ["knockback"] = Math.Round(item.knockBack, 2),
                ["mana"] = item.mana,
                ["defense"] = item.defense,
                ["autoReuse"] = item.autoReuse,
                ["rare"] = item.rare,
                ["sellCopper"] = item.value / 5,
                ["maxStack"] = item.maxStack,
                ["recipes"] = Recipes(item, zhTile, enTile, zhItem, enItem)
            });
        }
        return list;
    }

    private static List<Dictionary<string, object>> Recipes(
        Item target,
        Dictionary<int, string> zhTile, Dictionary<int, string> enTile,
        Dictionary<int, string> zhItem, Dictionary<int, string> enItem)
    {
        var result = new List<Dictionary<string, object>>();
        for (int i = 0; i < Recipe.numRecipes; i++)
        {
            Recipe recipe = Main.recipe[i];
            if (recipe?.createItem == null || recipe.createItem.type != target.type)
                continue;

            var ingredients = new List<Dictionary<string, object>>();
            foreach (Item required in recipe.requiredItem)
            {
                if (required == null || required.type <= ItemID.None || required.stack <= 0)
                    continue;
                bool isMod = required.ModItem != null && required.ModItem.Mod == Self;
                ingredients.Add(new Dictionary<string, object>
                {
                    ["internal"] = required.ModItem?.Name ?? ItemID.Search.GetName(required.type),
                    ["mod"] = isMod,
                    ["nameZh"] = Pick(zhItem, required.type),
                    ["nameEn"] = Pick(enItem, required.type),
                    ["stack"] = required.stack
                });
            }

            var stations = new List<Dictionary<string, object>>();
            foreach (int tile in recipe.requiredTile)
            {
                bool vanilla = tile <= TileID.Count;
                stations.Add(new Dictionary<string, object>
                {
                    ["internal"] = vanilla ? TileID.Search.GetName(tile) : ModContent.GetModTile(tile)?.Name ?? tile.ToString(),
                    ["vanilla"] = vanilla,
                    ["nameZh"] = vanilla ? "" : Pick(zhTile, tile),
                    ["nameEn"] = vanilla ? "" : Pick(enTile, tile)
                });
            }

            result.Add(new Dictionary<string, object>
            {
                ["ingredients"] = ingredients,
                ["stations"] = stations,
                ["stack"] = recipe.createItem.stack
            });
        }
        return result;
    }
}
