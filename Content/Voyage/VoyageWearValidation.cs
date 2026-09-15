using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using StarfallThrone.Content.Voyage.Equipment;

namespace StarfallThrone.Content.Voyage;

public static class VoyageWearValidation
{
    public static void Run()
    {
        if (!Main.gameMenu || !Environment.GetCommandLineArgs().Contains("-starfall-art-smoke"))
            throw new InvalidOperationException("Wear rendering is restricted to isolated menu tests.");
        var device = Main.instance.GraphicsDevice;
        var targets = device.GetRenderTargets();
        Vector2 screen = Main.screenPosition;
        using var atlas = new RenderTarget2D(device, 1000, 900);
        try
        {
            device.SetRenderTarget(atlas); device.Clear(new Color(16, 22, 36));
            Main.screenPosition = Vector2.Zero;
            for (int id = 0; id < 89; id++)
            {
                Player p = new() { active = true, whoAmI = 0, isDisplayDollOrInanimate = true, direction = id % 2 == 0 ? 1 : -1 };
                p.ResetEffects();
                int tier = id < 72 ? id / 12 : 0, style = id < 72 ? id / 3 % 4 : 0;
                p.head = new Item(id < 72 ? VoyageEquipmentData.Armor(tier * 6 + style) : VoyageCatalog.Mask(id - 72)).headSlot;
                p.body = new Item(VoyageEquipmentData.Armor(tier * 6 + 4)).bodySlot;
                p.legs = new Item(VoyageEquipmentData.Armor(tier * 6 + 5)).legSlot;
                int pose = id < 72 ? id % 3 : 0;
                p.bodyFrame = new Rectangle(0, (pose == 1 ? 3 : 0) * 56, 40, 56);
                p.legFrame = new Rectangle(0, (pose == 2 ? 10 : 0) * 56, 40, 56);
                Vector2 at = new(id % 10 * 100 + 40, id / 10 * 100 + 20);
                p.position = at;
                Main.PlayerRenderer.DrawPlayer(Main.Camera, p, at, 0, Vector2.Zero, 0, 1.4f);
                Main.spriteBatch.Begin();
                Utils.DrawBorderString(Main.spriteBatch, id < 72 ? $"T{tier+1} C{style+1} P{pose}" : $"Mask {id-71}",
                    new Vector2(id % 10 * 100 + 8, id / 10 * 100 + 80), Color.LightSteelBlue, .5f);
                Main.spriteBatch.End();
            }
            string path = Path.GetFullPath(Path.Combine(Main.SavePath,"..","..","outputs","voyage-wear-runtime.png"));
            using (var stream = File.Create(path)) atlas.SaveAsPng(stream, atlas.Width, atlas.Height);
            ModContent.GetInstance<VoyageWorld>().Mod.Logger.Info("VOYAGE_WEAR_RENDER_PASS armor-loadouts=24 poses=3 masks=17 engine-player-renderer=true");
        }
        finally { device.SetRenderTargets(targets); Main.screenPosition = screen; }
    }
}
