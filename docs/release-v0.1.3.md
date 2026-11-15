# RPBB Static Recomp v0.1.3

Native Windows release of **Rocket Power: Beach Bandits** (GameCube, USA, `GBQE78`).

## What's new in v0.1.3

- **Automatic controller setup.** The runner detects the connected SDL gamepad on
  every launch, so RecompLauncher does not need its own controller picker.
- Xbox, PlayStation, Switch Pro and generic SDL controllers are supported.
- The runner updates the controller device automatically when you switch pads.
- Switch controllers use the Nintendo face-button layout: A/B and X/Y are swapped
  from the physical south/east/west/north positions.
- Custom mappings are preserved when `controller_profile=manual` is set under
  `[Input]` in `config.ini`.
- Includes the v0.1.2 rumble fix: async rumble, 3× strength and ⅓ duration.

## Play

- Runs at a locked **60 FPS** on ModernGekko's JIT core.
- DSP LLE audio, SDL controller input.
- SpongeBob-style options launcher with PLAY, GRAPHICS, AUDIO and CONTROLLER
  pages.
- Internal resolution presets from 1x (640×528) to 12× (7680×4320).
- Fullscreen windowed (borderless, default), exclusive fullscreen (native
  resolution at 60 Hz, desktop mode restored on exit), and windowed modes.
- Widescreen support: 16:9 aspect ratio plus the widescreen hack, with Auto, 4:3
  and Stretch modes.
- Custom box-art icons on `rpbb.exe` and `rpbb-launcher.exe`.
- Supports [RecompLauncher](https://recomplauncher.com): pick your own USA ISO or
  extracted folder, choose resolution, aspect, graphics and window mode, and press
  Play.
- First setup extracts an ISO automatically, verifies its `GBQE78` ID and DOL hash,
  and checks the ISO against the verified USA dump.

## Requirements

- Windows 10/11 x64, Vulkan-capable GPU.
- Your own legally dumped USA copy. No game data is included.
- Any SDL gamepad: Xbox, DualSense, Switch Pro or generic.

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

## Release asset

`RocketPower-Recomp-v0.1.3-Windows-x64.zip`

SHA-256: `778D38552C975D2159A5B07A8C7DCE8AC9074E4ECAA635DE48648B0BB92BF886`
