using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Voyage.Equipment;

public static class VoyageEquipmentData
{
    public static readonly int[,] Defense = { {120,108,98,94}, {140,126,114,110}, {162,146,132,126}, {188,170,154,146}, {216,196,178,168}, {244,222,202,190} };
    public static readonly int[] Life = {320,380,450,530,620,720};
    public static readonly int[] Damage = {20,26,32,38,44,50};
    public static readonly int[] Reduction = {8,9,10,11,12,14};
    public static readonly int[] ClassDamage = {20,22,25,28,32,36};
    public static readonly int[] Mana = {80,100,120,150,180,220};
    public static readonly int[] ManaSaving = {8,10,12,14,16,18};
    public static readonly int[] AmmoSaving = {10,12,14,16,18,20};
    public static readonly int[] Crit = {6,7,8,9,10,12};
    public static readonly int[] Minions = {2,2,3,3,4,4};
    public static readonly int[] Sentries = {0,1,1,1,1,2};
    public static int Armor(int i) => VoyageCatalog.Item("VoyageArmor" + i);
    public static int Accessory(int i) => VoyageCatalog.Item("VoyageAccessory" + i);
    public static DamageClass Class(int i) => i switch { 0 => DamageClass.Melee, 1 => DamageClass.Ranged, 2 => DamageClass.Magic, _ => DamageClass.Summon };
    public static int PieceDefense(int tier, int part) => part == 4 ? Defense[tier,0] * 4 / 10 : part == 5 ? Defense[tier,0] * 3 / 10 : Defense[tier,part] - Defense[tier,0] * 4 / 10 - Defense[tier,0] * 3 / 10;
    public static int PieceLife(int tier, int part) => part == 4 ? Life[tier] * 4 / 10 : part == 5 ? Life[tier] * 35 / 100 : Life[tier] - Life[tier] * 4 / 10 - Life[tier] * 35 / 100;
    public static float Fraction(int part) => part == 4 ? .45f : part == 5 ? .30f : .25f;
    public static int HeadStyle(Player player) => player.armor[0].ModItem is VoyageArmor a && a.Part < 4 ? a.Part : -1;
    public static int EquippedTier(Player player)
    {
        if (player.armor[0].ModItem is not VoyageArmor head || head.Part >= 4) return -1;
        return player.armor[1].type == Armor(head.Tier * 6 + 4) && player.armor[2].type == Armor(head.Tier * 6 + 5) ? head.Tier : -1;
    }
}

public abstract class VoyageArmor : ModItem
{
    public abstract int Index { get; }
    public int Tier => Index / 6;
    public int Part => Index % 6;
    public override string Texture => VoyageCatalog.Root + "Armor" + Index;
    public override void SetDefaults()
    {
        Item.width = Item.height = 36;
        Item.defense = VoyageEquipmentData.PieceDefense(Tier, Part);
        Item.rare = ItemRarityID.Red;
        Item.value = Item.buyPrice(gold: 20 + Tier * 8);
    }
    public override void UpdateEquip(Player player)
    {
        player.statLifeMax2 += VoyageEquipmentData.PieceLife(Tier, Part);
        player.GetDamage(DamageClass.Generic) += VoyageEquipmentData.Damage[Tier] * .01f * VoyageEquipmentData.Fraction(Part);
        player.endurance += VoyageEquipmentData.Reduction[Tier] * .01f * VoyageEquipmentData.Fraction(Part);
        if (Part >= 4) return;
        player.GetDamage(VoyageEquipmentData.Class(Part)) += VoyageEquipmentData.ClassDamage[Tier] * .01f;
        switch (Part)
        {
            case 0: player.GetAttackSpeed(DamageClass.Melee) += .08f + Tier * .02f; break;
            case 1:
                player.GetCritChance(DamageClass.Ranged) += VoyageEquipmentData.Crit[Tier];
                player.GetModPlayer<VoyageEquipmentPlayer>().AmmoSaving = Math.Max(player.GetModPlayer<VoyageEquipmentPlayer>().AmmoSaving, VoyageEquipmentData.AmmoSaving[Tier] * .01f);
                break;
            case 2:
                player.statManaMax2 += VoyageEquipmentData.Mana[Tier];
                player.manaCost *= 1 - VoyageEquipmentData.ManaSaving[Tier] * .01f;
                break;
            case 3:
                player.maxMinions += VoyageEquipmentData.Minions[Tier];
                player.maxTurrets += VoyageEquipmentData.Sentries[Tier];
                break;
        }
    }
    public override bool IsArmorSet(Item head, Item body, Item legs) => Part < 4 && body.type == VoyageEquipmentData.Armor(Tier * 6 + 4) && legs.type == VoyageEquipmentData.Armor(Tier * 6 + 5);
    public override void UpdateArmorSet(Player player)
    {
        player.noKnockback = true;
        var state = player.GetModPlayer<VoyageEquipmentPlayer>();
        state.SetTier = Tier; state.SetStyle = Part;
        player.setBonus = Language.GetTextValue("Mods.StarfallThrone.VoyageEquipment.Set" + Tier);
    }
    public override void AddRecipes()
    {
        int count = Part == 4 ? 12 : Part == 5 ? 10 : 8;
        Recipe recipe = CreateRecipe().AddTile(VoyageCatalog.Workbench);
        if (Tier == 0)
        {
            recipe.AddIngredient(VoyageCatalog.Alloy, count);
            if (Part == 4) recipe.AddIngredient(VoyageCatalog.Item("MoonGodCore"), 2);
        }
        else
        {
            recipe.AddIngredient(VoyageCatalog.Plate(Tier == 5 ? 4 : Tier - 1), count);
            if (Tier == 5) recipe.AddIngredient(VoyageCatalog.Core(16), Part == 4 ? 2 : 1);
            else
            {
                int start = (Tier - 1) * 4;
                if (Part == 4) recipe.AddIngredient(VoyageCatalog.Core(start)).AddIngredient(VoyageCatalog.Core(start + 1));
                else recipe.AddIngredient(VoyageCatalog.Core(start + (Part == 5 ? 2 : 3)));
            }
        }
        recipe.Register();
        if (Part < 4)
            for (int head = 0; head < 4; head++)
                if (head != Part) CreateRecipe().AddIngredient(VoyageEquipmentData.Armor(Tier * 6 + head)).AddIngredient(VoyageCatalog.Alloy, 2).AddTile(VoyageCatalog.Workbench).Register();
    }
}

public abstract class VoyageExpertAccessory : ModItem
{
    public abstract int Index { get; }
    public override string Texture => VoyageCatalog.Root + "Expert" + Index;
    public override void SetStaticDefaults()
    {
        if (Index == 7) ArmorIDs.Wing.Sets.Stats[Item.wingSlot] = new WingStats(330, 12f, 3f, true, 12f, 3f);
    }
    public override void SetDefaults()
    {
        Item.width = Item.height = 36; Item.accessory = true;
        Item.rare = ItemRarityID.Expert; Item.expert = true;
        Item.value = Item.buyPrice(gold: 30 + Index * 3);
    }
    public override void UpdateAccessory(Player p, bool hideVisual)
    {
        var s = p.GetModPlayer<VoyageEquipmentPlayer>(); s.Expert[Index] = true;
        switch (Index)
        {
            case 0: p.statLifeMax2 += 120; break;
            case 2: p.moveSpeed += .18f; s.FlightBonus += .12f; break;
            case 3: p.GetDamage(DamageClass.Generic) += .15f; break;
            case 4: p.maxMinions += 2; p.GetDamage(DamageClass.Summon) += .20f; break;
            case 5: p.statDefense += 20; p.noKnockback = true; break;
            case 7: s.Hover = true; break;
            case 8: p.GetDamage(DamageClass.Ranged) += .18f; break;
            case 9: p.maxTurrets++; break;
            case 10: p.GetDamage(DamageClass.Melee) += .18f; p.GetAttackSpeed(DamageClass.Melee) += .08f; break;
            case 11: p.statLifeMax2 += 200; break;
            case 12: p.statDefense += 28; p.endurance += .08f; break;
            case 13: p.moveSpeed += .25f; s.FlightBonus += .20f; break;
            case 14: p.GetDamage(DamageClass.Magic) += .22f; p.manaCost *= .88f; break;
            case 15: p.maxMinions++; p.maxTurrets++; p.GetDamage(DamageClass.Summon) += .20f; break;
            case 16: p.statLifeMax2 += 300; p.endurance += .10f; break;
        }
    }
    public override void VerticalWingSpeeds(Player p, ref float falling, ref float rising, ref float maxAscendMultiplier, ref float maxAscentMultiplier, ref float constantAscend)
    {
        if (Index != 7) return;
        falling = .85f; rising = .18f; maxAscendMultiplier = 1f; maxAscentMultiplier = 3.2f; constantAscend = .15f;
    }
}

public abstract class VoyageOrdinaryAccessory : ModItem
{
    public abstract int Index { get; }
    public override string Texture => VoyageCatalog.Root + "Accessory" + Index;
    public override void SetStaticDefaults()
    {
        if (Index is 7 or 10) ArmorIDs.Wing.Sets.Stats[Item.wingSlot] = new WingStats(Index == 7 ? 270 : 360, Index == 7 ? 11.5f : 14f, 3f, Index == 10, 14f, 3f);
    }
    public override void SetDefaults()
    {
        Item.width = Item.height = 36; Item.accessory = true;
        Item.rare = ItemRarityID.Red; Item.value = Item.buyPrice(gold: 25 + Index * 4);
    }
    public override bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player)
    {
        if (equippedItem.ModItem is not VoyageOrdinaryAccessory a || incomingItem.ModItem is not VoyageOrdinaryAccessory b) return true;
        int low = Math.Min(a.Index, b.Index), high = Math.Max(a.Index, b.Index);
        if (low == high) return false;
        return !(high == 8 && low is >= 1 and <= 4 || high == 9 && low is 5 or 6 || high == 10 && low is 0 or 7);
    }
    public override void UpdateAccessory(Player p, bool hideVisual)
    {
        var s = p.GetModPlayer<VoyageEquipmentPlayer>(); s.Ordinary[Index] = true;
        switch (Index)
        {
            case 0: p.moveSpeed += .18f; s.FlightBonus += .12f; break;
            case 1: p.GetDamage(DamageClass.Ranged) += .16f; s.AmmoSaving = Math.Max(s.AmmoSaving, .15f); break;
            case 2: p.GetDamage(DamageClass.Melee) += .16f; p.GetAttackSpeed(DamageClass.Melee) += .08f; break;
            case 3: p.GetDamage(DamageClass.Magic) += .16f; p.statManaMax2 += 100; break;
            case 4: p.maxMinions++; p.GetDamage(DamageClass.Summon) += .14f; break;
            case 5: p.statLifeMax2 += 160; p.lifeRegen += 4; break;
            case 6: p.statDefense += 18; p.endurance += .05f; break;
            case 8:
                int style = VoyageEquipmentData.HeadStyle(p);
                if (style < 0) break;
                p.GetDamage(VoyageEquipmentData.Class(style)) += .20f;
                switch (style)
                {
                    case 0: p.GetAttackSpeed(DamageClass.Melee) += .08f; break;
                    case 1: s.AmmoSaving = Math.Max(s.AmmoSaving, .15f); break;
                    case 2: p.statManaMax2 += 100; break;
                    case 3: p.maxMinions++; break;
                }
                break;
            case 9: p.statLifeMax2 += 200; p.statDefense += 20; p.endurance += .06f; p.lifeRegen += 4; break;
            case 10: s.Hover = true; p.moveSpeed += .18f; break;
        }
    }
    public override void VerticalWingSpeeds(Player p, ref float falling, ref float rising, ref float maxAscendMultiplier, ref float maxAscentMultiplier, ref float constantAscend)
    {
        if (Index is not (7 or 10)) return;
        falling = .85f; rising = .18f; maxAscendMultiplier = 1f; maxAscentMultiplier = 3.2f; constantAscend = .15f;
    }
    public override void AddRecipes()
    {
        Recipe r = CreateRecipe().AddTile(VoyageCatalog.Workbench);
        void M(int i, int n) => r.AddIngredient(VoyageCatalog.Material(i), n);
        switch (Index)
        {
            case 0: M(0,12); M(1,12); r.AddIngredient(VoyageCatalog.Alloy,8); break;
            case 1: M(1,18); M(3,12); r.AddIngredient(VoyageCatalog.Alloy,8); break;
            case 2: M(2,18); M(5,12); r.AddIngredient(VoyageCatalog.Alloy,8); break;
            case 3: M(3,18); M(6,12); r.AddIngredient(VoyageCatalog.Alloy,8); break;
            case 4: M(4,18); M(7,12); r.AddIngredient(VoyageCatalog.Alloy,8); break;
            case 5: M(0,12); M(11,18); r.AddIngredient(VoyageCatalog.Alloy,10); break;
            case 6: M(7,18); M(12,12); r.AddIngredient(VoyageCatalog.Alloy,10); break;
            case 7: M(5,12); M(6,12); M(7,12); r.AddIngredient(VoyageCatalog.Alloy,16); break;
            case 8:
                for (int i = 1; i <= 4; i++) r.AddIngredient(VoyageEquipmentData.Accessory(i));
                M(15,18); r.AddIngredient(VoyageCatalog.Core(15)); break;
            case 9:
                r.AddIngredient(VoyageEquipmentData.Accessory(5)).AddIngredient(VoyageEquipmentData.Accessory(6));
                M(11,18); r.AddIngredient(VoyageCatalog.Core(12)); break;
            case 10:
                r.AddIngredient(VoyageEquipmentData.Accessory(7)).AddIngredient(VoyageEquipmentData.Accessory(0));
                M(14,18); M(15,18); r.AddIngredient(VoyageCatalog.Core(15)); break;
        }
        r.Register();
    }
}
