using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using global::StarfallThrone.Content.Divine;
using global::StarfallThrone.Content.Divine.Equipment;

namespace StarfallThrone.Content.Ascendant.Equipment;

public static class AscendantEquipmentData
{
    public static readonly int[,] Defense = { {28,23,20,18}, {60,48,40,36}, {104,86,76,68} };
    public static readonly int[] Chest = {10,22,39}, Legs = {5,12,23};
    public static DamageClass Class(int style) => style switch { 0 => DamageClass.Melee, 1 => DamageClass.Ranged, 2 => DamageClass.Magic, _ => DamageClass.Summon };
    public static Color Color(int tier) => tier switch { 0 => new Color(147,255,181), 1 => new Color(255,184,87), _ => new Color(236,216,255) };
    public static int PieceDefense(int tier, int part) => part == 4 ? Chest[tier] : part == 5 ? Legs[tier] : Defense[tier,part] - Chest[tier] - Legs[tier];
    public static int EquippedTier(Player p)
    {
        if (p.armor[0].ModItem is not AscendantArmor a || a.Part >= 4) return -1;
        return p.armor[1].type == AscendantCatalog.Armor(a.Tier*6+4) && p.armor[2].type == AscendantCatalog.Armor(a.Tier*6+5) ? a.Tier : -1;
    }
    public static bool HasOriginalExpert(Player p, int tier)
    {
        // Gameplay slots only; vanity slots must not disable an equipped accessory.
        for (int slot = 3; slot < 10; slot++)
            if ((slot < 8 || slot == 8 && Main.expertMode && p.extraAccessory || slot == 9 && Main.masterMode) && p.armor[slot].ModItem is DivineExpert d && d.Index == tier) return true;
        return false;
    }
}

public abstract class AscendantArmor : ModItem
{
    public abstract int Index { get; }
    public int Tier => Index/6;
    public int Part => Index%6;
    public override string Texture => AscendantCatalog.Root + "Armor" + Index;
    public override void SetDefaults()
    {
        Item.width = Item.height = 32; Item.defense = AscendantEquipmentData.PieceDefense(Tier,Part);
        Item.rare = Tier == 0 ? ItemRarityID.Orange : Tier == 1 ? ItemRarityID.Pink : ItemRarityID.Red;
        Item.value = Item.sellPrice(gold: 3 + Tier*7);
    }
    public override void UpdateEquip(Player p)
    {
        if (Part == 4) { p.GetDamage(DamageClass.Generic) += .04f + Tier*.02f; return; }
        if (Part == 5) { p.moveSpeed += .09f + Tier*.03f; return; }
        p.GetDamage(AscendantEquipmentData.Class(Part)) += .10f + Tier*.04f;
        switch (Part)
        {
            case 0: p.GetAttackSpeed(DamageClass.Melee) += .08f + Tier*.02f; break;
            case 1: p.GetCritChance(DamageClass.Ranged) += 6 + Tier*2; break;
            case 2: p.statManaMax2 += 40 + Tier*40; p.manaCost *= .92f - Tier*.05f; break;
            case 3: p.maxMinions += Tier+2; break;
        }
    }
    public override bool IsArmorSet(Item head, Item body, Item legs) => Part < 4 && body.type == AscendantCatalog.Armor(Tier*6+4) && legs.type == AscendantCatalog.Armor(Tier*6+5);
    public override void UpdateArmorSet(Player p)
    {
        p.GetModPlayer<AscendantEquipmentPlayer>().SetTier = Tier;
        p.setBonus = Language.GetTextValue("Mods.StarfallThrone.AscendantEquipment.Set" + Tier);
    }
    public override void AddRecipes() => CreateRecipe().AddIngredient(DivineCatalog.Armor(Index))
        .AddIngredient(AscendantCatalog.Material(Tier), Part == 4 ? 10 : Part == 5 ? 8 : 6).AddTile(AscendantCatalog.CraftStation(Tier)).Register();
}
[AutoloadEquip(EquipType.Head)] public sealed class AscendantArmor0 : AscendantArmor { public override int Index => 0; }
[AutoloadEquip(EquipType.Head)] public sealed class AscendantArmor1 : AscendantArmor { public override int Index => 1; }
[AutoloadEquip(EquipType.Head)] public sealed class AscendantArmor2 : AscendantArmor { public override int Index => 2; }
[AutoloadEquip(EquipType.Head)] public sealed class AscendantArmor3 : AscendantArmor { public override int Index => 3; }
[AutoloadEquip(EquipType.Body)] public sealed class AscendantArmor4 : AscendantArmor { public override int Index => 4; }
[AutoloadEquip(EquipType.Legs)] public sealed class AscendantArmor5 : AscendantArmor { public override int Index => 5; }
[AutoloadEquip(EquipType.Head)] public sealed class AscendantArmor6 : AscendantArmor { public override int Index => 6; }
[AutoloadEquip(EquipType.Head)] public sealed class AscendantArmor7 : AscendantArmor { public override int Index => 7; }
[AutoloadEquip(EquipType.Head)] public sealed class AscendantArmor8 : AscendantArmor { public override int Index => 8; }
[AutoloadEquip(EquipType.Head)] public sealed class AscendantArmor9 : AscendantArmor { public override int Index => 9; }
[AutoloadEquip(EquipType.Body)] public sealed class AscendantArmor10 : AscendantArmor { public override int Index => 10; }
[AutoloadEquip(EquipType.Legs)] public sealed class AscendantArmor11 : AscendantArmor { public override int Index => 11; }
[AutoloadEquip(EquipType.Head)] public sealed class AscendantArmor12 : AscendantArmor { public override int Index => 12; }
[AutoloadEquip(EquipType.Head)] public sealed class AscendantArmor13 : AscendantArmor { public override int Index => 13; }
[AutoloadEquip(EquipType.Head)] public sealed class AscendantArmor14 : AscendantArmor { public override int Index => 14; }
[AutoloadEquip(EquipType.Head)] public sealed class AscendantArmor15 : AscendantArmor { public override int Index => 15; }
[AutoloadEquip(EquipType.Body)] public sealed class AscendantArmor16 : AscendantArmor { public override int Index => 16; }
[AutoloadEquip(EquipType.Legs)] public sealed class AscendantArmor17 : AscendantArmor { public override int Index => 17; }

public abstract class AscendantExpert : ModItem
{
    public abstract int Index { get; }
    public override string Texture => AscendantCatalog.Root + "Expert" + Index;
    public override void SetDefaults()
    { Item.width = Item.height = 32; Item.accessory = true; Item.expert = true; Item.rare = ItemRarityID.Expert; Item.value = Item.sellPrice(gold: 5 + Index*8); }
    public override bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player)
    {
        bool Family(Item item) => item.ModItem is DivineExpert d && d.Index == Index || item.ModItem is AscendantExpert a && a.Index == Index;
        return !(Family(equippedItem) && Family(incomingItem));
    }
    public override void UpdateAccessory(Player p, bool hideVisual)
    {
        // Handles edited saves or third-party equip operations that bypass the mutual-exclusion hook.
        if (AscendantEquipmentData.HasOriginalExpert(p,Index)) return;
        p.GetModPlayer<AscendantEquipmentPlayer>().Expert[Index] = true;
        if (Index < 2) p.statLifeMax2 += Index == 0 ? 35 : 60;
        else p.GetDamage(DamageClass.Generic) += .12f;
    }
}
public sealed class AscendantExpert0 : AscendantExpert { public override int Index => 0; }
public sealed class AscendantExpert1 : AscendantExpert { public override int Index => 1; }
public sealed class AscendantExpert2 : AscendantExpert { public override int Index => 2; }
