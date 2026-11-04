<#
.SYNOPSIS
    Draws the app icon (the lime, slanted "FGC" tag from the title bar) and writes it as a multi-size .ico.

.DESCRIPTION
    Uses WPF to render the same font, colours and slant as the ArcadeTag style in Theme\Arcade.xaml, at every size
    Windows asks for (taskbar, Alt+Tab, Explorer, Start). Each size is stored as a PNG inside the .ico.
    Run it again after changing the tag's look:

        powershell -ExecutionPolicy Bypass -STA -File scripts\Make-AppIcon.ps1
#>
param(
    [string]$OutFile = (Join-Path $PSScriptRoot '..\src\FgcKeyboardSplitter.Wpf\Assets\AppIcon.ico')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

$lime = [System.Windows.Media.Color]::FromRgb(0xCD, 0xF2, 0x4B)   # ArcadeLimeColor
$black = [System.Windows.Media.Color]::FromRgb(0x0A, 0x0C, 0x07)  # ArcadeBlackColor
$typeface = New-Object System.Windows.Media.Typeface(
    (New-Object System.Windows.Media.FontFamily('Bahnschrift')),
    [System.Windows.FontStyles]::Normal,
    [System.Windows.FontWeights]::Bold,
    [System.Windows.FontStretches]::Condensed)
$slant = 14.0  # degrees, as in ArcadeTag

function New-IconPng([int]$size) {
    $s = [double]$size
    $height = $s * 0.66
    $lean = $height * [Math]::Tan($slant * [Math]::PI / 180)
    $margin = [Math]::Max(0.5, $s * 0.02)
    $width = $s - 2 * $margin - $lean
    $left = ($s - $width) / 2
    $top = ($s - $height) / 2

    $visual = New-Object System.Windows.Media.DrawingVisual
    $dc = $visual.RenderOpen()
    # Slant everything around the centre, like the tag's SkewTransform with RenderTransformOrigin 0.5,0.5.
    $dc.PushTransform((New-Object System.Windows.Media.SkewTransform(-$slant, 0, ($s / 2), ($s / 2))))
    $dc.DrawRectangle((New-Object System.Windows.Media.SolidColorBrush($lime)), $null,
        (New-Object System.Windows.Rect($left, $top, $width, $height)))

    # Largest text that fits the plate with a little padding.
    $fontSize = $height
    do {
        $text = New-Object System.Windows.Media.FormattedText('FGC', [Globalization.CultureInfo]::InvariantCulture,
            [System.Windows.FlowDirection]::LeftToRight, $typeface, $fontSize,
            (New-Object System.Windows.Media.SolidColorBrush($black)), 1.0)
        $fontSize -= 0.25
        $inkWidth = $text.Width - $text.OverhangLeading - $text.OverhangTrailing
    } while ($inkWidth -gt $width * 0.84 -or $text.Extent -gt $height * 0.68)

    # Centre the letters' ink, not their line box, so they sit in the middle of the plate.
    $inkTop = $text.Height + $text.OverhangAfter - $text.Extent
    $x = $left + ($width - $inkWidth) / 2 - $text.OverhangLeading
    $y = $top + ($height - $text.Extent) / 2 - $inkTop
    $dc.DrawText($text, (New-Object System.Windows.Point($x, $y)))
    $dc.Pop()
    $dc.Close()

    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($size, $size, 96, 96,
        [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = New-Object System.IO.MemoryStream
    $encoder.Save($stream)
    return , $stream.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$images = foreach ($size in $sizes) { , (New-IconPng $size) }

# ICO: 6-byte header, a 16-byte entry per image, then the PNG data.
$out = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($out)
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dimension = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }  # 0 means 256
    $writer.Write([byte]$dimension)
    $writer.Write([byte]$dimension)
    $writer.Write([byte]0)       # no palette
    $writer.Write([byte]0)
    $writer.Write([UInt16]1)     # colour planes
    $writer.Write([UInt16]32)    # bits per pixel
    $writer.Write([UInt32]$images[$i].Length)
    $writer.Write([UInt32]$offset)
    $offset += $images[$i].Length
}
foreach ($image in $images) {
    $writer.Write($image)
}
$writer.Flush()

$OutFile = [System.IO.Path]::GetFullPath($OutFile)
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($OutFile)) | Out-Null
[System.IO.File]::WriteAllBytes($OutFile, $out.ToArray())
"Wrote $OutFile ($($sizes -join ', ') px)"
