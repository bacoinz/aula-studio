# AULA Studio

A simple, portable Windows app for the **AULA F108 Pro** keyboard: put GIFs and videos on its screen, control the lights, remap keys, record macros and make the keyboard react to music.

**[Download the latest version](https://github.com/bacoinz/aula-studio/releases/latest)**

![The screen page, with the crop and preview of a GIF](docs/screenshots/screen.png)

> This is an unofficial project, not affiliated with AULA. It was built by studying how the keyboard's official app talks to the keyboard. Use it at your own risk.

## Features

- **Screen:** drag in a GIF, video or image, then crop, trim and preview it before sending it to the 240×135 screen. You can also compose an image with your own text, and reuse recent uploads.
- **Lighting:** all the keyboard's effects, per-key colours, and separate control of the sides and light bar.
- **Music lighting:** the keys react to whatever is playing on the PC. No microphone needed.
- **Keys:** remap keys on the normal and FN layers to other keys, mouse actions, media controls or macros.
- **Macros:** record sequences of keys and clicks that the keyboard plays by itself.
- **Profiles:** save a whole setup, and switch between setups with one click.
- **Keyboard options:** sleep time, key response time, FN behaviour, and locks for the Windows key, Alt+F4 and Alt+Tab.
- **Clock:** sets the screen clock from the PC whenever the keyboard connects.
- **Languages:** English and Portuguese.

| Lighting | Music lighting |
| --- | --- |
| ![Lighting effects](docs/screenshots/lighting.png) | ![Music lighting](docs/screenshots/music.png) |

| Keys | Settings |
| --- | --- |
| ![Key remapping](docs/screenshots/keys.png) | ![Keyboard and app settings](docs/screenshots/settings.png) |

## Try it

1. Close the official AULA app, because only one app can talk to the keyboard at a time.
2. Connect the keyboard with the **USB cable**.
3. Download `AulaStudio-Portable-1.0.0.zip` from the [releases page](https://github.com/bacoinz/aula-studio/releases/latest).
4. Unzip it to any folder you can write to (not `Program Files`) and open `AulaStudio.exe`.

Nothing is installed, and you don't need .NET or FFmpeg. To remove the app, delete the folder. Your settings stay in a `data` folder next to the exe.

The app isn't digitally signed, so Windows may show a SmartScreen warning. To run it anyway, choose **More info**, then **Run anyway**.

## Compatibility

| | |
| --- | --- |
| Keyboard | **AULA F108 Pro only** (USB ID `0C45:800A`) |
| Connection | USB cable only; 2.4 GHz and Bluetooth aren't supported |
| System | Windows x64; tested on Windows 11 only |

Other AULA keyboards aren't detected.

## Have a different AULA keyboard? Ask an AI to adapt it

The app only works with the F108 Pro, but you can give this repository to an AI coding agent, such as [Claude Code](https://claude.com/claude-code) or [Codex](https://openai.com/codex/), and ask it to adapt the app to your keyboard. Other models may use the same commands, or they may need new ones worked out, so the result isn't guaranteed.

Your keyboard doesn't have to be an AULA: you can also try adapting the code to another brand. Expect more work, because another brand is more likely to use different commands.

You'll need your keyboard and its official app, which the agent can study to see how your model differs. A prompt to start with:

```text
This repository is an app for the AULA F108 Pro keyboard. I want to adapt it to my
AULA [your model]. Its official app is installed at [folder].

Read docs/protocol.md first. Then compare my keyboard with the F108 Pro: USB IDs,
HID interfaces, commands, key and light layout, and screen size, if it has one.
Explain what differs before changing anything. Test with read-only commands first,
and ask me before sending anything that writes to the keyboard's memory.
```

These are the main places an agent will need to look:

| What | Where |
| --- | --- |
| The USB protocol, as worked out for the F108 Pro | [`docs/protocol.md`](docs/protocol.md) |
| USB IDs used to detect the keyboard | [`src/Aula.Protocol/WindowsHidTransport.cs`](src/Aula.Protocol/WindowsHidTransport.cs) |
| Commands and how they're sent | [`src/Aula.Protocol`](src/Aula.Protocol) |
| Key positions and light numbers | [`src/AulaStudio/Assets/f108pro-layout.json`](src/AulaStudio/Assets/f108pro-layout.json) |
| Screen format and frame limit | [`src/Aula.Protocol/LcdImage.cs`](src/Aula.Protocol/LcdImage.cs) |
| Command-line tool for testing commands | [`tools/aula-cli`](tools/aula-cli) |

**Be careful with writes.** Wrong commands could misconfigure a keyboard, especially the ones that write to its memory. On the F108 Pro, this app never sends more than 141 screen frames, which is the screen's limit; don't assume that limit applies to another model.

## Build from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) on Windows.

```bash
dotnet build AulaStudio.sln
```

```bash
dotnet test AulaStudio.sln
```

```bash
dotnet run --project src/AulaStudio
```

GIF and video conversion needs FFmpeg on your `PATH`, or in an `ffmpeg` folder next to the exe. [`portable/README.md`](portable/README.md) explains how to build the small FFmpeg and the portable zip.

## Licence

AULA Studio is released under the [MIT Licence](LICENSE). The portable build includes third-party software, including FFmpeg under the LGPL; see [`portable/THIRD-PARTY.txt`](portable/THIRD-PARTY.txt). "AULA" and "F108 Pro" are used only to name the keyboard this app works with.
