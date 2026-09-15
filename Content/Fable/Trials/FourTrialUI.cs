using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Fable.Trials;

public sealed class FourTrialUI : ModSystem
{
    public static bool Opened;

    public override void OnWorldUnload() => Opened = false;

    public static void Request(int index)
    {
        if (!FourTrialCatalog.Valid(index)) return;
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            ModPacket packet = ModContent.GetInstance<FourTrialUI>().Mod.GetPacket();
            packet.Write((byte)94);
            packet.Write((byte)1);
            packet.Write((byte)index);
            packet.Send();
            Opened = false;
            return;
        }

        if (FourTrialWorld.TrySummon(Main.LocalPlayer, index)) Opened = false;
    }

    internal static bool Draw()
    {
        if (!Opened || Main.gameMenu) return true;
        Player player = Main.LocalPlayer;
        if (player.dead)
        {
            Opened = false;
            return true;
        }

        Vector2 screen = StarfallUI.ScreenSizeUi;
        float sw = screen.X, sh = screen.Y;
        int width = (int)Math.Min(790, sw - 20), height = (int)Math.Min(600, sh - 20);
        int left = (int)(sw - width) / 2, top = (int)(sh - height) / 2;
        Point mouse = StarfallUI.MouseUiPoint;
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(left, top, width, height), new Color(27, 30, 40, 248));
        player.mouseInterface = true;
        bool click = Main.mouseLeft && Main.mouseLeftRelease;

        void Label(string text, int x, int y, float scale = .72f, Color? color = null) =>
            Utils.DrawBorderString(Main.spriteBatch, text, new Vector2(x, y), color ?? Color.Wheat, scale);

        bool Button(string text, int x, int y, int w, int h = 30, bool enabled = true)
        {
            Rectangle box = new(x, y, w, h);
            bool hover = box.Contains(mouse);
            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, box,
                !enabled ? new Color(43, 43, 48) : hover ? new Color(92, 82, 61) : new Color(57, 58, 67));
            Label(text, x + 8, y + 5, .65f, enabled ? Color.White : Color.Gray);
            if (hover && click && enabled)
            {
                Main.mouseLeftRelease = false;
                click = false;
                return true;
            }
            return false;
        }

        Label(FourTrialCatalog.Text("FourTrialTitle"), left + 18, top + 14, .95f);
        Label(FourTrialCatalog.Text("FourTrialNote"), left + 18, top + 49, .58f, Color.Silver);
        if (Button(FourTrialCatalog.Text("FourTrialReturn"), left + width - 160, top + 10, 118))
        {
            Opened = false;
            return true;
        }

        int rowHeight = 92;
        for (int i = 0; i < FourTrialCatalog.Count; i++)
        {
            int y = top + 88 + i * rowHeight;
            Color tint = FourTrialCatalog.Colors[i];
            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(left + 14, y - 5, width - 28, 78), new Color(tint.R, tint.G, tint.B, 26));
            Texture2D icon = ModContent.Request<Texture2D>(FourTrialCatalog.BossTexture(i)).Value;
            Main.spriteBatch.Draw(icon, new Vector2(left + 42, y + 30), null, Color.White, 0, icon.Size() / 2, 1.25f,
                SpriteEffects.None, 0);
            Label((i + 1).ToString("00") + "  " + FourTrialCatalog.Name(i), left + 76, y + 2, .74f, tint);
            Label(FourTrialCatalog.Text("FourTrialInfo" + i), left + 76, y + 30, .56f, Color.Silver);
            bool enabled = !FableWorld.AnyEncounter && !FourTrialWorld.AnyActive();
            if (Button(FourTrialCatalog.Text(FourTrialWorld.Downed[i] ? "Replay" : "Challenge"), left + width - 155, y + 17, 125, 30, enabled))
                Request(i);
            Label(FourTrialWorld.Downed[i] ? FourTrialCatalog.Text("FourTrialCleared") : FourTrialCatalog.Text("FourTrialUnclaimed"),
                left + 76, y + 52, .5f, FourTrialWorld.Downed[i] ? Color.LightGreen : Color.Gray);
        }

        Label(FourTrialCatalog.Text("FourTrialSpawnInfo"), left + 18, top + height - 42, .55f, Color.Silver);
        if (Main.keyState.IsKeyDown(Keys.Escape)) Opened = false;
        return true;
    }
}
