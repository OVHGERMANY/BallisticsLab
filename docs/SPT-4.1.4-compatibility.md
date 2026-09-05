# SPT 4.1.4 compatibility preview

## Finalization follow-up

After combined preview startup and first-raid use, the user closed further acceptance rounds and requested release completion. Final `0.3.1` keeps the preview implementation, changing identity/startup text only. The final build and 163 checks pass; the 11-contract compiled audit has zero failures. Package/installed hashes are recorded in `IMPLEMENTATION_STATUS.md`. See [release notes](SPT-4.1.4-release.md) for test limitations. The preview/no-publication statements below describe the earlier acceptance-only phase.

Local build `0.3.1-preview.1` starts from published `v0.3.0` commit `2f99708466a693ebe9e63c75250905c91548d5b9`, not the separate development worktree. It targets official SPT 4.1.4 and EFT 0.16.9.5.40743 only. Both the BepInEx minimum dependency and exact runtime equality use 4.1.4; 4.1.3, 4.1.5 and 4.1.4.0 are rejected.

The installed game assembly SHA-256 is `EE25CEE1259777B38ED8B3E7841FDC2DB3C98540B1469FA539B1FF183476E436`. The prior verified-hash constant is replaced with this measured target, not disabled. The existing hash-mismatch warning and fail-closed method resolution behavior are preserved.

The [official migration notice](https://github.com/SP-Tushonka/wiki/blob/main/modding/SPT_41_Modding/414_Changes.md) lists renamed serialized fields. Targeted source searches found no references to those listed types in either companion mod. No asset bundle is changed by this port.

The unchanged published source and final candidate both build against the current client with zero warnings/errors, and each installed-catalog validation passes 163 checks. The audit verified all four Harmony targets, five reflected hideout members and their unchanged normalized IL against the backed-up 4.1.3 game assembly. All 290 directly referenced runtime members resolve. Across 1,291 plugin method bodies, nine changed only in identity/compatibility strings or the exact-version constructor using its shared constant. Report schemas and fixture/physics behavior are unchanged.

Installed with Tarkov closed at `2026-09-05T11:30:06-05:00`; compiled/staged/installed SHA-256 is `4D20EA3953D66A605B7A34DC37916DA02A60F2C6E19ABCDF2FBB06FAEFC20091` (268,288 bytes). The transaction replaced only this DLL and BallisticPenetration; all 22 protected config/unrelated DLL files were unchanged. Original DLLs and configuration backups are in `E:\Games\SPT-Mod-Backups\20260905-112852-spt414-client-compatibility`. Fresh client startup, fixture interaction and raid acceptance remain pending.

No fixture, telemetry, physics, configuration or schema behavior is changed. Numeric plugin/report identity becomes 0.3.1; the assembly informational version and startup log include preview.1. No stable release, merge, push or tag is included in this local acceptance task.

## Fresh client startup

PID 2816 started at 11:30:56 -05:00 on 2026-09-05. Fresh `BepInEx\LogOutput.log` lines 57-60 enable all four Lab patches; line 61 identifies 0.3.1-preview.1 loaded for SPT 4.1.4. BallisticPenetration and HollywoodFX previews also loaded. The 11:32:49 snapshot contained zero client error lines, zero spark details and zero raid summaries. Startup is verified; fixture use, shot/campaign behavior and raid cleanup still need user-led acceptance.
