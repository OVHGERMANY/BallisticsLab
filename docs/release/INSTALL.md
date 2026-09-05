# BallisticsLab 0.3.1

## Requirements

- SPT 4.1.4
- EFT 0.16.9.5.40743

## Install

1. Close SPT, its launcher, server, and game.
2. Extract the ZIP directly into the SPT installation folder.
3. Confirm this file exists:

   `BepInEx\plugins\BallisticsLab\BallisticsLab.dll`

4. Start SPT once. BallisticsLab is disabled by default and applies no game patches while disabled.

## Enable and use

1. Close the game.
2. Open `BepInEx\config\com.janky.ballisticslab.cfg`.
3. Set `Enabled = true` under `[General]`.
4. Restart SPT, enter the hideout shooting range or an offline local raid, and press `Left Ctrl + F10`.
5. Select **Start Lab Session** before using fixtures or reports.

The plugin does not fire the player's weapon. It creates local fixtures and records local CSV/JSON reports when enabled.

## Update

Close SPT, preserve the configuration and any reports you want, then extract the new ZIP over the SPT folder. The included `SHA256SUMS.txt` identifies the packaged DLL.

## Uninstall

Close SPT and remove only `BepInEx\plugins\BallisticsLab`. To remove saved settings too, remove `BepInEx\config\com.janky.ballisticslab.cfg`.

## Compatibility and privacy

This build supports SPT 4.1.4 only. Reports remain on the local computer under `BepInEx\plugins\BallisticsLab\Reports`; nothing is uploaded automatically. Review reports before sharing because they can contain session, fixture, ammunition, target, and timestamp identifiers.

BallisticsLab provides game-simulation data. Its GOST-oriented screens are not physical certification.
