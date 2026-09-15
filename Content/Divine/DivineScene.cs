using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using StarfallThrone.Content.Divine.Bosses;

namespace StarfallThrone.Content.Divine;

public sealed class DivineScene : ModSystem
{
    public override void ModifySunLightColor(ref Color tileColor,ref Color backgroundColor)
    {
        if(Main.gameMenu||Main.dedServ)return;
        foreach(NPC npc in Main.ActiveNPCs)
            if(npc.ModNPC is DivineBossNPC boss&&boss.Index==2&&boss.Phase==3&&npc.Distance(Main.LocalPlayer.Center)<2400)
            {
                // Background-only desaturation: no fullscreen flash, color inversion,
                // player tint or reduction of the high-contrast combat warnings.
                int grey=(backgroundColor.R+backgroundColor.G+backgroundColor.B)/3;
                backgroundColor=Color.Lerp(backgroundColor,new Color(grey,grey,grey),.85f);return;
            }
    }
}
