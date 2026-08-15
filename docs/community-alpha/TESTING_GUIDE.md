# Community Alpha Testing Guide

BallisticsLab is an optional testing and telemetry tool. It is disabled by default and does not patch the game while disabled.

## Basic workflow

1. Enable the Lab in its configuration and restart.
2. Enter the hideout shooting range or an offline local raid.
3. Press `Left Ctrl + F10` and start a Lab session.
4. Build a fixture or select a bot, return to shooting, and fire the test round yourself.
5. Reopen the panel to inspect evidence. Automatic saving writes changed evidence without requiring a manual export.
6. End the session, disable the Lab, and restart when finished.

## Community priorities

- every ammunition family, rare rounds, and different barrel velocities;
- one through six armor layers and nonzero air gaps;
- intact penetration, stops, ricochets, deviations, and fragmentation;
- projectile fragments and target-spall fragments continuing into later targets;
- body entry/exit and live versus post-death armor durability;
- all fixture materials, Granit presets, steel stacks, and the witness backstop;
- long sessions, automatic report batching, pooling, cleanup, and frame time;
- compatibility with common SPT mods.

Ordinary gameplay bugs do not require a Lab report. For controlled evidence, attach the smallest JSON/CSV pair that contains the relevant chain and review it for personal information first.
