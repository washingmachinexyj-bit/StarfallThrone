using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.UI;
using StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Voyage.Utility;

public sealed class VoyageUtilityUI : ModSystem
{
    public static int OpenKind { get; private set; } = -1;
    private static int anchorX, anchorY, selected, detailScroll;
    private static float detailTop, detailBottom;
    private static Rectangle panel;
    private static readonly string TextRoot = "Mods.StarfallThrone.VoyageUtilityUI.";
    private static string Text(string key) => Language.GetTextValue(TextRoot + key);

    public static void Close() => OpenKind = -1;
    public static void Open(int kind, int i, int j)
    {
        if (OpenKind == kind && anchorX == i && anchorY == j) { Close(); return; }
        OpenKind = kind; anchorX = i; anchorY = j;
        Main.LocalPlayer.chest = -1;
        Main.LocalPlayer.CloseSign();
        Main.LocalPlayer.SetTalkNPC(-1);
        Main.npcChatCornerItem = 0;
        Main.npcChatText = "";
        Main.editChest = false;
        Recipe.FindRecipes();
        Main.playerInventory = kind == 0;
        SoundEngine.PlaySound(SoundID.MenuOpen);
        selected = 0;
        detailScroll = 0;
        while (selected < 16 && VoyageWorld.IsDowned(selected)) selected++;
    }

    public override void OnWorldUnload() => Close();
    public override void Unload() => Close();
    public override void UpdateUI(GameTime gameTime)
    {
        if (Main.gameMenu || Main.LocalPlayer.dead) { Close(); return; }
        if (OpenKind < 0) return;
        if (!VoyageUtilityPlayer.InReach(Main.LocalPlayer, anchorX, anchorY)
            || !Main.tile[anchorX, anchorY].HasTile
            || Main.tile[anchorX, anchorY].TileType != VoyageUtilityTiles.TypeFor(OpenKind)
            || (OpenKind == 0 && (!Main.playerInventory || Main.LocalPlayer.chest != -1))
            || (Main.keyState.IsKeyDown(Keys.Escape) && !Main.oldKeyState.IsKeyDown(Keys.Escape)))
        { Close(); return; }
        if (panel.Contains(StarfallUI.MouseUiPoint))
            Main.LocalPlayer.mouseInterface = true;
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
        if (index < 0) index = layers.Count;
        layers.Insert(index, new LegacyGameInterfaceLayer("StarfallThrone: Voyage terminals", Draw, InterfaceScaleType.UI));
    }

    private static bool Draw()
    {
        if (Main.gameMenu) return true;
        if (OpenKind == 0) DrawCargo();
        else if (OpenKind == 7) DrawChart();
        int ticks = Main.LocalPlayer.GetModPlayer<VoyageUtilityPlayer>().RecallTicks;
        if (ticks > 0)
        {
            Vector2 screen = StarfallUI.ScreenSizeUi;
            Vector2 at = new Vector2(screen.X / 2, screen.Y * .7f);
            VoyageFurnitureDrawing.Rect(Main.spriteBatch, at, 160, 9, new Color(15, 25, 40));
            float amount = Math.Clamp(ticks / 120f, 0, 1);
            VoyageFurnitureDrawing.Rect(Main.spriteBatch, at + new Vector2(-78 + amount * 78, 0), amount * 156, 5, Color.Cyan);
            Label(Text("Recalling"), at + new Vector2(-80, 12), Color.LightCyan, .8f);
        }
        return true;
    }

    private static void Background(Rectangle area, string title)
    {
        panel = area;
        VoyageFurnitureDrawing.Rect(Main.spriteBatch, area.Center.ToVector2(), area.Width, area.Height, new Color(14, 25, 44, 242));
        VoyageFurnitureDrawing.Rect(Main.spriteBatch, new Vector2(area.Center.X, area.Top + 2), area.Width, 3, new Color(80, 190, 220));
        Label(title, new Vector2(area.X + 14, area.Y + 12), Color.LightCyan, .95f);
        if (Button(new Rectangle(area.Right - 76, area.Top + 7, 65, 26), Text("Close"))) Close();
        if (area.Contains(Mouse())) Main.LocalPlayer.mouseInterface = true;
    }
    private static Point Mouse() => StarfallUI.MouseUiPoint;
    private static void Label(string label, Vector2 at, Color color, float scale = .8f)
        => Utils.DrawBorderString(Main.spriteBatch, label, at, color, scale);
    private static bool Button(Rectangle area, string label, float scale = .75f)
    {
        bool hover = area.Contains(Mouse());
        VoyageFurnitureDrawing.Rect(Main.spriteBatch, area.Center.ToVector2(), area.Width, area.Height,
            hover ? new Color(55, 103, 138) : new Color(30, 59, 85));
        Label(label, new Vector2(area.X + 7, area.Y + 5), hover ? Color.White : Color.LightBlue, scale);
        if (!hover) return false;
        Main.LocalPlayer.mouseInterface = true;
        if (!Main.mouseLeft || !Main.mouseLeftRelease) return false;
        Main.mouseLeftRelease = false;
        SoundEngine.PlaySound(SoundID.MenuTick);
        return true;
    }
    private static void DrawCargo()
    {
        float uiHeight = StarfallUI.ScreenSizeUi.Y;
        float slotScale = uiHeight >= 590 ? .85f : .72f;
        int step = (int)(52 * slotScale) + 3;
        int top = uiHeight >= 590 ? 285 : 246;
        Rectangle area = new(18, top, step * 10 + 28, step * 4 + 100);
        Background(area, Text("CargoTitle"));
        if (OpenKind < 0) return;
        VoyageUtilityPlayer state = Main.LocalPlayer.GetModPlayer<VoyageUtilityPlayer>();
        float originalScale = Main.inventoryScale;
        try
        {
            Main.inventoryScale = slotScale;
            for (int i = 0; i < 40; i++)
            {
                Vector2 at = new(area.X + 14 + i % 10 * step, area.Y + 43 + i / 10 * step);
                if (new Rectangle((int)at.X, (int)at.Y, step - 3, step - 3).Contains(Mouse()))
                {
                    Main.LocalPlayer.mouseInterface = true;
                    bool shift = Main.keyState.IsKeyDown(Keys.LeftShift) || Main.keyState.IsKeyDown(Keys.RightShift);
                    if (shift && Main.mouseLeft && Main.mouseLeftRelease)
                    {
                        state.Withdraw(i); Main.mouseLeftRelease = false;
                    }
                    else ItemSlot.Handle(state.Cargo, ItemSlot.Context.BankItem, i);
                }
                ItemSlot.Draw(Main.spriteBatch, state.Cargo, ItemSlot.Context.BankItem, i, at);
            }
        }
        finally { Main.inventoryScale = originalScale; }
        int buttonY = area.Bottom - 44;
        if (Button(new Rectangle(area.X + 14, buttonY, 120, 28), Text("QuickStack"))) state.QuickStackCargo();
        if (Button(new Rectangle(area.X + 143, buttonY, 112, 28), Text("LootAll")))
            for (int i = 0; i < 40; i++) state.Withdraw(i);
        Label(Text("CargoHint"), new Vector2(area.X + 266, buttonY + 6), Color.Gray, .62f);
    }

    private static void DrawChart()
    {
        Vector2 screen = StarfallUI.ScreenSizeUi;
        int screenWidth = (int)screen.X, screenHeight = (int)screen.Y;
        int width = Math.Min(920, screenWidth - 24), height = Math.Min(548, screenHeight - 24);
        Rectangle area = new((screenWidth - width) / 2, (screenHeight - height) / 2, width, height);
        Background(area, Text("ChartTitle"));
        if (OpenKind < 0) return;
        int leftWidth = Math.Clamp(width / 3, 230, 320);
        float rowHeight = (height - 65) / 17f;
        for (int i = 0; i < 17; i++)
        {
            Rectangle row = new(area.X + 10, area.Y + 45 + (int)(i * rowHeight), leftWidth - 16, (int)rowHeight - 2);
            bool cleared = VoyageWorld.IsDowned(i), unlocked = VoyageWorld.CanSummon(i);
            string state = cleared ? Text("ClearedShort") : unlocked ? Text("ReadyShort") : Text("LockedShort");
            string name = Language.GetTextValue("Mods.StarfallThrone.NPCs." + VoyageCatalog.Keys[i] + "NPC.DisplayName");
            // Tooltip contains the full localized name if a narrow layout needs an ellipsis.
            string title = (i + 1).ToString("00") + " " + name;
            while (FontAssets.MouseText.Value.MeasureString(title).X * .65f > row.Width - 42 && title.Length > 8)
                title = title[..^2] + "…";
            if (Button(row, title, .65f)) { selected = i; detailScroll = 0; }
            Label(state, new Vector2(row.Right - 32, row.Y + 4), cleared ? Color.LightGreen : unlocked ? Color.Gold : Color.Gray, .6f);
            if (row.Contains(Mouse())) Main.instance.MouseText(name);
        }
        float textX = area.X + leftWidth + 14, textY = area.Y + 49;
        int bodyWidth = area.Right - (int)textX - 14;
        Color accent = VoyageCatalog.Colors[selected];
        VoyageFurnitureDrawing.Icon(Main.spriteBatch, "Boss" + selected + "_Head_Boss", new Vector2(textX + 31, textY + 26), 54, Color.White);
        string bossName = Language.GetTextValue("Mods.StarfallThrone.NPCs." + VoyageCatalog.Keys[selected] + "NPC.DisplayName");
        Label(bossName, new Vector2(textX + 69, textY + 4), accent, .87f);
        Label(VoyageWorld.IsDowned(selected) ? Text("Cleared") : VoyageWorld.CanSummon(selected) ? Text("Ready") : Text("Locked"),
            new Vector2(textX + 69, textY + 29), Color.White, .76f);
        textY += 70;
        detailTop = textY;
        detailBottom = area.Bottom - 46;
        textY -= detailScroll;
        Item material = new(VoyageCatalog.Material(selected)), core = new(VoyageCatalog.Core(selected));
        Item summon = new(VoyageCatalog.Summon(selected));
        Wrapped(Text("Summon") + summon.Name, textX, ref textY, bodyWidth, Color.LightCyan);
        string gate = selected == 0 ? Text("OriginalFinal") : Language.GetTextValue("Mods.StarfallThrone.NPCs." + VoyageCatalog.Keys[selected - 1] + "NPC.DisplayName");
        Wrapped(Text("Requires") + gate, textX, ref textY, bodyWidth, Color.Silver);
        Wrapped(selected == 0 ? Text("FirstRecipe") : Text("LaterRecipe") + new Item(VoyageCatalog.Material(selected - 1)).Name + " ×10", textX, ref textY, bodyWidth, Color.Silver);
        textY += 8;
        Wrapped(Text("Material") + material.Name + " / " + core.Name, textX, ref textY, bodyWidth, accent);
        Wrapped(Text("MaterialUses"), textX, ref textY, bodyWidth, Color.White);
        Wrapped(Text("CoreUses"), textX, ref textY, bodyWidth, Color.Silver);
        int firstWeapon = selected < 16 ? selected * 2 : 32;
        string weaponNames = "";
        for (int i = firstWeapon; i < firstWeapon + (selected == 16 ? 4 : 2); i++)
            weaponNames += (i == firstWeapon ? "" : " / ") + new Item(VoyageCatalog.Weapon(i)).Name;
        Wrapped(Text("Weapons") + weaponNames, textX, ref textY, bodyWidth, Color.White);
        int plate = selected == 16 ? 4 : selected / 4;
        Wrapped(Text("Plate") + new Item(VoyageCatalog.Plate(plate)).Name, textX, ref textY, bodyWidth, Color.Silver);
        textY += 8;
        Wrapped(Text("Armor") + Text("ArmorTier" + (selected == 16 ? 5 : selected / 4 + 1)), textX, ref textY, bodyWidth, Color.LightGreen);
        Wrapped(Text("ArmorHelp"), textX, ref textY, bodyWidth, Color.Silver);
        int next = 0; while (next < 17 && VoyageWorld.IsDowned(next)) next++;
        string nextText = next == 17 ? Text("AllClear") : Text("Next") + new Item(VoyageCatalog.Summon(next)).Name;
        Wrapped(nextText, textX, ref textY, bodyWidth, Color.Gold);
        int maximumScroll = Math.Max(0, (int)(textY + detailScroll - detailBottom));
        if (Button(new Rectangle((int)textX, area.Bottom - 35, 70, 26), Text("ScrollUp"))) detailScroll = Math.Max(0, detailScroll - 80);
        if (Button(new Rectangle((int)textX + 80, area.Bottom - 35, 70, 26), Text("ScrollDown"))) detailScroll = Math.Min(maximumScroll, detailScroll + 80);
    }
    private static void Wrapped(string text, float x, ref float y, int width, Color color)
    {
        const float scale = .75f;
        string line = "";
        foreach (char c in text)
        {
            if (c == '\n' || FontAssets.MouseText.Value.MeasureString(line + c).X * scale > width)
            { if (y >= detailTop && y + 20 <= detailBottom) Label(line, new Vector2(x, y), color, scale); y += 21; line = ""; }
            if (c != '\n') line += c;
        }
        if (line.Length > 0) { if (y >= detailTop && y + 20 <= detailBottom) Label(line, new Vector2(x, y), color, scale); y += 21; }
    }

    public override void PostDrawTiles()
    {
        if (Main.gameMenu || Main.dedServ || !Main.LocalPlayer.GetModPlayer<VoyageUtilityPlayer>().Builder || Main.LocalPlayer.mouseInterface) return;
        Player player = Main.LocalPlayer;
        Item held = player.HeldItem;
        if (held.createTile < 0 && held.createWall < 0) return;
        int x = Player.tileTargetX, y = Player.tileTargetY;
        if (!WorldGen.InWorld(x, y, 10)) return;
        TileObjectData shape = held.createTile < 0 ? null : TileObjectData.GetTileData(held.createTile, held.placeStyle);
        int w = shape?.Width ?? 1, h = shape?.Height ?? 1;
        int left = x - (shape?.Origin.X ?? 0), top = y - (shape?.Origin.Y ?? 0);
        bool inRange = Math.Abs(x - player.Center.X / 16) <= Player.tileRangeX + player.blockRange + held.tileBoost
            && Math.Abs(y - player.Center.Y / 16) <= Player.tileRangeY + player.blockRange + held.tileBoost;
        Color color = inRange ? new Color(65, 220, 230, 130) : new Color(240, 90, 90, 130);
        // This is a footprint/range guide only; vanilla placement performs the real anchor,
        // collision and inventory checks, and still consumes the held building material.
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        Vector2 at = new Vector2(left * 16, top * 16) - Main.screenPosition;
        VoyageFurnitureDrawing.Rect(Main.spriteBatch, at + new Vector2(w * 8, 1), w * 16, 2, color);
        VoyageFurnitureDrawing.Rect(Main.spriteBatch, at + new Vector2(w * 8, h * 16 - 1), w * 16, 2, color);
        VoyageFurnitureDrawing.Rect(Main.spriteBatch, at + new Vector2(1, h * 8), 2, h * 16, color);
        VoyageFurnitureDrawing.Rect(Main.spriteBatch, at + new Vector2(w * 16 - 1, h * 8), 2, h * 16, color);
        Main.spriteBatch.End();
    }
}
