using System;
using System.Collections.Generic;
using Terraria.Localization;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Ascendant;

public sealed class AscendantChecklist : ModSystem
{
    public static LocalizedText SpawnInfo(int index)
    {
        if(!AscendantWorld.Open(index)) return Language.GetText("Mods.StarfallThrone.Ascendant."+(AscendantWorld.IsDowned(index)?"ChecklistClosedWon":"ChecklistMissed"));
        return Language.GetText("Mods.StarfallThrone.Ascendant.Spawn"+index).WithFormatArgs(AscendantWorld.RequirementText(index));
    }
    public override void PostSetupContent()
    {
        if(!ModLoader.TryGetMod("BossChecklist",out Mod checklist))return;
        for(int i=0;i<3;i++)
        {
            int index=i;
            checklist.Call("LogBoss",Mod,AscendantCatalog.Keys[index],AscendantCatalog.Progression(index),
                (Func<bool>)(()=>AscendantWorld.IsDowned(index)),new List<int>{AscendantCatalog.NPCType(index)},
                new Dictionary<string,object>{
                    ["displayName"]=Language.GetText("Mods.StarfallThrone.Ascendant.ChecklistName"+index),
                    ["spawnItems"]=new List<int>{AscendantCatalog.Summon(index)},
                    ["spawnInfo"]=(Func<LocalizedText>)(()=>SpawnInfo(index)),
                    // Keep missed challenges visible; never falsely set their defeat flag.
                    ["availability"]=(Func<bool>)(()=>true),
                    ["collectibles"]=new List<int>{AscendantCatalog.Relic(index),AscendantCatalog.Trophy(index),AscendantCatalog.Mask(index),AscendantCatalog.Pet(index),AscendantCatalog.Halo(index)},
                    ["despawnMessage"]=Language.GetText("Mods.StarfallThrone.Ascendant.Despawn")
                });
        }
    }
}
