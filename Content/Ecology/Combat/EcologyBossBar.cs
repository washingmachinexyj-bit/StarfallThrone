using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.BigProgressBar;
using Terraria.ModLoader;
namespace StarfallThrone.Content.Ecology.Combat;
public sealed class EcologyBossBar : ModBossBar
{
    private int head = -1;
    public override Asset<Texture2D> GetIconTexture(ref Rectangle? iconFrame)
        => head >= 0 && head < TextureAssets.NpcHeadBoss.Length ? TextureAssets.NpcHeadBoss[head] : ModContent.Request<Texture2D>(EcologyCatalog.Root + "Boss0_Head_Boss");
    public override bool? ModifyInfo(ref BigProgressBarInfo info, ref float life, ref float lifeMax, ref float shield, ref float shieldMax)
    {
        if (info.npcIndexToAimAt < 0 || info.npcIndexToAimAt >= Main.maxNPCs) return false;
        NPC n = Main.npc[info.npcIndexToAimAt]; if (!n.active || n.ModNPC is not EcologyBossNPC b || b.Cancelled) return false;
        life = n.life; lifeMax = n.lifeMax; head = n.GetBossHeadTextureIndex();
        shield = b.Shell; shieldMax = b.Index == 1 ? n.lifeMax / 8 : b.Index == 5 ? n.lifeMax / 30 : 0; return true;
    }
}
