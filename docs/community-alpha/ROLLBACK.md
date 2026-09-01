# Rollback

Create a timestamped backup of the existing Lab folder and configuration before extracting the package. Keep that backup until testing is finished.

To roll back manually:

1. Close every SPT process.
2. Restore the backed-up `BepInEx\plugins\BallisticsLab` folder.
3. Restore `BepInEx\config\com.janky.ballisticslab.cfg`.
4. If no earlier Lab installation existed, remove only the newly installed Lab folder and configuration.
5. Keep or remove `BepInEx\plugins\BallisticsLab\Reports` separately; rollback never deletes reports automatically.

The `0.3.0-alpha.2` package contains no installer or rollback script. It changes only the packaged Lab DLL when extracted; restore only the paths you backed up.
