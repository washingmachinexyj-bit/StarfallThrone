using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.BigProgressBar;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Oaths.Bosses;
public sealed class SupremeBossBar : ModBossBar
{
    private int head = -1;
    public override Asset<Texture2D> GetIconTexture(ref Rectangle? iconFrame)
        => head >= 0 && head < TextureAssets.NpcHeadBoss.Length ? TextureAssets.NpcHeadBoss[head]
            : ModContent.Request<Texture2D>(SupremeBossNPC.Root + "SupremeBoss0_Head_Boss");
    public override bool? ModifyInfo(ref BigProgressBarInfo info, ref float life, ref float lifeMax, ref float shield, ref float shieldMax)
    {
        if (info.npcIndexToAimAt < 0 || info.npcIndexToAimAt >= Main.maxNPCs) return false;
        NPC npc = Main.npc[info.npcIndexToAimAt];
        if (!npc.active || npc.ModNPC is not SupremeBossNPC b || b.Cancelled) return false;
        head = npc.GetBossHeadTextureIndex(); life = npc.life; lifeMax = npc.lifeMax; shield = shieldMax = 0;
        foreach (NPC n in Main.ActiveNPCs) if (n.ModNPC is SupremeNode node && node.OwnedBy(b) && node.Open)
        { shield += node.Goal - node.Progress; shieldMax += node.Goal; }
        return true;
    }
}
