# RPBB Static Recomp — Rocket Power: Beach Bandits (GameCube, Windows)

A native Windows port of **Rocket Power: Beach Bandits** (`GBQE78`, USA). The playable
release runs on ModernGekko's JIT core at a locked **60 FPS**, with DSP LLE audio and
SDL controller input. The repository also contains the DolRecomp **static C module**
(545,088 instructions, 0 unknown) that was built during the reverse-engineering work;
it runs the same game at 40–45 FPS and is kept as the static-recompilation artefact.

This repository contains **no game data**. You must supply your own legally dumped
copy of the USA disc (`GBQE78`).

## Works with RecompLauncher

This release supports [RecompLauncher](https://recomplauncher.com): install the
recomp, pick your own ISO or extracted folder, choose resolution and graphics, and
press Play. The included `recomplauncher.json` provides:

- **Easy setup** — pick the USA ISO (or extracted folder); the setup helper verifies the
  disc's `GBQE78` ID and DOL hash, and the launcher checks the ISO against the verified
  Redump USA dump.
- **Settings** — internal resolution from 1x (640×528) to 12x (7680×4320), graphics
  API (Vulkan / D3D12 / OpenGL), window mode and the FPS title display.
- **Full setup** — the ISO is extracted to a local game folder by `rpbb-launcher.exe`,
  with progress shown in the launcher; the verified dump gets the RecompLauncher badge.

## Requirements

- Windows 10 or 11, x64.
- A Vulkan-capable GPU (D3D12 and OpenGL are selectable if Vulkan has driver issues).
- A controller. The bundled profile is for a DualSense; other SDL controllers can be
  mapped in `Config/GCPadNew.ini`.
- Your own **Rocket Power: Beach Bandits (USA, GBQE78)** disc image (`.iso`) or an
  extracted game folder containing `sys/main.dol` and `files/`.

The verified dump is the USA release (`DL-DOL-GBQE-USA`), 1,459,978,240 bytes,
CRC32 `8BE53CE8`.

## Play

### RecompLauncher (recommended)

Install the latest release from RecompLauncher, open the game, and pick your copy.
The first setup extracts an ISO automatically. Press Play.

### Standalone

1. Extract the release zip.
2. Run `rpbb-launcher.exe` and pick your USA ISO or extracted game folder (a folder in
   `game/` and a RecompLauncher extraction are detected automatically).
3. Set resolution, graphics API and window mode, then press **Play**. The first ISO run
   extracts the disc to `%LOCALAPPDATA%\RocketPowerRecomp\game`.
4. `rpbb.exe` is the game runtime; the launcher starts it in JIT mode at 60 FPS.

`run.bat` and `rpbb-launcher.exe` also work from the command line:

```
rpbb-launcher.exe --prepare --game "D:\dump.iso" --user-dir "%LOCALAPPDATA%\RocketPowerRecomp"
rpbb-launcher.exe --game "D:\dump.iso" --user-dir "%LOCALAPPDATA%\RocketPowerRecomp"
```

For an extracted folder, point `--game` at the folder.

## Settings

In RecompLauncher the settings appear on the game's page and in Big Picture mode.
They are stored under `%LOCALAPPDATA%\RocketPowerRecomp`:

| Setting | File | Key |
|---|---|---|
| Internal resolution | `config.ini` | `Video/resolution` (`640x528` to `7680x4320`) |
| Graphics API | command line | `--graphics vulkan|d3d12|opengl` |
| Window mode | command line | `--window-mode borderless|fullscreen|windowed` |
| Aspect ratio | `config.ini` | `Video/aspect` (`auto`, `wide`, `standard`, `stretch`) |
| Widescreen hack | `config.ini` | `Video/widescreen_hack` (`true` / `false`) |
| Show FPS | `config.ini` | `Video/show_fps_in_title` |
| Audio output | `config.ini` | `Video/mute`, `Video/volume` |

The launcher offers the same resolution presets as the SpongeBob recomp: 1x
(640×528), 2x (1280×720), 3x (1920×1080), 4x (2560×1440), 6x (3840×2160), 8x
(5120×2880) and 12x (7680×4320). The label is the internal render resolution, not the
window size.

Window modes:

- **Fullscreen windowed (default)** — borderless at your desktop resolution.
- **Fullscreen** — switches the display to your monitor’s native resolution at 60 Hz
  and restores the desktop mode when the game exits.
- **Windowed** — the runner’s normal window.

Defaults: Vulkan, 3x internal resolution (1920×1584, suitable for 1080p), auto framing,
60 FPS speed limit, DSP LLE, Cubeb audio.

### Widescreen

The launcher and RecompLauncher expose the same framing options as the SpongeBob recomp:

- **Aspect ratio** — **Auto (game native)** keeps the game's 4:3 framing;
  **16:9 widescreen** fills a widescreen display; **4:3 standard** forces 4:3;
  **Stretch** stretches the 4:3 image to the window.
- **Widescreen hack (stretch 3D)** — widens the 3D projection to match 16:9. Enable it
  together with **16:9 widescreen** for a full-width 3D picture.

The 3D scene fills 16:9 correctly; the 2D HUD may stretch horizontally. Menus and other
2D screens may still render with their original 4:3 framing.

## Audio

The game uses a custom DSP microcode. DSP HLE produces silence, so the release uses
**DSP LLE**. The required DSP ROM/coefficient files are in `Sys/GC`.

If the game is silent, check the Windows volume mixer: `rpbb` must not be muted.
Audio is in sync at 60 FPS.

## Saves

The game uses a folder-based memory card:

```
%LOCALAPPDATA%\RocketPowerRecomp\GC\USA\Card A\
```

RecompLauncher backs this folder up after each session. To bring saves from Dolphin,
copy the `.gci` files from your Dolphin memory card into that folder. Card images
(`.raw`, `.mcd`) are not read directly.

## Controls

The bundled `defaults/GCPadNew.ini` maps a DualSense. The GameCube controls are:

| GameCube | Action | DualSense (bundled) |
|---|---|---|
| A | Jump / confirm | Cross |
| B | Cancel / attack | Circle |
| X / Y | Actions | Square / Triangle |
| Start | Pause | Options |
| Left stick / D-pad | Move | Left stick / D-pad |
| C stick | Camera | Right stick |
| L / R | Triggers | L2 / R2 |

For an Xbox or Switch Pro controller, edit `Config/GCPadNew.ini` and set the `Device`
line to the SDL name of your controller, or delete the file to use SDL's default
mapping. A controller is required; this build has no keyboard gameplay mapping.

## Verified dump

| | |
|---|---|
| Name | Nickelodeon Rocket Power: Beach Bandits (USA) |
| Disc ID | `GBQE78` (Redump: `DL-DOL-GBQE-USA`) |
| Size | 1,459,978,240 bytes |
| CRC32 | `8BE53CE8` |
| DOL SHA-256 | `5F73B291A1B8C33A7CEBDA40638F00F74FF7FAF3A5F3877190CD30B628FFA42D` |

## How it works

- The **static module** is generated by DolRecomp from the game's DOL: 545,088
  instructions, 0 unknown, 134 C chunks. Clang `-O3`, ThinLTO, PGO, x86-64-v3.
- The playable release uses ModernGekko's **JIT core**
  (`MODERNGEKKO_STATICRECOMP=0`) because it holds 60 FPS with headroom on the tested
  hardware (Ryzen 7 7800X3D). The static module is the project's recompilation
  artefact and reaches 40–45 FPS on the same scenes.
- Graphics run through Dolphin's Vulkan backend, audio through DSP LLE + Cubeb, and
  input through SDL.
- `rpbb_launch.exe` extracts a GameCube ISO's Dolphin layout (`sys/` + `files/`),
  verifies the DOL hash, writes default settings and starts the runner.

## Building

The release is built from the DolRecomp + ModernGekko pipeline in the SB-RFD
checkout. The static module build recipes are in `tools/build_pgo_*.bat` and the
runtime launcher is `tools/run_rpbb_jit.ps1`. The generated module and the game data
are not committed.

Release packaging:

```powershell
powershell -ExecutionPolicy Bypass -File tools\make_release_rpbb.ps1 -Version 0.1.1
```

This writes `dist/RocketPower-Recomp-v0.1.1-Windows-x64.zip` with
`recomplauncher.json` at the top of the release folder, next to `rpbb.exe` and
`rpbb-launcher.exe`.

## Layout

```
tools/      build, packaging and launcher helpers
docs/       project state and investigation notes
defaults/   controller profile shipped with the release
work/       extracted disc, game root, logs and evidence (ignored)
build/      recompiler output (ignored)
lib/        upstream recompiler/runtime checkouts (ignored)
```

## Licenses

The runtime contains Dolphin-derived code (GPL-2.0) and DolRecomp. See
`LICENSES/` in the release. No game files are included or redistributed.
