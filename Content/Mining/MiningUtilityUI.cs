using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Mining;

public sealed class MiningUtilityUI : ModSystem
{
    internal static readonly List<Point> FrostLamps = new();
    private int lampClock;
    public override void OnWorldUnload() => FrostLamps.Clear();
    public override void UpdateUI(GameTime gameTime)
    {
        if (Main.gameMenu || Main.dedServ) { FrostLamps.Clear(); return; }
        if (++lampClock % 60 != 0) return;
        FrostLamps.Clear();
        int cx = (int)Main.LocalPlayer.Center.X / 16, cy = (int)Main.LocalPlayer.Center.Y / 16;
        for (int x = Math.Max(2, cx - 38); x <= Math.Min(Main.maxTilesX - 3, cx + 38); x++)
        for (int y = Math.Max(2, cy - 28); y <= Math.Min(Main.maxTilesY - 3, cy + 28); y++)
        {
            Tile tile = Main.tile[x, y];
            if (tile.HasTile && tile.TileType == ModContent.TileType<MiningUtilityTile4>()
                && tile.TileFrameX % 54 == 0 && tile.TileFrameY % 54 == 0) FrostLamps.Add(new Point(x + 1, y + 1));
        }
    }
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int at = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
        layers.Insert(at < 0 ? layers.Count : at, new LegacyGameInterfaceLayer("StarfallThrone: Mining survey", DrawHUD, InterfaceScaleType.UI));
    }
    private static bool DrawHUD()
    {
        if (Main.gameMenu || Main.LocalPlayer.dead || Main.playerInventory) return true;
        Player player = Main.LocalPlayer; MiningPlayer state = player.GetModPlayer<MiningPlayer>();
        int kind = player.HeldItem.ModItem is MiningSurvey survey ? survey.Index : -1;
        if (kind < 0 && !state.Spectrum) return true;
        Vector2 screen = StarfallUI.ScreenSizeUi;
        float top = screen.Y - (kind == 2 ? 170 : 120);
        float width = Math.Min(460, screen.X - 32);
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(16, (int)top, (int)width, kind == 2 ? 142 : 92), new Color(10, 24, 32, 220));
        if (kind == 3)
        {
            Label(MiningText.Get("MarkerTitle"), new Vector2(28, top + 10), Color.LightCyan);
            Label(MiningText.Get(state.SelectionActive ? "MarkerHUDActive" : "MarkerHUDHint"), new Vector2(28, top + 36), Color.White, .7f);
            return true;
        }
        string oreName = new Item(MiningCatalog.OreType(state.SelectedOre)).Name;
        Label(MiningText.Get("SurveySelected", oreName), new Vector2(28, top + 9), MiningCatalog.Colors[state.SelectedOre]);
        if (kind >= 0)
        {
            Label(MiningText.Get("SurveyHUDHint"), new Vector2(28, top + 34), Color.Silver, .65f);
            if (state.SurveyResults.Count == 0) Label(MiningText.Get("SurveyNone"), new Vector2(28, top + 56), Color.Gray, .72f);
            for (int i = 0; i < state.SurveyResults.Count; i++)
            {
                Vector2 delta = state.SurveyResults[i].ToWorldCoordinates() - player.Center;
                string direction = MiningText.Get(Direction(delta));
                string distance = kind == 0 ? MiningText.Get(delta.Length() < 320 ? "Near" : "Far")
                    : kind == 1 ? MiningText.Get(delta.Length() < 480 ? "Near" : delta.Length() < 1000 ? "Medium" : "Far")
                    : MiningText.Get("Distance", (int)(delta.Length() / 16));
                Label((i + 1) + ". " + direction + " · " + distance, new Vector2(28, top + 56 + i * 23), Color.White, .72f);
            }
            if (kind == 2 && state.SavedWaypoint is Point waypoint)
            {
                Vector2 delta = waypoint.ToWorldCoordinates() - player.Center;
                Label(MiningText.Get("WaypointHUD", MiningText.Get(Direction(delta)), (int)(delta.Length() / 16)), new Vector2(28, top + 120), Color.Gold, .64f);
            }
        }
        else
        {
            Rectangle button = new(28, (int)top + 40, 205, 28);
            bool hover = button.Contains(StarfallUI.MouseUiPoint);
            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, button, hover ? Color.DarkSlateGray : new Color(26, 54, 64));
            Label(MiningText.Get("SpectrumCycle"), new Vector2(button.X + 6, button.Y + 4), Color.White, .7f);
            if (hover)
            {
                player.mouseInterface = true;
                if (Main.mouseLeft && Main.mouseLeftRelease)
                { Main.mouseLeftRelease = false; state.SelectNextOre(12); }
            }
        }
        return true;
    }
    private static string Direction(Vector2 delta)
    {
        if (delta.LengthSquared() < 24 * 24) return "Here";
        int sector = (int)MathF.Round(delta.ToRotation() / MathHelper.PiOver4);
        return ((sector + 8) % 8) switch { 0 => "East", 1 => "SouthEast", 2 => "South", 3 => "SouthWest", 4 => "West", 5 => "NorthWest", 6 => "North", _ => "NorthEast" };
    }
    private static void Label(string value, Vector2 at, Color color, float scale = .8f)
        => Utils.DrawBorderString(Main.spriteBatch, value, at, color, scale);
    public override void PostDrawTiles()
    {
        if (Main.dedServ || Main.gameMenu || Main.LocalPlayer.dead) return;
        Player player = Main.LocalPlayer; MiningPlayer state = player.GetModPlayer<MiningPlayer>();
        bool toolPreview = player.HeldItem.ModItem is MiningTool held && MiningTool.ModeCount(held.Index) > 1 && state.Modes[held.Index] > 0;
        bool marker = player.HeldItem.ModItem is MiningSurvey survey && survey.Index == 3;
        if (!toolPreview && !marker) return;
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        if (state.SelectionActive) Outline(state.Selection, Color.Cyan * .65f);
        if (marker && state.SelectionFirst is Point first)
        {
            Point at = new(Player.tileTargetX, Player.tileTargetY);
            int w = Math.Abs(first.X - at.X) + 1, h = Math.Abs(first.Y - at.Y) + 1;
            if (w <= 200 && h <= 200) Outline(new Rectangle(Math.Min(first.X, at.X), Math.Min(first.Y, at.Y), w, h), Color.Gold * .6f);
        }
        if (toolPreview && !player.mouseInterface && player.HeldItem.ModItem is MiningTool tool)
        {
            foreach (Point p in MiningPlayer.ShapeTargets(tool.Index, state.Modes[tool.Index], Player.tileTargetX, Player.tileTargetY))
                Outline(new Rectangle(p.X, p.Y, 1, 1), MiningPlayer.SafeExtra(player, p.X, p.Y) ? Color.LightGreen * .65f : Color.IndianRed * .5f);
        }
        Main.spriteBatch.End();
    }
    private static void Outline(Rectangle rectangle, Color color)
    {
        Vector2 topLeft = new Vector2(rectangle.X * 16, rectangle.Y * 16) - Main.screenPosition;
        Vector2 topRight = topLeft + new Vector2(rectangle.Width * 16, 0);
        Vector2 bottomLeft = topLeft + new Vector2(0, rectangle.Height * 16);
        Vector2 bottomRight = topLeft + new Vector2(rectangle.Width * 16, rectangle.Height * 16);
        MiningUtilityTile.DrawLine(Main.spriteBatch, topLeft, topRight, color, 2);
        MiningUtilityTile.DrawLine(Main.spriteBatch, topRight, bottomRight, color, 2);
        MiningUtilityTile.DrawLine(Main.spriteBatch, bottomRight, bottomLeft, color, 2);
        MiningUtilityTile.DrawLine(Main.spriteBatch, bottomLeft, topLeft, color, 2);
    }
}

public sealed class MiningSurveyHighlight : GlobalTile
{
    public override void PostDraw(int i, int j, int type, SpriteBatch spriteBatch)
    {
        if (Main.dedServ || Main.gameMenu || !WorldGen.InWorld(i, j, 2)) return;
        int ore = MiningCatalog.OreFromTile(type);
        if (ore < 0 || !MiningCatalog.Unlocked(ore)) return;
        Player player = Main.LocalPlayer; MiningPlayer state = player.GetModPlayer<MiningPlayer>();
        Vector2 point = new(i, j), origin = player.Center / 16;
        bool early = player.HeldItem.ModItem is MiningTool tool && tool.Index == 3 && ore < 4 && Vector2.DistanceSquared(point, origin) <= 10 * 10;
        bool spectrum = state.Spectrum && ore == state.SelectedOre && Vector2.DistanceSquared(point, origin) <= 20 * 20;
        bool lamp = MiningUtilityUI.FrostLamps.Exists(p => Vector2.DistanceSquared(p.ToVector2(), point) <= 18 * 18);
        if (!early && !spectrum && !lamp) return;
        bool exposed = !Main.tile[i - 1, j].HasTile || !Main.tile[i + 1, j].HasTile || !Main.tile[i, j - 1].HasTile || !Main.tile[i, j + 1].HasTile;
        if (!exposed) return;
        float pulse = .42f + MathF.Sin((float)Main.GlobalTimeWrappedHourly * 3 + i * .2f) * .12f;
        if (!Terraria.GameContent.Drawing.TileDrawing.IsVisible(Main.tile[i, j])) return;
        // The ore tiles use custom PreDraw, so changing TileDrawInfo.tileLight would be ignored.
        // Overlay their real texture instead of advertising a highlight that never renders.
        Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        Vector2 at = new Vector2(i * 16, j * 16) - Main.screenPosition + offset;
        spriteBatch.Draw(TextureAssets.Tile[type].Value, new Rectangle((int)at.X, (int)at.Y, 16, 16), null, MiningCatalog.Colors[ore] * pulse);
    }
}
