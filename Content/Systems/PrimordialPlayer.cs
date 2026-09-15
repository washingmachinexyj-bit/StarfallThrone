using Terraria;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Systems;

public sealed class PrimordialPlayer : ModPlayer
{
    public bool DawnshardCoreEquipped;
    public bool RootboundTalismanEquipped;
    public bool ResinHeartEquipped;
    public int DawnshardSurgeTime;
    public int ResinSurgeTime;

    public override void ResetEffects()
    {
        DawnshardCoreEquipped = false;
        RootboundTalismanEquipped = false;
        ResinHeartEquipped = false;
    }

    public override void PostUpdateEquips()
    {
        if (RootboundTalismanEquipped && Player.velocity.LengthSquared() < 0.02f)
            Player.statDefense += 3;

        if (DawnshardSurgeTime > 0)
        {
            DawnshardSurgeTime--;
            Player.moveSpeed += 0.12f;
            Player.GetDamage(DamageClass.Generic) *= 1.05f;
        }

        if (ResinSurgeTime > 0)
        {
            ResinSurgeTime--;
            Player.moveSpeed += 0.10f;
        }
    }

    public override void OnHurt(Player.HurtInfo info)
    {
        if (DawnshardCoreEquipped && DawnshardSurgeTime <= 0)
            DawnshardSurgeTime = 300;
        if (ResinHeartEquipped)
            ResinSurgeTime = 120;
    }

    public override void UpdateDead()
    {
        DawnshardSurgeTime = 0;
        ResinSurgeTime = 0;
    }
}
