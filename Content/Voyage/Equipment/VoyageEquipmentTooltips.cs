using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Voyage.Equipment;

public sealed class VoyageEquipmentTooltips : GlobalItem
{
    public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
    {
        ModKeybind key = item.ModItem switch
        {
            VoyageArmor armor when armor.Part < 4 && armor.Tier is 0 or 2 or 4 or 5 => VoyageEquipmentKeys.ArmorSkill,
            VoyageExpertAccessory accessory when accessory.Index is 2 or 13 => VoyageEquipmentKeys.AccessoryDash,
            _ => null
        };
        if (key == null) return;
        List<string> keys = key.GetAssignedKeys();
        string label = keys.Count > 0 ? string.Join(" / ", keys) : Language.GetTextValue("Mods.StarfallThrone.VoyageEquipment.Unbound");
        tooltips.Add(new TooltipLine(Mod, "VoyageAbilityKey", Language.GetTextValue("Mods.StarfallThrone.VoyageEquipment.Controls", label)));
        if (Main.gameMenu || Main.LocalPlayer == null) return;
        var player = Main.LocalPlayer.GetModPlayer<VoyageEquipmentPlayer>();
        int tier = item.ModItem is VoyageArmor a ? a.Tier : player.SetTier;
        int armorSeconds = tier >= 0 ? (player.Cooldowns[tier] + 59) / 60 : 0;
        tooltips.Add(new TooltipLine(Mod, "VoyageAbilityState", Language.GetTextValue("Mods.StarfallThrone.VoyageEquipment.Cooldowns", armorSeconds,
            (player.Cooldowns[VoyageEquipmentPlayer.DashSlot] + 59) / 60, player.EffectiveShield)));
    }
}

