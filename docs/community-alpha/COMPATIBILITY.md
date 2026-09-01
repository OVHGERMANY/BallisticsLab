# Compatibility

## Supported environment

- SPT 4.1.3 exactly.
- EFT 0.16.9.40743.
- Windows and `EscapeFromTarkov.exe`.
- BepInEx and the SPT runtime bundled with that SPT release.

When enabled, the Lab patches `Shot.HandleCollision`, `Shot.CreateFragments`, `ClientGameWorld.ShotDelegate`, and the player command path used by its panel. It fails closed when an exact target cannot be resolved.

The optional physical telemetry connection is reflection-only. The Lab remains loadable when the publisher is absent or incompatible. The package contains no EFT, Unity, Harmony, BepInEx, or SPT runtime assembly.
