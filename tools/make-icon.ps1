# assets\app.ico 를 만든다. 겹친 말풍선 두 개: 뒤는 오렌지 말풍선에 흰 "A", 앞은 파란 말풍선에 흰 "가".
# 외국어(A)를 한국어(가)로 번역한다는 뜻이다. 트레이(16~24px)에서 알아보기 쉽도록 큰 색 덩어리 두 개와 글자 두 개만 쓴다.
# Google 브랜드 지침은 로고뿐 아니라 고유한 색 조합(빨강·노랑·초록·파랑)을 흉내 내는 것도 금지하므로 파랑과 오렌지만 쓴다.
# 사용법: powershell -ExecutionPolicy Bypass -File .\tools\make-icon.ps1 [-PreviewPath 미리보기.png]
# 트레이 아이콘은 화면 배율에 따라 16~32px을 쓰므로 크기별로 직접 그려서 선명하게 한다. 16px은 픽셀을 직접 찍는다.
param([string]$PreviewPath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$out = Join-Path $PSScriptRoot '..\assets\app.ico'

function Rgb([int]$r, [int]$g, [int]$b) { [System.Drawing.Color]::FromArgb($r, $g, $b) }
$backBubbleColor = Rgb 239 108 0
$frontBubbleColor = Rgb 26 115 232
$letterColor = Rgb 255 255 255

# 16px 픽셀 그림. O = 뒤 말풍선(오렌지), B = 앞 말풍선(파랑), w = 흰 글자, . = 투명
# 두 말풍선 사이에는 1픽셀 투명한 틈을 둬서 밝은/어두운 배경 모두에서 경계가 보이게 한다.
$pixels16 = @(
  '.OOOOOOOOO......',
  'OOOOOOOOOOO.....',
  'OOOOwOOOOOO.....',
  'OOOwOwOOOOO.....',
  'OOwOOOwOOOO.....',
  'OOwwwwwOOOO.....',
  'OOwOOOwOOOO.....',
  'OOOOO...........',
  'OOOOO..BBBBBBBB.',
  '.OOOO.BBwwwBwBBB',
  '.OO...BBBBwBwBBB',
  '.O....BBBBwBwwBB',
  '......BBBwBBwBBB',
  '......BBwBBBwBBB',
  '......BBBBBBwBBB',
  '.......BBBBBBBB.'
)
# 모서리를 각각 다른 반지름으로 둥글린 사각형. 반지름 0이면 직각.
# 좌표는 정수 픽셀로 넘겨야 가장자리가 번지지 않는다.
function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$rTop, [float]$rBottom) {
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  if ($rTop -gt 0) {
    $path.AddArc($x, $y, $rTop * 2, $rTop * 2, 180, 90)
    $path.AddArc($x + $w - $rTop * 2, $y, $rTop * 2, $rTop * 2, 270, 90)
  } else {
    $path.AddLine($x, $y, $x + $w, $y)
  }
  if ($rBottom -gt 0) {
    $path.AddArc($x + $w - $rBottom * 2, $y + $h - $rBottom * 2, $rBottom * 2, $rBottom * 2, 0, 90)
    $path.AddArc($x, $y + $h - $rBottom * 2, $rBottom * 2, $rBottom * 2, 90, 90)
  } else {
    $path.AddLine($x + $w, $y + $h, $x, $y + $h)
  }
  $path.CloseFigure()
  return $path
}

# (cx, cy)에 글자를 가운데 맞춰 그린다. 글자 윤곽으로 크기를 잰 뒤 위치를 정수 픽셀에 맞춘다.
# 64px 미만은 글꼴 힌팅(픽셀 맞춤)이 적용되는 DrawString으로 그려야 작은 글자가 뭉개지지 않는다.
# 24px 이하에서는 부드럽게 처리하면 획이 반투명 픽셀로 연해지므로, 픽셀을 그대로 찍는 방식(SingleBitPerPixelGridFit)을 쓴다.
function Draw-CenteredText($g, [string]$text, [string]$fontName, [float]$emSize, [float]$cx, [float]$cy, $color, [bool]$hinted, [bool]$solidPixels) {
  $family = New-Object System.Drawing.FontFamily $fontName
  $format = [System.Drawing.StringFormat]::GenericTypographic
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $path.AddString($text, $family, [int][System.Drawing.FontStyle]::Bold, $emSize, (New-Object System.Drawing.PointF 0, 0), $format)
  $bounds = $path.GetBounds()
  $dx = [Math]::Round($cx - $bounds.X - $bounds.Width / 2)
  $dy = [Math]::Round($cy - $bounds.Y - $bounds.Height / 2)
  $brush = New-Object System.Drawing.SolidBrush $color
  if ($hinted) {
    $font = New-Object System.Drawing.Font $family, $emSize, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $g.TextRenderingHint = if ($solidPixels) { 'SingleBitPerPixelGridFit' } else { 'AntiAliasGridFit' }
    $g.DrawString($text, $font, $brush, (New-Object System.Drawing.PointF $dx, $dy), $format)
  } else {
    $matrix = New-Object System.Drawing.Drawing2D.Matrix
    $matrix.Translate($dx, $dy)
    $path.Transform($matrix)
    $g.FillPath($brush, $path)
  }
}

# 말풍선: 둥근 사각형 몸통 + 아래로 향한 삼각형 꼬리. 꼬리는 몸통과 겹치게 그려 이음매가 없게 한다.
function New-Bubble([float]$x, [float]$y, [float]$w, [float]$h, [float]$r, [float]$tailLeft, [float]$tailRight, [float]$tailTipX, [float]$tailBottom) {
  $path = New-RoundedRect $x $y $w $h $r $r
  # 기본 채우기 규칙(Alternate)은 겹친 부분을 비워서 꼬리와 몸통이 겹친 곳에 구멍이 생긴다.
  $path.FillMode = 'Winding'
  if ($tailBottom -gt $y + $h) {
    $path.AddPolygon([System.Drawing.PointF[]]@(
      (New-Object System.Drawing.PointF $tailLeft, ($y + $h - $r)),
      (New-Object System.Drawing.PointF $tailRight, ($y + $h - $r)),
      (New-Object System.Drawing.PointF $tailTipX, $tailBottom)))
  }
  return $path
}

function New-IconBitmap([int]$size) {
  $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.Clear([System.Drawing.Color]::Transparent)

  if ($size -le 16) {
    $colors = @{ 'O' = $backBubbleColor; 'B' = $frontBubbleColor; 'w' = $letterColor }
    for ($y = 0; $y -lt 16; $y++) {
      for ($x = 0; $x -lt 16; $x++) {
        $c = [string]$pixels16[$y][$x]
        if ($colors.ContainsKey($c)) { $bmp.SetPixel($x, $y, $colors[$c]) }
      }
    }
    $g.Dispose()
    return $bmp
  }

  $g.SmoothingMode = 'AntiAlias'
  # 픽셀 중심을 0.5에 두어, 정수 좌표의 가장자리가 픽셀 경계와 정확히 맞게 한다.
  $g.PixelOffsetMode = 'HighQuality'
  $s = [double]$size
  $small = $size -le 24
  $hinted = $size -lt 64
  $gap = [Math]::Max(1, [Math]::Round($s * 0.05))
  $radius = [Math]::Max(2, [Math]::Round($s * 0.14))

  # 뒤 말풍선: 왼쪽 위, 꼬리는 왼쪽 아래
  $bw = [Math]::Round($s * 0.66); $bh = [Math]::Round($s * 0.54)
  $back = New-Bubble 0 0 $bw $bh $radius ([Math]::Round($s * 0.12)) ([Math]::Round($s * 0.32)) ([Math]::Round($s * 0.10)) ([Math]::Round($s * 0.70))
  $g.FillPath((New-Object System.Drawing.SolidBrush $backBubbleColor), $back)

  # 앞 말풍선: 오른쪽 아래, 꼬리는 오른쪽 아래
  $fx = [Math]::Round($s * 0.38); $fy = [Math]::Round($s * 0.40)
  $fw = $size - $fx; $fh = [Math]::Round($s * 0.46)
  $front = New-Bubble $fx $fy $fw $fh $radius ($size - [Math]::Round($s * 0.32)) ($size - [Math]::Round($s * 0.12)) ($size - [Math]::Round($s * 0.10)) $size

  # 앞 말풍선 둘레를 gap만큼 투명하게 지워 뒤 말풍선과 떨어뜨린다.
  $knockout = $front.Clone()
  $knockout.Widen((New-Object System.Drawing.Pen ([System.Drawing.Color]::Black), ($gap * 2)))
  $g.CompositingMode = 'SourceCopy'
  $clear = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::Transparent)
  $g.FillPath($clear, $knockout)
  $g.FillPath($clear, $front)
  $g.CompositingMode = 'SourceOver'
  $g.FillPath((New-Object System.Drawing.SolidBrush $frontBubbleColor), $front)

  # 글자: "A"는 뒤 말풍선에서 앞 말풍선에 가리지 않는 윗부분 가운데, "가"는 앞 말풍선 몸통 가운데
  Draw-CenteredText $g 'A' 'Segoe UI' ($s * $(if ($small) { 0.38 } else { 0.36 })) ($s * 0.28) (($fy - $gap) / 2 + $s * 0.01) $letterColor $hinted $small
  Draw-CenteredText $g ([string][char]0xAC00) 'Malgun Gothic' ($s * $(if ($small) { 0.40 } else { 0.36 })) ($fx + $fw / 2) ($fy + $fh / 2) $letterColor $hinted $small

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

# 밝은/어두운 작업 표시줄 배경에서 크기별로 어떻게 보이는지 한 장에 모은다. 작은 크기는 4배 확대본도 붙인다.
function Save-Preview([string]$path) {
  $shown = 16, 20, 24, 32, 48, 64, 128
  $zoomed = 16, 24
  $gap = 16
  $width = $gap
  foreach ($s in $shown) { $width += $s + $gap }
  foreach ($s in $zoomed) { $width += $s * 4 + $gap }
  $rowHeight = 128 + $gap * 2
  $preview = New-Object System.Drawing.Bitmap $width, ($rowHeight * 2)
  $g = [System.Drawing.Graphics]::FromImage($preview)
  $g.InterpolationMode = 'NearestNeighbor'
  $g.PixelOffsetMode = 'Half'
  $backgrounds = [System.Drawing.Color]::FromArgb(243, 243, 243), [System.Drawing.Color]::FromArgb(32, 32, 32)
  for ($row = 0; $row -lt 2; $row++) {
    $top = $row * $rowHeight
    $g.FillRectangle((New-Object System.Drawing.SolidBrush $backgrounds[$row]), 0, $top, $width, $rowHeight)
    $x = $gap
    foreach ($s in $shown) {
      $icon = New-IconBitmap $s
      $g.DrawImage($icon, $x, $top + ($rowHeight - $s) / 2, $s, $s)
      $icon.Dispose()
      $x += $s + $gap
    }
    foreach ($s in $zoomed) {
      $icon = New-IconBitmap $s
      $g.DrawImage($icon, $x, $top + ($rowHeight - $s * 4) / 2, $s * 4, $s * 4)
      $icon.Dispose()
      $x += $s * 4 + $gap
    }
  }
  $g.Dispose()
  $preview.Save($path)
  $preview.Dispose()
  Write-Host "미리보기: $path"
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

if ($PreviewPath) { Save-Preview $PreviewPath }
