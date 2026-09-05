# BallisticsLab 0.3.1 — SPT 4.1.4 compatibility

Release DLLs omit commit-specific debug provenance so an unchanged implementation rebuilds identically after Git merges. Fixture/runtime methods are unchanged by this packaging correction.

For official SPT 4.1.4 / EFT 0.16.9.5.40743 only. The exact-version guard and verified-game-hash warning are retained.

This release ports the published 0.3.0 baseline. Only compatibility/build provenance and release identity change. Fixture behavior, telemetry, report schemas, configuration and disabled-by-default behavior are unchanged. Separate unpublished development work is not included.

The preview loaded all four patches alongside BallisticPenetration and HollywoodFX during the September 5 Factory-day raid, with no client error entries. This is combined startup/use evidence, not full fixture or campaign acceptance. The user closed further acceptance rounds; controlled fixture/campaign, second-raid and profiler checks were not completed. Finalization changes identity/startup wording only.

The compatibility audit checks 11 game method contracts shared by both companions, resolves runtime references and compares compiled method bodies against published baselines. Portable validation has 163 checks, not a claim of physical certification.

Close Tarkov, then extract `BallisticsLab-0.3.1-SPT-4.1.4.zip` into the SPT root. Keep the existing configuration. The lab remains optional and disabled by default, and uploads nothing. Preserve the previous DLL for rollback.
