?# Controller regression: Xbox pads dead after v0.2.0

Date: 2026-11-16

Status: reproduced at logic level, fixed in the shared ModernGekko sources,
RPBB hotfix staged but **not committed or published**. Physical Xbox
confirmation is still required.

## Symptom

Players on v0.2.0 report that an Xbox controller does nothing at all. The game
boots, the profile file exists, but the pad produces no input. DualSense and
8BitDo players mostly report wrong buttons, not a dead pad.

## What v0.2.0 changed

v0.2.0 replaced the old name/type layout guess with GUID + live-SDL-mapping
detection (`controller_layout.hpp`) and made the runner **rewrite a generated
profile on every launch** (`EnsureDefaultControllerConfig` in
`moderngekko_run.cpp`). The profile is a Dolphin `GCPadNew.ini` whose
`Device = SDL/<id>/<name>` line must match the runtime's SDL device exactly:
`DeviceQualifier::operator==` compares source, id and name.

Three separate defects came with that change.

### 1. The profile names a device the runtime never creates (pad dead)

The runner enumerated pads with a bare `SDL_Init(SDL_INIT_GAMEPAD |
SDL_INIT_JOYSTICK)` and no SDL hints. Dolphin's own SDL backend
(`InputCommon/ControllerInterface/SDL/SDL.cpp`) sets hints before *its*
`SDL_Init`, notably `SDL_HINT_JOYSTICK_WGI=0`, DirectInput and HIDAPI options.
On Windows an Xbox pad is exposed by several SDL drivers (XInput, RAWINPUT,
HIDAPI, DirectInput, WGI), and the driver that wins decides the string that
`SDL_GetGamepadNameForID` returns:

- XInput: `XInput Controller #1`
- HIDAPI/DirectInput: the HID product name, e.g. `Xbox One Controller`
- WGI: `Xbox One Controller`

The runner and the runtime could therefore pick different drivers and write a
`Device` line with the wrong name. The release's
`DeviceContainer::FindDevice` had **no fallback** (the Aggressive Inline /
SB-RFD fallback patch was not part of the RPBB build), so the profile bound
nothing and the pad was dead. This is the "not working at all" report.

Xbox is the family that breaks because its Windows name depends on the driver
stack. DualSense/8BitDo are HIDAPI in both sessions and keep one stable name.
Evidence:

- The working 11/14 profile on the build machine is
  `Device = SDL/0/Xbox One Controller` — a driver-specific name, not the
  DualSense name shipped in `defaults/GCPadNew.ini`.
- The shipped v0.2.0 `rpbb.exe` contains no `configured device ... not found`
  fallback string, so a mismatch is silent.
- The old runner only wrote a profile when none existed; v0.2.0 rewrites it on
  every launch, so a previously working profile is replaced by the runner's
  driver-set name.

Fix (shared sources): set the runtime's hints before the enumeration
(`WGI=0`, `RAWINPUT=0`, `DIRECTINPUT=1`, HIDAPI options), keep the diagnostic
device string in the log, and restore the `FindDevice` fallback to the first
SDL pad with a one-line log message.

### 2. The D-pad was inverted from raw button numbers

`MappingDpadSwapped` treated `dpleft` index > `dpright` index as "reversed".
Raw button order carries no direction. SDL's WGI Xbox mapping is
`dpleft:b13,dpright:b11` and 35+ rows in the shipped `SDL_gamepad_db.h` bind
left to a higher button than right (PS2/PS3 pads, the GameCube adapter, Razer
and SteelSeries sticks). Those pads got `dpad_swap=yes` and a profile with
`D-Pad/Left = Pad E` / `D-Pad/Right = Pad W`.

Fix: only a hat mask (`h0.2` / `h0.8`) or an axis sign can describe a reversed
D-pad, and only for a Nintendo-mapped pad. Stock pads keep `dpad_swap=no`.

### 3. `a > b` was applied to every pad, not just 8BitDo

The Nintendo rule fired for any pad without a GUID-table hit whose mapping had
`a` after `b`. That is true for PlayStation Classic (`054c:0cda`), the Wii
Classic Controller and remapped pads, which were forced to the Switch layout.
Standard Xbox rows are `a:b0,b:b1`, so this rule alone did not flip Xbox; a
remapped/third-party Xbox report could be, because there was no family guard.

Fix: the mapping rule requires a Nintendo/8BitDo signal (vendor `2dc8`, the
`SDL_GAMECONTROLLER_USE_BUTTON_LABELS` hint, or paddles) and never overrides a
resolved Xbox or PlayStation layout. Unknown Microsoft PIDs (`0B20`, `0B21`,
future SKUs) now resolve through the vendor family (`045e` -> Xbox) instead of
falling through to the mapping heuristic.

## Verification

Release-code walk (v0.2.0 sources): every standard Xbox GUID below resolves to
`layout=xbox` with `face_swap=no`, and generates a byte-identical profile to a
DualSense/Generic profile. Layout selection was *not* the dead-pad cause.

| Family | GUID (VID:PID) | Result |
|---|---|---|
| Xbox 360 USB | `030000005e0400008e02000000000000` (045e:028e) | xbox, no swaps |
| Xbox 360 BT | `050000005e0400008e02000000783f00` | xbox, no swaps |
| Xbox One | 02d1 / 02dd / 02e0 / 02ea / 02fd / 02ff | xbox, no swaps |
| Xbox Series X\|S | 0b12 / 0b13 | xbox, no swaps |
| Xbox Elite S1 | 02e3 | xbox, paddles unbound |
| Xbox Elite S2 | 0b00 / 0b05 / 0b22 | xbox, paddles unbound |
| DualSense | 0ce6 USB/BT | playstation |
| DualSense Edge | 0df2 USB/BT | playstation, paddles+Fn unbound |
| 8BitDo Pro 2 Nintendo | 2dc8:6006 `a:b1,b:b0` | switch (source=mapping) |
| 8BitDo Pro 2 Switch mode | 05ac:0004 `a:b3,b:b2` | switch |
| 8BitDo Pro 2 standard | 2dc8:6006 `a:b0,b:b1` | generic/standard |
| 8BitDo Ultimate | 2dc8:1330 `a:b0,b:b1` | generic/standard |
| 8BitDo 64 | 2dc8:1930 `a:b0,b:b1` | generic/standard |
| Switch Pro | 057e:2009 | switch, face swap |

Fixed-code matrix: `tools/build_controller_matrix.cmd` (RPBB-owned, 38 checks)
passes all rows, including the D-pad-inversion and remapped-`a>b` regressions.
The startup line is unchanged and still prints name/GUID/bus/VID/PID/layout/
source/dpad/face/paddles/family, plus the exact `device=` string and profile
path in the following `controller configuration:` line.

SDL database audit (`tools/check_sdl_controller_db.py`, 812 rows):

- Microsoft rows with a raw `a > b` order: **0**, so the Nintendo rule never
  legitimately applies to an Xbox row.
- Rows whose `dpleft` raw button is higher than `dpright`: **35** (Sony,
  Steam, 8BitDo, GameCube adapter, generic sticks). The old index comparison
  marked every one of them `dpad_swap=yes`; the corrected rule leaves them
  alone.
- Microsoft PIDs in the database: 14. The table covers 13; `045e:0b20`
  (Xbox Wireless Controller) now resolves through the Microsoft vendor family
  instead of the mapping heuristic.
- Sony PIDs not in the table (`054c:0ba0`, `054c:0cda` PlayStation Classic)
  resolve through the Sony family; Nintendo's five unlisted PIDs resolve
  through the Nintendo family.

Note for the shared-template owner: the shared
`tests/controller_layout_test.cpp` now passes with the hat-based reversed
D-pad case; the RPBB matrix uses the same hat form and additionally keeps a
raw-button case that must *not* swap.

## Hotfix

- Shared ModernGekko sources: hints + `FindDevice` fallback + vendor family +
  scoped Nintendo rule + direction-only D-pad swap.
- RPBB work: `tools/controller_matrix_test.cpp`,
  `tools/build_controller_matrix.cmd`, `tools/check_sdl_controller_db.py`,
  `tools/make_hotfix_rpbb.ps1`, this note, and the memory-card note
  [`memcard-hotfix-2026-11-16.md`](memcard-hotfix-2026-11-16.md) in the same
  artifact.
- Staged (not committed, tagged or published):
  `dist/RocketPower-Recomp-v0.2.1-Windows-x64.zip`, 19,924,570 bytes,
  SHA-256 `87940668E45472C6F174EA7B4003D6D60C7DAF9EA7DE48EC5B550B862D2D3CCE`.
  Staged `rpbb.exe` SHA-256
  `1BD91B1EB29C492E2F6709789601095392281216AE177A9D38185A1784D8B9DE`.
  Matrix gate: 38/38. Real run: the staged package booted on the hidden desktop
  and wrote the startup lines, no crash.
- Pinned shared sources: `controller_layout.hpp` `02C6012...`, `moderngekko_run.cpp`
  `5397954...`, `CoreDevice.cpp` `4175DAD...` (full list in
  `tools/make_hotfix_rpbb.ps1`; the script refuses to build if they drift).
- Required confirmation: an Xbox 360, Xbox One and Elite pad must each show
  `layout=xbox` in `logs/run.log` and respond in game. If the pad is still
  dead, the `device=` string and Dolphin's `Added device:` line are the pair to
  compare.
