using System;
using System.Collections.Generic;
using Terraria.Localization;
using Terraria.ModLoader;
namespace StarfallThrone.Content.Pantheon;
public sealed class PantheonChecklist : ModSystem
{
    public override void PostSetupContent()
    {
        if(!ModLoader.TryGetMod("BossChecklist",out Mod checklist))return;
        for(int i=0;i<17;i++)
        {
            int index=i;
            checklist.Call("LogBoss",Mod,"Pantheon"+PantheonCatalog.Keys[i],PantheonCatalog.Progression(i),
                (Func<bool>)(()=>PantheonWorld.IsDowned(index)),new List<int>{PantheonCatalog.BossType(i)},
                new Dictionary<string,object>{
                    ["displayName"]=Language.GetText("Mods.StarfallThrone.Pantheon.Name"+i),
                    ["spawnItems"]=new List<int>{PantheonCatalog.Summon(i)},
                    ["spawnInfo"]=Language.GetText("Mods.StarfallThrone.Pantheon.Spawn"+i),
                    ["availability"]=(Func<bool>)(()=>PantheonWorld.CanSummon(index)||PantheonWorld.IsDowned(index)),
                    ["collectibles"]=new List<int>{PantheonCatalog.Relic(i),PantheonCatalog.Trophy(i),PantheonCatalog.Mask(i),PantheonCatalog.Pet(i)},
                    ["despawnMessage"]=Language.GetText("Mods.StarfallThrone.Pantheon.Despawn")
                });
        }
    }
}
