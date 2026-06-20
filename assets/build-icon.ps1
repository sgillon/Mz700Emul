# Regenerates MZRaku.ico (project root) from MZONLYlogo.png (this folder).
#
# Run from the repo root:
#     powershell -NoProfile -ExecutionPolicy Bypass -File assets\build-icon.ps1
#
# Produces a multi-resolution .ico containing 16, 32, 48, and 256 px square
# variants. The wordmark is letterboxed onto a black square at each size
# (≈8% margin). Each variant is stored as an embedded PNG inside the ICO
# container, which Windows handles natively (Vista+).
#
# No external dependencies — uses System.Drawing.

Add-Type -AssemblyName System.Drawing

$root    = Split-Path -Parent $PSScriptRoot
$srcPath = Join-Path $PSScriptRoot 'MZONLYlogo.png'
$outPath = Join-Path $root        'MZRaku.ico'

if (-not (Test-Path $srcPath)) { throw "Source not found: $srcPath" }

$src = [System.Drawing.Image]::FromFile($srcPath)
$sw  = $src.Width
$sh  = $src.Height

$sizes = @(16, 32, 48, 256)
$pngs  = New-Object 'System.Collections.Generic.List[object]'

foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Black)

    $margin = [int]($size * 0.08)
    $maxW   = $size - 2 * $margin
    $maxH   = $size - 2 * $margin
    $aspect = $sw / $sh
    if ($maxW / $aspect -le $maxH) { $dw = $maxW; $dh = [int]($maxW / $aspect) }
    else                            { $dh = $maxH; $dw = [int]($maxH * $aspect) }
    $dx = [int](($size - $dw) / 2)
    $dy = [int](($size - $dh) / 2)
    $g.DrawImage($src, $dx, $dy, $dw, $dh)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $pngs.Add(@{Size = $size; Data = $ms.ToArray()})
}

$bw = New-Object System.IO.BinaryWriter([System.IO.File]::Create($outPath))
try {
    # ICONDIR header
    $bw.Write([uint16]0)                 # reserved
    $bw.Write([uint16]1)                 # type = icon
    $bw.Write([uint16]$pngs.Count)       # image count

    # ICONDIRENTRYs
    $offset = 6 + 16 * $pngs.Count
    foreach ($p in $pngs) {
        $w = if ($p.Size -ge 256) { 0 } else { $p.Size }  # 0 means 256
        $bw.Write([byte]$w); $bw.Write([byte]$w)
        $bw.Write([byte]0);  $bw.Write([byte]0)
        $bw.Write([uint16]1)               # planes
        $bw.Write([uint16]32)              # bit count
        $bw.Write([uint32]$p.Data.Length)  # size of embedded PNG
        $bw.Write([uint32]$offset)         # offset
        $offset += $p.Data.Length
    }

    # Embedded PNG payloads
    foreach ($p in $pngs) { $bw.Write($p.Data) }
} finally { $bw.Close() }
$src.Dispose()

"Wrote $outPath ($((Get-Item $outPath).Length) bytes)"
