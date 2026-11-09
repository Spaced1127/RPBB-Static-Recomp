# Project state

Updated: 2026-11-08

## Latest verified result

The recompiled game boots and renders on Windows:

- Disc: **Rocket Power: Beach Bandits (USA)**, ID **GBQE78**.
- Static recompilation: **545,088 instructions, 0 unknown**, 134 C chunks.
- Native module: `gGBQE78_recomp.dll`, loaded by ModernGekko at `0x80003140`.
- Boot sequence reached: memory-card probe → legal screen → THQ logo → Klasky Csupo
  logo → **3D intro scene** (character, beach/city world, textures).
- Frame rate at this stage: **~19 FPS MSVC `/O2`; 40–45 FPS Clang `-O3`,
  x86-64-v3, ThinLTO** (1920x1080 internal); ~41 FPS in gameplay with DSP LLE.
- Input verified: DualSense mapping works.
- Audio verified: the game loads a **custom DSP ucode (CRC `0x4e8a8b21`)**.
  DSP HLE produced only zeros; **DSP LLE** produces real audio (dump peak 12085).
  The final silence was the Windows volume mixer: `moderngekko-run` had been muted.
- PGO module built from the training profile: 1,197 functions, 147B block counts.
  `tools/run_rpbb.bat` now uses `build/pgo/gGBQE78_recomp.dll`; the non-PGO Clang
  module remains available for A/B. Initial title-screen reading: **44.8 FPS** vs
  35.7 FPS for the non-PGO build (gameplay A/B still pending).

Screenshots: `work/evidence/01-legal.png`, `02-thq.png`, `03-klasky.png`,
`04-intro-3d.png`, `05-intro-3d-clang.png`.

## Disc facts

- ISO size: 1,459,978,240 bytes.
- Game ID: `GBQE78`; title: `Rocket Power: Beach Bandits`; country `0x00`.
- Magic at `0x1C`: `0xC2339F3D` (GameCube).
- DOL offset: `0x1DA00`; entry point `0x80003140`.
- FST offset: `0x262A00`; FST size `0x113` (12 entries).
- BSS: `0x802435E0` + `0x2E2D4`.
- DOL SHA-256: `5F73B291A1B8C33A7CEBDA40638F00F74FF7FAF3A5F3877190CD30B628FFA42D`.
- FST contents: `data/SB.thp` (56.8 MB), `data/pakfile.pak` (516.2 MB),
  `data/RR.thp` (66.7 MB), `data/alllogo.thp` (26.4 MB), `data/JR.thp` (13.4 MB),
  `rpbanner.tpl`, `rpico1..4.tpl`, `opening.bnr`.

### DOL sections

| Kind | Index | File offset | Virtual range | Size |
|---|---:|---:|---|---:|
| text | 0 | 0x000100 | 0x80003100–0x80005600 | 0x2500 |
| text | 1 | 0x002600 | 0x800056A0–0x802176A0 | 0x212000 |
| data | 0 | 0x214600 | 0x80005600–0x80005640 | 0x40 |
| data | 1 | 0x214640 | 0x80005640–0x800056A0 | 0x60 |
| data | 2 | 0x2146A0 | 0x802176A0–0x802176E0 | 0x40 |
| data | 3 | 0x2146E0 | 0x802176E0–0x80217700 | 0x20 |
| data | 4 | 0x214700 | 0x80217700–0x80231580 | 0x19E80 |
| data | 5 | 0x22E580 | 0x80231580–0x802435E0 | 0x12060 |
| data | 6 | 0x2405E0 | 0x8026C540–0x8026D220 | 0xCE0 |
| data | 7 | 0x2412C0 | 0x8026DBA0–0x802718A0 | 0x3D00 |

## Recompilation

DolRecomp (patched SB-RFD build) generated:

- text0: 2,368 instructions (725 known, 1,643 embedded data, 0 unknown).
- text1: 542,720 instructions (542,712 known, 8 embedded data, 0 unknown).
- 134 C chunks, C backend.
- `generated_smc.txt` flags possible runtime text patches at 0x8000336C, 0x800035E0,
  0x801D4C90, 0x801D4DDC, 0x801D6414, 0x801DB828–0x801DB858, 0x801DF2A0, and others.
  Review before assuming they are benign.

## Runtime

- ModernGekko (Dolphin-derived RecompCore) boots the recompiled module.
- Two modules built: MSVC 14.51 `/O2` (19 FPS) and Clang 22.1.8 `-O3` with
  `-march=x86-64-v3 -mtune=generic`, `-flto=thin`, loop budget 4096, call depth 128
  (40–45 FPS). Use the Clang module.
- LLVM-native module backend is not usable here (same finding as SB-RFD).
- Audio: **DSP LLE required**. `Config/Dolphin.ini` sets `DSPHLE = False`,
  `Backend = Cubeb`, `Muted = False`, `Volume = 100`. The DSP ROM/coefficient files
  are present in `build\moderngekko\Sys\GC` (`dsp_rom.bin`, `dsp_coef.bin`).
- Diagnostic dump `Dump/Audio/GBQE78_..._dspdump1.wav` is ~6.2 MB with peak 12085;
  the HLE builds produced only zero samples.
- PGO: `C:\mg-pgo\merged.profdata` (6 MB, 1,197 functions) → manual Clang
  `-fprofile-use` build. Module copied to `build\pgo\gGBQE78_recomp.dll`,
  SHA-256 `3859FA3B840BBA778AC623403B50A91695492B631B86F51365608842C5B68273`.
- **Affinity is a big win on lighter scenes.** The Ryzen 7 7800X3D exposes 8 cores /
  16 threads. Running the process on all logical cores let the main thread and the DSP
  LLE thread share SMT siblings. Pinning the process to `0xFF` (physical cores 0–7)
  raised the title/loading screen from **47.7 → 58.5 FPS**; main-thread load fell from
  95.3% to 84.7%, DSP LLE stayed at 15.6%. The launcher (`tools/run_rpbb.ps1`) applies
  `ProcessorAffinity = 0xFF` and `PriorityClass = High`. The cave level still shows
  ~48 FPS / 97.2% main-thread load, so it remains CPU-code-bound.
- **SB-RFD PGO flags were missing the loop/call budget defines.** Its final
  `module-pgo/CMakeCache.txt` passes:

  ```
  -DDOLRECOMP_C_LOOP_CYCLE_BUDGET=4096
  -DDOLRECOMP_C_MAX_CALL_DEPTH=128
  ```

  The generated default is `256` / `24`. All earlier RPBB modules were built with the
  defaults. Rebuilt as `build\pgo-flags\gGBQE78_recomp.dll` with 4096/128 + PGO;
  cave measurement pending. SB-RFD also used **separate boot and play profiles**
  (`boot-call.profraw` + `play-call.profraw`); RPBB still has only a boot/loading
  profile, so a gameplay-profile pass is the next step if the cave is still short.
- The game root was built by `tools/make_game_dir.py` in Dolphin's extracted layout:
  `sys/{boot.bin,bi2.bin,apploader.img,main.dol,fst.bin}` + `files/`.
- Runtime user directory on this machine: `C:\Users\Gamer\AppData\Local\moderngekko`.
- A DualSense `GCPadNew.ini` was copied there from the SB-RFD test setup.

## Next actions

1. A/B the PGO build vs the non-PGO Clang build in the same gameplay scene; record FPS
   and audio sync. If PGO wins, use it for the final package.
2. Package the standalone launcher/runner (mirroring SB-RFD `dist/`), with a project-local
   user dir (`--user-dir`) containing `Dolphin.ini` (`DSPHLE=False`, Cubeb), `GFX.ini`,
   and `GCPadNew.ini`, plus ISO/RVZ import and graphics settings.
3. Keep diagnostic `DumpAudio`/logging off in the shipped profile.
4. Use the prior `RPBB Gamecube Decomp\work` PAK/TPL/OBP research for asset validation and
   future modding.

## Known risks

- Self-modifying code sites flagged by DolRecomp may need explicit patches.
- The memory-card screen appears before legal; it advanced on its own but save/load still
  needs verification.
- The game runs ~19 FPS in the intro with the unoptimized C module; this is a build
  configuration issue, not a boot blocker.
- Input needs a controller bound; without `GCPadNew.ini` the game cannot be played.
