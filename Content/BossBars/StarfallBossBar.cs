using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.ModLoader;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.BigProgressBar;

namespace StarfallThrone.Content.BossBars;

public sealed class StarfallBossBar : ModBossBar
{
    private int headIndex = -1;

    public override bool? ModifyInfo(ref BigProgressBarInfo info, ref float life, ref float lifeMax, ref float shield, ref float shieldMax)
    {
        headIndex = -1;
        if (info.npcIndexToAimAt < 0 || info.npcIndexToAimAt >= Main.maxNPCs)
            return false;
        NPC npc = Main.npc[info.npcIndexToAimAt];
        if (!npc.active || !npc.boss)
            return false;
        headIndex = npc.GetBossHeadTextureIndex();
        return null;
    }

    public override Asset<Texture2D> GetIconTexture(ref Microsoft.Xna.Framework.Rectangle? iconFrame)
    {
        return headIndex >= 0 && headIndex < TextureAssets.NpcHeadBoss.Length
            ? TextureAssets.NpcHeadBoss[headIndex]
            : ModContent.Request<Texture2D>("StarfallThrone/Content/UI/StarfallBossBarIcon");
    }
}
