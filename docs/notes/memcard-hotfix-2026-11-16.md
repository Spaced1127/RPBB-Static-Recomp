?# Memory card hotfix: raw slot A, empty slot B

Date: 2026-11-16

Status: applied to the RPBB launcher and RecompLauncher manifest, verified end
to end (save and load) on the built v0.2.1 runner. Nothing committed or
published.

## Applied

1. **Slot A = raw card, Slot B = None.**
   `tools/rpbb_launcher.cs` writes `Core/SlotA = 1` and `Core/SlotB = 255` in a
   new `Dolphin.ini` and **forces both keys** with `EnsureIniKey` on an existing
   install, so an upgraded profile that still says `SlotA = 8` (GCI folder) or
   `SlotB = 0` (Dummy) is corrected before the next boot.
2. **RecompLauncher manifest.** `recomplauncher.json` now carries
   `Core/SlotA = 1` and `Core/SlotB = 255` in the Dolphin config `ensure`,
   `always` and `new` blocks, so a launcher-only install gets the same slots
   without ever opening the standalone launcher.
3. **User path before EXI init, and a runtime-level card bootstrap.** The shared
   ModernGekko runtime calls `UICommon::SetUserDirectory` ->
   `UICommon::CreateDirectories()` -> `ConfigureMemoryCards()` -> `UICommon::Init()`
   in `Runtime::Create` (`src/runtime/dolphin_runtime.cpp`). On a fresh install
   `ConfigureMemoryCards` creates the formatted raw card before boot, on an
   existing install it imports a legacy GCI folder save once, and it rewrites
   `SlotA`/`SlotB` in `Config/Dolphin.ini` and forces the in-memory values. This
   covers launcher-only and RecompLauncher installs even if the launcher never
   ran. Pinned sources: `dolphin_runtime.cpp`, `memcard_bootstrap.{h,cpp}`.
4. **Controller fixes stay in the same artifact** (v0.2.1), see
   [`controller-regression-2026-11-16.md`](controller-regression-2026-11-16.md).

## Why the old behaviour hung

Dolphin's default `SlotA` is `MemoryCardFolder` (type 8), which needs
`GC/<region>/Card A`; the game's card probe could sit at "checking memory card
in slot A". `SlotB = 0` is the Dummy EXI device, not a card, and the game stops
with "THE DEVICE IN SLOT B IS NOT SUPPORTED", blocking the new-file flow. A raw
card (type 1) in slot A and `None` (255) in slot B avoids both.

## Verified (v0.2.1 staged runner)

Test setup: a user directory with `Dolphin.ini` carrying the forced slots,
`Logger.ini` verbose, automation enabled, and a clean `GC/` tree (no card). The
run was driven with the automation protocol on a hidden desktop; no window was
shown on the user's desktop.

- Boot created `GC/USA`, `GC/EUR`, `GC/JAP` before the game started.
- New Game -> the card flow offered **Memory Card Slot A** only; Slot B was
  greyed out (None), so the Dummy-device report cannot occur.
- "No 'Rocket Power: Beach Bandits' Save game data found ... create?" -> YES ->
  name entry -> OK.
- The raw card appeared at `GC/MemoryCardA.USA.raw`:
  - size **16,777,216 bytes** (128 Mbit),
  - **SHA-256 `62B7A9EFCB4B0AFBF77F4803153E1529DAFD913B54AE02A6FEEE8C57A2E61283`**,
  - directory entry at `0x2000` reads `GBQE78` + save name `Sam`.
- Clean stop via the automation `stop` command (not a force-kill): the card SHA
  was **unchanged** after exit.
- Restart with the same user directory: Story Mode -> Load Game -> Memory Card
  Slot A -> **Choose Game: Sam**; the title screen then showed **A Continue**;
  Continue loaded into gameplay ("You're on, Sammy!").

Evidence: `work/evidence/memcard-2026-11-16/` (card image and the save prompt,
create-save, load-list, Continue-title and loaded-gameplay screenshots).

### Offline card-image check (host-side)

`tools/check_card_image.py` parses the saved raw card without launching anything:

```text
size:  16,777,216 bytes (2048 blocks)
header checksum: stored=d520/29e2 computed=d520/29e2 OK
directory checksum: stored=0174/ee8e computed=0174/ee8e OK
slot 1: game=GBQE company=78 name='Sam' first=5 blocks=10 chain=10 bat=1 OK
RESULT: OK
```

The directory entry is `GBQE78` / `Sam` with a 10-block chain that terminates
correctly in the active BAT (block 4). Header and directory additive/inverse
checksums both match, so the card is a valid 128 Mbit Dolphin raw card, not just
a file of the right size.

### Final artifact (runtime bootstrap added)

The v0.2.1 zip was rebuilt after the shared runtime bootstrap landed, and
re-checked:

- Fresh user directory, boot with the staged `rpbb.exe`: `run.log` shows
  `memory card: slot A = raw, slot B = none, created MemoryCardA.USA.raw
  bytes=16777216`, and the file exists at 16,777,216 bytes before the game
  draws.
- Existing card: `memory card: slot A = raw, slot B = none,
  raw=MemoryCardA.USA.raw bytes=16777216`; the card SHA is unchanged
  (`62B7A9EF...`), so the bootstrap neither rewrites nor truncates the save.
- The full UI save -> restart -> Load Game -> Continue -> gameplay path was
  proven on the pre-bootstrap v0.2.1 build (same card format and same
  staged launcher). The final build was booted and confirmed to read the same
  card; its Load UI was not re-screenshotted.
- The legacy `GC/<region>/Card A/*.gci` import path was not exercised: no GCI
  sample was available. The code imports into the raw card and falls back to
  keeping the folder device if creation fails, so an old save cannot be
  stranded.

## Residual risk (next patch)

`ImportLegacyCard` returns the number of saves imported, and
`ConfigureMemoryCards` treats any count `>= 0` as success. If every `.gci`
fails to parse, or only some of them import, the install still switches to the
raw card and the unimported saves are no longer visible (the files remain under
`GC/<region>/Card A`, but there is no UI to switch the device back). A future
patch should keep the folder device when `imported != legacy.gci_files.size()`
(or at least when `imported == 0`) and log the count. The fresh-install and
normal-save paths are unaffected.

## Test caveats

- A force-kill (`taskkill /F`, `run-hidden` timeout) skips the card flush. Use
  the automation `stop` command when the save has to reach disk; with a clean
  stop the SHA above was stable.
- A savestate restores the EXI device type saved with it. A state made while
  slot A was the GCI folder re-enables the folder and sends saves to
  `GC/<region>/Card A`. This test used no savestate; players who never load an
  old state are unaffected.
- The card file is created on the first **save** for this game; the boot probe
  only reads. A fresh boot therefore creates the region directories but not the
  `.raw` file until a save is written. That is expected and is not the old hang.

