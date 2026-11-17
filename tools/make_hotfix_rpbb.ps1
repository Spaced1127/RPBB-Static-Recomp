param(
    [string]$Version = "0.2.2",
    [string]$Template = "H:\Projects\SB-RFD\lib\ModernGekko-Template\lib\ModernGekko",
    [switch]$SkipPinCheck
)

# Rebuild the RPBB runner from the shared ModernGekko sources and stage the
# release zip. This does NOT commit, tag or publish.
#
# The shared template is edited by more than one project session, so the source
# files are pinned. If a hash differs, stop, review the diff, and update the pin
# deliberately.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

$pin = [ordered]@{
    "vendor\dolphin\Source\Core\InputCommon\ControllerInterface\SDL\LayoutClass.h" =
        "4D9661EA52813D81A2DE5B9A416618F4428A3D35F47AA744B8AB0C930A4715DE"
    "vendor\dolphin\Source\Core\InputCommon\ControllerInterface\SDL\SDLGamepad.h" =
        "0A5E339D273126717A63E84EE6E737E4904FFC1827822428060CAA57CF2BCF1A"
    "vendor\dolphin\Source\Core\InputCommon\ControllerInterface\SDL\SDLGamepad.cpp" =
        "2A3A0FDE952BAC2B4CB5441597BDAA82B805BBBEB8A0CD640481D658F5D0DC0E"
    "vendor\dolphin\Source\Core\InputCommon\ControllerInterface\CoreDevice.cpp" =
        "3A82A0678FEA5C8A40BF046D972F151D16C816B83CFBA08082C2785CADD81ECA"
    "src\runtime\dolphin_runtime.cpp" =
        "6898466594ABE2C200D637C15D74E40132B33EE8C4F42A0994CD2B6DF5119DB3"
    "include\moderngekko\runtime.hpp" =
        "F59A41017830CD10A3A29956AF5EB729C575C5A6E876A91A634B426DE40E5546"
    "tools\frontend_config.cpp" =
        "37D2235683247B32CC5C37CD42AD26B1A07A35871603DBE92350237FC5C0371E"
    "tools\frontend_config.hpp" =
        "48FE4D1BCC5D214813D1E9F39959F700353D46EEC51EA3E1E5B73D0B3BBC6AAA"
    "tools\moderngekko_run.cpp" =
        "AADF8AC0A24B5321953B898D909166E9E9DD8A416C8B235C99E674C41B8DC88C"
    "tools\moderngekko_launcher.cpp" =
        "B2DD6F9117BD872EC72FCE6D77BC1448C957438AED84A4BB14A9F99502F1D272"
    "tools\netplay_session.cpp" =
        "84F1E24A8AAD80AF65D60701F276613C2434C45C99572C24385CAA60FFC637D2"
    "tests\controller_layout_test.cpp" =
        "7BF6C1D3E7057953BAF307C882938370883A3398AB092D83B1A31DBC70CC7FE0"
    "src\runtime\memcard_bootstrap.h" =
        "767978890ECDFDF88FD333F40B2394BFDA781988F91AFCFFC6E7D5FB34C83E00"
    "src\runtime\memcard_bootstrap.cpp" =
        "B6AA7054ED1BCDE98027F777D97FF7612D8FC099DEE18B4A6E08C9618FEC5014"
    "CMakeLists.txt" =
        "97A99FFD8E2C5F5118673F791899D6938DD183B83373C361E8AF84E794A80EB0"
}

if (-not $SkipPinCheck) {
    $drift = @()
    foreach ($relative in $pin.Keys) {
        $path = Join-Path $Template $relative
        if (-not (Test-Path $path)) { throw "Missing controller source: $path" }
        $hash = (Get-FileHash $path -Algorithm SHA256).Hash
        if ($hash -ne $pin[$relative]) {
            $drift += "$relative`n  expected $($pin[$relative])`n  actual   $hash"
        }
    }
    if ($drift) {
        throw ("Shared controller sources changed; review and re-pin before building:`n" +
               ($drift -join "`n"))
    }
}

# 1. Rebuild the packaged runner from the shared sources.
& cmd /c "`"$root\work\build_rpbb_runner.cmd`""
if ($LASTEXITCODE -ne 0) { throw "RPBB runner build failed ($LASTEXITCODE)" }

$built = Join-Path "H:\Projects\SB-RFD\build\moderngekko" "rpbb.exe"
if (-not (Test-Path $built)) { throw "Runner not found at $built" }

# 2. Freeze it as the release input.
Copy-Item -LiteralPath $built -Destination (Join-Path $root "vendor\rpbb.exe") -Force
"runner SHA-256: $((Get-FileHash (Join-Path $root 'vendor\rpbb.exe') -Algorithm SHA256).Hash)"

# 3. Matrix gate before packaging.
& cmd /c "`"$root\tools\build_controller_matrix.cmd`""
if ($LASTEXITCODE -ne 0) { throw "Controller matrix failed; not packaging" }

# 4. Stage + zip. Publishing is a separate, approved step.
& powershell -ExecutionPolicy Bypass -File (Join-Path $root "tools\make_release_rpbb.ps1") -Version $Version
if ($LASTEXITCODE -ne 0) { throw "Release staging failed ($LASTEXITCODE)" }

"Hotfix v$Version staged. Nothing was committed, tagged or published."
