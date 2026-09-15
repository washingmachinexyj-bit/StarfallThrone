using System;
using System.Reflection;
using System.Collections.Generic;
using MonoMod.RuntimeDetour;
using Terraria.Localization;
using Terraria.ModLoader;
using StarfallThrone.Content.Fable;
using StarfallThrone.Content.Pantheon;
namespace StarfallThrone.Content.Oaths;
public sealed class OathChecklist:ModSystem
{
    private Hook hook;
    private PropertyInfo keyProperty;
    public bool StrictVisibilityInstalled=>hook!=null;
    public override void PostSetupContent()
    {
        if(!ModLoader.TryGetMod("BossChecklist",out Mod checklist))return;
        for(int i=0;i<3;i++)
        {
            int index=i;
            checklist.Call("LogMiniBoss",Mod,"OathSeed"+i,-1.2f+i*.04f,(Func<bool>)(()=>OathWorld.SeedWon[index]),OathCatalog.Seed(i),new Dictionary<string,object>{
                ["displayName"]=Language.GetText("Mods.StarfallThrone.NPCs.SeedBoss"+i+".DisplayName"),["spawnItems"]=ModContent.ItemType<FableBook>(),
                ["spawnInfo"]=Language.GetText("Mods.StarfallThrone.Oaths.SeedSpawn"),["collectibles"]=new List<int>{OathCatalog.Item("SeedToken"+i),OathCatalog.Item("SeedWeapon"+i)}
                // Deliberately no availability filter: all three seeds remain listed even after being missed.
            });
            checklist.Call("LogBoss",Mod,"OathSupreme"+i,151f+i,(Func<bool>)(()=>OathWorld.SupremeWon[index]),OathCatalog.Boss(i),new Dictionary<string,object>{
                ["displayName"]=Language.GetText("Mods.StarfallThrone.NPCs.SupremeBoss"+i+".DisplayName"),["spawnItems"]=OathCatalog.Item("SupremeSummon"+i),
                ["spawnInfo"]=Language.GetText("Mods.StarfallThrone.Oaths.SupremeSpawn"),
                ["collectibles"]=new List<int>{OathCatalog.Item("SupremeRelic"+i),OathCatalog.Item("SupremeTrophy"+i),OathCatalog.Item("SupremeMask"+i),OathCatalog.Item("SupremePetItem"+i)}
            });
        }
        Type entry=checklist.Code.GetType("BossChecklist.EntryInfo",true);
        keyProperty=entry.GetProperty("Key",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        var method=entry.GetMethod("VisibleOnChecklist",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if(keyProperty==null||method==null)throw new NotSupportedException("Boss Checklist lacks required oath discovery visibility API.");
        hook=new Hook(method,(Func<Func<object,bool>,object,bool>)Filter);
    }
    private bool Filter(Func<object,bool> original,object entry)
    {
        string key=keyProperty.GetValue(entry) as string;
        if(key!=null&&key.StartsWith("StarfallThrone OathSeed",StringComparison.Ordinal))return true;
        if(key!=null&&key.StartsWith("StarfallThrone OathSupreme",StringComparison.Ordinal))return PantheonWorld.Downed[16];
        return original(entry);
    }
    public override void Unload(){hook?.Dispose();hook=null;keyProperty=null;}
}
