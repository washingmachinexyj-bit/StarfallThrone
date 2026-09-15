using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using global::StarfallThrone.Content.Systems;

namespace StarfallThrone.Content.Voyage.Utility;

/// <summary>Engine-native frames around the generated encounter art, shared by items and tiles.</summary>
public static class VoyageFurnitureDrawing
{
    public static void Rect(SpriteBatch batch, Vector2 center, float w, float h, Color color)
        => batch.Draw(TextureAssets.MagicPixel.Value, center, new Rectangle(0, 0, 1, 1), color, 0,
            new Vector2(.5f), new Vector2(w, h), SpriteEffects.None, 0);

    public static void Icon(SpriteBatch batch, string asset, Vector2 center, float size, Color color, float rotation = 0)
    {
        Texture2D texture = ModContent.Request<Texture2D>(VoyageCatalog.Root + asset).Value;
        batch.Draw(texture, center, null, color, rotation, texture.Size() / 2,
            size / Math.Max(texture.Width, texture.Height), SpriteEffects.None, 0);
    }

    private static void Edge(SpriteBatch b, Vector2 c, Vector2 a, Vector2 z, float scale, Color color, float width = 2)
        => CombatDrawing.Line(b, c + a * scale, c + z * scale, color, width * scale);

    public static void Draw(SpriteBatch batch, Vector2 center, int theme, int kind, float scale, Color lighting)
    {
        Color accent = VoyageCatalog.Colors[theme].MultiplyRGB(lighting);
        Color metal = new Color(75, 91, 113).MultiplyRGB(lighting);
        Color dark = new Color(19, 26, 40).MultiplyRGB(lighting);
        if (kind == 0)
        {
            // Navigation plaques: polygon bulkhead, colored status studs, full unique boss insignia.
            Rect(batch, center, 42 * scale, 34 * scale, dark);
            int sides = 4 + theme % 3;
            for (int k = 0; k < sides; k++)
            {
                float angle = k * MathHelper.TwoPi / sides + MathHelper.PiOver4;
                float next = (k + 1) * MathHelper.TwoPi / sides + MathHelper.PiOver4;
                Edge(batch, center, angle.ToRotationVector2() * new Vector2(23, 20), next.ToRotationVector2() * new Vector2(23, 20), scale, metal, 3);
            }
            Icon(batch, "Boss" + theme + "_Head_Boss", center, 30 * scale, lighting);
            for (int k = 0; k <= theme % 4; k++)
                Rect(batch, center + new Vector2(-17 + k * 5, 17) * scale, 3 * scale, 2 * scale, accent);
        }
        else if (kind == 1)
        {
            // An upright lamp, with a caged floating material specimen and asymmetric antenna.
            Rect(batch, center + new Vector2(0, 20) * scale, 27 * scale, 5 * scale, dark);
            Rect(batch, center + new Vector2(0, 17) * scale, 19 * scale, 3 * scale, metal);
            Edge(batch, center, new(-7, 15), new(-7, -13), scale, metal, 3);
            Edge(batch, center, new(7, 15), new(7, -13), scale, metal, 3);
            Edge(batch, center, new(-11, -13), new(11, -13), scale, accent);
            Edge(batch, center, new(theme % 2 == 0 ? -7 : 7, -13), new(theme % 2 == 0 ? -14 : 14, -21), scale, metal);
            CombatDrawing.Circle(batch, center + new Vector2(0, -3) * scale, 12 * scale, accent * .45f, scale);
            Icon(batch, "Material" + theme, center + new Vector2(0, -3) * scale, 20 * scale, Color.Lerp(lighting, Color.White, .3f));
            Rect(batch, center + new Vector2(0, 14) * scale, 11 * scale, 2 * scale, accent);
        }
        else if (kind == 2)
        {
            // Solid souvenir statue: stepped pedestal, embossed miniature, bronze rim.
            Rect(batch, center + new Vector2(0, 20) * scale, 40 * scale, 5 * scale, dark);
            Rect(batch, center + new Vector2(0, 16) * scale, 32 * scale, 4 * scale, metal);
            Rect(batch, center + new Vector2(0, 13) * scale, 22 * scale, 3 * scale, accent);
            Icon(batch, "Boss" + theme + "_Head_Boss", center + new Vector2(0, -6) * scale, 35 * scale, lighting.MultiplyRGB(new Color(215, 202, 182)));
            for (int k = 0; k < 2; k++)
                Edge(batch, center, new(-16 + k * 32, 12), new(-12 + k * 24, -1), scale, accent, 2);
            Icon(batch, "Core" + theme, center + new Vector2(0, 18) * scale, 7 * scale, lighting);
        }
        else if (kind == 3)
        {
            Rect(batch, center, 42 * scale, 7 * scale, dark);
            Rect(batch, center + new Vector2(0, -3) * scale, 42 * scale, 3 * scale, accent);
            for (int k = -1; k <= 1; k++)
                Edge(batch, center, new(k * 13 - 4, 3), new(k * 13 + 3, 10), scale, metal, 3);
        }
        else
        {
            Rect(batch, center, 31 * scale, 31 * scale, dark);
            Rect(batch, center, 27 * scale, 27 * scale, metal.MultiplyRGB(VoyageCatalog.Colors[theme]));
            Icon(batch, "Material" + theme, center, 19 * scale, lighting);
            Edge(batch, center, new(-13, -13), new(13, -13), scale, accent);
            Edge(batch, center, new(-13, -13), new(-13, 13), scale, accent);
        }
    }

    public static void BlockMark(SpriteBatch batch, Vector2 center, int theme, float scale, Color lighting)
    {
        Color tint = VoyageCatalog.Colors[theme].MultiplyRGB(lighting) * .5f;
        Rect(batch, center + new Vector2(0, -6) * scale, 13 * scale, scale, tint);
        Rect(batch, center + new Vector2(-6, 0) * scale, scale, 12 * scale, tint);
        if ((theme & 1) == 0) Icon(batch, "Material" + theme, center, 8 * scale, lighting * .6f);
        else Edge(batch, center, new(-3, -3), new(3, 3), scale, tint, 1);
    }

    public static void DrawUtility(SpriteBatch batch, Vector2 center, int utility, float scale, Color lighting, int phase)
    {
        Icon(batch, "Utility" + utility, center, 46 * scale, lighting);
        float t = (float)Main.GlobalTimeWrappedHourly;
        Color teal = new Color(85, 225, 235).MultiplyRGB(lighting);
        if (utility == 0)
        {
            for (int k = 0; k < 3; k++)
                Rect(batch, center + new Vector2(-13 + k * 5, 11) * scale, 3 * scale, 2 * scale, teal * (.45f + .35f * MathF.Sin(t * 2 + k)));
        }
        else if (utility == 3)
        {
            Rect(batch, center + new Vector2(11, -11) * scale, 9 * scale, 2 * scale, Color.Gold.MultiplyRGB(lighting));
            Rect(batch, center + new Vector2(11, -11) * scale, 2 * scale, 9 * scale, Color.Gold.MultiplyRGB(lighting));
        }
        else if (utility == 5)
        {
            for (int k = 0; k < 3; k++)
                Rect(batch, center + new Vector2(MathF.Sin(t * .7f + k * 2) * 11, -8 + MathF.Cos(t + k) * 8) * scale,
                    scale, 2 * scale, Color.LightGreen.MultiplyRGB(lighting));
        }
        else if (utility == 7)
        {
            Vector2 above = center + new Vector2(0, -7) * scale;
            CombatDrawing.Circle(batch, above, 14 * scale, teal * .5f, scale);
            Edge(batch, above, Vector2.Zero, (t * .25f).ToRotationVector2() * 13, scale, teal, 1);
            for (int k = 0; k < 4; k++)
                Rect(batch, above + (k * 1.7f + 1).ToRotationVector2() * (7 + k % 2 * 5) * scale, 2 * scale, 2 * scale, teal);
        }
        else if (utility == 8)
        {
            Vector2 above = center + new Vector2(0, -7) * scale;
            Color gold = Color.Gold.MultiplyRGB(lighting);
            if (phase == 0)
            {
                Icon(batch, "Boss16_Head_Boss", above, 28 * scale, lighting);
                Edge(batch, above, new(-12, 3), new(-21, -8), scale, gold);
                Edge(batch, above, new(12, 3), new(21, -8), scale, gold);
            }
            else if (phase == 1)
            {
                Icon(batch, "Boss16_Head_Boss", above, 23 * scale, lighting);
                CombatDrawing.Circle(batch, above, 17 * scale, gold * .7f, scale, t, .4f);
                CombatDrawing.Circle(batch, above, 10 * scale, teal * .6f, scale, -t, .4f);
            }
            else
            {
                Icon(batch, "Core16", above, 27 * scale, Color.Lerp(lighting, Color.White, .3f));
                for (int k = 0; k < 6; k++)
                    Edge(batch, above, (t * .2f + k * MathHelper.Pi / 3).ToRotationVector2() * 13,
                        (t * .2f + k * MathHelper.Pi / 3).ToRotationVector2() * 19, scale, gold, 1);
            }
        }
    }
}
