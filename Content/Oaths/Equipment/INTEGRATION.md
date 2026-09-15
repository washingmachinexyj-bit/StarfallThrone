# Oaths equipment/support integration

Owned slice only: Equipment, Support, OathsEquipment localization, equipment-art and non-boss/non-seed/non-mining assets.

Main hooks required:
- packet 92 => `Support.SupremeSummonBase.ReceivePacket(reader, sender)`.
- packet 93 => `Equipment.OathEquipmentPlayer.ReceivePacket(reader, sender)`.
- `Equipment.OathEquipmentValidation.Data()` after recipes; `Run()` in isolated headless smoke.
- Combat can call `Support.SupremeLoot.Add(loot, Index)` for standard expert-only bag loot, ordinary 40–55 materials + one of four weapons, master relic/pet and all-mode trophy.
- Rewind is server-authorized and history is recorded on server, never accepts positions from client. It directly permits ONLY live Supreme boss encounters with both endpoints within 600 pixels of fixed `Arena`, and rejects any solid tile or damaging Supreme hazard along the sampled swept player box. All other boss encounters, Fable, seed, and older mini encounters are denied. `RewindBoundary` is an OPTIONAL additional stricter validator; main does not need to assign one.
- Optional first-win rewards now exist: `SupremeChoice0..2`. Main's legitimate first-win ledger should grant one matching cache to eligible players, once per ledger. Holding and left-clicking cycles the class; native inventory right-click consumes one cache and gives exactly the selected weapon. A cache cannot set world progress and has no crafting recipe. No cache is emitted by normal repeatable boss loot.
- Own localization includes OathsEquipment keybind/tooltip keys; no shared files are edited.

Triune altar uses PantheonStationTile5 and PantheonCore16 as contracted. Gear uses OathCatalog's public types, not mining internal implementation.

Assets: equipment-preview.png and manifest.json in work/oaths-equipment-art; six SeedToken0..2/SeedWeapon0..2 icons delivered without editing SeedItems.
