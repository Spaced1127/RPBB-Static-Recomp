# Players

## Controls

The release is built for a controller. Xbox, PlayStation, Switch Pro and generic SDL
gamepads are detected automatically; the runner generates or updates the profile for
the connected pad.

| GameCube | Action | Standard SDL gamepad |
|---|---|---|
| A | Jump / confirm | South face button (A / Cross / B on Switch) |
| B | Cancel / attack | East face button (B / Circle / A on Switch) |
| X | Action 1 | West face button |
| Y | Action 2 | North face button |
| Start | Pause | Start / Options / Plus |
| Left stick | Move | Left stick |
| D-pad | Move | D-pad |
| C stick | Camera | Right stick |
| L / R | Triggers | Left / right triggers |

Rumble is enabled through the controller profile.

To customize the mapping, edit

```
%LOCALAPPDATA%\RocketPowerRecomp\Config\GCPadNew.ini
```

Deleting that file makes the runner generate a fresh profile for the connected pad.
If you customize the mapping, add `controller_profile=manual` under `[Input]` in
`config.ini` so the automatic refresh does not overwrite your edits. There is no
keyboard gameplay mapping in this build.

## Save data

The game uses a folder-based memory card:

```
%LOCALAPPDATA%\RocketPowerRecomp\GC\USA\Card A\
```

RecompLauncher backs this folder up after every session. To bring saves from Dolphin,
copy the `.gci` files from your Dolphin memory card into that folder. Card images
(`.raw`, `.mcd`, `.mcs`) are not read directly.

## Settings

RecompLauncher shows the settings on the game's page and in Big Picture mode. The files
live in `%LOCALAPPDATA%\RocketPowerRecomp`:

- `Config/Dolphin.ini` — DSP LLE, Cubeb audio, 60 FPS speed limit.
- `config.ini` — internal resolution, aspect ratio, widescreen hack, window title FPS
  display, volume and 2D-screen compatibility.
- `Config/GCPadNew.ini` — controller mapping.

The internal resolution presets match the SpongeBob recomp: 1x (640×528), 2x
(1280×720), 3x (1920×1080), 4x (2560×1440), 6x (3840×2160), 8x (5120×2880) and 12x
(7680×4320). Higher settings are sharper but cost more GPU time.

Window mode is a launch option (and a RecompLauncher setting):

- **Fullscreen windowed** — borderless at your desktop resolution (default).
- **Fullscreen** — your monitor’s native resolution at 60 Hz, with the desktop mode
  restored on exit.
- **Windowed** — the normal window.

## Widescreen

The game is natively 4:3. For a full-width 16:9 picture:

1. Set **Aspect ratio** to **16:9 widescreen**.
2. Turn on **Widescreen hack (stretch 3D)**.

The 3D scene fills the screen; the 2D HUD may stretch. Auto keeps the original 4:3
framing, and 4:3 standard forces it back if a setting looks wrong.

## Launcher

Run `rpbb-launcher.exe` to pick your game copy and change the video, audio and controller
options. The same options are available in RecompLauncher. `rpbb.exe` is the game
runtime.

## RecompLauncher

This release supports [RecompLauncher](https://recomplauncher.com). Pick your own USA
ISO or extracted folder, set resolution, graphics and window mode, and press Play. The
first setup extracts an ISO automatically and checks it against the verified USA dump.
