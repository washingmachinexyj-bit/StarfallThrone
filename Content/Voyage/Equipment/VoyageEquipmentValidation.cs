using System;
using System.Linq;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace StarfallThrone.Content.Voyage.Equipment;

/// <summary>Optional isolated-client test entry point. Main's smoke runner calls Validate after recipes are loaded.</summary>
public static class VoyageEquipmentValidation
{
    public static void Validate()
    {
        for (int tier = 0; tier < 6; tier++)
            for (int style = 0; style < 4; style++)
            {
                Player player = new Player();
                var state = player.GetModPlayer<VoyageEquipmentPlayer>();
                int lifeBefore = player.statLifeMax2, minionsBefore = player.maxMinions, sentriesBefore = player.maxTurrets;
                float genericBefore = player.GetDamage(DamageClass.Generic).Additive, reductionBefore = player.endurance;
                player.statDefense = new Player.DefenseStat();
                int[] indices = {tier * 6 + style, tier * 6 + 4, tier * 6 + 5};
                for (int slot = 0; slot < 3; slot++)
                {
                    int type = VoyageEquipmentData.Armor(indices[slot]);
                    Item item = new Item(type);
                    Require(item.ModItem is VoyageArmor, "Armor registration " + indices[slot]);
                    Require(slot == 0 ? item.headSlot >= 0 : slot == 1 ? item.bodySlot >= 0 : item.legSlot >= 0, "Equip texture slot " + indices[slot]);
                    player.armor[slot] = item;
                    player.statDefense += item.defense;
                    item.ModItem.UpdateEquip(player);
                }
                player.armor[0].ModItem.UpdateArmorSet(player);
                Require(player.statDefense == VoyageEquipmentData.Defense[tier,style], "Defense total " + tier + "/" + style);
                Require(player.statLifeMax2 - lifeBefore == VoyageEquipmentData.Life[tier], "Life total " + tier);
                Require(Math.Abs(player.GetDamage(DamageClass.Generic).Additive - genericBefore - VoyageEquipmentData.Damage[tier] * .01f) < .0001f, "Damage split " + tier);
                Require(Math.Abs(player.endurance - reductionBefore - VoyageEquipmentData.Reduction[tier] * .01f) < .0001f, "Reduction split " + tier);
                Require(state.SetTier == tier && state.SetStyle == style && player.noKnockback, "Set ability " + tier);
                if (style == 3)
                    Require(player.maxMinions - minionsBefore == VoyageEquipmentData.Minions[tier] && player.maxTurrets - sentriesBefore == VoyageEquipmentData.Sentries[tier], "Summon slots " + tier);
            }

        for (int i = 0; i < 17; i++)
        {
            Item item = new Item(VoyageCatalog.Expert(i));
            Require(item.expert && item.accessory && item.ModItem is VoyageExpertAccessory, "Expert registration " + i);
            Require(item.ModItem.Tooltip.Value.Length > 20, "Expert tooltip " + i);
        }
        for (int i = 0; i < 11; i++)
        {
            Item item = new Item(VoyageEquipmentData.Accessory(i));
            Require(item.accessory && !item.expert && item.ModItem is VoyageOrdinaryAccessory, "Ordinary registration " + i);
            Require(item.ModItem.Tooltip.Value.Length > 20, "Ordinary tooltip " + i);
        }
        foreach (int index in new[] {7,10})
        {
            Item item = new Item(VoyageEquipmentData.Accessory(index));
            Require(item.wingSlot > 0 && ArmorIDs.Wing.Sets.Stats[item.wingSlot].FlyTime == (index == 7 ? 270 : 360), "Ordinary wing flight " + index);
        }
        Item wing = new Item(VoyageCatalog.Expert(7));
        Require(wing.wingSlot > 0 && ArmorIDs.Wing.Sets.Stats[wing.wingSlot].FlyTime == 330, "Expert wing flight");

        Player p = new Player();
        var data = p.GetModPlayer<VoyageEquipmentPlayer>();
        data.Cooldowns[0] = 480; data.Cooldowns[7] = 7200;
        data.ResetEffects();
        Require(data.Cooldowns[0] == 480 && data.Cooldowns[7] == 7200, "Equipment changes preserve cooldowns");
        TagCompound tag = new TagCompound(); data.SaveData(tag);
        var copy = new Player().GetModPlayer<VoyageEquipmentPlayer>(); copy.LoadData(tag);
        Require(copy.Cooldowns[0] == 480 && copy.Cooldowns[7] == 7200, "Cooldown save/load");
        data.Expert[0] = true; data.SetTier = 5; data.GelShield = 220; data.ArkShield = 450; data.ArkTime = 300;
        Require(data.EffectiveShield == 450, "Shield uses maximum rather than sum");
        for (int high = 8; high <= 10; high++)
        {
            int[] ingredients = high == 8 ? new[]{1,2,3,4} : high == 9 ? new[]{5,6} : new[]{0,7};
            Item combined = new Item(VoyageEquipmentData.Accessory(high));
            foreach (int low in ingredients)
            {
                Item ingredient = new Item(VoyageEquipmentData.Accessory(low));
                Require(!combined.ModItem.CanAccessoryBeEquippedWith(combined, ingredient, p), "Combined exclusion " + high + "/" + low);
                Require(!ingredient.ModItem.CanAccessoryBeEquippedWith(ingredient, combined, p), "Reverse combined exclusion " + high + "/" + low);
            }
        }
        Projectile beam = new Projectile { active = true, hostile = true, damage = 100, width = 12, height = 12, tileCollide = true, penetrate = 1, timeLeft = 120, velocity = Microsoft.Xna.Framework.Vector2.UnitX * 10, aiStyle = ProjAIStyleID.Beam };
        Require(!VoyageEquipmentPlayer.EligibleForInterception(beam), "Do not intercept beams");
        beam.aiStyle = 0;
        Require(VoyageEquipmentPlayer.EligibleForInterception(beam), "Allow ordinary bullet interception");
        VoyageEquipmentExpandedValidation.Validate();
        ModContent.GetInstance<VoyageEquipmentKeys>().Mod.Logger.Info("VOYAGE_EQUIPMENT_PASS armor=36 loadouts=24 expert=17 ordinary=11 stats=exact wings=3 exclusions=8 cooldown-save=pass shields=max secondary-owner=pass");
    }
    private static void Require(bool condition, string text)
    {
        if (!condition) throw new InvalidOperationException("Voyage equipment validation: " + text);
    }
}
