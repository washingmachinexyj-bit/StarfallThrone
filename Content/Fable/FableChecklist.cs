using System;
using System.Collections.Generic;
using System.Reflection;
using MonoMod.RuntimeDetour;
using Terraria.Localization;
using Terraria.ModLoader;
using StarfallThrone.Content.Fable.Trials;

namespace StarfallThrone.Content.Fable;

public sealed class FableChecklist : ModSystem
{
    private Hook visibilityHook;
    private PropertyInfo entryKey;
    public bool StrictVisibilityInstalled => visibilityHook!=null;
    public override void PostSetupContent()
    {
        if(!ModLoader.TryGetMod("BossChecklist",out Mod checklist))return;
        for(int i=0;i<FourTrialCatalog.Count;i++)
        {
            int index=i;
            checklist.Call("LogMiniBoss",Mod,"FourTrial"+i,FourTrialCatalog.Progression(i),(Func<bool>)(()=>FourTrialWorld.Downed[index]),FourTrialCatalog.BossType(i),
                new Dictionary<string,object>{["displayName"]=Language.GetText("Mods.StarfallThrone.NPCs.FourTrialBoss"+i+".DisplayName"),["spawnItems"]=ModContent.ItemType<FableBook>(),
                ["spawnInfo"]=Language.GetText("Mods.StarfallThrone.Fable.FourTrialSpawnInfo"),["collectibles"]=new List<int>{FourTrialCatalog.Weapon(i),FourTrialCatalog.Bag(i),FourTrialCatalog.Memento(i),FourTrialCatalog.Trophy(i),FourTrialCatalog.Relic(i),FourTrialCatalog.Mask(i)}});
        }
        for(int i=0;i<18;i++)
        {
            int index=i;
            checklist.Call("LogMiniBoss",Mod,"Fable"+i,FableCatalog.Progression(i),(Func<bool>)(()=>FableWorld.Downed[index]),FableCatalog.BossType(i),
                new Dictionary<string,object>{["displayName"]=Language.GetText("Mods.StarfallThrone.NPCs.FableBoss"+i+".DisplayName"),["spawnItems"]=ModContent.ItemType<FableBook>(),
                ["spawnInfo"]=(Func<LocalizedText>)(()=>Language.GetText("Mods.StarfallThrone.Fable."+(FableWorld.Skipped?"SkippedNotice":"SpawnInfo"))),
                ["availability"]=(Func<bool>)(()=>FableWorld.Unlocked(index)),["collectibles"]=new List<int>{FableCatalog.Weapon(i)}});
        }
        // Availability is only a user-configurable filter in Checklist. Gate the actual visibility predicate,
        // scoped to our own 18 keys, without changing its config, saved hidden list, entries or record indices.
        Type entry=checklist.Code.GetType("BossChecklist.EntryInfo",true);
        entryKey=entry.GetProperty("Key",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        MethodInfo method=entry.GetMethod("VisibleOnChecklist",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if(entryKey==null || method==null)throw new NotSupportedException("Boss Checklist version lacks the Fable discovery visibility hook. Disable Boss Checklist or use its supported 1.4.4 release.");
        visibilityHook=new Hook(method,(Func<Func<object,bool>,object,bool>)Filter);
    }
    private bool Filter(Func<object,bool> original,object self)
    {
        string key=entryKey.GetValue(self) as string;
        if(key!=null && key.StartsWith("StarfallThrone FourTrial",StringComparison.Ordinal))return true;
        const string prefix="StarfallThrone Fable";
        if(key!=null && key.StartsWith(prefix,StringComparison.Ordinal) && int.TryParse(key[prefix.Length..],out int index) && FableCatalog.Valid(index))
            return FableWorld.Visible(index);
        return original(self);
    }
    public override void Unload(){visibilityHook?.Dispose();visibilityHook=null;entryKey=null;}
}
