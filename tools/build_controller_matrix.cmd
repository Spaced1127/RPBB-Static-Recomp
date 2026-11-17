@echo off
rem Build and run the shared canonical-controller offline suite against the
rem pinned ModernGekko sources. This is the RPBB release gate.
rem
rem The suite covers the class matrix (positional/nintendo), override precedence,
rem the 8BitDo fallback, the canonical profile text, write-once behaviour and
rem legacy config keys.
setlocal
set "MG=H:\Projects\SB-RFD\lib\ModernGekko-Template\lib\ModernGekko"
set "OUT=%~dp0..\work\ctrl-hotfix"
if not exist "%OUT%" mkdir "%OUT%"

echo Controller sources:
for %%F in (
  "%MG%\vendor\dolphin\Source\Core\InputCommon\ControllerInterface\SDL\LayoutClass.h"
  "%MG%\tools\frontend_config.cpp"
  "%MG%\tools\frontend_config.hpp"
  "%MG%\tools\moderngekko_run.cpp"
  "%MG%\tests\controller_layout_test.cpp"
) do certutil -hashfile %%F SHA256 | findstr /v "hash CertUtil"

call "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\VC\Auxiliary\Build\vcvars64.bat" >nul
if errorlevel 1 exit /b 1

cl /nologo /std:c++latest /EHsc /O2 /MD /DMODERNGEKKO_GAMECUBE_CONTROLLERS=1 ^
  /I"%MG%\tools" ^
  /I"%MG%\vendor\dolphin\Source\Core" ^
  "%MG%\tests\controller_layout_test.cpp" "%MG%\tools\frontend_config.cpp" ^
  /Fo:"%OUT%\\" /Fe:"%OUT%\controller_layout_test.exe"
if errorlevel 1 exit /b 2

"%OUT%\controller_layout_test.exe" > "%OUT%\controller_layout_test.log" 2>&1
set "RC=%ERRORLEVEL%"
type "%OUT%\controller_layout_test.log"
exit /b %RC%