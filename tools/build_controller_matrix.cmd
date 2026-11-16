@echo off
rem Build and run the RPBB offline controller matrix against the shared
rem ModernGekko controller headers. The source paths are pinned here so the
rem result can be tied to an exact header hash (printed first).
setlocal
set "MG=H:\Projects\SB-RFD\lib\ModernGekko-Template\lib\ModernGekko"
set "OUT=%~dp0..\work\ctrl-hotfix"
if not exist "%OUT%" mkdir "%OUT%"

echo Controller sources:
for %%F in (
  "%MG%\tools\controller_layout.hpp"
  "%MG%\tools\controller_guid.hpp"
  "%MG%\tools\frontend_config.cpp"
  "%MG%\tools\moderngekko_run.cpp"
  "%MG%\vendor\dolphin\Source\Core\InputCommon\ControllerInterface\CoreDevice.cpp"
) do certutil -hashfile %%F SHA256 | findstr /v "hash CertUtil"

call "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\VC\Auxiliary\Build\vcvars64.bat" >nul
if errorlevel 1 exit /b 1

cl /nologo /std:c++latest /EHsc /O2 /MD /DMODERNGEKKO_GAMECUBE_CONTROLLERS=1 ^
  /I"%MG%\tools" ^
  /I"%MG%\vendor\dolphin\Externals\SDL\SDL\include" ^
  "%~dp0controller_matrix_test.cpp" "%MG%\tools\frontend_config.cpp" ^
  /Fo:"%OUT%\\" /Fe:"%OUT%\controller_matrix_test.exe"
if errorlevel 1 exit /b 2

"%OUT%\controller_matrix_test.exe" > "%OUT%\controller_matrix_test.log" 2>&1
set "RC=%ERRORLEVEL%"
type "%OUT%\controller_matrix_test.log"
exit /b %RC%