# BallisticsLab implementation status

Snapshot date: 2026-08-13

## Current baseline candidate

- Plugin version: `0.2.8`
- Development report schema: `4`
- Installed runtime-evidence schema: `3`
- Runtime source: current repository tree
- Target: SPT `4.1.2`, EFT `0.16.9.40743`, `EscapeFromTarkov.exe`
- Release target: `netstandard2.1`
- Currently deployed and playtested DLL SHA-256: `31CEF59F837919A1E334E977544E8A7FD45725AA59B738613C67333DD68542E3`
- Current strict-build candidate SHA-256: `EA6B10BE1F2824A5132A845403AFC75528DEE13E22ADE81E98BB88C3912B2540`
- Default configuration: disabled

The current source enables nullable analysis, checked arithmetic, recommended analyzers, code-style enforcement, and warnings-as-errors. Lifecycle contracts and locale-sensitive display formatting were corrected without changing lab ballistics or patch targets. The strict-build candidate has not been deployed or runtime-accepted. Tagging remains gated on the controlled current-build report matrix and the direct observations that telemetry cannot prove.

## Active physical-telemetry development

The `development/physical-projectile-telemetry` branch now has an offline-validated foundation for optional physical-state ingestion:

- Reflection-only discovery from assemblies already loaded by the host; there is no compile-time project or assembly reference.
- Exact publisher schema-1 validation before subscription.
- Subscription only during an active Lab session, one-second late-discovery retries, and deterministic unsubscription during session teardown.
- Immediate detached copies into immutable BallisticsLab-owned host, impact, component, collision-history, output, loss-budget, and conservation records.
- Complete copied component geometry, mass, motion, energy, attitude, lineage, material provenance, damage and penetration capability, terminal state, and render state.
- Safe absence, unsupported-schema, malformed-event, and bounded-buffer behavior.
- Exact ordinal transition-ID pairing with first-seen ordering, pending prepared evidence, orphaned resolved evidence, and deterministic duplicate counters that preserve the first accepted stage.
- Schema-4 JSON `physicalTransitions` export with complete parent/output state, material origin, prior collisions, loss budget, and conservation ledger.
- Physical-transition-only manual and automatic reports with independent revision tracking; the flat shot-record CSV columns remain unchanged.
- Offline mass and energy closure validation across parent allocation, target spall, output counts, modeled losses, residual energy, output energy, and closure error.
- Built-in controlled-fixture and seven-material campaigns with exact installed material/class selectors plus explicit velocity, layer, backstop, physical-transition, and conservation gates.
- One accepted complete chain per attempt, exact fixture and lineage correlation, deterministic duplicate rejection, and a 350 ms evidence-settle window before evaluation.
- Automatic durability reset between ordinary repeated shots, automatic fixture advancement between cases, and an accessible guided-campaign panel that does not fire the weapon.
- Stable case-identification seeds derived from a configurable Lab seed. These seeds label repeatable cases; they do not replace EFT's observed shot seed.
- Schema-4 campaign definitions, detached attempt evidence, and result matrices whose case seeds, statuses, counts, means, maxima, and completion state are independently recomputed by the offline validator.
- A completed or stopped campaign cannot be silently replaced. Evidence revisions remain monotonic across replacements, unsaved attempts are checkpointed first, and a failed checkpoint blocks the new campaign start.
- Per-attempt protocol evidence records absolute target-impact speed, projectile mass and diameter, actual incidence, fixture-local hit coordinates, face dimensions, fixture distance, and witness-backstop state without retaining a pooled shot or Unity object.
- A nominal GOST 34286-2017 catalog represents all eight Br1-through-Br6 threat rows, including the two Br4 and two Br5 threats. The supported item and English/Russian locale snapshots establish five exact designation mappings, the `57-N-181S-01` Br1 variant, and the absence of both Br5 designations.
- Mapping metadata keeps installed simulation mass and diameter, locale-described mass, initial speed, internal name, display name, caliber, and exact designation evidence separate. The evaluator admits only exact designations whose recorded projectile mass and diameter match the installed simulation state, while preserving flags for nominal-mass and locale-mass differences. Variants and unavailable threats fail closed.
- A pure GOST-oriented screening evaluator enforces the five-shot count, same-sample identity, five-caliber edge and neighbour spacing, 5-degree incidence limit, test-distance tolerance, velocity window, and the standard's more-severe low-velocity penetration and high-velocity stop exceptions.
- Guided protocol campaigns exist for the four exact designations whose Br class also has an installed armored-steel sample. The exact Br2 ammunition mapping remains available to the pure evaluator, but it is not exposed as a runnable campaign because the supported database has no class-2 armored-steel plate. A deterministic five-region marker plan uses the installed projectile diameter and a tolerance/safety envelope that preserves at least five diameters between every accepted hit region and every plate edge.
- Qualifying protocol hits retain the same fixture identity and accumulated durability. A rejected, malformed, or queued extra hit invalidates the partial sample, preserves its attempt evidence, marks prior hits as sequence-invalidated, and requires a fresh fixture instead of restoring damaged armor.
- Schema-4 campaign evidence records the shot-sequence policy, threat and ammunition mapping, sample ordinal, per-shot protocol qualification reason, protocol rejection count, and invalidated-sequence count. Offline replay recomputes the final statuses and cursor.
- Screening results are explicitly non-certifying. Runtime sampling reads cached EFT trajectory nodes without advancing the trajectory, measures cumulative path length, and interpolates velocity at 3 metres. A shot whose cached path is shorter remains `TargetImpactProxy` and cannot satisfy the protocol basis.

This development layer has not been deployed or tested in-game. The formal protocol report remains next.

## Automated and startup verification

- Clean Debug and deterministic Release solution builds at the `latest-all` analyzer tier: 0 warnings, 0 errors.
- Pure and installed-database validation: 132 checks passed.
- Validation with all installed-runtime reports: 139 checks passed.
- Installed plate catalog: 39 usable templates across seven supported materials.
- Disabled startup: `0.2.8` reports disabled and installs zero game patches.
- Enabled startup: all four intended patches install exactly once.
- Exact SPT version and game-assembly compatibility checks remain active.
- The source default remains disabled.
- Automatic saving emits nonempty changed-chain batches only. A later continuation includes its required parents but does not recopy unrelated saved chains.
- Installed runtime evidence remains gated to schema `3` and plugin version `0.2.8`. Development schema `4` is separately covered by synthetic pair, physical-only, provenance, mass-closure, and energy-closure tests until the final deferred runtime campaign.
- The tracked coverage evaluator separates casual bot traffic from controlled fixture evidence. Synthetic regressions prove that bot records cannot satisfy a fixture gate and duplicate automatic batches cannot inflate coverage.
- .NET SDK `10.0.303` is pinned with roll-forward disabled. Deterministic source paths are enabled and Git-SHA injection into the assembly informational version is disabled.
- Explicit deployment performs an SHA-256 parity check and fails if the compiled and installed assemblies differ.

The installed `31CEF59F...` DLL remains the runtime-tested baseline. A guarded restart produced one responsive Tarkov process, loaded `0.2.8` disabled, installed zero Lab patches, and emitted zero Lab startup errors. The current `EA6B10BE...` strict-build candidate was validated in isolation and deliberately not copied over that installed baseline. Existing reports remain evidence for the installed build; the current candidate requires its own deployment and runtime gate before it can replace that baseline.

## Installed runtime evidence

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

## Remaining development sequence

- Add a dedicated protocol result document and cross-campaign material comparisons that distinguish simulation-screening outcome from accredited certification.
- Review the guided protocol panel and marker visibility offline; in-game interaction remains deferred to the final integrated campaign.
- Run the final integrated in-game campaign only after every offline development layer is complete.

Existing Granit BR4 and BR5 labels are repeatable EFT database presets only. They are not certification tests.
