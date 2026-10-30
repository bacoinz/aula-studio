# Portable build

`build.ps1` publishes AULA Studio as one self-contained exe (no .NET install needed), adds a small LGPL FFmpeg next to
it, and zips the folder. `build-ffmpeg.sh` builds that FFmpeg in MSYS2 with only what the app uses, about 7 MB per
program instead of 125 MB. A format the app should read but doesn't means adding it to the lists in that script.

```bash
# In an MSYS2 UCRT64 shell
pacman -S --needed make nasm diffutils mingw-w64-ucrt-x86_64-{gcc,dav1d,zlib,pkgconf}
bash portable/build-ffmpeg.sh ffmpeg-small
```

```powershell
powershell -File portable/build.ps1 -Ffmpeg ffmpeg-small\install
```

The result is `portable/out/AulaStudio-Portable-<version>.zip`. Unzip it to any writable folder and run
`AulaStudio.exe`; nothing is installed. Deleting the folder removes the app and its data.

`portable.txt` next to the exe is what makes it portable: settings and the screen library go in a `data` folder beside
the exe. On the first run, it copies them from `%APPDATA%\AulaStudio` if an earlier run saved them there.

Only LGPL FFmpeg builds may be bundled; `build.ps1` refuses a GPL one. `THIRD-PARTY.txt` lists the licences.
