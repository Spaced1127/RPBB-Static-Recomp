# RPBB Static Recomp v0.2.2 (controller system)

Native Windows release of **Rocket Power: Beach Bandits** (GameCube, USA,
`GBQE78`). No game data is included.

## Controllers: one canonical profile

- **One static `Config/GCPadNew.ini`** with SDL positional bindings. It is
  written on the first run and **never rewritten**. A sleeping wireless pad can
  no longer be replaced by a keyboard profile, and a hand-edited profile stays
  untouched.
- **Two classes.** `positional` keeps the physical A/B/X/Y positions (Xbox,
  PlayStation, generic, 8BitDo XInput). `nintendo` swaps A<->B and X<->Y so the
  printed labels match (Switch Pro, Joy-Con, 8BitDo Switch/Nintendo mode).
  D-pad, sticks and triggers are never swapped, so DualSense/Edge D-pads are no
  longer mirrored.
- **Class resolution:** user override (`controller_class =
  auto|positional|nintendo`, or `--controller-layout`) wins; then the SDL
  gamepad type; then a small 8BitDo Nintendo-mode GUID fallback
  (`2dc8:6006` Pro 2, `05ac:0004` Switch clone).
- **Device binding without name dependence.** If the profile's pad is absent,
  the runtime binds the first enumerated pad in memory and logs
  `controller: configured device missing; bound "..."`. A 2 s watcher does the
  same after launch, so hot-plug works with no profile write and no restart.
- **No migration.** Old `Device = SDL/0/<name>` lines are harmless: the fallback
  binds the first pad and the class is resolved at runtime. Deleting an old
  `GCPadNew.ini` is optional.
- Removed: per-device GUID tables (DualSense Edge, Xbox Elite), the live-mapping
  `a>b` rule, the D-pad button-index correction, launch-time profile generation
  and profile migration.

The startup line is now:

```text
controller: name="SDL/0/<name>" guid=<32 hex> type=<SDL gamepad type> class=<positional|nintendo> device="<bound device>" source=<auto|override|vendor-fallback|default> dpad=normal profile=<static|missing>
```

Include it (and the `configured device missing; bound` line if present) when a
pad is mapped wrongly.

## Please verify

No physical Switch/8BitDo pad is attached to the build machine. The class
matrix is verified offline against the SDL database, the reference was
hand-tested with a physical Xbox pad, and this build's live run used a wired
Xbox One pad. Please confirm:

- Xbox 360/One/Series/Elite, DualSense/Edge, DS4/DS3 and generic pads act
  positional; Switch Pro/Joy-Con and 8BitDo in Switch mode act Nintendo;
  8BitDo in XInput mode stays positional.
- A pad plugged in after launch works without a restart.
- `controller_class=nintendo` forces the swap on a positional pad.

## What was verified here

The reference implementation (WWE WMX8 v0.1.4) was validated by the user with a
physical Xbox pad — bind and buttons work — so the first-pad fallback, class
resolution and write-once profile path are hardware-proven, not just offline.
RPBB v0.2.2 carries the same design plus the fixes listed in
`docs/notes/controller-generic-design.md`.

PlayStation pads remain **positional** by SDL type (PS3/PS4/PS5), so the
DualSense/Edge, DS4 and DS3 behaviour that worked before is unchanged; the
offline matrix covers all of them.

- Offline suite: **46 checks, 0 failed** (class matrix, override precedence,
  8BitDo fallback, canonical profile, write-once, legacy keys).
- Live wired Xbox One (`0300fa675e040000ff02000000007801`): startup line
  `type=xboxone class=positional`, canonical profile written on first run
  (`profile=written` in a direct run; the launcher pre-writes it), second run
  `profile=static` with the profile SHA unchanged.
- Forced `controller_class=nintendo`: `class=nintendo override=nintendo`, file
  unchanged.
- Missing profile device: `controller: configured device missing; bound
  "SDL/0/Xbox One Controller"` and the game runs at **59.94 FPS / speed 1.00**.
- Fresh boot creates `GC/MemoryCardA.USA.raw` (16,777,216 bytes).
- Not verified: physical button presses, a real Switch/8BitDo pad, and unplug/
  replug hot-plug. The watcher calls the same binding helper every 2 s.

## Memory card

Unchanged from v0.2.1: raw 128 Mbit card in slot A, empty slot B, `GC/`
directories created before EXI init, one-time import of a legacy folder card.
Save and load were verified on v0.2.1.

## Play

60 FPS JIT core, DSP LLE audio, the launcher pages, 1x-12x internal resolution,
borderless/exclusive fullscreen/windowed, 16:9 with the widescreen hack, `.iso`,
`.rvz` and extracted folders, RecompLauncher support. Saves live in
`%LOCALAPPDATA%\RocketPowerRecomp\GC\MemoryCardA.USA.raw`.

## Requirements

- Windows 10/11 x64, Vulkan-capable GPU.
- Your own legally dumped USA copy. No game data is included.
- Any SDL gamepad.

## Verified USA dump

| | |
|---|---|
| Name | Nickelodeon Rocket Power: Beach Bandits (USA) |
| Disc ID | `GBQE78` (Redump `DL-DOL-GBQE-USA`) |
| Size | 1,459,978,240 bytes |
| CRC32 | `8BE53CE8` |
| DOL SHA-256 | `5F73B291A1B8C33A7CEBDA40638F00F74FF7FAF3A5F3877190CD30B628FFA42D` |

## Release assets

`RocketPower-Recomp-v0.2.2-Windows-x64.zip`
`SHA256SUMS`

Size: 19,920,322 bytes
SHA-256: `EAEE73818FEEBFE07D81BC54929728053A310C9E6BC5000D62E8F27FA645D6FF`
Staged `rpbb.exe` SHA-256:
`2B3C0BE4B6BA5082D88129FD57DE4649A871A4F8C4ED8029DDA72DAEE87DE630`

## Notes

- controllers: one canonical profile, positional/nintendo classes, no profile
  rewriting, first-pad fallback + hot-plug.
- Controller-only change on top of v0.2.1; rendering, audio and saves are
  unchanged.
- Known issue: unplugging a controller while the game runs can freeze the input
  thread. Reconnect it before launching; if it freezes, restart the game.