# Rollback

The integrated installer creates a timestamped backup before replacing any installed file. Keep that folder until testing is finished.

To roll back manually:

1. Close every SPT process.
2. Restore the backed-up `BepInEx\plugins\BallisticsLab` folder.
3. Restore `BepInEx\config\com.janky.ballisticslab.cfg`.
4. If no earlier Lab installation existed, remove only the newly installed Lab folder and configuration.
5. Keep or remove `BepInEx\plugins\BallisticsLab\Reports` separately; rollback never deletes reports automatically.

The package rollback script accepts an explicit SPT root and restores only files listed in its backup manifest.
