@echo off
setlocal
call "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\Common7\Tools\VsDevCmd.bat" -arch=amd64 -host_arch=amd64
if errorlevel 1 exit /b 1
set "PATH=C:\Program Files\LLVM\bin;%PATH%"
set "GEN=C:/mg-pgo/gen/GBQE78/5f73b291a1b8c33a7cebda40638f00f74ff7faf3a5f3877190cd30b628ffa42d-e48b3db4fc6c517f/dolrecomp-output/generated"
set "BUILD=C:/mg-pgo/pgo-debug"
cmake -S "H:/Projects/SB-RFD/lib/ModernGekko-Template/lib/ModernGekko/vendor/dolphin/module-template" -B "%BUILD%" -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_C_COMPILER=clang -DGAME_ID=GBQE78 -DGENERATED_DIR="%GEN%" -DGXRUNTIME_DIR="H:/Projects/SB-RFD/lib/ModernGekko-Template/lib/ModernGekko/vendor/dolphin/GXRuntime" -DRECOMPCORE_MODULE_OPT_LEVEL=3 -DCHASSIS_ABI_DIR="H:/Projects/SB-RFD/lib/ModernGekko-Template/lib/ModernGekko/vendor/dolphin/Source/Core/Core/PowerPC/StaticRecomp" "-DCMAKE_C_FLAGS=-march=x86-64-v3 -mtune=generic -DDOLRECOMP_C_LOOP_CYCLE_BUDGET=4096 -DDOLRECOMP_C_MAX_CALL_DEPTH=128 -gline-tables-only -gdwarf-4 -fprofile-use=C:/mg-pgo/merged-gameplay.profdata -Wno-profile-instr-unprofiled -Wno-profile-instr-out-of-date" "-DCMAKE_SHARED_LINKER_FLAGS=-fprofile-use=C:/mg-pgo/merged-gameplay.profdata -Wl,/MAP:C:/mg-pgo/pgo-debug/gGBQE78_recomp.map -Wl,/debug:dwarf"
if errorlevel 1 exit /b 1
cmake --build "%BUILD%" -j16
exit /b %ERRORLEVEL%
