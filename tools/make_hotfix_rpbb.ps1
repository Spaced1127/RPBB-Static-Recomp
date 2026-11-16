param(
    [string]$Version = "0.2.1",
    [string]$Template = "H:\Projects\SB-RFD\lib\ModernGekko-Template\lib\ModernGekko",
    [switch]$SkipPinCheck
)

# Rebuild the RPBB runner from the shared ModernGekko controller sources and
# stage the hotfix zip. This does NOT commit, tag or publish.
#
# The shared template is edited by more than one project session, so the four
# source files are pinned. If a hash differs, stop, review the diff, and update
# the pin deliberately.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

$pin = [ordered]@{
    "tools\controller_layout.hpp" = "02C6012685A9D85560DE0F1E12730D2317C01BB67F13955B4FBFBBF1FD3908EB"
    "tools\controller_guid.hpp" = "F43F37A794121FB7CAB0225462042ABDA9F8889719173DC0E37E194F6C7BA3A9"
    "tools\frontend_config.cpp" = "D59C067C35ADB2FE34926A4C9F070DE5B3C20384A988EE78D275A506FD550640"
    "tools\frontend_config.hpp" = "FF3DDB012B01136C8D68D0A54B08FCCDD6E089DFB2D18730BC25DB9C03008709"
    "tools\moderngekko_run.cpp" = "53979545254E9C2C860828940072F5D612C9639AD5264F0F7E5868C876D436C9"
    "vendor\dolphin\Source\Core\InputCommon\ControllerInterface\CoreDevice.cpp" =
        "4175DAD185E3F8A5353324D363B77A728CF0EB8430D577762260F61703EE0C88"
    "src\runtime\dolphin_runtime.cpp" =
        "BA285DA9B00996C9414FCCABCE79B838431C66E40D7A0F4C9A42DC6D8D98AB07"
    "src\runtime\memcard_bootstrap.h" =
        "767978890ECDFDF88FD333F40B2394BFDA781988F91AFCFFC6E7D5FB34C83E00"
    "src\runtime\memcard_bootstrap.cpp" =
        "B6AA7054ED1BCDE98027F777D97FF7612D8FC099DEE18B4A6E08C9618FEC5014"
    "CMakeLists.txt" =
        "B66F69D14A684FE006EA0895B66950B07ADCCAF23B59D8E14750AC00E2EE60DA"
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
