# Players

## Controls

The release is built for a controller. There is **one canonical profile**
(`Config/GCPadNew.ini`) with SDL positional bindings. It is written on the first
run and **never rewritten**. Two classes decide the face buttons:

- **Positional** (default): A/B/X/Y follow the physical positions (Xbox,
  PlayStation, generic and 8BitDo XInput pads).
- **Nintendo**: A and B swap, X and Y swap, so the printed labels match
  (Switch Pro, Joy-Con, 8BitDo in Switch/Nintendo mode). D-pad, sticks and
  triggers are never swapped.

The class is chosen per pad from the SDL gamepad type, with a small 8BitDo
fallback. If the profile names a pad that is not connected, the first connected
pad is bound instead, and a pad plugged in after launch is picked up without a
restart. To force a class, set `controller_class = auto|positional|nintendo`
under `[Input]` in `config.ini`. `--controller-layout` is accepted as an alias.

| GameCube | Action | Positional | Nintendo |
|---|---|---|---|
| A | Jump / confirm | South (A / Cross) | East (printed A) |
| B | Cancel / attack | East (B / Circle) | South (printed B) |
| X | Action 1 | West (X / Square) | North (printed X) |
| Y | Action 2 | North (Y / Triangle) | West (printed Y) |
| Start | Pause | Start / Options / Plus | same |
| Left stick | Move | Left stick | same |
| D-pad | Move | D-pad | same |
| C stick | Camera | Right stick | same |
| L / R | Triggers | Left / right triggers | same |

Rumble is enabled through the controller profile.

To customize the mapping, edit `%LOCALAPPDATA%\RocketPowerRecomp\Config\GCPadNew.ini`.
Deleting it makes the next launch write a fresh canonical profile; an existing
profile is never regenerated or migrated.

### DualSense Edge, Xbox Elite and 8BitDo

These pads need no special profile: DualSense Edge behaves like a DualSense,
Xbox Elite Series 1/2 behave like a standard Xbox pad, and 8BitDo pads use the
positional class in XInput/standard mode and the Nintendo class in Switch mode.
Paddles and the Edge Fn button are unbound; duplicate a face button in the pad's
own app if you want a paddle shortcut.

Send the startup line if a pad is mapped wrongly:

```
controller: name="SDL/0/<name>" guid=<32 hex> type=<SDL gamepad type> class=<positional|nintendo> device="<bound device>" source=<auto|override|vendor-fallback|default> dpad=normal profile=<static|missing>
```

`class=auto` in `config.ini` is shown as the resolved class. Include the following
`controller: configured device missing; bound "..."` line if it appears.

## Save data

The game uses a raw 128 Mbit card in slot A and an empty slot B:

```
%LOCALAPPDATA%\RocketPowerRecomp\GC\MemoryCardA.USA.raw
```

The card is created on first boot. A legacy folder card (`GC/USA/Card A/*.gci`)
is imported once into the raw card when it is found. To bring a Dolphin save
over, copy the `.gci` into `GC/USA/Card A` before the first run; raw `.raw`
images are used directly at the path above. RecompLauncher backs up `GC` after
every session.

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
