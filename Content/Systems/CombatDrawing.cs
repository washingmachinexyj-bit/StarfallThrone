using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace StarfallThrone.Content.Systems;

public static class CombatDrawing
{
    public static void Disc(SpriteBatch batch, Vector2 center, float radius, Color color)
    {
        for (float y = -radius; y <= radius; y += 5)
        {
            float x = MathF.Sqrt(Math.Max(0, radius * radius - y * y));
            Line(batch, center + new Vector2(-x, y), center + new Vector2(x, y), color, 5);
        }
    }
    public static void Line(SpriteBatch batch, Vector2 start, Vector2 end, Color color, float width)
    {
        Vector2 delta = end - start;
        batch.Draw(TextureAssets.MagicPixel.Value, start, new Rectangle(0, 0, 1, 1), color, delta.ToRotation(), new Vector2(0, .5f), new Vector2(delta.Length(), width), SpriteEffects.None, 0);
    }
    public static void Circle(SpriteBatch batch, Vector2 center, float radius, Color color, float width, float gapAngle = 0, float gapWidth = 0)
    {
        for (int i = 0; i < 48; i++)
        {
            float angle = MathHelper.TwoPi * i / 48;
            if (Math.Abs(MathHelper.WrapAngle(angle - gapAngle)) < gapWidth) continue;
            Line(batch, center + angle.ToRotationVector2() * radius, center + (angle + MathHelper.TwoPi / 48).ToRotationVector2() * radius, color, width);
        }
    }
}
