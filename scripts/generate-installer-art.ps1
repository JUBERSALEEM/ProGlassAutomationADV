# =======================================================================
# FILE: scripts/generate-installer-art.ps1
# Paints the only images Velopack will show:
#   installer/generated/splash.png       Setup.exe splash (PNG, rounded)
#   installer/generated/msi-banner.bmp   MSI top banner, 493x58
#   installer/generated/msi-dialog.bmp   MSI welcome/finish background, 493x312
# Windows draws the wizard text on top of the MSI images. The right
# side of the dialog bitmap stays light for that reason.
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
    $d = $r * 2
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

function Save-Bmp24([System.Drawing.Bitmap]$src, [string]$path) {
    $bmp = New-Object System.Drawing.Bitmap $src.Width, $src.Height, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.DrawImage($src, 0, 0, $src.Width, $src.Height)
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $bmp.Dispose()
}

function New-ArtFont([string]$name, [single]$size, [System.Drawing.FontStyle]$style) {
    try { return New-Object System.Drawing.Font $name, $size, $style }
    catch { return New-Object System.Drawing.Font "Arial", $size, $style }
}
$bold = [System.Drawing.FontStyle]::Bold
$fontTitle = New-ArtFont "Segoe UI" 28 $bold
$fontBody = New-ArtFont "Segoe UI" 14 ([System.Drawing.FontStyle]::Regular)
$fontSmall = New-ArtFont "Segoe UI" 11 $bold
$ink = [System.Drawing.Color]::FromArgb(30, 27, 75)
$white = [System.Drawing.Color]::White
$cyan = [System.Drawing.Color]::FromArgb(34, 211, 238)
$violet = [System.Drawing.Color]::FromArgb(124, 58, 237)
$indigo = [System.Drawing.Color]::FromArgb(76, 29, 149)
$wash = [System.Drawing.Color]::FromArgb(247, 245, 255)
$track = [System.Drawing.Color]::FromArgb(226, 219, 239)

# --- splash: rounded Prism card, transparent outside the card ---
$splash = New-Object System.Drawing.Bitmap 720, 440, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($splash)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)
$card = New-RoundedPath 36 28 648 384 28
$grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush (
    (New-Object System.Drawing.Rectangle 36, 28, 648, 384),
    $indigo, $violet, 35)
$g.FillPath($grad, $card)
$washBrush = New-Object System.Drawing.SolidBrush $wash
$g.SetClip($card)
$g.FillRectangle($washBrush, 300, 28, 384, 384)
$g.ResetClip()
$g.DrawString("ProGlass", $fontTitle, (New-Object System.Drawing.SolidBrush $ink), 64, 56)
$g.DrawString("SUITE", $fontSmall, (New-Object System.Drawing.SolidBrush $violet), 250, 70)
$g.DrawString("Installing", $fontBody, (New-Object System.Drawing.SolidBrush $ink), 64, 108)
$g.DrawString("Windows x64    This user    Records kept", $fontSmall, (New-Object System.Drawing.SolidBrush $ink), 64, 340)
$g.FillRectangle((New-Object System.Drawing.SolidBrush $track), 64, 372, 560, 12)
$g.FillRectangle((New-Object System.Drawing.SolidBrush $cyan), 64, 372, 347, 12)
$g.Dispose()
$grad.Dispose()
Save-Png $splash (Join-Path $out "splash.png")
$splash.Dispose()

# --- MSI banner 493x58. Left stays empty: Windows paints the heading. ---
$banner = New-Object System.Drawing.Bitmap 493, 58, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$g = [System.Drawing.Graphics]::FromImage($banner)
$g.Clear($wash)
$g.FillRectangle((New-Object System.Drawing.SolidBrush $violet), 448, 14, 30, 30)
$g.FillRectangle((New-Object System.Drawing.SolidBrush $track), 0, 56, 493, 2)
$g.Dispose()
Save-Bmp24 $banner (Join-Path $out "msi-banner.bmp")
$banner.Dispose()

# --- MSI dialog 493x312. Right of x=156 stays light for black system text. ---
$dialog = New-Object System.Drawing.Bitmap 493, 312, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$g = [System.Drawing.Graphics]::FromImage($dialog)
$g.Clear($wash)
$panel = New-Object System.Drawing.Drawing2D.LinearGradientBrush (
    (New-Object System.Drawing.Rectangle 0, 0, 156, 312),
    $indigo, $violet, 90)
$g.FillRectangle($panel, 0, 0, 156, 312)
$g.FillEllipse((New-Object System.Drawing.SolidBrush $cyan), 42, 78, 72, 72)
$g.DrawString("ProGlass", $fontSmall, (New-Object System.Drawing.SolidBrush $white), 28, 170)
$g.DrawString("SUITE", $fontSmall, (New-Object System.Drawing.SolidBrush $track), 48, 196)
$g.Dispose()
$panel.Dispose()
Save-Bmp24 $dialog (Join-Path $out "msi-dialog.bmp")
$dialog.Dispose()

$fontTitle.Dispose()
$fontBody.Dispose()
$fontSmall.Dispose()

Write-Host "Installer art written to $out"
