# RPBB Static Recomp v0.2.0

Native Windows release of **Rocket Power: Beach Bandits** (GameCube, USA, `GBQE78`).

## What's new in v0.2.0

- **Native RVZ support.** Drop a Dolphin `.rvz` straight into the standalone
  launcher or `run.bat`; no manual RVZ-to-ISO conversion. The bundled
  `rvz_extract.exe` decodes the RVZ, then the normal FST extractor produces the
  game files. RecompLauncher's own picker still offers ISO and extracted folders;
  use the standalone launcher or `run.bat` for RVZ.
- **8BitDo layout fix.** The runner now reads the live SDL `a`/`b` and
  `dpleft`/`dpright` bindings per GUID instead of guessing from the product name.
  8BitDo Pro 2, SN30, SF30 and Switch-mode pads in Nintendo layout get the correct
  A/B/X/Y swap. 8BitDo Ultimate and 64 pads keep the standard layout. A firmware
  that reports a reversed D-pad is corrected left/right.
- **DualSense Edge support.** USB and Bluetooth variants use the standard
  DualSense layout. Paddles and the Fn button are intentionally unbound.
- **Xbox Elite Series 1/2 support.** `045e:02e3`, `045e:0b00`, `045e:0b05` and
  `045e:0b22` use the standard Xbox layout. Paddles are intentionally unbound.
- **Controller profiles migrate.** Existing generated profiles pick up the newly
  detected layout and D-pad order on the next launch. Hand-edited profiles for
  the same pad are preserved. `controller_profile=manual` keeps a profile
  untouched, and `controller_profile=switch` (or `nintendo`) still forces the
  Nintendo A/B/X/Y swap.

## Please verify

Physical controller hardware was not available for this build, so the controller
paths below were verified offline. Please check them and report anything that is
off.

- **8BitDo Pro 2 in Nintendo/XInput or Switch mode:** A, B, X and Y should match
  the printed labels; D-pad left/right, sticks, C-stick, triggers and rumble
  should work. Ultimate and 64 should stay in the standard (non-swapped) layout.
- **DualSense Edge (USB and Bluetooth):** should behave exactly like a standard
  DualSense. Paddles and Fn should do nothing.
- **Xbox Elite Series 1 and 2:** should behave exactly like a standard Xbox pad.
  Paddles should do nothing unless you map them to duplicate buttons in the Xbox
  Accessories app.

If a pad is mapped wrongly, send the runner's startup `controller:` line and the
following `controller configuration:` line. The `controller:` line contains the
name, 32-character GUID, resolved layout, `dpad_swap`, `face_swap`, and family:

```text
controller: "<name>" guid=<32 hex> bus=usb|bluetooth vid=<4 hex> pid=<4 hex> layout=<xbox|playstation|switch|generic> source=<guid|mapping|type|default> dpad_swap=<no|yes> face_swap=<no|yes> paddles=unbound extras=unbound family="<model>"
```

## Play

- Runs at a locked **60 FPS** on ModernGekko's JIT core.
- DSP LLE audio, SDL controller input.
- SpongeBob-style options launcher with PLAY, GRAPHICS, AUDIO and CONTROLLER
  pages.
- Internal resolution presets from 1x (640x528) to 12x (7680x4320).
- Fullscreen windowed (borderless, default), exclusive fullscreen (native
  resolution at 60 Hz, desktop mode restored on exit), and windowed modes.
- Widescreen support: 16:9 aspect ratio plus the widescreen hack, with Auto, 4:3
  and Stretch modes.
- Custom box-art icons on `rpbb.exe` and `rpbb-launcher.exe`.
- Supports [RecompLauncher](https://recomplauncher.com): pick your own USA ISO or
  extracted folder, choose resolution, aspect, graphics and window mode, and press
  Play. The standalone launcher and `run.bat` also accept `.rvz`.

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

## Notes

- The playable release uses ModernGekko's JIT core because it holds 60 FPS with
  headroom. The static DolRecomp C module (545,088 instructions, 0 unknown) is kept
  in the repository as the static-recompilation artefact and reaches 40-45 FPS.
- Audio requires DSP LLE. If the game is silent, check the Windows volume mixer.
- Saves live in `%LOCALAPPDATA%\RocketPowerRecomp\GC\USA\Card A`.
- Known issue: unplugging the controller while the game is running can freeze the
  input thread. Reconnect it before launching; if it freezes, restart the game.
- No game files, saves, or generated modules are in this repository.

## Release assets

`RocketPower-Recomp-v0.2.0-Windows-x64.zip`
`SHA256SUMS`

SHA-256: `9749F76CC5F8DA1FDADA4E88927E1CF3B1A2F3EE6042680E9D5D41054F6EB392`
