# Builds the AULA Studio installer.
#   powershell -File installer/build.ps1 -Ffmpeg <unpacked LGPL FFmpeg build folder>
# The FFmpeg folder is an LGPL build (e.g. BtbN "ffmpeg-n8.1-latest-win64-lgpl-8.1"): bin\ffmpeg.exe,
# bin\ffprobe.exe and LICENSE.txt. GPL builds must not be bundled.
param(
    [Parameter(Mandatory)] [string] $Ffmpeg,
    [string] $Iscc = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $PSScriptRoot "out"
$app = Join-Path $out "app"

if (-not (Test-Path $Iscc)) { $Iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" }
if (-not (Test-Path $Iscc)) { throw "Inno Setup 6 not found (winget install JRSoftware.InnoSetup)." }
foreach ($f in "bin\ffmpeg.exe", "bin\ffprobe.exe", "LICENSE.txt") {
    if (-not (Test-Path (Join-Path $Ffmpeg $f))) { throw "Missing $f in $Ffmpeg." }
}
if (Select-String -Path (Join-Path $Ffmpeg "LICENSE.txt") -Pattern "GNU GENERAL PUBLIC LICENSE" -SimpleMatch -Quiet) {
    throw "This FFmpeg build is GPL; use an LGPL build."
}

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish (Join-Path $root "src\AulaStudio\AulaStudio.csproj") -c Release -r win-x64 --self-contained true `
    -p:PublishReadyToRun=true -o $app
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

$ff = Join-Path $app "ffmpeg"
New-Item -ItemType Directory -Force $ff | Out-Null
Copy-Item (Join-Path $Ffmpeg "bin\ffmpeg.exe"), (Join-Path $Ffmpeg "bin\ffprobe.exe") $ff
Copy-Item (Join-Path $Ffmpeg "LICENSE.txt") (Join-Path $ff "LICENSE-FFmpeg.txt")
Copy-Item (Join-Path $PSScriptRoot "THIRD-PARTY.txt") $app

& $Iscc "/DSourceDir=$app" "/DOutputDir=$out" (Join-Path $PSScriptRoot "AulaStudio.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed." }
Get-ChildItem $out -Filter *.exe
