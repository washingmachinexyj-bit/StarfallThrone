using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Divine.Equipment;

public static class DivineEquipmentData
{
    public static readonly int[,] Defense = { {20,17,14,12}, {48,38,30,28}, {88,72,62,54} };
    public static readonly int[] Chest = {7,17,32}, Legs = {4,10,20};
    public static DamageClass Class(int style) => style switch { 0 => DamageClass.Melee, 1 => DamageClass.Ranged, 2 => DamageClass.Magic, _ => DamageClass.Summon };
    public static int PieceDefense(int tier, int part) => part == 4 ? Chest[tier] : part == 5 ? Legs[tier] : Defense[tier,part] - Chest[tier] - Legs[tier];
    public static int EquippedTier(Player p)
    {
        if (p.armor[0].ModItem is not DivineArmor a || a.Part >= 4) return -1;
        return p.armor[1].type == DivineCatalog.Armor(a.Tier * 6 + 4) && p.armor[2].type == DivineCatalog.Armor(a.Tier * 6 + 5) ? a.Tier : -1;
    }
    public static Color Color(int tier) => tier switch { 0 => new Color(160,245,184), 1 => new Color(255,160,65), _ => new Color(255,233,175) };
}

public abstract class DivineArmor : ModItem
{
    public abstract int Index { get; }
    public int Tier => Index / 6;
    public int Part => Index % 6;
    public override string Texture => DivineCatalog.Root + "Armor" + Index;
    public override void SetDefaults()
    {
        Item.width = Item.height = 32; Item.defense = DivineEquipmentData.PieceDefense(Tier, Part);
        Item.rare = Tier == 0 ? ItemRarityID.Orange : Tier == 1 ? ItemRarityID.Pink : ItemRarityID.Red;
        Item.value = Item.sellPrice(gold: 1 + Tier * 4);
    }
    public override void UpdateEquip(Player p)
    {
        if (Part == 4) { p.GetDamage(DamageClass.Generic) += .02f * (Tier + 1); return; }
        if (Part == 5) { p.moveSpeed += .06f + Tier * .03f; return; }
        p.GetDamage(DivineEquipmentData.Class(Part)) += new[] { .06f, .08f, .12f }[Tier];
        switch (Part)
        {
            case 0: p.GetAttackSpeed(DamageClass.Melee) += .05f + Tier * .025f; break;
            case 1: p.GetCritChance(DamageClass.Ranged) += 4 + Tier * 2; break;
            case 2: p.statManaMax2 += 20 + Tier * 40; p.manaCost *= 1 - (.05f + Tier * .05f); break;
            case 3: p.maxMinions += Tier + 1; break;
        }
    }
    public override bool IsArmorSet(Item head, Item body, Item legs) => Part < 4 && body.type == DivineCatalog.Armor(Tier * 6 + 4) && legs.type == DivineCatalog.Armor(Tier * 6 + 5);
    public override void UpdateArmorSet(Player p)
    {
        p.GetModPlayer<DivineEquipmentPlayer>().SetTier = Tier;
        p.setBonus = Language.GetTextValue("Mods.StarfallThrone.DivineEquipment.Set" + Tier);
    }
    public override void AddRecipes() => CreateRecipe().AddIngredient(DivineCatalog.Material(Tier), Part == 4 ? 24 : Part == 5 ? 16 : 14).AddTile(DivineCatalog.CraftStation(Tier)).Register();
}

[AutoloadEquip(EquipType.Head)] public sealed class DivineArmor0 : DivineArmor { public override int Index => 0; }
[AutoloadEquip(EquipType.Head)] public sealed class DivineArmor1 : DivineArmor { public override int Index => 1; }
[AutoloadEquip(EquipType.Head)] public sealed class DivineArmor2 : DivineArmor { public override int Index => 2; }
[AutoloadEquip(EquipType.Head)] public sealed class DivineArmor3 : DivineArmor { public override int Index => 3; }
[AutoloadEquip(EquipType.Body)] public sealed class DivineArmor4 : DivineArmor { public override int Index => 4; }
[AutoloadEquip(EquipType.Legs)] public sealed class DivineArmor5 : DivineArmor { public override int Index => 5; }
[AutoloadEquip(EquipType.Head)] public sealed class DivineArmor6 : DivineArmor { public override int Index => 6; }
[AutoloadEquip(EquipType.Head)] public sealed class DivineArmor7 : DivineArmor { public override int Index => 7; }
[AutoloadEquip(EquipType.Head)] public sealed class DivineArmor8 : DivineArmor { public override int Index => 8; }
[AutoloadEquip(EquipType.Head)] public sealed class DivineArmor9 : DivineArmor { public override int Index => 9; }
[AutoloadEquip(EquipType.Body)] public sealed class DivineArmor10 : DivineArmor { public override int Index => 10; }
[AutoloadEquip(EquipType.Legs)] public sealed class DivineArmor11 : DivineArmor { public override int Index => 11; }
[AutoloadEquip(EquipType.Head)] public sealed class DivineArmor12 : DivineArmor { public override int Index => 12; }
[AutoloadEquip(EquipType.Head)] public sealed class DivineArmor13 : DivineArmor { public override int Index => 13; }
[AutoloadEquip(EquipType.Head)] public sealed class DivineArmor14 : DivineArmor { public override int Index => 14; }
[AutoloadEquip(EquipType.Head)] public sealed class DivineArmor15 : DivineArmor { public override int Index => 15; }
[AutoloadEquip(EquipType.Body)] public sealed class DivineArmor16 : DivineArmor { public override int Index => 16; }
[AutoloadEquip(EquipType.Legs)] public sealed class DivineArmor17 : DivineArmor { public override int Index => 17; }

public abstract class DivineExpert : ModItem
{
    public abstract int Index { get; }
    public override string Texture => DivineCatalog.Root + "Expert" + Index;
    public override void SetDefaults()
    { Item.width = Item.height = 32; Item.accessory = true; Item.expert = true; Item.rare = ItemRarityID.Expert; Item.value = Item.sellPrice(gold: 3 + Index * 5); }
    public override void UpdateAccessory(Player p, bool hideVisual)
    {
        p.GetModPlayer<DivineEquipmentPlayer>().Expert[Index] = true;
        if (Index < 2) p.statLifeMax2 += Index == 0 ? 20 : 40;
        else p.GetDamage(DamageClass.Generic) += .08f;
    }
}
public sealed class DivineExpert0 : DivineExpert { public override int Index => 0; }
public sealed class DivineExpert1 : DivineExpert { public override int Index => 1; }
public sealed class DivineExpert2 : DivineExpert { public override int Index => 2; }
