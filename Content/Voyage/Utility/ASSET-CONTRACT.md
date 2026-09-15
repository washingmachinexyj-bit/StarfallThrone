# Voyage utility asset contract

This slice uses ONLY existing agreed `Utility0.png` through `Utility8.png`,
`Boss{i}_Head_Boss.png`, `Material{i}.png`, `Core{i}.png` and the vanilla stone/platform
frame atlas. No extra tile sheets or furniture inventory images are required.

Multitile sprites are manually drawn once at the origin (3 x 3 tiles, 48 x 48 pixels)
with proper off-screen offsets and visibility checks. Their registered texture is
the relevant utility icon; default atlas drawing is suppressed. Decorative
plaque/lamp/statue styles use the generated Boss heads and materials with different
purpose-built frames. Platforms and blocks use native frame geometry, tinted by
theme and decorated with the generated material icon. Inventory furniture icons
are custom drawn to match placed forms.

Main integration: route packet byte **22** to
`VoyageUtilityPlayer.ReceivePacket(BinaryReader reader, int sender)`.
It handles validated repair-station use, recall initiation and display phase switch.
Storage uses a separate 40-slot saved ModPlayer inventory, like vanilla personal
banks. Only the owning local player's UI can edit it; no shared world chest, no
storage contents accepted over a custom network packet and no content drop on break.

Compile check 2026-09-12: all shared source compiled successfully against the
installed tML references after main fixed the workbench AdjTiles type. The utility
slice has no C# errors. Utility compilation used TEMP for all generated output;
the temporary compile project has been removed from Content. No local obj/bin
was created by this slice.

Validation integration:
- `VoyageUtilityValidation.Validate()` after recipes have been added.
- `VoyageUtilityValidation.Run()` from the isolated main-menu smoke client after
  graphics are initialized (for example FinishSmoke after the combat checks).
- Neither method has an automatic ModSystem hook; main controls invocation.
- Data marker: `VOYAGE_UTILITY_DATA_PASS`.
- Runtime marker: `VOYAGE_UTILITY_RUNTIME_PASS`.
- Runtime preview: `outputs/voyage-utility-runtime.png`, 96 actual draw cells.
- The runtime tests use an in-memory arena at tiles x600..649, y55..99 only in
  the explicitly flagged main-menu process, never a real loaded world/save.
- Coverage: 9 utilities, 85 furniture recipes, 40-slot save/load and independent
  item instances, manual quick-stack and repeated withdrawal conservation,
  92 actual furniture placements/frame styles/break paths (one total drop each),
  96 nonempty draw cells, eight recall interruption/success cases, one repair
  buff slot refreshed to 36000 ticks, +10 defense/+40 mana without healing.
- Runtime tests are implemented but have NOT been run by this worker; main owns
  all client launches and final verification.
