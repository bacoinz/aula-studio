# Installer

`build.ps1` publishes AULA Studio self-contained (no .NET install needed), adds an LGPL FFmpeg build next to it,
and compiles `AulaStudio.iss` with Inno Setup 6.

```powershell
winget install JRSoftware.InnoSetup
gh release download latest --repo BtbN/FFmpeg-Builds --pattern "ffmpeg-n8.1-latest-win64-lgpl-8.1.zip"
Expand-Archive ffmpeg-n8.1-latest-win64-lgpl-8.1.zip ffmpeg-lgpl
powershell -File installer/build.ps1 -Ffmpeg ffmpeg-lgpl\ffmpeg-n8.1-latest-win64-lgpl-8.1
```

The result is `installer/out/AulaStudio-Setup-<version>.exe`. It installs per user by default (no admin prompt).
Optional tasks are a desktop icon and starting with Windows in the tray (`--tray`).

Only LGPL FFmpeg builds may be bundled; `build.ps1` refuses a GPL one. `THIRD-PARTY.txt` lists the licences.
