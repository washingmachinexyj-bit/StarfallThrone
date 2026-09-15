# Mining slice handoff

Owned files: `Content/Oaths/Mining/*`, `Localization/OathsMining/{en-US,zh-Hans}.hjson`, `work/oaths-mining-art/*`, and exactly 18 `Content/Assets/Oaths/Oath{Ore,Bar,Pick,Station,OreTile,StationTile}{0,1,2}.png` files. Old mining files and global arrays are untouched.

## Main hooks

- Call `OathMiningWorld.Unlock(i)` immediately after authoritative, legitimate `OathWorld.SupremeWon[i] = true`. Idempotent; the public entry point rejects clients and absent victory flags.
- `OathMiningValidation.Data()` after item/tile/recipe setup.
- `OathMiningValidation.Run()` in the isolated native fixture, after a minimum 1024x640 `Main.tile` buffer exists. The fixture accepts menu `-starfall-art-smoke` or dedicated `-testservermodloading` only. It never loads/saves real worlds, never reallocates shared tile buffers, and restores touched cell data, arrays, config, readonly old-mining collections and world state in `finally`.
- Expected markers: `OATHS_MINING_DATA_PASS`, `OATHS_MINING_RUN_PASS`.
- ModSystem save/net/update hooks are automatic. No packet registration, UI registration or old MiningWorld array extension needed.

## Content and progression

- Item IDs `OathOre0..2`, `OathBar0..2`, `OathPick0..2`, `OathStation0..2`; tile IDs `OathOreTile0..2`, `OathStationTile0..2`.
- All ore requires exactly 600% pick power. Existing `MiningCatalog.ToolPower[13] == 600` and existing area tool `MiningPlayer` checks ModTile.MinPick, so no old mining edits needed.
- Picks 610/630/650: own SupremeMaterial x10 at TriuneAltarTile, own victory condition. No ore or bar prerequisite.
- Each independent station: own SupremeMaterial x20 + PantheonMaterial16 x10 + StoneBlock x30 at TriuneAltarTile, own victory condition. Does not depend on a different supreme kill or a new ore.
- Own station smelts 4 ore into 1 bar. Own material x1 + StoneBlock x10 cultivates 8 ore at own station. Both check own world victory; imported materials cannot unlock recipes.
- Stations count as work benches only, not as each other. Equipment agent consumes the public OathCatalog.Bar/Station APIs.
- 12 ModItems, 6 ModTiles, 3 ore types, 3 stations, 3 new tools. All mining item sale values zero to avoid renewable money farms.

## Generation

- Underground-only patches; max1200 tiles per ore, max6000 attempts, max4 small patch attempts per ore per update. No global terrain scans. Skip liquid, colored/wired/actuated tiles, slopes, non-natural stone, structures, protected regions, edited chunks, housing walls, spawn, nearby players, chests, NPC homes and tile entities through existing public `MiningWorld.CanGenerateAt` / `IsProtected`, plus conservative 13x13 neighborhood checks.
- Save stores started/done/cursor/placed/RNG for each independent job. Mid-job load resumes rather than reseeding. Repeated kills cannot regenerate. Exhausted or fully protected worlds retain renewable cultivation. Future schemas preserved losslessly and generation fails closed.
- Honors existing `MiningWorldConfig.GenerateOnFutureFirstKills`: disabled unlocks cultivation without edits. Disabling it mid-job ends the job safely; turning it on later does not retroactively rerun that completed job.
- Only server edits/sends tile squares; localization-backed queue/completion/cultivation messages. WorldData sync reads into staged arrays before mutation.
- No retroactive automatic generation on merely loading an old victory without a job; cultivation remains usable. Normal new victory always supplies a job.

## Verification status

- Isolated `SliceCheck.csproj` compiles only these three mining C# files against installed native tML assemblies, using explicit compile-only contract stubs for other slices. Passed with zero warnings/errors. This is NOT a full mod build or runtime pass.
- C#/GDI asset generator ran successfully. `audit.ps1` checks all 18 dimensions and every native 18px gutter. The generator extends the existing code-native icon/furniture pipeline; no boss art is replaced.
- Runtime Data/Run fixtures are implemented but MUST be invoked by main in integrated headless tests before release. They test actual protected generation, job resume, repeat kills, tile counts, client refusal, opt-out persistence, finite exhaustion, native 599/600 pick gates, native placed 3x3 station frames, drop rules and recipe cycles.
- No shared project build, installation, real world/player writes, or child agents were performed.
