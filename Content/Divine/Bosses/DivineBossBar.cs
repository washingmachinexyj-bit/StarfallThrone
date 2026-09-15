using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.BigProgressBar;
using Terraria.ModLoader;

namespace StarfallThrone.Content.Divine.Bosses;

public sealed class DivineBossBar : ModBossBar
{
    private int head = -1;
    public override Asset<Texture2D> GetIconTexture(ref Rectangle? iconFrame)
        => head >= 0 && head < TextureAssets.NpcHeadBoss.Length ? TextureAssets.NpcHeadBoss[head] : ModContent.Request<Texture2D>(DivineCatalog.Root + "Boss0_Head_Boss");
    public override bool? ModifyInfo(ref BigProgressBarInfo info, ref float life, ref float lifeMax, ref float shield, ref float shieldMax)
    {
        if (info.npcIndexToAimAt < 0 || info.npcIndexToAimAt >= Main.maxNPCs) return false;
        NPC n = Main.npc[info.npcIndexToAimAt];
        if (!n.active || n.ModNPC is not DivineBossNPC b || b.Cancelled) return false;
        life = n.life; lifeMax = n.lifeMax; shield = shieldMax = 0; head = n.GetBossHeadTextureIndex();
        return true;
    }
}
