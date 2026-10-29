# AULA F108 Pro — USB protocol notes

Derived from the vendor app (`DeviceDriver.exe`, "Beta 1.0.0.1", SHA-256 `446b362b…79b6`) decompiled with Ghidra 12.1.4.
Function names refer to `docs/re/DeviceDriver.decompiled.c` (kept locally, not committed: it is derived from proprietary code).
Background reading: the MIT-licensed [parsiya/f108-pro](https://github.com/parsiya/f108-pro) notes. Where they differ from the binary, the binary wins.

Status legend: **HW** = verified on the keyboard, **RE** = read from the decompilation only.

## Interfaces (HW)

| Interface | Usage page | Reports | Use |
|---|---|---|---|
| MI_03 | `FF13` | feature 65 (ID 0 + 64) | configuration commands |
| MI_02 | `FF68` | output 4097, input 65 | LCD pages and their ACKs |

Wired VID:PID `0C45:800A`. The 2.4 GHz dongle is `05AC:024F` (MI_03) and uses other commands; not supported yet.

## Command pacing (HW)

`FUN_0044edc0`: sleep 35 ms (`cmd_delaytime`) → `HidD_SetFeature` (65 bytes) → if readback: sleep 35 ms → `IOCTL_HID_GET_FEATURE` (`0xB0192`) with the same bytes pre-filled.
Replies echo the command with byte 3 = `01`. Payloads over 65 bytes are split into 64-byte reports (RE).

Many data reports end with the marker `0x55AA` stored little-endian: bytes `AA 55`.

## Sequences

Readback after each step is marked RB.

| Feature | Steps | Status |
|---|---|---|
| Clock | `04 18` RB → `04 28` (b8=01) RB → data RB → `04 02` RB | HW |
| Backlight | `04 18` RB → `04 13` (b8=01, b2=00) RB → data → `04 02` RB → `04 F0` | HW |
| LCD image | `04 18` RB → `04 72` RB → pages → `04 02` RB | HW |
| Per-key RGB | user-light mode (`04 18` RB → `04 13` RB → `80 …` → `04 02` RB → `04 F0`) then `04 18` RB → `04 23` (b8=09) RB → 576-byte table → `04 02` RB → `04 F0` RB | HW |
| Settings | `04 18` RB → `04 17` (b2=01, b8=01) RB → `00 01 …` → `04 02` RB | RE |
| LED layer (light bar / sides) | `04 18` RB → `04 13` RB → `[mode 0x1F–0x28] … AA55@14` → `04 02` RB → `04 F0` | **HW: ignored** |

**Per-key table** (HW): 576 bytes. Slot `i × 4` = `[i, R, G, B]` for light index i (1–123, equal to `key_index` in `rgb-keyboard.xml`); unlisted keys stay dark; `AA 55` at 574. Sent as nine 64-byte reports, 35 ms apart, with no readback (up to 10 retries per report). User-light mode data: byte0 `80`, byte9 = brightness.

**Light bar and side LEDs** (HW): the F108 Pro ignores the LED-layer modes. The apply readback keeps reporting `1F` (flowing) in byte 4, matching what the bar shows. These zones are firmware-only, changed with FN combos from the official F108 Pro manual:

| Zone | Effect | Colour | Brightness | Speed |
|---|---|---|---|---|
| Side strips | FN + R-Shift | FN + /? | FN + R-Alt | — |
| Light bar above the arrows | FN + L-Shift | FN + Z | FN + L-Alt | FN + L-Ctrl |

FN + B toggles the light bar's power mode.

**Clock data:** `00 slot 5A Y%2000 M D h m s 00 weekday … AA55@62`, where slot is 1.

**Backlight data:** `mode R G B 00 00 00 00 colorful brightness(0–5) speed(0–5) direction 00 00 AA 55`. With mode 0, all parameters are 0.

## LCD image (HW)

- **Header report:** `04 72`, byte 2 = slot (1), bytes 8–9 = page count, little-endian.
- **Buffer:** a 256-byte header, then N frames of 240×135 RGB565 little-endian (64,800 bytes each), padded with `FF` to a multiple of 4096 bytes.
- **Header layout:**
  - byte 0 = N;
  - byte 1+i = frame i delay in **2 ms units**, minimum 1 (500 ms → 250 verified);
  - the rest is `FF`.
- **Pages:** 4096-byte `WriteFile` to MI_02 (with report ID 0). Each page gets a 64-byte ACK `01 5A 02 00 …`, read with a 300 ms timeout.
- **Frame limit: N ≤ 141** (`gif_maxframes`). Larger uploads overflow into the menu graphics in flash and corrupt them permanently, as reported upstream.
- **Verified uploads:** 1 frame = 16 pages; 10 frames = 159 pages.
