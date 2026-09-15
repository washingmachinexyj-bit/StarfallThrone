using System;
using System.Collections.Generic;
using Terraria.Localization;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Voyage;

public sealed class VoyageChecklist : ModSystem
{
    public override void PostSetupContent()
    {
        if (!ModLoader.TryGetMod("BossChecklist", out Mod checklist)) return;
        for (int i = 0; i < VoyageCatalog.Count; i++)
        {
            int index = i;
            checklist.Call("LogBoss", Mod, VoyageCatalog.Keys[i], VoyageCatalog.Progression(i),
                (Func<bool>)(() => VoyageWorld.IsDowned(index)), new List<int> { VoyageCatalog.BossType(i) },
                new Dictionary<string, object>
                {
                    ["spawnItems"] = new List<int> { VoyageCatalog.Summon(i) },
                    ["collectibles"] = new List<int> { VoyageCatalog.Relic(i), VoyageCatalog.Trophy(i), VoyageCatalog.Mask(i) },
                    ["spawnInfo"] = Language.GetText("Mods.StarfallThrone.Voyage.SpawnInfo"),
                    ["despawnMessage"] = Language.GetText("Mods.StarfallThrone.Voyage.DespawnMessage")
                });
        }
    }
}
