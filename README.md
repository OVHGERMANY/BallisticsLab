# BallisticsLab

BallisticsLab is an opt-in SPT 4.1.2 client-side range and telemetry plugin for repeatable terminal-ballistics testing.

The current development build is a public experimental alpha. Installation, testing,
privacy, rollback, compatibility, and confirmed-issue guidance is under
[`docs/community-alpha`](docs/community-alpha/TESTING_GUIDE.md).

The verified baseline, historical evidence, remaining acceptance gates, and deferred stages are tracked in [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md).

It creates disposable plate fixtures from EFT's own armor templates, supports one-to-six physical layers, angle and spacing controls, game-labelled Granit BR4 and BR5 presets, armored-steel stacks, quick material selection, bot selection and freeze controls, shot-chain visualization, and CSV/JSON reports. It never inserts test items into the profile, stash, or inventory.

The plugin is disabled by default and installs no game patches while disabled. Set `Enabled = true` in `BepInEx\config\com.janky.ballisticslab.cfg`, restart the game, enter the hideout shooting range or an offline local raid, then press `Left Ctrl + F10`. A lab session exists only after the panel's explicit **Start Lab Session** action and is destroyed on world change or plugin shutdown. Each changed shot-chain batch saves as a timestamped CSV/JSON pair after a completed burst and once more when the session or world ends. A later continuation includes its earlier parent records, so every pair remains independently auditable without copying unrelated saved chains. Manual saving remains available as a backup; empty or unchanged state is not exported again.

The panel refits itself after display-resolution changes, uses a compact advanced-control layout when space is limited, and owns game input while open so typing or clicking cannot fire an unrelated player command. Close it with its large return-to-shooting button or the same shortcut. Target crosses lie in the fixture surface instead of facing the camera, cast no shadows, and hide when the camera moves behind or inside the marker plane.

Fixture plates use generic ballistic colliders so separate physical layers cannot be suppressed as overlapping body-part colliders. For penetrated fixture hits, the lab applies EFT's otherwise body-collider-only child-shot damage, penetration, deviation-velocity, future-outcome, and armor-CF factors before the child reaches the next layer. Telemetry records the original and corrected values for every continuation in the chain.

The active development branch also contains session-scoped physical telemetry. It discovers an optional schema-2 publisher only from assemblies already loaded by the host, subscribes only while a Lab session is active, and immediately copies prepared and resolved events into immutable BallisticsLab-owned records. The copy includes host identity, measured target geometry, an optional opaque surface identity, complete projectile or fragment state including projectile design, material provenance, collision history, and conservation data; no foreign event, collection, pooled shot, or Unity object is retained. BallisticsLab remains loadable when the publisher is absent or incompatible. Events pair by exact transition ID in first-seen order: prepared-only evidence remains pending, resolved-only evidence is marked orphaned, and duplicate stages are counted without replacing the first accepted event or creating duplicate transitions. Report schema 5 writes this evidence to a separate JSON `physicalTransitions` collection and can save a physical-only report while leaving the flat shot-record CSV columns unchanged. Offline replay requires the exact snapshot and publisher schemas, recomputes every component's speed, momentum, and kinetic energy from its copied state, and rejects invalid orientation, coupling, provenance, collision-history, mass, or energy evidence.

Guided campaigns remove the repeated panel work from fixture testing without firing the weapon for the user. The controlled baseline advances through ten named one-to-six-layer, spaced-steel, Granit BR4, and Granit BR5 cases. The physical material matrix advances through seven material families and requires three accepted shots per case. Its selectors require an exact installed material and armor class: class 4 is used where available, while Aramid uses its installed class-2 plate because the current database has no class-4 Aramid plate. Every fixture layer publishes a generic reflection-only material class and opaque layer identity. A physical attempt counts only transitions bound to that exact fixture and layer set, excludes the metal backstop, and rejects a material-class mismatch. Each campaign receives a unique run-instance ID, waits for one complete shot chain, applies velocity/layer/backstop/physical/conservation evidence gates, restores durability or builds the next fixture, and writes the case definitions, every accepted or rejected attempt, stable Lab case seeds, observed game shot seeds, and a recomputed result matrix into the schema-5 JSON report. The run ID distinguishes repeated uses of the same seed; the Lab seed identifies repeatable test cases and does not override EFT's shot seed.

Campaign attempts carry detached protocol-shot evidence: absolute target-impact speed, projectile mass and diameter, impact angle, fixture-local hit coordinates, plate-face dimensions, fixture distance, and witness-backstop state. Four exact installed designations can start a guided GOST-oriented simulation screen. The exact Br2 ammunition mapping remains in the evidence catalog, but the supported item database has no class-2 armored-steel sample, so the Lab will not present a false runnable fixture. The Lab builds one fresh armored-steel game sample, places five deterministic red marker regions sized from the installed projectile diameter, and preserves durability after each qualifying hit. Every region is designed so any accepted point remains at least five projectile diameters from the plate edge and every earlier accepted point. A rejected, malformed, or rapid extra hit invalidates the partial sequence and places a new sample rather than silently restoring or reusing the damaged plate. The attempt history retains the rejection and marks earlier hits from that sample as sequence-invalidated.

The runtime reads only trajectory nodes EFT has already computed, measures cumulative path from the shot start, and linearly interpolates speed at 3 metres without advancing or rewriting the game trajectory. Evidence is marked `EftTrajectoryThreeMetres` when the cached path reaches that distance; a shorter path remains `TargetImpactProxy` and cannot complete a protocol screen. This is a simulation value, not a physical chronograph measurement or certification result.

The installed SPT 4.1.2 item database and its English/Russian locale records provide five exact designation mappings, one related variant, and two unavailable threats. Exact identity does not imply exact physical representation: the evaluator requires the shot mass and diameter to match the installed simulation values, then separately reports known differences from the nominal threat mass and the locale description. A variant or unavailable designation fails closed. Even an exact mapping can produce only a non-certifying simulation screen.

| Threat | Installed EFT mapping | Identity | Mass evidence in grams: nominal / simulation / locale |
|---|---|---|---:|
| Br1 9x18 Pst 57-N-181S | `5737201124597760fc4431f1` | Variant `57-N-181S-01`; not qualifying | 5.9 / 5.9 / 5.9 |
| Br2 9x21 P 7N28 | `5a26abfac4a28232980eabff` | Exact | 7.93 / 7.9 / 7.5 |
| Br3 9x19 Pst 7N21 | `56d59d3ad2720bdb418b4577` | Exact | 7.0 / 5.4 / 5.4 |
| Br4 5.45x39 PP 7N10 | `56dff2ced2720bb4668b4567` | Exact | 3.5 / 3.68 / 3.5 |
| Br4 7.62x39 PS 57-N-231 | `5656d7c34bdc2d9d198b4587` | Exact | 7.9 / 7.9 / 7.9 |
| Br5 7.62x54 PP 7N13 | None | Not present in supported database | 9.4 / — / — |
| Br5 7.62x54 B-32 7-BZ-3 | None | Not present in supported database | 10.4 / — / — |
| Br6 12.7x108 B-32 57-BZ-542 | `5cde8864d7f00c0010373be1` | Exact | 48.2 / 48.3 / 48.0 |

The guided sequence, per-shot qualification reason, sample identity, invalidated partial sequences, and protocol policy are preserved in schema-5 campaign evidence. The same atomic JSON report contains a derived `protocolScreeningResult` document with the cited standards, exact threat and ammunition mapping, fixture construction, observed velocity range, sample history, rejection counts, and an explicit non-certifying result. Offline replay rebuilds this document from the underlying attempts and rejects altered claims. A fifth qualifying hit remains `PendingEvidence` until any queued impact is resolved, so rapid extra fire cannot produce a false completion.

The BR4 and BR5 buttons select EFT database templates bearing those names. They are repeatable game-data presets, not a claim of real-world certification. Layer spacing is the physical air gap from the back face of one collider to the front face of the next. Plate thickness controls physical collider geometry; armor class, durability, material resistance, and penetration chance come from the selected EFT template.

Normal `Build` does not alter the live installation. Explicit deployment:

```powershell
dotnet msbuild .\src\BallisticsLab\BallisticsLab.csproj -t:Deploy -p:Configuration=Release -p:SptRoot="$env:SPT_ROOT"
```

Set `SPT_ROOT` to the local SPT installation directory before building or validating. The project deliberately has no machine-specific fallback path.

The repository pins .NET SDK `10.0.303` in `global.json`. Release builds normalize source paths and omit the Git commit from `AssemblyInformationalVersion`, so documentation-only commits do not change the plugin DLL. Track the exact source commit and DLL SHA-256 in the release evidence instead. The explicit `Deploy` target finishes by hashing the compiled and installed assemblies and fails if they are not byte-identical; `VerifyDeployment` can also be invoked directly to recheck an existing installation.

Reports are written under `BepInEx\plugins\BallisticsLab\Reports` automatically while a nonempty session changes, at teardown, or when the user requests an immediate save. Automatic pairs contain changed chains plus the parent records required to validate those chains, the current unsaved physical-transition evidence, and the current campaign matrix when present; manual saves remain full-session snapshots. A physical transition or completed campaign attempt can produce a report without an ordinary shot record, in which case the CSV contains its unchanged header and the JSON carries the non-flat evidence. JSON metadata and every CSV row identify the exact plugin version and report schema that produced the evidence. Ammunition identity and physical values are read from the same live `AmmoTemplate`; pooled item identity is used only when no template is available. Reports include fixture identity and geometry, template/material/class data, calculated resistance and penetration chance, incoming and decision values, child continuation factors, parent/root shot identity, target state, armor durability changes, the complete recorded shot chain, physical mass and geometry, projectile and target-material provenance, collision history, terminal/render state, mass/energy closure, campaign case/attempt/result data, per-attempt protocol evidence, and the derived protocol-screening result when applicable. Post-death armor changes remain visible in the latest-shot panel even when body health is already zero.

## Armor testing and controlled acceptance

For ordinary armor testing, use any weapon and ammunition. Build the plate or stack you want, fire at it, and read the resulting penetration, stop, deviation, fragmentation, durability, and chain telemetry. No prescribed cartridge or manual export is required.

Controlled acceptance is separate. Only a shot fired through a known Lab fixture can satisfy a fixture-chain gate; unrelated raid and bot records cannot. When a reproducible gate is needed, use the exact fixture named in the checklist and ammunition capable of reaching its final layer. Automatic saving captures the result without interrupting the session.

For the repeatable fixture sequence, start **Controlled Fixture Baseline** and fire one round at each center marker. For material response, start **Physical Material Matrix**; a case does not advance until its shot includes compatible resolved physical telemetry and conservation data. The Lab restores fixture durability between ordinary repetitions and places the next case automatically. For a GOST-oriented screen, select one of the four threats with both an exact ammunition mapping and an installed armored-steel sample, then shoot only the current red marker. Protocol shots are queued rather than silently ignored, because every physical hit can change the sample.

The validation console reports controlled coverage without treating missing manual evidence as a calculation failure:

```powershell
dotnet run --project .\tests\BallisticsLab.Validation\BallisticsLab.Validation.csproj -c Release --no-build -- "$env:SPT_ROOT\SPT_Runtime\SPT_Data\database\templates\items.json" "$env:SPT_ROOT\BepInEx\plugins\BallisticsLab\Reports"
```

Add `--require-report-coverage` only at the final report-evidence gate. It exits with code `1` while any controlled report item is still missing. Direct corpse observations listed below remain separate because reports cannot prove the loot screen, armor call order, or absence of repeated death events.

An optional third positional path writes a schema-5 cross-report comparison document. Progressive snapshots are deduplicated by exact run-instance ID and divergent histories fail closed. A material run becomes comparable only after all seven cases complete with one exact ammunition template; cohorts also require identical fixture material, class, layers, thickness, and required shot count. Incomplete or mixed-ammunition runs remain listed with an explicit exclusion reason. Protocol summaries retain `simulationEvidenceOnly=true` and `certificationClaim=false`.

```powershell
dotnet run --project .\tests\BallisticsLab.Validation\BallisticsLab.Validation.csproj -c Release --no-build -- "$env:SPT_ROOT\SPT_Runtime\SPT_Data\database\templates\items.json" "$env:SPT_ROOT\BepInEx\plugins\BallisticsLab\Reports" "$env:TEMP\BallisticsLab-comparison.json"
```

## Manual acceptance

1. Set `Enabled = true` and restart the game. Confirm the BepInEx log reports four BallisticsLab patches.
2. Enter the hideout or an offline local raid, press `Left Ctrl + F10`, and choose **Start Lab Session**.
3. Use the large quick-fixture buttons. Each button builds and places its fixture immediately and closes the panel so shooting control returns without another click. Test one Granit BR4 fixture and one Granit BR5 fixture. Fire at the red center mark and confirm the panel records the selected template, class, material, resistance, penetration chance, durability change, and terminal outcome.
4. Record complete one-, two-, three-, four-, five-, and six-layer chains. Include the one-layer class-6, two-layer class-3, three-layer class-4, and three-layer class-6 steel presets. Each struck layer must receive its own record under one chain ID. No particular cartridge is mandated; use ammunition capable of reaching the requested later layers.
5. Record at least one complete multi-layer chain with a nonzero face-to-face air gap. Change angle, spacing, collider thickness, material, and backstop state; rebuild the fixture and confirm the new geometry and template values appear in the next record.
6. On a multi-layer fixture, capture one `DeviationHit` child and one `FragmentationHit` child striking a later armor layer. Their records must show corrected penetration, velocity, future-outcome, and armor-CF factors with contiguous parent/root lineage. A valid fragmentation event may produce zero children, so only a record with an actual child satisfies this continuation check.
7. In an offline raid, aim at a live bot, select it, and use **Hold Movement + Fire**. Confirm body health and equipped-armor durability changes are recorded. Confirm ordinary live hits do not apply armor durability damage twice.
8. Keep the dead target selected and test covered and uncovered post-death hits. Covered armor must lose durability at zero body health; an uncovered zone must not change armor. If several armor pieces match, confirm the existing game order is retained. Loot the armor and confirm the changed durability persists, with no repeated body-health or death event.
9. Confirm automatic CSV and JSON pairs appear under `BepInEx\plugins\BallisticsLab\Reports`. The acceptance validator admits only nonempty reports with the exact current schema and plugin version, so stale or incomplete files cannot satisfy current-build gates. Use **Save Report Now** only for an immediate checkpoint.
10. End the session, set `Enabled = false`, and restart. The log must say no game methods were patched.
