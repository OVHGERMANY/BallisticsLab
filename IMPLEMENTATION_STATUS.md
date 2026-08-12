# BallisticsLab implementation status

Snapshot date: 2026-08-12

## Current baseline candidate

- Plugin version: `0.2.8`
- Report schema: `3`
- Runtime build source commit: `e3dc463`
- Target: SPT `4.1.2`, EFT `0.16.9.40743`, `EscapeFromTarkov.exe`
- Release target: `netstandard2.1`
- Deployed DLL SHA-256: `3C4BAAA940FB61CD557CC173B61A55FBAA9860A651DBEFA3A33A4DEC0171B5E6`
- Default configuration: disabled

The current candidate is not tagged as an accepted baseline. Tagging requires current-build manual evidence with JSON `schema = 3` and `pluginVersion = 0.2.8`.

Accepted development evidence from `0.2.7` retains schema `3` and covers automatic nonempty report capture plus a
larger task-oriented panel. Quick fixture actions build, place, and close the panel in one click;
advanced geometry and catalog controls remain behind one disclosure. Its Release build passed 49 pure/database
checks with zero compiler warnings or errors. The deployed and local `0.2.7` Release DLLs are byte-identical
at SHA-256 `C1EC7610C1BAB46936E445F74B5C894B2C711455DAF680CFF033766D1259BB6D`;
one responsive Tarkov process loaded `0.2.7`, enabled the four intended Lab patches, and reported no
Lab startup error.

Direct `0.2.7` acceptance confirmed that the revised quick buttons correctly created and placed all
six named fixtures and returned control to shooting. Automatic saving produced matching nonempty
schema-3 CSV/JSON pairs without a manual export. Thirteen automatic/manual checkpoints through
`BallisticsLab-20260812-022036-750-012-auto` passed 56 validation checks. The evidence directly adds:

- One-layer class-6 armored steel.
- Two-layer class-3 armored steel with both layers struck.
- Granit BR4 and Granit BR5 game-template presets.
- A real `FragmentationHit` child from layer 1 subsequently striking layer 2.

The three-layer class-4 and class-6 buttons were exercised, but those shots reached only layer 1 and
do not complete either chain. Sustained all-session recording also exposed a `0.2.7` checkpoint
efficiency defect: it repeatedly serialized the complete accumulated session, producing many large
cumulative files. Version `0.2.8` fixes this by exporting only changed chains plus their required
parent records. Manual saves remain complete session snapshots. This keeps each automatic pair
independently valid without repeatedly copying unrelated evidence. The current `0.2.8` Release build
passes 52 pure/database checks with zero compiler warnings or errors. The deployed DLL is byte-identical
to that Release artifact at the SHA-256 above. A disabled-mode guarded relaunch loaded
`Janky-BallisticsLab 0.2.8`, reported that no game methods were patched, and logged no startup error.
The setting was then restored to enabled and a second guarded relaunch produced exactly one Tarkov
process, PID `7244`; BepInEx loaded `0.2.8`, enabled its intended Lab patches, and logged no startup
error.

Runtime acceptance of changed-chain automatic saving passed with the first six `0.2.8` automatic
report pairs, `BallisticsLab-20260812-231152-284-001-auto` through
`BallisticsLab-20260812-231350-960-006-auto`. Every CSV/JSON pair matched field for field and all
34 records passed the current schema/version and telemetry invariants. Each pair contained exactly
one new chain: their chain IDs were disjoint and their sequence ranges advanced from `1-5`, `6-10`,
`11-15`, `16-22`, `23`, and `24-34`, proving that later batches did not recopy earlier unrelated
chains. The evidence includes both real fragmentation and deviation continuation plus a backstop hit.

## Previous-candidate evidence recorded

`BallisticsLab-20260811-233928` is the final `0.2.4` export:

- JSON schema `3`, plugin version `0.2.4`.
- 19 records across 13 shot chains.
- Matching 19-row CSV; every shared CSV and JSON field is equal.
- 12 live 5.45x39 ammunition templates; every reported template ID, internal name, and base
  speed matches the installed database.
- One-layer class-6 steel impacts, durability loss, armor blocks, penetrations, and backstop stops.
- Three deviated-child records with continuation factors and contiguous parent/root lineage.
- Every ordinary collision reproduces the configured damage and penetration curves from its
  reported incoming values and collision-point speed.
- All reported values are finite and non-negative, durability never increases, and every trajectory
  ends at its reported hit point.

The deviated children in this export revisit the single source plate and then reach the backstop.
They do not strike a later armor layer. The export also proved that the fixture collider had no
fragmentation contribution, which made EFT fragmentation impossible on Lab plates regardless of
the ammunition's fragmentation chance.

## Current-candidate evidence recorded

`BallisticsLab-20260812-002316`, `BallisticsLab-20260812-002449`,
`BallisticsLab-20260812-012345`, and `BallisticsLab-20260812-014330` are schema-3 exports
from plugin version `0.2.6`:

- The reports contain 32 current-build records across 14 shot chains. All four CSV/JSON pairs have
  equal row counts and match field for field; the one-record pair also verifies scalar-row handling
  in the external acceptance watcher.
- The 25-record report contains 11 chains produced with the installed
  `patron_1143x23_acp_ap` template. Its reported template identity and base speed match the installed
  database.
- Four shots are stopped by the Granit BR4 armor plate. The later hits consume its remaining
  durability without any reported durability increase.
- One forward plate hit reports `PENETRATED / FRAGMENTED` and creates a real child with
  `continuationKind = FragmentationHit`, fragment index `1`, and parent depth `1`.
- Six forward plate hits create children with `continuationKind = DeviationHit`. The fragment and
  deviated children preserve their corrected continuation damage and penetration, re-contact the
  back face of the source plate, and then terminate at the backstop at parent depth `2`.
- The latest three-record chain uses the installed `patron_545x39_BS` template and records a
  forward penetration/deviation through the final durability of a one-layer class-4 NewSphereTech
  plate, the deviated child re-contacting that plate's back face, and the same child terminating at
  the backstop. Its ammunition identity, collision-point speed, locked falloff values, and complete
  parent/root lineage pass the same validators.
- A second three-record chain uses the installed `patron_545x39_7n40` template against the
  two-layer class-3 armored-steel preset with a `0.02 m` face gap. The parent penetrates layer 1,
  its deviated child re-contacts the source plate's back face, and that same child reaches layer 2,
  where the armor blocks it. This is the first current-build proof of a complete two-layer chain,
  physically spaced armor, the two-layer class-3 steel preset, and deviated continuation into a
  later armor layer.
- Every record passes the automatic finite/non-negative, velocity-fraction, locked damage and
  penetration curve, durability, trajectory-endpoint, and lineage checks.

This proves that version `0.2.6` restores EFT fragment and deviation child construction on the Lab
plate. Because the fixture had only one armor layer, it does not yet prove either child type entering
a later armor layer.

## Verified automated and startup gates

- Release solution build: 0 warnings, 0 errors.
- Pure and installed-database validation: 45 checks passed.
- Current-report validation: 52 checks passed across all four current-build exports through
  `BallisticsLab-20260812-014330`.
- Installed plate catalog: 39 usable templates across seven supported materials.
- Layer limit: one through six.
- Six-layer geometry preserves the configured face-to-face air gap.
- Six-layer backstop preserves one meter of face clearance after the final plate.
- Ammunition identity, internal name, and base speed are sourced from the same live template.
- The newest acceptance report is rejected unless its schema and plugin version match the current build.
- Automatic report validation rejects duplicate sequences, missing forward-hit state, non-finite or
  negative telemetry, incorrect speed fractions or falloff curves, increasing fixture durability,
  broken continuation inputs, detached trajectory endpoints, and incomplete recorded continuation
  lineage. Every nonempty schema-3 report from the current plugin version is checked because the
  acceptance watcher aggregates current-build coverage across exports; validating only the latest
  file would leave earlier contributing records unverified. A two-report regression rejects an
  incorrect earlier current-build report even when the latest report is valid, while an equally
  incorrect historical-version report is correctly excluded from current acceptance.
- Chain validation requires a stable fire index and root random seed, non-negative fragment indices,
  and an earlier parent collision whose fixture and layer exactly match each continuation's reported
  source. Negative regressions reject both a changed root identity and a child attached to the wrong
  source collision.
- Local and deployed `0.2.6` assemblies are byte-identical.
- Disabled startup: the plugin reports disabled and installs zero of its four patches.
- Enabled startup: all four intended patches install exactly once.
- Exact SPT version and game-assembly compatibility checks remain active.

## Historical manual evidence

Schema-2 reports from an earlier build demonstrate these runtime paths:

- Single-plate fixtures.
- Complete one-, two-, and three-layer chains.
- Physically separated plate colliders.
- One-layer class-6 steel.
- Two-layer class-3 steel.
- Three-layer class-4 steel.
- Granit BR4 and BR5 game-template presets.
- Deviated child-shot continuation into a later physical layer.
- Backstop termination.
- Matching CSV and JSON record counts.

These reports remain useful regression evidence, but they do not identify their plugin version. Their ammunition-name field also predates the live-template identity correction, and their spacing value predates the exact face-gap correction. They therefore do not prove final acceptance of `0.2.6`.

## Current-build manual evidence still required

The schema-3 report can directly establish the remaining cases below:

- Complete three-layer chain.
- Complete four-layer chain.
- Complete five-layer chain.
- Complete six-layer chain.
- Complete three-layer class-4 steel chain.
- Complete three-layer class-6 steel chain.
- Bot body-health change.
- Bot equipped-armor durability change.
- Covered post-death armor durability change at zero body health.

The following require direct observation or combined report and code evidence. Version `0.2.6`
does not export the struck body part, loot-screen state, armor-application call order, or death-event
count, so the report alone cannot prove them:

- An uncovered post-death hit leaves armor unchanged.
- Living armor is not double-damaged.
- Multiple matching armor layers follow the existing game order.
- Looted armor retains its changed durability.
- Corpse health and death events are not replayed.

## Stabilization fixes completed after the first baseline

- Live ammunition-template identity replaces stale pooled-item identity in telemetry.
- Live report validation rejects stale ammunition identity.
- Post-death armor changes remain visible when health is already zero.
- Layer spacing now means the physical air gap between plate faces.
- Backstop placement is derived from the final plate face.
- Reports carry exact schema and plugin-version provenance.
- The external acceptance watcher admits only schema-3 records whose plugin version equals the
  current candidate. Historical reports remain visible as reference counts but cannot satisfy any
  current-build gate. CSV and JSON fields must match individually; equal row counts alone are not
  accepted. A deliberate `decisionPenetration` mismatch was rejected while its row count still matched.
- Lab plates now load all six fields from EFT's installed `BodyArmor` ballistic preset. The supported
  build serializes penetration level/chance, ricochet chance, fragmentation chance, deviation chance,
  and deviation magnitude as `0`, `0.097`, `0.378`, `0.249`, `0.28`, and `0.463`. The exact values are
  also the fail-safe if settings are unavailable or invalid. The fixture's armor component still owns
  the actual block, penetration, and deflection decision; these collider fields supply EFT's original
  fragment spread and continuation-child construction after that decision.
- Candidate `0.2.5` was superseded before acceptance because it restored only the fragmentation gate.
  Its fragment and deviation children still had zero spread, and its deviation continuation used zero
  collider outcome values. No `0.2.5` report was accepted.

## Deliberately not started

The following stages remain gated behind acceptance of the current Lab baseline:

- Explicit physical state for intact, deformed, fragmented, and spall projectiles.
- Projectile deformation and material-response calculations.
- Conserved projectile fragmentation and separate target spall.
- Individual fragment flight, collision, damage, and penetration.
- Rendering driven by calculated projectile and fragment state.
- Automated campaigns, deterministic seeds, reset control, and result matrices.
- Formal GOST-style protocols and standardized reports.

Existing game-template BR4 and BR5 labels are repeatable catalog presets only. They are not certification tests.
