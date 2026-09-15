using Terraria.GameContent.Bestiary;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Ecology.Combat;

public static class EcologyBestiary
{
    public static void Attach(BestiaryEntry entry, int biome)
    {
        // SpawnModBiomes belongs to the registration template. Fresh ModNPC instances use
        // Activator.CreateInstance, so explicitly preserve the association on the actual entry.
        // Reuse the biome's registered element so filtering/icons/backgrounds all identify
        // the same biome, and avoid duplicates when NPCLoader already attached it.
        ModBiomeBestiaryInfoElement element = ModContent.Find<ModBiome>("StarfallThrone", "EcologyBiome" + biome).ModBiomeBestiaryInfoElement;
        if (element != null && !entry.Info.Contains(element)) entry.Info.Add(element);
    }
}
