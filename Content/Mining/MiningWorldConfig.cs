using System.ComponentModel;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader.Config;

namespace StarfallThrone.Content.Mining;

/// <summary>Generation is server-authoritative; clients cannot silently change preservation settings.</summary>
public sealed class MiningWorldConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ServerSide;

    [DefaultValue(true)]
    public bool GenerateOnFutureFirstKills { get; set; } = true;

    [DefaultValue(160)]
    [Range(80, 400)]
    public int SpawnProtectionRadius { get; set; } = 160;

    [DefaultValue(8)]
    [Range(4, 20)]
    public int StructurePadding { get; set; } = 8;

    [DefaultValue(8)]
    [Range(1, 24)]
    public int AttemptsPerTick { get; set; } = 8;

    public override bool AcceptClientChanges(ModConfig pendingConfig, int whoAmI, ref NetworkText message)
    {
        if (MiningWorld.IsHost(whoAmI)) return true;
        message = NetworkText.FromKey(MiningWorld.Locale + "HostOnly");
        return false;
    }
}
