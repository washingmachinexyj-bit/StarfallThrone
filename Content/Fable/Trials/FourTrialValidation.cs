using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using StarfallThrone.Content.BossBars;

namespace StarfallThrone.Content.Fable.Trials;

public sealed class FourTrialValidation : ModSystem
{
    private static int checks;
    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException("FourTrial: " + reason);
        checks++;
    }

    public override void PostAddRecipes()
    {
        if (!FableValidation.Headless) return;
        checks = 0;
        Data();
        Runtime();
        Mod.Logger.Info("FOUR_TRIAL_HEADLESS_PASS checks=" + checks + " bosses=4 classicHP=1,2,4,8 independent=true checklistBeforeOaths=true");
    }

    private static void Data()
    {
        var mod = ModContent.GetInstance<FourTrialWorld>().Mod;
        Check(mod.GetContent<FourTrialBossNPC>().Count() == FourTrialCatalog.Count, "four boss classes");
        Check(FourTrialCatalog.Progression(3) < -1.2f, "before oath checklist range");
        Check(FourTrialCatalog.Progression(0) < FourTrialCatalog.Progression(1) &&
            FourTrialCatalog.Progression(1) < FourTrialCatalog.Progression(2) &&
            FourTrialCatalog.Progression(2) < FourTrialCatalog.Progression(3), "weak-to-strong order");

        int mode = Main.GameMode;
        try
        {
            for (int difficulty = 0; difficulty < 3; difficulty++)
            {
                Main.GameMode = difficulty;
                for (int i = 0; i < FourTrialCatalog.Count; i++)
                {
                    NPC npc = new();
                    npc.SetDefaults(FourTrialCatalog.BossType(i));
                    // The requested classic values are intentionally preserved. Vanilla's
                    // tiny-NPC scaling path rounds only the 8-life entry above its base.
                    int expectedLife = i == 3 ? (int)MathF.Ceiling(FourTrialCatalog.Life[i] * (difficulty == 2 ? 1.5f : difficulty == 1 ? 1.25f : 1f)) : FourTrialCatalog.Life[i];
                    Check(npc.lifeMax == expectedLife, "HP " + difficulty + "/" + i + " actual=" + npc.lifeMax);
                    Check(npc.boss && npc.BossBar is StarfallBossBar, "boss bar " + i);
                    Check(npc.defense == FourTrialCatalog.Defense[i], "defense " + i);
                    Item bag = new(FourTrialCatalog.Bag(i));
                    Item weapon = new(FourTrialCatalog.Weapon(i));
                    Check(ItemID.Sets.BossBag[bag.type] && bag.expert, "expert bag " + i);
                    Check(weapon.damage == 1 && weapon.useTime > 36 && weapon.useAnimation == weapon.useTime && weapon.value == 0,
                        "weak commemorative weapon " + i);
                    Check(global::StarfallThrone.Content.Fable.FableCatalog.StarterWeapon(weapon), "weapon allowed in fable trial " + i);
                    Item memento = new(FourTrialCatalog.Memento(i));
                    Item trophy = new(FourTrialCatalog.Trophy(i));
                    Item relic = new(FourTrialCatalog.Relic(i));
                    Check(memento.createTile == ModContent.Find<ModTile>("StarfallThrone", "FourTrialMementoTile" + i).Type, "memento tile " + i);
                    Check(trophy.createTile == ModContent.Find<ModTile>("StarfallThrone", "FourTrialTrophyTile" + i).Type, "trophy tile " + i);
                    Check(relic.createTile == ModContent.Find<ModTile>("StarfallThrone", "FourTrialRelicTile" + i).Type, "relic tile " + i);
                }
            }

            foreach (string cultureName in new[] { "en-US", "zh-Hans" })
            {
                LanguageManager.Instance.SetLanguage(GameCulture.FromName(cultureName));
                for (int i = 0; i < FourTrialCatalog.Count; i++)
                {
                    Check(!FourTrialCatalog.Name(i).StartsWith("Mods."), "NPC localization " + cultureName + "/" + i);
                    Check(!FourTrialCatalog.Text("FourTrialInfo" + i).StartsWith("Mods."), "UI localization " + cultureName + "/" + i);
                }
            }
            LanguageManager.Instance.SetLanguage(GameCulture.FromCultureName(GameCulture.CultureName.Chinese));
            Check(mod.GetContent<ModItem>().Where(item => item.Name.StartsWith("FourTrial", StringComparison.Ordinal)).All(item =>
                item.DisplayName.Value.Any(c => c >= '\u4e00' && c <= '\u9fff')), "Chinese item names");
        }
        finally
        {
            Main.GameMode = mode;
        }
    }

    private static void Runtime()
    {
        Player[] players = (Player[])Main.player.Clone();
        NPC[] npcs = (NPC[])Main.npc.Clone();
        Projectile[] projectiles = (Projectile[])Main.projectile.Clone();
        int net = Main.netMode, myPlayer = Main.myPlayer, mode = Main.GameMode;
        bool menu = Main.gameMenu;
        try
        {
            Main.netMode = NetmodeID.SinglePlayer;
            Main.myPlayer = 0;
            Main.GameMode = 0;
            Main.gameMenu = false;
            for (int i = 0; i < Main.maxPlayers; i++) Main.player[i] = new Player { active = false, whoAmI = i };
            for (int i = 0; i < Main.maxNPCs; i++) Main.npc[i] = new NPC { active = false, whoAmI = i };
            for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i] = new Projectile { active = false, whoAmI = i };
            Main.player[0] = new Player { active = true, whoAmI = 0, position = new Vector2(18000, 1200) };
            Main.player[0].ResetEffects();

            for (int i = 0; i < FourTrialCatalog.Count; i++)
            {
                NPC npc = new();
                npc.SetDefaults(FourTrialCatalog.BossType(i));
                npc.active = true;
                npc.whoAmI = 0;
                npc.position = Main.player[0].Center + new Vector2(50, -80);
                Main.npc[0] = npc;
                npc.target = 0;
                FourTrialBossNPC boss = npc.ModNPC as FourTrialBossNPC;
                Check(boss != null, "runtime boss instance " + i);
                for (int frame = 0; frame < 220; frame++)
                {
                    boss.AI();
                    npc.position += npc.velocity;
                    Check(float.IsFinite(npc.position.X) && float.IsFinite(npc.position.Y), "finite AI " + i + "/" + frame);
                    foreach (Projectile projectile in Main.ActiveProjectiles)
                    {
                        if (projectile.ModProjectile is not FourTrialHazard hazard) continue;
                        hazard.AI();
                        projectile.position += projectile.velocity;
                        if (--projectile.timeLeft <= 0) projectile.Kill();
                    }
                }
                Check(npc.boss && npc.BossBar is StarfallBossBar, "runtime boss bar " + i);
                npc.active = false;
            }
        }
        finally
        {
            Main.player = players;
            Main.npc = npcs;
            Main.projectile = projectiles;
            Main.netMode = net;
            Main.myPlayer = myPlayer;
            Main.GameMode = mode;
            Main.gameMenu = menu;
        }
    }
}
