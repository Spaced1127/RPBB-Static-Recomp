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
- **Settings** — internal resolution, graphics API (Vulkan / D3D12 / OpenGL) and the
  FPS title display.
- **Full setup** — the ISO is extracted to a local game folder by `rpbb_launch.exe`,
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
2. Either:
   - put your extracted game folder in `game/` (it must contain `sys/main.dol` and
     `files/`), then run `run.bat`; or
   - run `run.bat "D:\path\to\Rocket Power Beach Bandits.iso"`. The first run extracts
     the ISO to `%LOCALAPPDATA%\RocketPowerRecomp\game`.
3. The game starts in JIT mode at 60 FPS.

`rpbb_launch.exe` is the setup/start helper. It can be used directly:

```
rpbb_launch.exe --prepare --game "D:\dump.iso" --user-dir "%LOCALAPPDATA%\RocketPowerRecomp"
rpbb_launch.exe --game "D:\dump.iso" --user-dir "%LOCALAPPDATA%\RocketPowerRecomp"
```

For an extracted folder, point `--game` at the folder.

## Settings

In RecompLauncher the settings appear on the game's page and in Big Picture mode.
They are stored under `%LOCALAPPDATA%\RocketPowerRecomp`:

| Setting | File | Key |
|---|---|---|
| Internal resolution | `Config/GFX.ini` | `Settings/InternalResolution` (1, 2 or 3) |
| Graphics API | command line | `--graphics vulkan|d3d12|opengl` |
| Window mode | command line | `--window-mode borderless|fullscreen|windowed` |
| Show FPS | `config.ini` | `Video/show_fps_in_title` |

Window modes:

- **Fullscreen windowed (default)** — borderless at your desktop resolution.
- **Fullscreen** — switches the display to your monitor’s native resolution at 60 Hz
  and restores the desktop mode when the game exits.
- **Windowed** — the runner’s normal window.

Defaults: Vulkan, 3x internal resolution (1920×1584, suitable for 1080p), 60 FPS speed
limit, DSP LLE, Cubeb audio. The game is natively 4:3; the fullscreen modes preserve
that aspect and pillarbox on a 16:9 display.

## Audio

The game uses a custom DSP microcode. DSP HLE produces silence, so the release uses
**DSP LLE**. The required DSP ROM/coefficient files are in `Sys/GC`.

If the game is silent, check the Windows volume mixer: `moderngekko-run` must not be
muted. Audio is in sync at 60 FPS.

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
powershell -ExecutionPolicy Bypass -File tools\make_release_rpbb.ps1 -Version 0.1.0
```

This writes `dist/RocketPower-Recomp-v0.1.0-Windows-x64.zip` with
`recomplauncher.json` at the top of the release folder, next to `moderngekko-run.exe`.

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
