# Privacy and Telemetry

BallisticsLab never uploads telemetry, logs, reports, system information, or player data. Report creation is local and evidence submission is voluntary.

Reports are stored under:

`BepInEx\plugins\BallisticsLab\Reports`

They can contain plugin and schema versions, fixture and campaign IDs, ammunition template IDs, pooled shot and parent/root identities, target-surface identities, target and armor data, collision chains, physical component lineage, deterministic seeds, and local session timestamps. These fields are useful for reproduction but may reveal session or profile context when combined with other logs.

Before sharing a report:

1. Open the JSON and CSV in a text editor.
2. Remove unrelated chains and any field you consider identifying.
3. Attach only the smallest report pair needed to reproduce the defect.
4. Never attach a full game directory, profile, EFT assembly, or private credential.

To stop report generation, set `Enabled = false` and restart. While the Lab remains enabled, set `Automatic Report Saving = false` to disable automatic saves; manual saves remain user-controlled. To delete existing evidence, close SPT and remove only the files inside the Reports directory.
