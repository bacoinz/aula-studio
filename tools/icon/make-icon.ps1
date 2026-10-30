# Writes src/AulaStudio/Assets/app.ico from the same drawing as AppIcon.cs (keep the two in sync).
# Usage: powershell -File tools/icon/make-icon.ps1
Add-Type -AssemblyName PresentationCore, WindowsBase
$out = Join-Path $PSScriptRoot "..\..\src\AulaStudio\Assets\app.ico"

function Render([int]$size) {
    $accent = [Windows.Media.SolidColorBrush]::new([Windows.Media.Color]::FromRgb(0x00, 0x5F, 0xB8))
    $light = [Windows.Media.SolidColorBrush]::new([Windows.Media.Color]::FromRgb(0xE6, 0xF1, 0xFB))
    $visual = [Windows.Media.DrawingVisual]::new()
    $dc = $visual.RenderOpen()
    $s = $size / 64.0
    $dc.DrawRoundedRectangle($accent, $null, [Windows.Rect]::new(2*$s, 14*$s, 60*$s, 38*$s), 8*$s, 8*$s)
    $dc.DrawRoundedRectangle($light, $null, [Windows.Rect]::new(36*$s, 19*$s, 21*$s, 12*$s), 2*$s, 2*$s)
    for ($row = 0; $row -lt 2; $row++) { for ($col = 0; $col -lt 4; $col++) {
        $dc.DrawRoundedRectangle($light, $null, [Windows.Rect]::new((8 + $col*7)*$s, (20 + $row*8)*$s, 5*$s, 5*$s), $s, $s) } }
    $dc.DrawRoundedRectangle($light, $null, [Windows.Rect]::new(8*$s, 37*$s, 49*$s, 6*$s), $s, $s)
    $dc.Close()
    $bmp = [Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bmp.Render($visual)
    $enc = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $enc.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bmp))
    $ms = [IO.MemoryStream]::new()
    $enc.Save($ms)
    return ,$ms.ToArray()
}

# ICO container with PNG-compressed entries (supported since Windows Vista).
$sizes = 16, 24, 32, 48, 64, 128, 256
$images = $sizes | ForEach-Object { ,(Render $_) }
$fs = [IO.File]::Create($out)
$w = [IO.BinaryWriter]::new($fs)
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Close()
"wrote $out"
