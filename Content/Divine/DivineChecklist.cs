using System;
using System.Collections.Generic;
using Terraria.Localization;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Divine;

public sealed class DivineChecklist : ModSystem
{
    public static LocalizedText SpawnInfo(int index)
    {
        if(!DivineWorld.Open(index)) return Language.GetText("Mods.StarfallThrone.Divine."+(DivineWorld.IsDowned(index)?"ChecklistClosedWon":"ChecklistMissed"));
        return Language.GetText("Mods.StarfallThrone.Divine.Spawn"+index);
    }
    public override void PostSetupContent()
    {
        if(!ModLoader.TryGetMod("BossChecklist",out Mod checklist))return;
        for(int i=0;i<3;i++)
        {
            int index=i;
            checklist.Call("LogBoss",Mod,DivineCatalog.Keys[index],DivineCatalog.Progression(index),
                (Func<bool>)(()=>DivineWorld.IsDowned(index)),new List<int>{DivineCatalog.NPCType(index)},
                new Dictionary<string,object>{
                    ["displayName"]=Language.GetText("Mods.StarfallThrone.Divine.ChecklistName"+index),
                    ["spawnItems"]=new List<int>{DivineCatalog.Summon(index)},
                    ["spawnInfo"]=(Func<LocalizedText>)(()=>SpawnInfo(index)),
                    // Keep missed challenges visible; never falsely set their defeat flag.
                    ["availability"]=(Func<bool>)(()=>true),
                    ["collectibles"]=new List<int>{DivineCatalog.Relic(index),DivineCatalog.Trophy(index),DivineCatalog.Mask(index),DivineCatalog.Pet(index),DivineCatalog.Halo(index)},
                    ["despawnMessage"]=Language.GetText("Mods.StarfallThrone.Divine.Despawn")
                });
        }
    }
}
