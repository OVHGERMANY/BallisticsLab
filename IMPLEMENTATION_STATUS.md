# BallisticsLab implementation status

Snapshot date: 2026-08-13

## Current baseline candidate

- Plugin version: `0.2.8`
- Report schema: `3`
- Runtime source: current repository tree
- Target: SPT `4.1.2`, EFT `0.16.9.40743`, `EscapeFromTarkov.exe`
- Release target: `netstandard2.1`
- Currently deployed and playtested DLL SHA-256: `31CEF59F837919A1E334E977544E8A7FD45725AA59B738613C67333DD68542E3`
- Current strict-build candidate SHA-256: `2742343222553B4E356FD8A65AA0271EDD01E5AE7C149D4D4A93D659CCAD0B1D`
- Default configuration: disabled

The current source enables nullable analysis, checked arithmetic, recommended analyzers, code-style enforcement, and warnings-as-errors. Lifecycle contracts and locale-sensitive display formatting were corrected without changing lab ballistics or patch targets. The strict-build candidate has not been deployed or runtime-accepted. Tagging remains gated on the controlled current-build report matrix and the direct observations that telemetry cannot prove.

## Automated and startup verification

- Clean Debug and deterministic Release solution builds at the `latest-all` analyzer tier: 0 warnings, 0 errors.
- Pure and installed-database validation: 55 checks passed.
- Validation with all current reports: 62 checks passed.
- Installed plate catalog: 39 usable templates across seven supported materials.
- Disabled startup: `0.2.8` reports disabled and installs zero game patches.
- Enabled startup: all four intended patches install exactly once.
- Exact SPT version and game-assembly compatibility checks remain active.
- The source default remains disabled.
- Automatic saving emits nonempty changed-chain batches only. A later continuation includes its required parents but does not recopy unrelated saved chains.
- Every admitted report must have schema `3`, plugin version `0.2.8`, a matching CSV partner, exact field equality, and valid ammunition identity, falloff, durability, trajectory, and lineage data.
- The tracked coverage evaluator separates casual bot traffic from controlled fixture evidence. Synthetic regressions prove that bot records cannot satisfy a fixture gate and duplicate automatic batches cannot inflate coverage.
- .NET SDK `10.0.303` is pinned with roll-forward disabled. Deterministic source paths are enabled and Git-SHA injection into the assembly informational version is disabled.
- Explicit deployment performs an SHA-256 parity check and fails if the compiled and installed assemblies differ.

The installed `31CEF59F...` DLL remains the runtime-tested baseline. A guarded restart produced one responsive Tarkov process, loaded `0.2.8` disabled, installed zero Lab patches, and emitted zero Lab startup errors. The current `27423432...` strict-build candidate was validated in isolation and deliberately not copied over that installed baseline. Existing reports remain evidence for the installed build; the current candidate requires its own deployment and runtime gate before it can replace that baseline.

## Current-build runtime evidence

The admitted `0.2.8` set contains 104 automatic CSV/JSON pairs through `BallisticsLab-20260812-233342-265-104-auto.json`:

- 5,133 unique records.
- 3,370 shot chains.
- All 104 CSV/JSON pairs match field for field.
- All records pass the current ballistic, durability, trajectory, and lineage invariants.
- The first six changed-chain batches contain disjoint chain IDs and advancing sequence ranges, proving that later automatic saves do not recopy earlier unrelated chains.

Current report gates passed:

- Single-plate and one-layer chains.
- One-layer class-6 armored steel.
- Granit BR4 game-template preset.
- Bot body-health telemetry.
- Bot equipped-armor durability telemetry.
- Covered post-death armor durability loss while body health remains zero.
- Deviated-shot continuation into a later layer.
- Fragment continuation into a later layer.
- Backstop termination.
- Automatic CSV and JSON export.

Current controlled report gates still missing:

- Complete two-layer chain.
- Complete three-layer chain.
- Complete four-layer chain.
- Complete five-layer chain.
- Complete six-layer chain.
- Complete spaced-armor chain.
- Complete two-layer class-3 armored-steel chain.
- Complete three-layer class-4 armored-steel chain.
- Complete three-layer class-6 armored-steel chain.
- Granit BR5 game-template evidence.

Ordinary raids may continue producing useful telemetry, but they do not satisfy these fixture gates. No particular cartridge is mandated; a controlled shot must simply traverse every requested physical layer under one chain ID.

## Direct observations still required

Version `0.2.8` does not export struck body-part coverage, loot-screen state, armor-application call order, or death-event count. Reports alone therefore cannot prove:

- An uncovered post-death hit leaves armor unchanged.
- Ordinary living-target armor is not double-damaged.
- Multiple matching armor pieces follow the existing EFT order.
- Looted armor retains the post-death durability change.
- Corpse health and death events are not replayed.

Covered post-death durability loss at zero body health is already present in current reports and does not need to be rediscovered. The remaining items require one deliberate observation session, not more random combat telemetry.

## Stabilized systems

- One-to-six independent physical armor layers.
- Adjustable face-to-face spacing, collider thickness, angle, and backstop state.
- EFT armor-template catalog plus armored-steel and Granit BR4/BR5 quick presets.
- Live ammunition-template identity and base speed.
- Exact collision-point telemetry and locked damage/penetration curves.
- Fragment and deviation child construction with corrected continuation factors and parent/root lineage.
- Bot selection, reversible movement/fire controls, health telemetry, armor telemetry, and covered post-death durability telemetry.
- Automatic changed-chain CSV/JSON capture and manual full-session checkpoints.
- UTF-8-safe external report inspection for non-ASCII bot names.
- Disabled-by-default startup with zero patches while disabled.

## Superseded evidence

- Schema-2 reports remain historical reference only because they lack exact plugin-version provenance.
- `0.2.4` exposed missing fixture fragmentation contribution.
- `0.2.5` was superseded because it restored the fragmentation gate without valid child spread and deviation inputs.
- `0.2.6` restored fragment and deviation construction and produced a valid two-layer class-3 chain, but that evidence belongs to an older binary.
- `0.2.7` proved the larger task-oriented panel, all quick-fixture buttons, Granit BR4/BR5 placement, and automatic saving. Its cumulative-save behavior was superseded by `0.2.8` changed-chain batches.

Historical evidence does not satisfy a missing `0.2.8` gate.

## Deliberately not started

The following stages remain gated behind a stable, versioned, manually accepted Lab baseline:

- Explicit physical state for intact, deformed, fragmented, and spall projectiles.
- Projectile deformation and material-response calculations.
- Conserved projectile fragmentation and separate target spall.
- Individual fragment flight, collision, damage, and penetration.
- Rendering driven by calculated projectile and fragment state.
- Automated campaigns, deterministic seeds, reset control, and result matrices.
- Formal GOST-style protocols and standardized reports.

Existing Granit BR4 and BR5 labels are repeatable EFT database presets only. They are not certification tests.
