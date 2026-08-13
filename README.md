# BallisticsLab

BallisticsLab is an opt-in SPT 4.1.2 client-side range and telemetry plugin for repeatable terminal-ballistics testing.

The verified baseline, historical evidence, remaining acceptance gates, and deferred stages are tracked in [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md).

It creates disposable plate fixtures from EFT's own armor templates, supports one-to-six physical layers, angle and spacing controls, game-labelled Granit BR4 and BR5 presets, armored-steel stacks, quick material selection, bot selection and freeze controls, shot-chain visualization, and CSV/JSON reports. It never inserts test items into the profile, stash, or inventory.

The plugin is disabled by default and installs no game patches while disabled. Set `Enabled = true` in `BepInEx\config\com.janky.ballisticslab.cfg`, restart the game, enter the hideout shooting range or an offline local raid, then press `Left Ctrl + F10`. A lab session exists only after the panel's explicit **Start Lab Session** action and is destroyed on world change or plugin shutdown. Each changed shot-chain batch saves as a timestamped CSV/JSON pair after a completed burst and once more when the session or world ends. A later continuation includes its earlier parent records, so every pair remains independently auditable without copying unrelated saved chains. Manual saving remains available as a backup; empty or unchanged state is not exported again.

Fixture plates use generic ballistic colliders so separate physical layers cannot be suppressed as overlapping body-part colliders. For penetrated fixture hits, the lab applies EFT's otherwise body-collider-only child-shot damage, penetration, deviation-velocity, future-outcome, and armor-CF factors before the child reaches the next layer. Telemetry records the original and corrected values for every continuation in the chain.

The active development branch also contains session-scoped physical telemetry. It discovers an optional schema-1 publisher only from assemblies already loaded by the host, subscribes only while a Lab session is active, and immediately copies prepared and resolved events into immutable BallisticsLab-owned records. The copy includes host identity, measured target geometry, complete projectile or fragment state, material provenance, collision history, and conservation data; no foreign event, collection, pooled shot, or Unity object is retained. BallisticsLab remains loadable when the publisher is absent or incompatible. Events pair by exact transition ID in first-seen order: prepared-only evidence remains pending, resolved-only evidence is marked orphaned, and duplicate stages are counted without replacing the first accepted event or creating duplicate transitions. Report schema 4 writes this evidence to a separate JSON `physicalTransitions` collection and can save a physical-only report while leaving the flat shot-record CSV columns unchanged.

The BR4 and BR5 buttons select EFT database templates bearing those names. They are repeatable game-data presets, not a claim of real-world certification. Layer spacing is the physical air gap from the back face of one collider to the front face of the next. Plate thickness controls physical collider geometry; armor class, durability, material resistance, and penetration chance come from the selected EFT template.

Normal `Build` does not alter the live installation. Explicit deployment:

```powershell
dotnet msbuild .\src\BallisticsLab\BallisticsLab.csproj -t:Deploy -p:Configuration=Release -p:SptRoot=E:\Games\SPT
```

The repository pins .NET SDK `10.0.303` in `global.json`. Release builds normalize source paths and omit the Git commit from `AssemblyInformationalVersion`, so documentation-only commits do not change the plugin DLL. Track the exact source commit and DLL SHA-256 in the release evidence instead. The explicit `Deploy` target finishes by hashing the compiled and installed assemblies and fails if they are not byte-identical; `VerifyDeployment` can also be invoked directly to recheck an existing installation.

Reports are written under `BepInEx\plugins\BallisticsLab\Reports` automatically while a nonempty session changes, at teardown, or when the user requests an immediate save. Automatic pairs contain changed chains plus the parent records required to validate those chains and the current unsaved physical-transition evidence; manual saves remain full-session snapshots. A physical transition can produce a report without an ordinary shot record, in which case the CSV contains its unchanged header and the JSON carries the physical evidence. JSON metadata and every CSV row identify the exact plugin version and report schema that produced the evidence. Ammunition identity and physical values are read from the same live `AmmoTemplate`; pooled item identity is used only when no template is available. Reports include fixture identity and geometry, template/material/class data, calculated resistance and penetration chance, incoming and decision values, child continuation factors, parent/root shot identity, target state, armor durability changes, the complete recorded shot chain, physical mass and geometry, projectile and target-material provenance, collision history, terminal/render state, and mass/energy closure. Post-death armor changes remain visible in the latest-shot panel even when body health is already zero.

## Armor testing and controlled acceptance

For ordinary armor testing, use any weapon and ammunition. Build the plate or stack you want, fire at it, and read the resulting penetration, stop, deviation, fragmentation, durability, and chain telemetry. No prescribed cartridge or manual export is required.

Controlled acceptance is separate. Only a shot fired through a known Lab fixture can satisfy a fixture-chain gate; unrelated raid and bot records cannot. When a reproducible gate is needed, use the exact fixture named in the checklist and ammunition capable of reaching its final layer. Automatic saving captures the result without interrupting the session.

The validation console reports controlled coverage without treating missing manual evidence as a calculation failure:

```powershell
dotnet run --project .\tests\BallisticsLab.Validation\BallisticsLab.Validation.csproj -c Release --no-build -- E:\Games\SPT\SPT_Runtime\SPT_Data\database\templates\items.json E:\Games\SPT\BepInEx\plugins\BallisticsLab\Reports
```

Add `--require-report-coverage` only at the final report-evidence gate. It exits with code `1` while any controlled report item is still missing. Direct corpse observations listed below remain separate because reports cannot prove the loot screen, armor call order, or absence of repeated death events.

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
