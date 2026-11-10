# RPBB Static Recomp v0.1.2

Native Windows release of **Rocket Power: Beach Bandits** (GameCube, USA, `GBQE78`).

## What's new in v0.1.2

- Fixed the landing/collision stutter caused by controller rumble.
- Rumble now runs on a dedicated thread, so controller writes cannot stall a
  frame.
- The game's rapid rumble pulse train is averaged into a steady value instead of
  being passed through as weak on/off bursts.
- Rumble strength is boosted to **3×** the previous output.
- Rumble duration is **⅓** of the previous timeout: 3.33 seconds instead of 10.

## Play

- Runs at a locked **60 FPS** on ModernGekko's JIT core.
- DSP LLE audio, SDL controller input, DualSense profile included.
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
- Known issue: unplugging the controller while the game is running can freeze the
  input thread. Reconnect it before launching; if it freezes, restart the game.
- No game files, saves, or generated modules are in this repository.

## Release asset

`RocketPower-Recomp-v0.1.2-Windows-x64.zip`

SHA-256: `3DF1853E3019E184E3BFDFEC1397FCE0541DEFA43440A657F2A7FBF8DCD83348`
