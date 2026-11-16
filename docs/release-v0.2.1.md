# RPBB Static Recomp v0.2.1 (hotfix)

Native Windows hotfix for **Rocket Power: Beach Bandits** (GameCube, USA,
`GBQE78`). No game data is included. This is the next public patch after
v0.2.0; it contains both the controller and memory-card fixes.

## What's fixed

- **Xbox pads no longer go dead.** v0.2.0 wrote `GCPadNew.ini` from a controller
  scan that used a different Windows driver set than the game runtime. On Xbox
  pads the SDL device name changes between XInput, HIDAPI/DirectInput, RAWINPUT
  and WGI, so the `Device = SDL/0/<name>` line could name a pad the runtime never
  created and the profile bound nothing. The runner now scans with the runtime's
  own SDL settings, logs the exact device string, and the runtime falls back to
  the first connected SDL pad (with a log line) if a profile name is stale.
  Xbox 360, Xbox One, Xbox Series X|S and Xbox Elite Series 1/2 are covered.
- **Xbox D-pad left/right is no longer inverted.** The previous build treated
  any D-pad whose left binding had a higher raw button number than right as
  reversed. Normal Xbox raw reports do exactly that, so their profile swapped
  Left and Right. Only a reported hat/axis direction can mark a pad reversed now.
- **The Nintendo A/B/X/Y swap is limited to Nintendo-labelled pads.** It used to
  apply to any pad with a reversed raw `a`/`b` order (PlayStation Classic, Wii
  Classic Controller, remapped pads). It now requires an 8BitDo/Nintendo signal
  and never overrides an Xbox or PlayStation pad. Unknown Microsoft product IDs
  (new Xbox SKUs) resolve as Xbox from the vendor ID.
- **Saving and loading work on a fresh install.** The launcher now configures a
  raw 128 Mbit memory card in slot A (`SlotA = 1`) and an empty slot B
  (`SlotB = 255`) on new **and** existing installs, and the RecompLauncher
  manifest applies the same keys. The user-path tree is created before the EXI
  devices initialise, so `GC/USA`, `GC/EUR` and `GC/JAP` exist before the game
  probes the card. This removes the "checking memory card in slot A" hang, the
  GCI-folder default and the "device in slot B is not supported" stop.
- **DualSense Edge and Xbox Elite verified again.** Edge USB/Bluetooth is
  identical to DualSense, Elite Series 1/2 (USB, Bluetooth, BLE) identical to a
  standard Xbox pad, paddles/Fn unbound. 8BitDo Pro 2 (Nintendo, Switch and
  standard modes), Ultimate and 64 verified against the shipped SDL database.

## Please verify

No physical Xbox or 8BitDo pad is available on the build machine; those paths
are verified offline against the SDL database. One earlier build did detect and
bind a live Xbox pad on the build machine (profile and startup log), but
button-level confirmation on this build is still requested. The memory card was
verified end to end with a real save and load.

If a pad is still wrong, send both startup lines from
`%LOCALAPPDATA%\RocketPowerRecomp\logs\run.log`:

```text
controller: "<name>" guid=<32 hex> bus=usb|bluetooth vid=<4 hex> pid=<4 hex> layout=<xbox|playstation|switch|generic> source=<guid|guid-family|mapping|type|default> dpad_swap=<no|yes> face_swap=<no|yes> paddles=unbound extras=unbound family="<model>"
controller configuration: <n> GameCube controller(s) mapped (<layout> layout) device="SDL/0/<name>" profile=<path>
```

- **Xbox 360 / One / Series / Elite:** A jumps, B cancels, X/Y match the printed
  labels, left stick moves, right stick is the C-stick, D-pad moves in the
  pressed direction, Start pauses, LT/RT are L/R, RB is Z.
- **Saves:** create a new save from Story Mode; it is written to
  `GC/MemoryCardA.USA.raw` (16,777,216 bytes). Restarting offers **Continue**,
  and Story Mode -> Load Game lists the save.
- **8BitDo Pro 2 in Nintendo/Switch mode:** the printed A/B and X/Y match the
  actions. Ultimate and 64 stay in the standard positions.

## Play

Unchanged from v0.2.0: 60 FPS JIT core, DSP LLE audio, the launcher's
PLAY/GRAPHICS/AUDIO/CONTROLLER pages, 1x-12x internal resolution, borderless /
exclusive fullscreen / windowed, 16:9 with the widescreen hack, `.iso`, `.rvz`
and extracted folders, RecompLauncher support.

Saves live in `%LOCALAPPDATA%\RocketPowerRecomp\GC\`.

## Requirements

- Windows 10/11 x64, Vulkan-capable GPU.
- Your own legally dumped USA copy. No game data is included.
- Any SDL gamepad: Xbox, DualSense, DualSense Edge, Xbox Elite Series 1/2,
  Switch Pro, 8BitDo or generic.

## Verified USA dump

| | |
|---|---|
| Name | Nickelodeon Rocket Power: Beach Bandits (USA) |
| Disc ID | `GBQE78` (Redump `DL-DOL-GBQE-USA`) |
| Size | 1,459,978,240 bytes |
| CRC32 | `8BE53CE8` |
| DOL SHA-256 | `5F73B291A1B8C33A7CEBDA40638F00F74FF7FAF3A5F3877190CD30B628FFA42D` |

## Release assets

`RocketPower-Recomp-v0.2.1-Windows-x64.zip`
`SHA256SUMS`

Zip size: 19,924,570 bytes
SHA-256: `87940668E45472C6F174EA7B4003D6D60C7DAF9EA7DE48EC5B550B862D2D3CCE`
Staged `rpbb.exe` SHA-256:
`1BD91B1EB29C492E2F6709789601095392281216AE177A9D38185A1784D8B9DE`

## Notes

- This is a controller + memory-card hotfix on top of v0.2.0; rendering, audio
  and save layout are unchanged.
- Known issue: unplugging a controller while the game runs can freeze the input
  thread. Reconnect it before launching; if it freezes, restart the game.
- Do not force-kill the game immediately after saving; let it exit normally so
  the card flush completes.