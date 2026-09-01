# Installation, Update, and Uninstallation

## Requirements

- SPT 4.1.3 exactly.
- EFT 0.16.9.40743.
- Windows client process `EscapeFromTarkov.exe`.

## Install

1. Close SPT, the launcher, server, and game.
2. Back up any existing `BepInEx\plugins\BallisticsLab` folder and `BepInEx\config\com.janky.ballisticslab.cfg`.
3. Extract the package into the SPT root. The DLL installs at:

   `BepInEx\plugins\BallisticsLab\BallisticsLab.dll`

4. Start SPT once. The log should say the Lab loaded disabled and patched no game methods.

## Enable one Lab session

1. Close the game.
2. Edit `BepInEx\config\com.janky.ballisticslab.cfg`.
3. Set `Enabled = true` under `[General]`.
4. Restart, enter the hideout range or an offline local raid, then press `Left Ctrl + F10`.
5. Choose **Start Lab Session** before building fixtures or recording reports.

Set `Enabled = false` and restart when testing is finished.

## Update

Back up the DLL, configuration, and any reports you want to keep. Replace only the Lab DLL, preserve the configuration unless the release notes say otherwise, and verify the installed hash against `BallisticsLab-Docs\SHA256SUMS.txt`.

## Uninstall

Close SPT, save any reports you want to keep, then delete only `BepInEx\plugins\BallisticsLab`. Optionally delete `BepInEx\config\com.janky.ballisticslab.cfg` after saving its settings. Do not delete BepInEx, SPT, or EFT directories.
