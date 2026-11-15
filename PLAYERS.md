# Players

## Controls

The release is built for a controller. Xbox, PlayStation, Switch Pro and generic SDL
gamepads are detected automatically, including the DualSense Edge and Xbox Elite
Series 1/2; the runner generates or updates the profile for the connected pad.

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

### DualSense Edge and Xbox Elite

Model detection uses the SDL GUID (bus, vendor and product IDs), not the product
name. USB and Bluetooth variants of the same pad resolve to the same layout.

| Pad | IDs | Layout | Paddles / Fn |
|---|---|---|---|
| DualSense Edge | `054c:0df2` | PlayStation, identical to DualSense | Unbound |
| Xbox Elite Series 1 | `045e:02e3` | Xbox, identical to standard Xbox | Unbound |
| Xbox Elite Series 2 | `045e:0b00`, `045e:0b05`, `045e:0b22` | Xbox, identical to standard Xbox | Unbound |

The paddles and the Edge Fn button are deliberately not bound to GameCube inputs,
so they cannot fire extra buttons by accident. If you want paddle shortcuts, map
them to duplicate face buttons in the Xbox Accessories or PlayStation Accessories
app; the game then sees the duplicated standard button. Standard pads leave the
D-pad alone; a firmware that reports reversed D-pad left/right is corrected, and
Switch pads swap only the printed A/B/X/Y labels.

### 8BitDo pads

8BitDo Pro 2, SN30, SF30 and Switch-mode pads report Nintendo-style SDL bindings
(`a:b1,b:b0`, or `a:b3,b:b2` in Switch mode). The runner reads those live bindings
and writes the Nintendo A/B/X/Y swap automatically. 8BitDo Ultimate and 64 pads
report the standard `a:b0,b:b1` order and stay standard. If a firmware reports a
reversed D-pad, the runner corrects left/right in the generated profile. Existing
generated profiles migrate on the next launch; hand-edited profiles are preserved.
To force the Nintendo A/B swap yourself, set `controller_profile=switch` (or
`nintendo`) under `[Input]` in `config.ini`.

Send this line from the startup log if a pad is mapped wrongly:

```
controller: "<name>" guid=<32 hex> bus=usb|bluetooth vid=<4 hex> pid=<4 hex> layout=<xbox|playstation|switch|generic> source=<guid|mapping|type|default> dpad_swap=<no|yes> face_swap=<no|yes> paddles=unbound extras=unbound family="<model>"
```

`paddles=unbound` and `extras=unbound` appear only on pads that have those
buttons. Include the following `controller configuration: ... (... layout)` line too.

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
ISO/RVZ or extracted folder, set resolution, graphics and window mode, and press Play.
The first setup extracts an image automatically and checks it against the verified USA
dump. The standalone `rpbb-launcher.exe` and `run.bat` accept `.rvz` directly;
RecompLauncher's own picker currently offers `.iso` and folders.
