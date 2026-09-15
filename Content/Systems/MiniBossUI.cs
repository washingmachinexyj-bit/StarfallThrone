using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;
using StarfallThrone.Content.NPCs;

namespace StarfallThrone.Content.Systems;

public sealed class MiniBossUI : ModSystem
{
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int at = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
        if (at >= 0) layers.Insert(at, new LegacyGameInterfaceLayer("StarfallThrone: Mini Boss Bar", DrawBar, InterfaceScaleType.UI));
    }
    private bool DrawBar()
    {
        if (Main.gameMenu || Main.LocalPlayer.dead) return true;
        NPC selected = null;
        foreach (NPC npc in Main.ActiveNPCs)
            if (npc.ModNPC is MiniBossNPC { Dormant: false } && npc.Distance(Main.LocalPlayer.Center) < 2000f
                && (selected == null || npc.Distance(Main.LocalPlayer.Center) < selected.Distance(Main.LocalPlayer.Center))) selected = npc;
        if (selected == null) return true;
        int index = ((MiniBossNPC)selected.ModNPC).Index;
        Vector2 screen=StarfallUI.ScreenSizeUi;
        Vector2 at = new(screen.X / 2f - 150, screen.Y - 95);
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        Main.spriteBatch.Draw(pixel, new Rectangle((int)at.X, (int)at.Y, 300, 22), new Color(29, 33, 39, 235));
        Main.spriteBatch.Draw(pixel, new Rectangle((int)at.X + 3, (int)at.Y + 3, (int)(294f * selected.life / selected.lifeMax), 16), MiniBossData.Colors[index]);
        int head = selected.GetBossHeadTextureIndex();
        if (head >= 0) Main.spriteBatch.Draw(TextureAssets.NpcHeadBoss[head].Value, at + new Vector2(-42, -8), Color.White);
        Utils.DrawBorderString(Main.spriteBatch, selected.FullName, at + new Vector2(150, -23), Color.White, 0.8f, 0.5f);
        Utils.DrawBorderString(Main.spriteBatch, $"{selected.life} / {selected.lifeMax}", at + new Vector2(150, 2), Color.White, 0.7f, 0.5f);
        return true;
    }
}
