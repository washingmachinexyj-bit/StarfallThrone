using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;
using Terraria.Localization;
using StarfallThrone.Content.Voyage;
using StarfallThrone.Content.Voyage.Bosses;
using StarfallThrone.Content.Voyage.Weapons;
using StarfallThrone.Content.Voyage.Equipment;
using StarfallThrone.Content.Voyage.Utility;
using StarfallThrone.Content.Mining;
using StarfallThrone.Content.Divine;
using StarfallThrone.Content.Divine.Bosses;
using StarfallThrone.Content.Divine.Equipment;
using StarfallThrone.Content.Ecology;
using StarfallThrone.Content.Ecology.Combat;
using StarfallThrone.Content.Ecology.Equipment;

namespace StarfallThrone.Content.Systems;

// Fail early with a useful path instead of an invisible asset in an ongoing fight.
public sealed class ArtIntegritySystem : ModSystem
{
    private int smokeMenuFrames;

    public override void Unload()
    {
        Main.OnPostDraw -= FinishSmokeTest;
    }

    private void FinishSmokeTest(GameTime gameTime)
    {
        // Main menu 0 is reached only after ModContent.Load and Steam bookkeeping finish.
        if (!Main.gameMenu || Main.menuMode != 0)
            return;
        if (++smokeMenuFrames < 120)
            return;
        Main.OnPostDraw -= FinishSmokeTest;
        // Independent throwaway fixtures restore their own state. Collect failures so one
        // tooltip assertion does not conceal a separate world/weapon regression until the next launch.
        var failures = new System.Collections.Generic.List<Exception>();
        void RunCheck(Action check)
        {
            var seedFlags=global::StarfallThrone.Content.Oaths.OathWorld.SeedWon;
            global::StarfallThrone.Content.Oaths.OathWorld.SeedWon=new[]{true,true,true}; // Existing timed-god fixtures begin with their newly required opening victories.
            try { check(); }
            catch (Exception error) { failures.Add(error); Mod.Logger.Error("ISOLATED_VALIDATION_FAILURE " + check.Method.DeclaringType?.Name, error); }
            finally { global::StarfallThrone.Content.Oaths.OathWorld.SeedWon=seedFlags; }
        }
        RunCheck(ValidateReadyRecipes);
        RunCheck(MiniBossValidation.SimulateCombat);
        RunCheck(FormalCombatValidation.Run);
        RunCheck(VoyageSupportValidation.Run);
        RunCheck(VoyageBossValidation.Run);
        RunCheck(VoyageWeaponValidation.Run);
        RunCheck(VoyageUtilityValidation.Run);
        RunCheck(VoyageWearValidation.Run);
        RunCheck(MiningValidation.Run);
        RunCheck(DivineCombatValidation.Run);
        RunCheck(DivineValidation.Run);
        RunCheck(DivineEquipmentValidation.Run);
        RunCheck(EcologyValidation.Run);
        RunCheck(EcologyContentValidation.Runtime);
        RunCheck(EcologyCombatValidation.Run);
        RunCheck(EcologyEquipmentValidation.Run);
        RunCheck(global::StarfallThrone.Content.Ascendant.AscendantValidation.Run);
        RunCheck(global::StarfallThrone.Content.Ascendant.Bosses.AscendantCombatValidation.Run);
        RunCheck(global::StarfallThrone.Content.Ascendant.Equipment.AscendantEquipmentValidation.Run);
        RunCheck(global::StarfallThrone.Content.Pantheon.PantheonValidation.Run);
        RunCheck(global::StarfallThrone.Content.Pantheon.Bosses.PantheonCombatValidation.Run);
        RunCheck(global::StarfallThrone.Content.Pantheon.Equipment.PantheonEquipmentValidation.Run);
        RunCheck(global::StarfallThrone.Content.Fable.FableValidation.Run);
        RunCheck(global::StarfallThrone.Content.Oaths.OathValidation.RunAll);
        if (failures.Count != 0) throw new AggregateException("Isolated validation failed", failures);
        Mod.Logger.Info("ART_SMOKE_MENU_PASS: successful load reached the main menu and rendered 120 frames.");
        Main.instance.Exit();
    }

    public override void PostSetupContent()
    {
        int bosses = Mod.GetContent<ModNPC>().Count(n => n is NPCs.AscendantBossNPC or NPCs.PrimordialBossNPC);
        if (bosses != 27)
            throw new InvalidOperationException($"Expected 27 StarfallThrone bosses, got {bosses}.");
        int voyage = Mod.GetContent<ModNPC>().Count(n => n is VoyageBossNPC);
        if (voyage != 17) throw new InvalidOperationException($"Expected 17 Voyage bosses, got {voyage}.");
        int divine = Mod.GetContent<ModNPC>().Count(n => n is DivineBossNPC);
        if (divine != 3) throw new InvalidOperationException($"Expected 3 Divine bosses, got {divine}.");
        int ecology = Mod.GetContent<ModNPC>().Count(n => n is EcologyBossNPC);
        if (ecology != 10) throw new InvalidOperationException($"Expected 10 Ecology bosses, got {ecology}.");
        int trials=Mod.GetContent<ModNPC>().Count(n=>n is global::StarfallThrone.Content.Ascendant.Bosses.AscendantBossNPC);
        if(trials!=3)throw new InvalidOperationException($"Expected 3 True God Trials, got {trials}.");
        int pantheon=Mod.GetContent<ModNPC>().Count(n=>n is global::StarfallThrone.Content.Pantheon.Bosses.PantheonBossNPC);
        if(pantheon!=17)throw new InvalidOperationException($"Expected 17 Pantheon bosses, got {pantheon}.");
        Mod.Logger.Info($"Art release: registered {bosses} bosses and {Mod.GetContent<ModItem>().Count()} items.");
        int minis = Mod.GetContent<ModNPC>().Count(n => n is NPCs.MiniBossNPC);
        int fables = Mod.GetContent<ModNPC>().Count(n => n is global::StarfallThrone.Content.Fable.Bosses.FableBossNPC);
        int oaths=Mod.GetContent<ModNPC>().Count(n=>n is global::StarfallThrone.Content.Oaths.Seeds.SeedBossNPC or global::StarfallThrone.Content.Oaths.Bosses.SupremeBossNPC);
        if(oaths!=6)throw new InvalidOperationException("Expected six oath gods");
        if(fables!=18)throw new InvalidOperationException("Expected18Fableminibosses");
        if (minis != MiniBossData.Count) throw new InvalidOperationException($"Expected {MiniBossData.Count} minibosses, got {minis}.");
        if (Main.dedServ)
            return;
        int count = 0;
        foreach (string path in Mod.GetFileNames().Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".rawimg", StringComparison.OrdinalIgnoreCase)))
        {
            // Only runtime assets are packaged; generated source atlases stay outside the tmod.
            string assetPath = path[..path.LastIndexOf('.')];
            Texture2D texture = ModContent.Request<Texture2D>($"{Mod.Name}/{assetPath}", AssetRequestMode.ImmediateLoad).Value;
            if (assetPath.EndsWith("_Body") && (texture.Width != 360 || texture.Height != 224))
                throw new InvalidOperationException($"Invalid composite armor sheet: {path}");
            if ((assetPath.EndsWith("_Head") || assetPath.EndsWith("_Legs")) && (texture.Width != 40 || texture.Height != 1120))
                throw new InvalidOperationException($"Invalid armor animation sheet: {path}");
            if (assetPath.EndsWith("MiniDisplayTile") && (texture.Width != 54 * MiniBossData.Count || texture.Height != 54))
                throw new InvalidOperationException("Miniboss furniture sheet style count mismatch");
            count++;
        }
        foreach (ModNPC npc in Mod.GetContent<ModNPC>())
            if ((npc is NPCs.AscendantBossNPC or NPCs.PrimordialBossNPC or NPCs.MiniBossNPC or VoyageBossNPC or DivineBossNPC or EcologyBossNPC or global::StarfallThrone.Content.Ascendant.Bosses.AscendantBossNPC or global::StarfallThrone.Content.Pantheon.Bosses.PantheonBossNPC) && NPCHeadLoader.GetBossHeadSlot(npc.BossHeadTexture) < 0)
                throw new InvalidOperationException($"Boss head not registered: {npc.Name}");
        foreach(ModNPC npc in Mod.GetContent<ModNPC>())if(npc is global::StarfallThrone.Content.Oaths.Seeds.SeedBossNPC or global::StarfallThrone.Content.Oaths.Bosses.SupremeBossNPC or global::StarfallThrone.Content.Fable.Bosses.FableBossNPC)
            if(NPCHeadLoader.GetBossHeadSlot(npc.BossHeadTexture)<0)throw new InvalidOperationException("Boss head not registered: "+npc.Name);
        Mod.Logger.Info($"Art release: loaded and validated {count} PNG textures and {bosses + minis + voyage + divine + ecology + trials + pantheon + fables + oaths} boss/miniboss-head registrations.");
    }

    public override void PostAddRecipes()
    {
        if (Main.dedServ || !Terraria.Program.LaunchParameters.ContainsKey("-starfall-art-smoke"))
            return;
        // Recipe migrations run in other PostAddRecipes hooks. Validate after ALL
        // hooks finish, not depending on the registration order of the systems.
        Main.QueueMainThreadAction(() => Main.OnPostDraw += FinishSmokeTest);
    }

    private void ValidateReadyRecipes()
    {
        LanguageManager.Instance.SetLanguage(GameCulture.FromCultureName(GameCulture.CultureName.Chinese));
        string[] untranslated = Mod.GetContent<ModItem>()
            .Where(item => !item.DisplayName.Value.Any(c => c >= '\u4e00' && c <= '\u9fff'))
            .Select(item => item.Name + "=" + item.DisplayName.Value).ToArray();
        if (untranslated.Length != 0)
            throw new InvalidOperationException("Missing Chinese item names: " + string.Join(", ", untranslated));
        if (ModLoader.TryGetMod("BossChecklist", out Mod checklist))
        {
            var entries = checklist.Call("GetBossInfoDictionary", Mod, "2.0.0") as System.Collections.IDictionary;
            int registered = entries?.Keys.Cast<object>().Count(key => key.ToString()!.StartsWith(Mod.Name, StringComparison.Ordinal)) ?? 0;
            int expected = 101 + MiniBossData.Count + global::StarfallThrone.Content.Fable.Trials.FourTrialCatalog.Count;
            if (registered != expected)
                throw new InvalidOperationException($"Boss Checklist expected {expected} entries, got {registered}.");
            for (int i = 0; i < MiniBossData.Count; i++)
            {
                var entry = entries[$"{Mod.Name} {MiniBossData.Keys[i]}"] as System.Collections.IDictionary;
                if (entry == null || !Equals(entry["isMiniboss"], true) || Convert.ToSingle(entry["progression"]) != MiniBossData.Progression(i) || Convert.ToSingle(entry["progression"]) >= PrimordialBossData.Progression(0))
                    throw new InvalidOperationException($"Invalid mini classification/order: {MiniBossData.Keys[i]}");
                if (entry["loot"] is not System.Collections.Generic.List<int> loot || !loot.Contains(MiniBossData.MaterialType(i)))
                    throw new InvalidOperationException($"Missing mini loot entry: {MiniBossData.Keys[i]}");
            }
            for (int i = 0; i < 17; i++)
            {
                var entry = entries[$"{Mod.Name} {VoyageCatalog.Keys[i]}"] as System.Collections.IDictionary;
                if (entry == null || !Equals(entry["isBoss"], true) || Convert.ToSingle(entry["progression"]) != VoyageCatalog.Progression(i))
                    throw new InvalidOperationException("Invalid Voyage Checklist entry " + i);
            }
            for(int i=0;i<17;i++)
            {
                string pantheonKey=global::StarfallThrone.Content.Pantheon.PantheonCatalog.Keys[i];
                var entry=entries[$"{Mod.Name} Pantheon{pantheonKey}"] as System.Collections.IDictionary;
                if(entry==null || !Equals(entry["isBoss"],true) || Convert.ToSingle(entry["progression"])!=global::StarfallThrone.Content.Pantheon.PantheonCatalog.Progression(i))
                    throw new InvalidOperationException("Invalid Pantheon Checklist entry "+i);
            }
            Mod.Logger.Info("ART_SMOKE_CHECKLIST_PASS: 109 entries present, including all previous entries and six oath gods.");
        }
        MiniBossValidation.Run();
        FormalCombatValidation.Data();
        VoyageBossValidation.Data();
        VoyageSupportValidation.Data();
        VoyageWeaponValidation.ValidateRegistered(message => Mod.Logger.Info(message));
        VoyageEquipmentValidation.Validate();
        VoyageUtilityValidation.Validate();
        MiningValidation.Data();
        DivineValidation.Data();
        DivineCombatValidation.Data();
        DivineEquipmentValidation.Data();
        EcologyValidation.Data();
        EcologyContentValidation.Data();
        EcologyCombatValidation.Data();
        EcologyEquipmentValidation.Data();
        global::StarfallThrone.Content.Ascendant.AscendantValidation.Data();
        global::StarfallThrone.Content.Ascendant.Bosses.AscendantCombatValidation.Data();
        global::StarfallThrone.Content.Ascendant.Equipment.AscendantEquipmentValidation.Data();
        global::StarfallThrone.Content.Pantheon.PantheonValidation.Data();
        global::StarfallThrone.Content.Pantheon.Bosses.PantheonCombatValidation.Data();
        global::StarfallThrone.Content.Pantheon.Equipment.PantheonEquipmentValidation.Data();
        global::StarfallThrone.Content.Fable.FableValidation.Data();
        Mod.Logger.Info("ART_SMOKE_PASS: all textures loaded, 109 boss/miniboss heads registered, all item names localized in Chinese, recipes loaded.");
    }
}
