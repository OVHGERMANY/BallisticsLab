# BallisticsLab

BallisticsLab is an opt-in SPT 4.1.2 client-side range and telemetry plugin for repeatable terminal-ballistics testing.

It creates disposable plate fixtures from EFT's own armor templates, supports one-to-six physical layers, angle and spacing controls, game-labelled Granit BR4 and BR5 presets, armored-steel stacks, quick material selection, bot selection and freeze controls, shot-chain visualization, and CSV/JSON reports. It never inserts test items into the profile, stash, or inventory.

The plugin is disabled by default and installs no game patches while disabled. Set `Enabled = true` in `BepInEx\config\com.janky.ballisticslab.cfg`, restart the game, enter the hideout shooting range or an offline local raid, then press `Left Ctrl + F10`. A lab session exists only after the panel's explicit **Start Lab Session** action and is destroyed on world change or plugin shutdown.

Fixture plates use generic ballistic colliders so separate physical layers cannot be suppressed as overlapping body-part colliders. For penetrated fixture hits, the lab applies EFT's otherwise body-collider-only child-shot damage, penetration, deviation-velocity, future-outcome, and armor-CF factors before the child reaches the next layer. Telemetry records the original and corrected values for every continuation in the chain.

The BR4 and BR5 buttons select EFT database templates bearing those names. They are repeatable game-data presets, not a claim of real-world certification. Layer spacing is the physical air gap from the back face of one collider to the front face of the next. Plate thickness controls physical collider geometry; armor class, durability, material resistance, and penetration chance come from the selected EFT template.

Normal `Build` does not alter the live installation. Explicit deployment:

```powershell
dotnet msbuild .\src\BallisticsLab\BallisticsLab.csproj -t:Deploy -p:Configuration=Release -p:SptRoot=E:\Games\SPT
```

Reports are written under `BepInEx\plugins\BallisticsLab\Reports` only when the user presses an export button. Ammunition identity and physical values are read from the same live `AmmoTemplate`; pooled item identity is used only when no template is available. Reports include fixture identity and geometry, template/material/class data, calculated resistance and penetration chance, incoming and decision values, child continuation factors, parent/root shot identity, target state, armor durability changes, and the complete recorded shot chain. Post-death armor changes remain visible in the latest-shot panel even when body health is already zero.

## Manual acceptance

1. Set `Enabled = true` and restart the game. Confirm the BepInEx log reports four BallisticsLab patches.
2. Enter the hideout or an offline local raid, press `Left Ctrl + F10`, and choose **Start Lab Session**.
3. Place a single Granit BR4 or BR5 fixture and fire at the red center mark. Confirm the panel records the selected template, class, material, resistance, penetration chance, durability change, and terminal outcome.
4. Place the two-layer class-3 and three-layer class-4/class-6 steel presets. Confirm each physical layer receives its own record and all records share one chain ID. A deviated or fragmented continuation must show its penetration, velocity, future-outcome, and armor-CF factors before the next layer.
5. Change angle, spacing, collider thickness, material, and backstop state; rebuild the fixture and confirm the new geometry and template values appear in the next record.
6. In an offline raid, aim at a live AI target, select it, and use **Hold Movement + Fire**. Confirm body health and equipped-armor durability changes are recorded. A dead target remains selected for postmortem armor telemetry until **Clear Selection** is pressed.
7. Export CSV and JSON and confirm both files appear under `BepInEx\plugins\BallisticsLab\Reports` with the current shot chain.
8. End the session, set `Enabled = false`, and restart. The log must say no game methods were patched.
