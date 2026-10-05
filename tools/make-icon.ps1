# assets\app.ico 를 만든다. 파란 둥근 사각형에 흰색 "가".
# 사용법: powershell -ExecutionPolicy Bypass -File .\tools\make-icon.ps1
# 트레이 아이콘은 화면 배율에 따라 16~32px을 쓰므로 크기별로 직접 그려서 선명하게 한다.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$out = Join-Path $PSScriptRoot '..\assets\app.ico'

function New-IconBitmap([int]$size) {
  $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'
  $g.TextRenderingHint = 'AntiAliasGridFit'
  $g.Clear([System.Drawing.Color]::Transparent)

  $r = [Math]::Max(2, [int]($size * 0.22))
  $w = $size - 1
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $path.AddArc(0, 0, $r * 2, $r * 2, 180, 90)
  $path.AddArc($w - $r * 2, 0, $r * 2, $r * 2, 270, 90)
  $path.AddArc($w - $r * 2, $w - $r * 2, $r * 2, $r * 2, 0, 90)
  $path.AddArc(0, $w - $r * 2, $r * 2, $r * 2, 90, 90)
  $path.CloseFigure()
  $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(26, 115, 232))), $path)

  $font = New-Object System.Drawing.Font 'Malgun Gothic', ([float]($size * 0.6)), ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
  $format = New-Object System.Drawing.StringFormat
  $format.Alignment = 'Center'
  $format.LineAlignment = 'Center'
  $rect = New-Object System.Drawing.RectangleF 0, ([float]($size * 0.04)), $size, $size
  $g.DrawString([string][char]0xAC00, $font, [System.Drawing.Brushes]::White, $rect, $format)
  $g.Dispose()
  return $bmp
}

# 256px은 PNG로, 나머지는 32비트 BMP(DIB)로 넣는다.
function Get-ImageBytes([System.Drawing.Bitmap]$bmp) {
  $size = $bmp.Width
  if ($size -ge 256) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    return , $ms.ToArray()
  }

  $ms = New-Object System.IO.MemoryStream
  $bw = New-Object System.IO.BinaryWriter $ms
  $maskStride = [int]([Math]::Ceiling($size / 32.0) * 4)
  # BITMAPINFOHEADER (높이는 색상 + AND 마스크라서 두 배)
  $bw.Write([int]40); $bw.Write([int]$size); $bw.Write([int]($size * 2))
  $bw.Write([int16]1); $bw.Write([int16]32); $bw.Write([int]0)
  $bw.Write([int]($size * $size * 4 + $maskStride * $size))
  $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0)
  # 픽셀은 아래 줄부터 BGRA 순서
  for ($y = $size - 1; $y -ge 0; $y--) {
    for ($x = 0; $x -lt $size; $x++) {
      $c = $bmp.GetPixel($x, $y)
      $bw.Write([byte]$c.B); $bw.Write([byte]$c.G); $bw.Write([byte]$c.R); $bw.Write([byte]$c.A)
    }
  }
  # AND 마스크: 투명도는 알파 채널로 처리하므로 모두 0
  $bw.Write((New-Object byte[] ($maskStride * $size)))
  $bw.Flush()
  return , $ms.ToArray()
}

$images = New-Object 'System.Collections.Generic.List[byte[]]'
foreach ($size in $sizes) {
  $bmp = New-IconBitmap $size
  $images.Add([byte[]](Get-ImageBytes $bmp))
  $bmp.Dispose()
}

$fs = [System.IO.File]::Create($out)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([int16]0); $bw.Write([int16]1); $bw.Write([int16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
  $s = $sizes[$i]
  $dim = if ($s -ge 256) { 0 } else { $s }
  $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
  $bw.Write([int16]1); $bw.Write([int16]32)
  $bw.Write([int]$images[$i].Length); $bw.Write([int]$offset)
  $offset += $images[$i].Length
}
foreach ($bytes in $images) { $bw.Write($bytes) }
$bw.Close()
Write-Host "만들었습니다: $((Resolve-Path $out).Path)"
