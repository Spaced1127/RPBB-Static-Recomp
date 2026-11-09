# Players

## Controls

The release is built for a controller. The bundled profile is for a DualSense; the
GameCube pad buttons map as follows.

| GameCube | Action | DualSense |
|---|---|---|
| A | Jump / confirm | Cross |
| B | Cancel / attack | Circle |
| X | Action 1 | Square |
| Y | Action 2 | Triangle |
| Start | Pause | Options |
| Left stick | Move | Left stick |
| D-pad | Move | D-pad |
| C stick | Camera | Right stick |
| L / R | Triggers | L2 / R2 |

Rumble is enabled through the controller profile.

For an Xbox or Switch Pro controller, edit

```
%LOCALAPPDATA%\RocketPowerRecomp\Config\GCPadNew.ini
```

and set the `Device` line to the SDL name of your controller. Deleting the file lets
SDL use its default mapping. There is no keyboard gameplay mapping in this build.

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
- `Config/GFX.ini` — internal resolution.
- `Config/GCPadNew.ini` — controller mapping.
- `config.ini` — window title FPS display and 2D-screen compatibility.

Window mode is a launch option (and a RecompLauncher setting):

- **Fullscreen windowed** — borderless at your desktop resolution (default).
- **Fullscreen** — 1920×1080 at 60 Hz, with the desktop mode restored on exit.
- **Windowed** — the normal window.

The game is natively 4:3, so fullscreen modes preserve the aspect and pillarbox on a
16:9 display.

## RecompLauncher

This release supports [RecompLauncher](https://recomplauncher.com). Pick your own USA
ISO or extracted folder, set resolution and graphics, and press Play. The first setup
extracts an ISO automatically and checks it against the verified USA dump.
