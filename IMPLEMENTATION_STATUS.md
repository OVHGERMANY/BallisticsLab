# BallisticsLab implementation status

Snapshot date: 2026-08-11

## Current baseline candidate

- Plugin version: `0.2.4`
- Report schema: `3`
- Runtime build source commit: `b199b2d`
- Target: SPT `4.1.2`, EFT `0.16.9.40743`, `EscapeFromTarkov.exe`
- Release target: `netstandard2.1`
- Deployed DLL SHA-256: `4D363678F18A381689776D37830EFF662C17DC7976564E666995C483D4559B08`
- Default configuration: disabled

The current candidate is not tagged as an accepted baseline. Tagging requires current-build manual evidence with JSON `schema = 3` and `pluginVersion = 0.2.4`.

## Verified automated and startup gates

- Release solution build: 0 warnings, 0 errors.
- Pure and installed-database validation: 32 checks passed.
- Installed plate catalog: 39 usable templates across seven supported materials.
- Layer limit: one through six.
- Six-layer geometry preserves the configured face-to-face air gap.
- Six-layer backstop preserves one meter of face clearance after the final plate.
- Ammunition identity, internal name, and base speed are sourced from the same live template.
- The newest acceptance report is rejected unless its schema and plugin version match the current build.
- Local and deployed plugin assemblies are byte-identical.
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

These reports remain useful regression evidence, but they do not identify their plugin version. Their ammunition-name field also predates the live-template identity correction, and their spacing value predates the exact face-gap correction. They therefore do not prove final acceptance of `0.2.4`.

## Current-build manual evidence still required

- At least one nonempty schema-3 export from version `0.2.4`.
- Current ammo template ID, internal name, and base speed match the installed template.
- Complete four-layer chain.
- Complete five-layer chain.
- Complete six-layer chain.
- Complete three-layer class-6 steel chain.
- Bot body-health change.
- Bot equipped-armor durability change.
- Covered post-death armor durability change at zero body health.
- Uncovered post-death hit leaves armor unchanged.
- Living armor is not double-damaged.
- Multiple matching armor layers follow the existing game order.
- Looted armor retains its changed durability.
- Corpse health and death events are not replayed.
- CSV and JSON parity for the schema-3 export.

## Stabilization fixes completed after the first baseline

- Live ammunition-template identity replaces stale pooled-item identity in telemetry.
- Live report validation rejects stale ammunition identity.
- Post-death armor changes remain visible when health is already zero.
- Layer spacing now means the physical air gap between plate faces.
- Backstop placement is derived from the final plate face.
- Reports carry exact schema and plugin-version provenance.

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
