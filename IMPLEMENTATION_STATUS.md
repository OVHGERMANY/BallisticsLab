# BallisticsLab implementation status

Snapshot date: 2026-09-01

## Community alpha candidate

- Plugin version: `0.3.0`
- Planned prerelease tag: `v0.3.0-alpha.2`
- Development report schema: `5`
- Supported physical publisher schema: `2`
- Detached physical snapshot schema: `2`
- Supported environment: SPT `4.1.3`, EFT `0.16.9.40743`
- Release target: `netstandard2.1`
- Default configuration: disabled

BallisticsLab is an optional local testing and telemetry tool. It is not required for ordinary
gameplay, does not fire the user's weapon, and uploads nothing. While disabled it installs zero
game patches.

## Current capabilities

- Reflection-only discovery of an optional schema-2 physical telemetry publisher already loaded
  by the host; no compile-time project or assembly dependency is introduced.
- Session-scoped subscription, late discovery, deterministic unsubscription, bounded capture, and
  immediate detached copies of every accepted event.
- Complete copied host identity, impact geometry, target profile/surface identity, projectile
  construction, projectile design, shape, SI state, material provenance, immediate-parent mass
  source, collision history, loss budget, and conservation evidence.
- Exact transition-ID pairing with pending, orphaned, completed, and duplicate-stage accounting.
- Schema-5 JSON physical transitions while the existing flat shot-record CSV columns remain
  unchanged.
- Offline replay recomputes component speed, momentum, kinetic energy, provenance, mass closure,
  output counts, loss totals, residual energy, output energy, and closure error.
- One-to-six-layer fixtures, spaced armor, installed plate catalog, armored-steel presets, Granit
  BR4/BR5 presets, backstop handling, bot controls, armor/body telemetry, and automatic changed-chain
  report capture.
- Controlled fixture baseline and seven-material matrix campaigns with exact fixture, layer,
  material, velocity, backstop, physical-transition, and conservation gates.
- GOST-oriented simulation screening for exact installed threat mappings and available armored-
  steel samples. Results are explicitly non-certifying and cannot claim laboratory compliance.
- Campaign reports preserve definitions, accepted and rejected attempts, sample identity,
  sequence invalidation, stable Lab case seeds, observed game seeds, result matrices, protocol
  screening evidence, and run-instance identity.
- Cross-report comparison validates each source, deduplicates progressive snapshots, rejects
  divergent history, and excludes incomplete or mixed-ammunition cohorts.
- The panel refits to screen size, blocks translated game commands while visible, and uses
  plate-plane markers that hide behind the plate, inside the camera guard distance, or behind the
  camera.

## Privacy and report ownership

Reports stay local until the user deliberately shares them. They can contain profile identifiers,
session/run identifiers, ammunition and fixture identity, hardware-independent simulation state,
and exact shot evidence. Users must review and redact reports and logs before attaching them to a
public issue. Existing reports can be deleted without changing the game installation, and disabling
the Lab prevents new sessions and report generation.

## Offline verification

- Release solution build: warnings as errors, checked arithmetic, recommended .NET analyzers,
  code-style enforcement, deterministic source paths; `0` warnings and `0` errors.
- Validation: `163` checks passed, `0` failed against the official SPT 4.1.3 item database.
- Catalog: `39` usable plate templates across aluminum, aramid, armored steel, ceramic, combined,
  titanium, and UHMWPE.
- Schema-5 report, schema-2 telemetry, exact projectile-design copy, provenance/mass-source
  independence, mass/energy closure, campaign replay, protocol replay, report pairing, comparison,
  UI policy, marker policy, and disabled-startup contracts pass offline.

## Known alpha limitations

- The `0.3.0` candidate has not yet completed its minimal startup/load smoke test.
- Controlled campaigns require the exact installed fixture and ammunition identities they declare;
  ordinary raid records cannot satisfy those gates.
- The supported database lacks an unambiguous runnable sample for every nominal GOST threat.
- Screening is simulation evidence only and is not certification or a substitute for physical
  laboratory testing.
- Direct loot-screen persistence, uncovered corpse hits, living-target double-damage exclusion,
  multi-piece armor order, and death-event non-replay still require deliberate in-game observation.
- Broad compatibility, long-session report volume, performance, and UI behavior need community
  runtime coverage.

## Release boundary

No new campaign, automation, replay, standards, or laboratory feature category is required before
the community alpha. Post-release work is limited to defects reproduced during local or community
testing and to documentation needed to make those tests repeatable.
