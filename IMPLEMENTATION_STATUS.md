# BallisticsLab implementation status

## Final SPT 4.1.4 compatibility release - 2026-09-05

- The user closed further shooting rounds and requested finalization. Current version is `0.3.1`; preview checkpoints below are historical. The separate development worktree is excluded.
- Final Release: 0 warnings/errors; 163 validation checks pass. The final companion audit passes 11 game contracts, resolves runtime references, and permits only reviewed compatibility/identity changes.
- Final source/installed DLL SHA-256: `BA239F4A6C73873B0C420436FC0B302A3B7A933D4291CE8D4581B7DE36E9FAD1`. Installed with Tarkov closed at 2026-09-05 13:08 local. All 9 saved configurations and both HollywoodFX files remained unchanged.
- Rollback: `E:\Games\SPT-Mod-Backups\20260905-130800-client-compatibility-final`, containing the previous preview DLL and configuration copies.
- Package `BallisticsLab-0.3.1-SPT-4.1.4.zip` reproduced exactly: 103,782 bytes, SHA-256 `403B213C69678F21A79801E29221F71F13929B1F3C522334ACB99DE28C1C546B`.
- Combined preview startup/raid use was observed, not full fixture/campaign acceptance. Finalization changes release identity/startup wording only; fixture behavior and schemas remain unchanged. See [release notes](docs/SPT-4.1.4-release.md).
- Publication targets `spt-4.1.4` and `v0.3.1`, preserving prior branches/releases.

## Historical local compatibility preview - 2026-09-05

- Worktree branch: `codex/spt-4.1.4-compatibility`, published base `2f99708466a693ebe9e63c75250905c91548d5b9` (`v0.3.0`). The original development checkout is unchanged.
- Candidate: `0.3.1-preview.1`, numeric plugin version `0.3.1`, exact SPT `4.1.4`, EFT `0.16.9.5.40743`.
- Only build/provenance constants, compatibility diagnostics, tests and documentation change. Lab behavior, default configuration and report/publisher schemas remain unchanged.
- Baseline and final candidate builds against current references: 0 warnings/errors; 163 validation checks pass in each run. Audit resolved all 290 runtime references and compared 1,291 method bodies; changes are limited to version/hash strings plus the exact-version constructor using its shared constant.
- Installed with Tarkov closed at `2026-09-05T11:30:06-05:00`. Source and installed SHA-256 `4D20EA3953D66A605B7A34DC37916DA02A60F2C6E19ABCDF2FBB06FAEFC20091`, 268,288 bytes. Two-companion transaction preserved 22 protected config/unrelated DLL files.
- Rollback: `E:\Games\SPT-Mod-Backups\20260905-112852-spt414-client-compatibility\before\BallisticsLab.dll`, SHA-256 `FD33343D2A9AEFF40D1C2B804F6A51E7E0CDCA7225B418DCB3C2863ACA7B7AF6`.
- Fresh client PID 2816 began at 11:30:56 -05:00. Log lines 57-60 enable all four Lab patches; line 61 identifies 0.3.1-preview.1 loaded for 4.1.4. BP/HFX loaded together, with zero client error lines at 11:32:49. Fixture/raid acceptance remains pending; no old 4.1.3 rejection log is treated as a result of this candidate.
- See [compatibility evidence](docs/SPT-4.1.4-compatibility.md). No stable publication is part of this test.
- The remainder of this file is the historical 4.1.3 release snapshot.

Snapshot date: 2026-09-01

## Release candidate

- Plugin version: `0.3.0`
- Release tag: `v0.3.0`
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

## Known limitations

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

No new campaign, automation, replay, standards, or laboratory feature category is required for
version `0.3.0`. Post-release work is limited to reproduced defects and documentation needed to
make normal use repeatable.
