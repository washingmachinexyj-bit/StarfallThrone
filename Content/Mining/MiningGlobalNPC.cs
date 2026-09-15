using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ModLoader;
using StarfallThrone.Content.NPCs;

namespace StarfallThrone.Content.Mining;

/// <summary>Observe existing downed flags; never replace encounter AI or its original loot rules.</summary>
public sealed class MiningGlobalNPC : GlobalNPC
{
    public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
    {
        // Saved miniboss ID 6 is DewShell, not MossTumbler (ID 0).
        if (npc.ModNPC is MiniBossNPC { Index: 6 })
            npcLoot.Add(ItemDropRule.Common(MiningCatalog.ItemType("ShellPatternFragment"), 1, 6, 6));
    }
}
