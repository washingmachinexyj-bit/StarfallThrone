using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using StarfallThrone.Content.Ascendant.Bosses;

namespace StarfallThrone.Content.Ascendant;

public sealed class AscendantScene : ModSystem
{
    public override void ModifySunLightColor(ref Color tileColor,ref Color backgroundColor)
    {
        if(Main.gameMenu||Main.dedServ)return;
        foreach(NPC npc in Main.ActiveNPCs)
            if(npc.ModNPC is AscendantBossNPC boss&&boss.Index==2&&npc.life<npc.lifeMax*.1f&&npc.Distance(Main.LocalPlayer.Center)<2400)
            {
                // Background-only desaturation: no fullscreen flash, color inversion,
                // player tint or reduction of the high-contrast combat warnings.
                int grey=(backgroundColor.R+backgroundColor.G+backgroundColor.B)/3;
                backgroundColor=Color.Lerp(backgroundColor,new Color(grey,grey,grey),.85f);return;
            }
    }
}
