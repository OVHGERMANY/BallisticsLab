# BallisticsLab

BallisticsLab is an opt-in SPT 4.1.2 client-side range and telemetry plugin for repeatable terminal-ballistics testing.

The verified baseline, historical evidence, remaining acceptance gates, and deferred stages are tracked in [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md).

It creates disposable plate fixtures from EFT's own armor templates, supports one-to-six physical layers, angle and spacing controls, game-labelled Granit BR4 and BR5 presets, armored-steel stacks, quick material selection, bot selection and freeze controls, shot-chain visualization, and CSV/JSON reports. It never inserts test items into the profile, stash, or inventory.

The plugin is disabled by default and installs no game patches while disabled. Set `Enabled = true` in `BepInEx\config\com.janky.ballisticslab.cfg`, restart the game, enter the hideout shooting range or an offline local raid, then press `Left Ctrl + F10`. A lab session exists only after the panel's explicit **Start Lab Session** action and is destroyed on world change or plugin shutdown.

Fixture plates use generic ballistic colliders so separate physical layers cannot be suppressed as overlapping body-part colliders. For penetrated fixture hits, the lab applies EFT's otherwise body-collider-only child-shot damage, penetration, deviation-velocity, future-outcome, and armor-CF factors before the child reaches the next layer. Telemetry records the original and corrected values for every continuation in the chain.

The BR4 and BR5 buttons select EFT database templates bearing those names. They are repeatable game-data presets, not a claim of real-world certification. Layer spacing is the physical air gap from the back face of one collider to the front face of the next. Plate thickness controls physical collider geometry; armor class, durability, material resistance, and penetration chance come from the selected EFT template.

Normal `Build` does not alter the live installation. Explicit deployment:

```powershell
dotnet msbuild .\src\BallisticsLab\BallisticsLab.csproj -t:Deploy -p:Configuration=Release -p:SptRoot=E:\Games\SPT
```

Reports are written under `BepInEx\plugins\BallisticsLab\Reports` only when the user presses an export button. JSON metadata and every CSV row identify the exact plugin version and report schema that produced the evidence. Ammunition identity and physical values are read from the same live `AmmoTemplate`; pooled item identity is used only when no template is available. Reports include fixture identity and geometry, template/material/class data, calculated resistance and penetration chance, incoming and decision values, child continuation factors, parent/root shot identity, target state, armor durability changes, and the complete recorded shot chain. Post-death armor changes remain visible in the latest-shot panel even when body health is already zero.

## Manual acceptance

1. Set `Enabled = true` and restart the game. Confirm the BepInEx log reports four BallisticsLab patches.
2. Enter the hideout or an offline local raid, press `Left Ctrl + F10`, and choose **Start Lab Session**.
3. Test one Granit BR4 fixture and one Granit BR5 fixture. Fire at the red center mark and confirm the panel records the selected template, class, material, resistance, penetration chance, durability change, and terminal outcome.
4. Record complete one-, two-, three-, four-, five-, and six-layer chains. Include the one-layer class-6, two-layer class-3, three-layer class-4, and three-layer class-6 steel presets. Each struck layer must receive its own record under one chain ID. No particular cartridge is mandated; use ammunition capable of reaching the requested later layers.
5. Record at least one complete multi-layer chain with a nonzero face-to-face air gap. Change angle, spacing, collider thickness, material, and backstop state; rebuild the fixture and confirm the new geometry and template values appear in the next record.
6. On a multi-layer fixture, capture one `DeviationHit` child and one `FragmentationHit` child striking a later armor layer. Their records must show corrected penetration, velocity, future-outcome, and armor-CF factors with contiguous parent/root lineage. A valid fragmentation event may produce zero children, so only a record with an actual child satisfies this continuation check.
7. In an offline raid, aim at a live bot, select it, and use **Hold Movement + Fire**. Confirm body health and equipped-armor durability changes are recorded. Confirm ordinary live hits do not apply armor durability damage twice.
8. Keep the dead target selected and test covered and uncovered post-death hits. Covered armor must lose durability at zero body health; an uncovered zone must not change armor. If several armor pieces match, confirm the existing game order is retained. Loot the armor and confirm the changed durability persists, with no repeated body-health or death event.
9. Export CSV and JSON once after the test batch. Confirm both files appear under `BepInEx\plugins\BallisticsLab\Reports`; exporting does not clear accumulated records.
10. End the session, set `Enabled = false`, and restart. The log must say no game methods were patched.
