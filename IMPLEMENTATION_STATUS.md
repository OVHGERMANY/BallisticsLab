# BallisticsLab implementation status

Snapshot date: 2026-08-11

## Current baseline candidate

- Plugin version: `0.2.5`
- Report schema: `3`
- Target: SPT `4.1.2`, EFT `0.16.9.40743`, `EscapeFromTarkov.exe`
- Release target: `netstandard2.1`
- Default configuration: disabled

The current candidate is not tagged as an accepted baseline. Tagging requires current-build manual evidence with JSON `schema = 3` and `pluginVersion = 0.2.5`.

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

## Verified automated and startup gates

- Release solution build: 0 warnings, 0 errors.
- Pure and installed-database validation: 36 checks passed.
- Installed plate catalog: 39 usable templates across seven supported materials.
- Layer limit: one through six.
- Six-layer geometry preserves the configured face-to-face air gap.
- Six-layer backstop preserves one meter of face clearance after the final plate.
- Ammunition identity, internal name, and base speed are sourced from the same live template.
- The newest acceptance report is rejected unless its schema and plugin version match the current build.
- The final `0.2.5` local/deployed artifact-parity check is pending deployment.
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

These reports remain useful regression evidence, but they do not identify their plugin version. Their ammunition-name field also predates the live-template identity correction, and their spacing value predates the exact face-gap correction. They therefore do not prove final acceptance of `0.2.5`.

## Current-build manual evidence still required

The schema-3 report can directly establish the remaining cases below:

- Complete four-layer chain.
- Complete five-layer chain.
- Complete six-layer chain.
- Complete three-layer class-6 steel chain.
- Bot body-health change.
- Bot equipped-armor durability change.
- Covered post-death armor durability change at zero body health.
- Deviated-shot continuation into a later armor layer.
- Fragment continuation into a later armor layer.

The following require direct observation or combined report and code evidence. Version `0.2.5`
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
- Lab plates now load EFT's installed `BodyArmor` fragmentation contribution. The supported build's
  serialized value is `0.249`; a finite positive game value is preserved, while the exact installed
  value is the fail-safe if settings are unavailable. Penetration and ricochet collider values stay
  zero because those decisions remain owned by the fixture's armor component.

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
