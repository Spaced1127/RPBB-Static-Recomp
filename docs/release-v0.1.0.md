# RPBB Static Recomp v0.1.0

Native Windows release of **Rocket Power: Beach Bandits** (GameCube, USA, `GBQE78`).

## Play

- Runs at a locked **60 FPS** on ModernGekko's JIT core.
- DSP LLE audio, SDL controller input, DualSense profile included.
- SpongeBob-style options launcher with PLAY, GRAPHICS, AUDIO and CONTROLLER pages,
  plus internal resolution presets from 1x (640×528) to 12x (7680×4320).
- Fullscreen windowed (borderless, default), exclusive fullscreen (native
  resolution at 60 Hz, desktop mode restored on exit), and windowed modes.
- Widescreen support: 16:9 aspect ratio plus the widescreen hack, with Auto, 4:3 and
  Stretch modes. The 3D scene fills the screen; the 2D HUD may stretch.
- Custom icons on both `rpbb.exe` and `rpbb-launcher.exe`.
- Supports [RecompLauncher](https://recomplauncher.com): pick your own USA ISO or
  extracted folder, choose resolution, graphics and window mode, and press Play.
- First setup extracts an ISO automatically, verifies its `GBQE78` ID and DOL hash,
  and checks the ISO against the verified USA dump.

## Requirements

- Windows 10/11 x64, Vulkan-capable GPU.
- Your own legally dumped USA copy. No game data is included.
- A controller.

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
- No game files, saves, or generated modules are in this repository.

## Release asset

`RocketPower-Recomp-v0.1.0-Windows-x64.zip`

SHA-256: `FCA1154B02508AFAA3A3613DB901F6EA1034AEEA15BC5C0EFF2C3141D23FA592`
