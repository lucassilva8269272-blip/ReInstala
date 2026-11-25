$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Desenha o icone em cada tamanho, em vez de reduzir um 256x256.
# Assim as linhas continuam nitidas no Explorer, na barra de tarefas e no atalho.
$sizes = @(256, 128, 64, 48, 40, 32, 24, 20, 16)

function New-RoundPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-IconPng([int]$size) {
    $s = $size / 256.0
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $bgPath = New-RoundPath (8 * $s) (8 * $s) (240 * $s) (240 * $s) (56 * $s)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Point 0, 0),
        (New-Object System.Drawing.Point $size, $size),
        [System.Drawing.Color]::FromArgb(255, 109, 94, 246),
        [System.Drawing.Color]::FromArgb(255, 61, 48, 190))
    $g.FillPath($brush, $bgPath)
    $brush.Dispose()
    $bgPath.Dispose()

    $width = [Math]::Max(1.0, 15 * $s)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([float]$width)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    # Haste e ponta da seta
    $g.DrawLine($pen, 128 * $s, 58 * $s, 128 * $s, 138 * $s)
    $head = New-Object System.Drawing.Drawing2D.GraphicsPath
    $head.AddLine(96 * $s, 110 * $s, 128 * $s, 142 * $s)
    $head.AddLine(160 * $s, 110 * $s, 128 * $s, 142 * $s)
    $g.DrawPath($pen, $head)
    $head.Dispose()

    # Bandeja
    $tray = New-Object System.Drawing.Drawing2D.GraphicsPath
    $tray.AddLine(64 * $s, 142 * $s, 64 * $s, 178 * $s)
    $tray.AddArc(64 * $s, 178 * $s, 28 * $s, 28 * $s, 180, 90)
    $tray.AddLine(78 * $s, 192 * $s, 178 * $s, 192 * $s)
    $tray.AddArc(178 * $s, 178 * $s, 28 * $s, 28 * $s, 90, 90)
    $tray.AddLine(192 * $s, 142 * $s, 192 * $s, 178 * $s)
    $g.DrawPath($pen, $tray)
    $tray.Dispose()
    $pen.Dispose()
    $g.Dispose()

    $stream = New-Object System.IO.MemoryStream
    $bmp.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return ,$stream.ToArray()
}

$images = @()
foreach ($size in $sizes) {
    $images += ,@($size, (New-IconPng $size))
}

# Salva um PNG grande para uso em documentacao e lojas.
[System.IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'icon-256.png'), $images[0][1])

# Monta um .ico com todas as imagens (PNG comprimido, suportado desde o Windows Vista).
$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $ms
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$sizes.Count)

$offset = 6 + (16 * $sizes.Count)
foreach ($entry in $images) {
    $size = $entry[0]
    $png = $entry[1]
    $dim = if ($size -ge 256) { 0 } else { $size }
    $bw.Write([Byte]$dim)
    $bw.Write([Byte]$dim)
    $bw.Write([Byte]0)
    $bw.Write([Byte]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]$png.Length)
    $bw.Write([UInt32]$offset)
    $offset += $png.Length
}
foreach ($entry in $images) {
    $bw.Write($entry[1])
}
$bw.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'icon.ico'), $ms.ToArray())
$bw.Dispose()

Write-Host "Icon generated: $PSScriptRoot\icon.ico ($($sizes -join ', ') px)"
