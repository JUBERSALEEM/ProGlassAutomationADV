# =======================================================================
# FILE: scripts/generate-installer-art.ps1
# Paints the only images Velopack will show:
#   installer/generated/splash.png       Setup.exe splash (PNG, rounded)
#   installer/generated/msi-banner.bmp   MSI top banner, 493x58
#   installer/generated/msi-dialog.bmp   MSI welcome/finish background, 493x312
# Windows draws the wizard text, fonts, and buttons. The right side of
# the dialog bitmap stays light for that black text. The banner's left
# side stays empty for the Windows heading.
# Requires Windows (System.Drawing). Called by build-installer.ps1.
# =======================================================================

[CmdletBinding()]
param(
    [string]$RepoRoot = ""
)

$ErrorActionPreference = "Stop"
if (-not $RepoRoot) {
    $RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
}

Add-Type -AssemblyName System.Drawing

$out = Join-Path $RepoRoot "installer\generated"
if (-not (Test-Path $out)) { New-Item -ItemType Directory -Path $out | Out-Null }

function New-RoundedPath([int]$x, [int]$y, [int]$w, [int]$h, [int]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = [Math]::Max(2, $r * 2)
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function Save-Png([System.Drawing.Bitmap]$bmp, [string]$path) {
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
}

function Save-Bmp24([System.Drawing.Bitmap]$src, [string]$path, [System.Drawing.Color]$backdrop) {
    $bmp = New-Object System.Drawing.Bitmap $src.Width, $src.Height, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear($backdrop)
    $g.DrawImage($src, 0, 0, $src.Width, $src.Height)
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $bmp.Dispose()
}

function New-ArtFont([string]$name, [single]$size, [System.Drawing.FontStyle]$style) {
    try { return New-Object System.Drawing.Font $name, $size, $style }
    catch { return New-Object System.Drawing.Font "Arial", $size, $style }
}

function Add-Blob([System.Drawing.Graphics]$g, [int]$x, [int]$y, [int]$w, [int]$h, [System.Drawing.Color]$color) {
    # Concentric ellipses. System.Drawing has no blur, and a hard circle
    # would not match the Prism glow.
    $steps = 12
    for ($i = $steps; $i -ge 1; $i--) {
        $t = $i / $steps
        $alpha = [int]($color.A * [Math]::Pow((1 - $t), 1.4))
        if ($alpha -lt 6) { continue }
        $brush = New-Object System.Drawing.SolidBrush (
            [System.Drawing.Color]::FromArgb($alpha, $color.R, $color.G, $color.B))
        $insetX = [int](($w * (1 - $t)) / 2)
        $insetY = [int](($h * (1 - $t)) / 2)
        $g.FillEllipse($brush, ($x + $insetX), ($y + $insetY), [int]($w * $t), [int]($h * $t))
        $brush.Dispose()
    }
}

$bold = [System.Drawing.FontStyle]::Bold
$fontTitle = New-ArtFont "Segoe UI" 28 $bold
$fontBody = New-ArtFont "Segoe UI" 14 ([System.Drawing.FontStyle]::Regular)
$fontSmall = New-ArtFont "Segoe UI" 11 $bold
$white = [System.Drawing.Color]::White
$cyan = [System.Drawing.Color]::FromArgb(34, 211, 238)
$violet = [System.Drawing.Color]::FromArgb(124, 58, 237)
$indigo = [System.Drawing.Color]::FromArgb(76, 29, 149)
$amber = [System.Drawing.Color]::FromArgb(245, 158, 11)
$wash = [System.Drawing.Color]::FromArgb(247, 245, 255)
$track = [System.Drawing.Color]::FromArgb(226, 219, 239)

# --- splash: rounded aurora card. Live progress is hidden so this pill stays rounded. ---
$splash = New-Object System.Drawing.Bitmap 720, 440, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($splash)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)
$card = New-RoundedPath 28 24 664 392 32
$grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush (
    (New-Object System.Drawing.Rectangle 28, 24, 664, 392),
    $indigo, $violet, 40)
$g.FillPath($grad, $card)
$g.SetClip($card)
Add-Blob $g 360 10 280 220 ([System.Drawing.Color]::FromArgb(150, 34, 211, 238))
Add-Blob $g 40 40 240 200 ([System.Drawing.Color]::FromArgb(120, 124, 58, 237))
Add-Blob $g 420 200 240 180 ([System.Drawing.Color]::FromArgb(110, 245, 158, 11))
$g.ResetClip()
$g.DrawString("ProGlass", $fontTitle, (New-Object System.Drawing.SolidBrush $white), 64, 78)
$g.DrawString("Installing", $fontBody, (New-Object System.Drawing.SolidBrush $white), 64, 124)
$pill = New-RoundedPath 64 332 420 14 7
$g.FillPath((New-Object System.Drawing.SolidBrush $track), $pill)
$done = New-RoundedPath 64 332 250 14 7
$g.FillPath((New-Object System.Drawing.SolidBrush $cyan), $done)
$g.Dispose()
$grad.Dispose()
Save-Png $splash (Join-Path $out "splash.png")
$splash.Dispose()

# --- MSI banner 493x58. Left stays empty: Windows paints the heading. ---
$banner = New-Object System.Drawing.Bitmap 493, 58, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($banner)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear($wash)
$right = New-Object System.Drawing.Drawing2D.LinearGradientBrush (
    (New-Object System.Drawing.Rectangle 360, 0, 133, 58),
    $wash, $violet, 0)
$g.FillRectangle($right, 360, 0, 133, 58)
$g.FillEllipse((New-Object System.Drawing.SolidBrush $cyan), 438, 12, 34, 34)
$g.FillRectangle((New-Object System.Drawing.SolidBrush $track), 0, 56, 493, 2)
$g.Dispose()
$right.Dispose()
Save-Bmp24 $banner (Join-Path $out "msi-banner.bmp") $wash
$banner.Dispose()

# --- MSI dialog 493x312. Right of x=156 stays light for black system text. ---
$dialog = New-Object System.Drawing.Bitmap 493, 312, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($dialog)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear($wash)
$g.SetClip((New-Object System.Drawing.Rectangle 0, 0, 156, 312))
$panel = New-Object System.Drawing.Drawing2D.LinearGradientBrush (
    (New-Object System.Drawing.Rectangle 0, 0, 156, 312),
    $indigo, $violet, 90)
$g.FillRectangle($panel, 0, 0, 156, 312)
Add-Blob $g -20 20 160 140 ([System.Drawing.Color]::FromArgb(140, 34, 211, 238))
Add-Blob $g 20 180 150 140 ([System.Drawing.Color]::FromArgb(120, 245, 158, 11))
$g.ResetClip()
$g.FillEllipse((New-Object System.Drawing.SolidBrush $indigo), 46, 78, 64, 64)
$g.DrawEllipse((New-Object System.Drawing.Pen $cyan, 2), 54, 86, 48, 48)
$g.DrawString("PGA", $fontSmall, (New-Object System.Drawing.SolidBrush $cyan), 62, 98)
$g.DrawString("ProGlass", $fontSmall, (New-Object System.Drawing.SolidBrush $white), 36, 160)
$g.Dispose()
$panel.Dispose()
Save-Bmp24 $dialog (Join-Path $out "msi-dialog.bmp") $wash
$dialog.Dispose()

$fontTitle.Dispose()
$fontBody.Dispose()
$fontSmall.Dispose()

Write-Host "Installer art written to $out"
