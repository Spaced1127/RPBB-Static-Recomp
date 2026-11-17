# Canonical controller system (ported from WWE WMX8 v0.1.4)

Status: ported to the shared ModernGekko template and to RPBB. Reference:
`H:\Projects\WWE WMX8` commit `131523d`, release v0.1.4, and
`docs/notes/controller-generic-design.md` there.

## Why

The per-device approach carried a profile generator, per-device GUID tables
(DualSense Edge, Xbox Elite), a name heuristic and an `a>b` live-mapping rule.
Every new pad needed a table entry and the heuristics misfired: the `a>b` rule
flipped 219 of the SDL DB's 812 rows, the button-index D-pad rule mirrored
DualSense/Edge (`dpleft:b15,dpright:b13` is normal), and rewriting the profile
from a scan that found no pad (sleeping wireless pad) produced a keyboard profile
and dead input.

## Design

1. **ONE static `GCPadNew.ini`** with SDL positional bindings, written only when
   missing. Never regenerated or rewritten.
2. **Two classes**: `positional` (default) and `nintendo` (A<->B and X<->Y only).
   D-pad, sticks and triggers are never swapped.
3. **Class resolved per pad at input init**: user override
   (`controller_class = auto|positional|nintendo`, `--controller-layout` alias)
   wins; then `SDL_GetGamepadType` Nintendo types; then a small 8BitDo
   Nintendo-mode GUID fallback (`2dc8:6006` Pro 2, `05ac:0004` Switch clone).
4. **Device resolution in memory**: `EnsurePadBound()` sets the first enumerated
   pad as the emulated controller's default device when the profile's pad is
   absent and calls `UpdateReferences()`. A 2 s watcher repeats it for hot-plug.
   The `.ini` stays byte-identical.
5. **No migration.** An old `Device = SDL/0/<name>` line is harmless: binding
   falls back to the first pad and the class is resolved at runtime. Deleting an
   old profile is optional, never required.
6. **No per-device tables, no generation from scans.**

## Files

Shared ModernGekko:

| File | Change |
|---|---|
| `vendor/.../SDL/LayoutClass.h` (new) | `LayoutClass`, header-only override slot. |
| `vendor/.../SDL/SDLGamepad.h/.cpp` | `ClassifyGamepad`, `ResolveLayoutButton`; `Button::GetState` reads through the resolver. |
| `include/moderngekko/runtime.hpp` | `RuntimeConfig::controller_class`. |
| `src/runtime/dolphin_runtime.cpp` | apply class override after `InitControllers`; `EnsurePadBound`; 2 s watcher joined on shutdown. |
| `tools/frontend_config.hpp/.cpp` | `ControllerClass`, `ParseControllerClass`, `CanonicalPadProfile`, `EnsureCanonicalPadProfile`; remove all profile generation/migration and `controller_profile`. |
| `tools/moderngekko_run.cpp` | `EnsureControllerSetup` (hints, scan for the log, write-once profile, class); startup line. No `SDL_Quit`. |
| `tools/moderngekko_launcher.cpp` | class picker; profile write only on explicit "use this controller". |
| `tools/netplay_session.cpp` | lobby toggles use `EnsureCanonicalPadProfile`. |

RPBB:

| File | Change |
|---|---|
| `defaults/GCPadNew.ini` | canonical positional template (placeholder device). |
| `tools/rpbb_launcher.cs` | controller page text/status; no profile generation. |
| `recomplauncher.json` | `Input/controller_class = auto` in ensure/always/new + a class setting. |
| `tools/controller_layout_test.cpp` | class parsing/precedence, canonical profile, write-once, read-back, legacy keys. |
| `tools/check_sdl_controller_db.py` | audit that no Microsoft row is Nintendo-class. |
| `PLAYERS.md`, `README.md` | one canonical profile, two classes, no rewrite, hot-plug, `controller_class`. |

## Verification

Offline: the controller test suite (classes, precedence, canonical body,
write-once, read-back) and the SDL DB audit.

Real runs (visible windows): per pad, read the startup line and confirm
`class=positional` for Xbox 360/One/Series/Elite, DualSense/Edge, DS4/DS3,
generic DInput; `class=nintendo` for Switch Pro/Joy-Con and 8BitDo in
Switch/Nintendo mode; `class=positional` for 8BitDo XInput. Check pad absent at
boot, hot-plug, `controller_class=nintendo` forcing the swap, and the profile
file byte-identical across launches.

## Reference issues fixed in this port

- The reference `ClassifyGamepad` read a file-local `std::atomic<int>` while the
  runtime set the header-only override, so the override never applied. The SB
  port uses the single `LayoutClass.h` policy (`ResolveLayoutClass`) everywhere.
- The reference `EnsurePadBound` used `GetDefaultDeviceString()`, which returned
  `DInput/0/Keyboard Mouse` on a machine with a keyboard. It now picks the first
  **SDL** device by `GetQualifiedName()`, skipping keyboard/mouse.
- The reference startup line had no SDL type or source. The port logs
  `name/guid/type/class/override/device/source/profile`.
- The reference shipped one 8BitDo pair (`05ac:0004`). The port also covers the
  Pro 2 Nintendo mode (`2dc8:6006`); other PIDs remain candidates until a real
  pad is observed.
- A pad-less first boot wrote a keyboard profile, which would leave a pad bound
  later with no usable buttons. RPBB's launcher pre-writes the canonical
  positional profile, so the runner never takes the keyboard path for a normal
  install.
- The old vendor-level wildcard fallback in `CoreDevice.cpp` was removed, so
  `EnsurePadBound` is the single in-memory binding path (matching the design).

## Known limits

- Pads that expose A/B/X/Y as raw buttons without a recognised type are
  positional; the override covers them.
- Two pads with different classes are supported because the class is per pad,
  but the profile binds port 1.
- The canonical profile binds one device name for the picker; binding is
  in-memory and falls back to the first pad.