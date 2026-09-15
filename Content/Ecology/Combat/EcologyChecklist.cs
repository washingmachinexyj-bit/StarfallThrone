using System;
using System.Collections.Generic;
using Terraria.Localization;
using Terraria.ModLoader;
namespace StarfallThrone.Content.Ecology.Combat;
public sealed class EcologyChecklist : ModSystem
{
    public static readonly float[] Progression = { 2.35f, 3.65f, 4.65f, 5.65f, 7.7f, 11.7f, 12.7f, 13.7f, 15.8f, 18.4f };
    public override void PostSetupContent()
    {
        if (!ModLoader.TryGetMod("BossChecklist", out Mod checklist)) return;
        for (int i = 0; i < EcologyCatalog.Count; i++)
        {
            int index = i;
            checklist.Call("LogBoss", Mod, "Ecology" + EcologyCatalog.Keys[index], Progression[index],
                (Func<bool>)(() => EcologyWorld.Downed[index]), new List<int> { EcologyCatalog.NPCType(index) },
                new Dictionary<string, object> {
                    ["displayName"] = Language.GetText("Mods.StarfallThrone.NPCs.EcologyBoss" + index + ".DisplayName"),
                    ["spawnItems"] = new List<int> { EcologyCatalog.Summon(index) },
                    ["spawnInfo"] = Language.GetText("Mods.StarfallThrone.EcologyCombat.Spawn" + index),
                    ["availability"] = (Func<bool>)(() => true),
                    ["collectibles"] = new List<int> { EcologyCatalog.Relic(index), EcologyCatalog.Trophy(index), EcologyCatalog.Mask(index), EcologyCatalog.Pet(index) },
                    ["despawnMessage"] = Language.GetText("Mods.StarfallThrone.EcologyCombat.Despawn")
                });
        }
    }
}
