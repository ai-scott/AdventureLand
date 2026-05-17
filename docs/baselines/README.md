# Baselines (pre-port C# reference data)

Captured against the C# build at git tag `port-baseline-2026-05-16` (commit `b5c5c60`). Use these for regression checks during the C# → GDScript port.

## Captures planned

- `perf_csharp_baseline.csv` — PerfMonitor trace from a full golden-path playthrough. Compare against post-port traces for frame-time + spike regressions.
- `costume_*.png` — 4 screenshots of the player's costume variants. Pixel-diff post-port to catch PaletteSwapper regressions.
- `golden_path_timing.md` — Wall-clock duration of the golden-path playthrough. Within ±10% post-port.

## How to capture (one-time)

1. Launch the C# build (current state of `port/gdscript` HEAD)
2. Press F9 to enable PerfMonitor overlay
3. Play the golden path: TitleScreen → New Game → Penny cat quest → Pete herbs → Sea Monster pearl → trident reveal
4. After Quit, copy `~/Library/Application Support/Godot/app_userdata/AdventureLandPrototype/perf_log.csv` here as `perf_csharp_baseline.csv`
5. For each of the 4 costume variants (helmet on/off, two hair colors), take a screenshot mid-game and save as `costume_helmet_off_dark.png`, etc.
6. Note the playthrough duration in `golden_path_timing.md`
