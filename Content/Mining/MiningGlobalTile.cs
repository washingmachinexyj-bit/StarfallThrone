using Terraria;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Mining;

/// <summary>Remember edited chunks without preventing vanilla digging or changing unrelated tile behavior.</summary>
public sealed class MiningGlobalTile : GlobalTile
{
    public override void PlaceInWorld(int i, int j, int type, Item item) => MiningWorld.RecordLocalPlacement(i, j, false);

    public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
    {
        if (!fail && !effectOnly) MiningWorld.RecordServerEdit(i, j);
    }
}

public sealed class MiningGlobalWall : GlobalWall
{
    public override void PlaceInWorld(int i, int j, int type, Item item) => MiningWorld.RecordLocalPlacement(i, j, true);

    public override void KillWall(int i, int j, int type, ref bool fail)
    {
        if (!fail) MiningWorld.RecordServerEdit(i, j);
    }
}
