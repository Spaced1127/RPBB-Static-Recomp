# Bring-up log — first boot

Date: 2026-11-08

## 1. Disc extraction

Source: `Nickelodeon Rocket Power - Beach Bandits (USA).rvz` (user's own dump).

```
python tools\rvz_extract.py "...rvz" work\disc\game.iso
```

- RVZ: 11,139 groups, Zstandard.
- ISO: 1,459,978,240 bytes.

Inventory (`gcdisc.py info`):

```
Game ID:     GBQE78
Title:       Rocket Power: Beach Bandits
Magic:       0xc2339f3d (GameCube)
DOL offset:  0x1da00
FST offset:  0x262a00  size 0x113
Entry point: 0x80003140
BSS:         0x802435e0 + 0x2e2d4
FST entries: 12
```

## 2. DOL

`tools/extract_dol.py` delegates to GCDisc's `read_dol`, which reads the DOL pointer at
`0x420` and slices the contiguous blob.

```
Wrote work\disc\main.dol (2379712 bytes), 10 sections
SHA-256: 5F73B291A1B8C33A7CEBDA40638F00F74FF7FAF3A5F3877190CD30B628FFA42D
```

Entry disassembly starts with the standard SDK boot:

```
80003140  bl 0x80003254          ; set r1/r2/r13
80003144  bl 0x80003330
80003158  bl 0x80003270
...       relocation pass over [0x800000F4]
80003214  bl 0x801d4610
80003218  bl 0x801d4830
```

## 3. Recompile

```
H:\Projects\SB-RFD\build\dolrecomp\dolrecomp.exe --gamecube work\disc\main.dol build\recomp
```

Result:

```
text[0]: 2368 instructions
  2368 decoded, 725 known, 1643 embedded data, 0 unknown
text[1]: 542720 instructions
  542720 decoded, 542712 known, 8 embedded data, 0 unknown
134 chunks
```

Possible self-modifying-code sites are listed in
`build/recomp/generated/generated_smc.txt` (0x8000336C, 0x800035E0, 0x801D4C90,
0x801D4DDC, 0x801D6414, 0x801DB828–0x801DB858, 0x801DF2A0, ...). None blocked boot.

## 4. Game root

`tools/make_game_dir.py` writes the Dolphin/ModernGekko extracted layout:

```
sys/boot.bin       1088
sys/bi2.bin        8192
sys/apploader.img  111844
sys/main.dol       2379712
sys/fst.bin        275
files/             11 files, 679,534,432 bytes
```

`moderngekko-port inspect` confirms:

```
Game name: Rocket Power: Beach Bandits
Disc ID:   GBQE78
Platform:  GameCube (Gekko)
Entry:     0x80003140
DOL SHA-256: 5f73b291a1b8c33a7cebda40638f00f74ff7faf3a5f3877190cd30b628ffa42d
```

## 5. Module build

Under `VsDevCmd.bat` (VS 2026 BuildTools, MSVC 14.51, Ninja):

```
moderngekko-port.exe build work\game --backend c --toolchain msvc --opt-level 2
```

Produces `gGBQE78_recomp.dll` (134 C chunks, MSVC, Release). Module cache key:

```
moderngekko-modules\GBQE78\5f73b291a1b8c33a7cebda40638f00f74ff7faf3a5f3877190cd30b628ffa42d-a71f4a5f346fbe4a\gGBQE78_recomp.dll
```

## 5b. Clang build

```
set PATH=C:\Program Files\LLVM\bin;%PATH%
set CFLAGS=-march=x86-64-v3 -mtune=generic
set DOLRECOMP_C_LOOP_CYCLE_BUDGET=4096
set DOLRECOMP_C_MAX_CALL_DEPTH=128
moderngekko-port.exe build work\game --backend c --toolchain clang --opt-level 3
```

Generated module flags (from `build.ninja`):

```
-march=x86-64-v3 -mtune=generic -O3 -DNDEBUG -flto=thin
-ffp-contract=off -fno-fast-math
```

Module cache: `...\5f73...a42d-8f78e41483de3f25\gGBQE78_recomp.dll`
(same Clang toolchain hash as the SB-RFD module cache).

Performance in the intro/logo sequence:

| Build | FPS |
|---|---:|
| MSVC `/O2` | ~19 |
| Clang `-O3` + x86-64-v3 + ThinLTO | **40–45** |

Evidence: `work/evidence/05-intro-3d-clang.png` (3D intro at 45.2 FPS).

## 5c. Audio bring-up

The game calls `AXInit()` and streams sound data into ARAM, but **DSP HLE was silent**:
the `Dump/Audio` WAVs contained only zero samples.

DSP LLE reveals the reason in `Logs/dolphin.log`:

```
[DSPLLE]: g_dsp.iram_crc: 18712672
[DSPLLE]: g_dsp.iram_crc: 4e8a8b21
[DSPLLE]: *** Copy new UCode from 0x8023f3c0 to 0x0000 (crc: 0x4e8a8b21)
```

This is a custom DSP ucode, not the stock AX ucode the HLE expects. With
`Dolphin.ini` `DSPHLE = False`;

```
GBQE78_..._dspdump1.wav  6,248,844 bytes  peak 12085
```

The HLE builds produced 771 KB / 6.2 MB dumps with peak 0.

Current working audio config:

```ini
[Core]
DSPHLE = False

[DSP]
Backend = Cubeb
DSPThread = True
Muted = False
Volume = 100
DumpAudio = False   # set True only for diagnosis
```

`dsp_rom.bin` and `dsp_coef.bin` are present in `build\moderngekko\Sys\GC`.

Diagnostic logging used `Logger.ini`: `AI/Audio/DSP/DSPHLE/DSPLLE/OSREPORT=True`,
`WriteToFile=True`. Turn these off for normal play.

## 6. Boot

Runner stderr:

```
[staticrecomp] core init
[staticrecomp] module loaded: ...\gGBQE78_recomp.dll entry=0x80003140
```

Observed sequence (~19–21 FPS, C backend, opt-level 2):

| Time | Screen | Evidence |
|---:|---|---|
| 10s | “Reading Memory Card Slot A and Slot B” | — |
| 40s | Legal / “Licensed by Nintendo” | `work/evidence/01-legal.png` |
| 60s | THQ logo | `work/evidence/02-thq.png` |
| 90s | Klasky Csupo logo | `work/evidence/03-klasky.png` |
| 180s | 3D intro scene (character, beach/city) | `work/evidence/04-intro-3d.png` |

No crash, no unimplemented-stub abort in the captured stderr during these runs.

## 6b. PGO

The first `pgo-run` training run exited with status `-1073740771` (`0xC000001D`) when the
game window was closed, so the tool refused to merge. The instrumented run had still
flushed a valid profile:

```
C:\mg-pgo\raw\GBQE78-148804.profraw   12,011,728 bytes
```

Manual merge:

```
llvm-profdata merge -output=C:\mg-pgo\merged.profdata C:\mg-pgo\raw\GBQE78-148804.profraw
```

Profile summary:

```
Total functions:             1,197
Maximum function count:      973,174,728
Maximum internal block count: 2,214,658,048
Total number of blocks:      704,004
Total count:                 147,381,431,551
```

Manual PGO build with the module template and:

```
-march=x86-64-v3 -mtune=generic
-O3 -flto=thin
-fprofile-use=C:/mg-pgo/merged.profdata -Wno-profile-instr-unprofiled
```

Output: `C:\mg-pgo\pgo-build\gGBQE78_recomp.dll` (83,267,072 bytes), copied to
`build\pgo\gGBQE78_recomp.dll`, SHA-256
`3859FA3B840BBA778AC623403B50A91695492B631B86F51365608842C5B68273`.

Initial title-screen reading: 44.8 FPS vs 35.7 FPS for the non-PGO build.

Affinity test on the Ryzen 7 7800X3D (8 cores / 16 threads):

| Affinity | FPS | Main thread | DSP LLE |
|---|---:|---:|---:|
| all logical cores (65535) | 47.7 | 95.3% | 17.5% |
| physical cores 0–7 (`0xFF`) | **58.5** | 84.7% | 15.6% |

The DSP LLE thread was contending with the main thread on an SMT sibling. The
launcher now sets `ProcessorAffinity = 0xFF` and `PriorityClass = High`.

## 8. SB-RFD comparison — loop budget and profiles

The SB-RFD final PGO module cache
(`build\modules-call\GSQE78\...\module-pgo\CMakeCache.txt`) contains:

```
CMAKE_C_FLAGS = -march=x86-64-v3 -mtune=generic
  -DDOLRECOMP_C_LOOP_CYCLE_BUDGET=4096
  -DDOLRECOMP_C_MAX_CALL_DEPTH=128
  -fprofile-use=H:/Projects/SB-RFD/work/pgo/merged-call.profdata
  -Wno-profile-instr-out-of-date -Wno-profile-instr-unprofiled
```

The generated header defaults to:

```c
#define DOLRECOMP_C_LOOP_CYCLE_BUDGET 256
#define DOLRECOMP_C_MAX_CALL_DEPTH 24
```

Our earlier modules did not pass the `-D` overrides, so they ran with 256/24. Rebuilt
with SB-RFD's 4096/128 and the existing profile to
`build\pgo-flags\gGBQE78_recomp.dll` (83,267,072 bytes, SHA-256
`DF87B319581C0C9393D1E56036E8FB1DF0762FE73D11735413E7DF307BB6544B`).

SB-RFD also kept separate `boot-call.profraw` and `play-call.profraw` profiles,
merged into `work/pgo/merged-call.profdata`. RPBB currently has only a boot/loading
profile, so if the cave level remains below 60, the next pass is a proper gameplay
profile with the 4096/128 instrumented module.

## 7. Open issues

1. **Input**: no controller config initially; copied SB-RFD's DualSense mapping to
   `C:\Users\Gamer\AppData\Local\moderngekko\Config\GCPadNew.ini`. Needs a real button
   press to verify and advance from the intro.
2. **Performance**: ~19 FPS with C opt-level 2. Try LLVM backend / PGO / x86-64-v3.
3. **Audio**: not auditioned yet.
4. **Title/menu/gameplay**: not reached in automation yet; the intro may need input.
5. **Saves**: memory-card path works at boot; actual save/load not tested.
