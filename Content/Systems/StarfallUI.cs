using Microsoft.Xna.Framework;
using Terraria;

namespace StarfallThrone.Content.Systems;

/// <summary>
/// Coordinate helpers for legacy UI layers.
///
/// A LegacyGameInterfaceLayer with InterfaceScaleType.UI does two things before
/// it invokes the draw delegate: it begins a SpriteBatch with UIScaleMatrix and
/// temporarily changes Main.mouseX/Main.mouseY/Main.screenWidth/Main.screenHeight
/// to the already-scaled UI coordinate space. Applying the inverse matrix here
/// therefore scales the pointer a second time. These helpers intentionally read
/// the values as they exist inside that delegate.
/// </summary>
internal static class StarfallUI
{
    public static Point MouseUiPoint
        => Main.MouseScreen.ToPoint();

    public static Vector2 ScreenSizeUi
        => new(Main.screenWidth, Main.screenHeight);
}
