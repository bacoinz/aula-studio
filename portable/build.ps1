# Builds the portable AULA Studio zip.
#   powershell -File portable/build.ps1 -Ffmpeg <unpacked LGPL FFmpeg build folder>
# The FFmpeg folder is the output of build-ffmpeg.sh, or any LGPL build: bin\ffmpeg.exe, bin\ffprobe.exe and
# LICENSE.txt. GPL builds must not be bundled.
param(
    [Parameter(Mandatory)] [string] $Ffmpeg
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $PSScriptRoot "out"
$app = Join-Path $out "AulaStudio"

foreach ($f in "bin\ffmpeg.exe", "bin\ffprobe.exe", "LICENSE.txt") {
    if (-not (Test-Path (Join-Path $Ffmpeg $f))) { throw "Missing $f in $Ffmpeg." }
}
if (Select-String -Path (Join-Path $Ffmpeg "LICENSE.txt") -Pattern "GNU GENERAL PUBLIC LICENSE" -SimpleMatch -CaseSensitive -Quiet) {
    throw "This FFmpeg build is GPL; use an LGPL build."
}

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish (Join-Path $root "src\AulaStudio\AulaStudio.csproj") -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none -o $app
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

$ff = Join-Path $app "ffmpeg"
New-Item -ItemType Directory -Force $ff | Out-Null
Copy-Item (Join-Path $Ffmpeg "bin\ffmpeg.exe"), (Join-Path $Ffmpeg "bin\ffprobe.exe") $ff
Copy-Item (Join-Path $Ffmpeg "LICENSE.txt") (Join-Path $ff "LICENSE-FFmpeg.txt")
Copy-Item (Join-Path $PSScriptRoot "THIRD-PARTY.txt") $app
Set-Content (Join-Path $app "portable.txt") "AULA Studio keeps its settings in the data folder next to the exe while this file exists."

$version = (Get-Item (Join-Path $app "AulaStudio.exe")).VersionInfo.ProductVersion -replace '[+].*$', ''
$zip = Join-Path $out "AulaStudio-Portable-$version.zip"
Compress-Archive -Path $app -DestinationPath $zip
Get-Item $zip
